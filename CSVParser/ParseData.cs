using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Microsoft.VisualBasic.FileIO;
using System.Data;

namespace CSVParser;

public class ParseData
{
    private readonly ILogger<ParseData> _logger;

    public ParseData(ILogger<ParseData> logger)
    {
        _logger = logger;
    }

    [Function("ReadCSV")]
    public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequest req)
    {
        _logger.LogInformation("Starting function execution: ReadCSV");

        var debug = req.Query["debug"].ToString() == 1.ToString() ? true : false;

        var csv = debug ? await GetAttachmentContentLocalFile() : await GetAttachmentContent(req, _logger);

        if (csv == null)
        {
            return new BadRequestObjectResult("No valid CSV content found in the request.");
        }


        var parsedData = ParseCsvData(csv);

        var parsedDataAsJson = ParseDataAsJson(parsedData);

        return new OkObjectResult(parsedDataAsJson);
    }


    /// <summary>
    /// Parses the data based on the specified data source and formats it as JSON strings.
    /// </summary>
    /// <param name="parsedData">The parsed data dictionary.</param>
    /// <param name="dataSource">The data source type.</param>
    /// <returns>An array of JSON strings representing the parsed data.</returns>
    private string[] ParseDataAsJson(Dictionary<string, string> parsedData)
    {
        return parsedData?.Select(kv => $"{{Date/time: {kv.Key}, Total: {kv.Value}}}").ToArray();
    }


    /// <summary>
    /// Parses the CSV data to extract the header row and the total usage row, then creates a dictionary with the header as key and total usage as value, transforming time values to total minutes.
    /// </summary>
    /// <param name="csvData">The CSV data as a string.</param>
    /// <returns>A dictionary with the header as key and total usage as value, with time values converted to total minutes.</returns>
    private static Dictionary<string, string>? ParseCsvData(string csvData)
    {
        try
        {
            // split csv data into lines, remove empty lines
            var lines = csvData.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            // get header row with dates only
            var headerRow = ParseHeaderRow(lines[0]);
            headerRow = headerRow.Skip(2).ToList().Take(headerRow.Count - 1).ToList();

            // get total usage row with time values only
            var totalUsageRow = GetTotalUsageRow(lines);


            // create dictionary with header as key and total usage as value
            var totals = headerRow.Zip(totalUsageRow ?? new List<string>(), (header, total) => new { header, total })
                .ToDictionary(x => x.header.Trim('"'), x => x.total.Trim('"'));

            return TransformValuesToMinutes(totals);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error parsing CSV data: {ex.Message}");
        }

        return null;
    }


    /// <summary>
    /// Parses the CSV data to find the last row containing "Total Usage" and extracts the time values, skipping the first two columns and the last column.
    /// </summary>
    /// <param name="lines">The lines of the CSV file.</param>
    /// <returns>A list of time values from the last "Total Usage" row.</returns>
    private static List<string> GetTotalUsageRow(string[] lines)
    {
        // find time values on last data row containing "Total Usage" 
        lines = lines.Skip(1).ToArray();
        var totalUsageRow = lines.LastOrDefault(row => row.Contains("Total Usage", StringComparison.OrdinalIgnoreCase))?.Split(',').ToList();

        // remove everything else but time values
        return totalUsageRow?.Skip(2).ToList().Take(totalUsageRow.Count - 1).ToList();
    }


    /// <summary>
    /// Parses the header row of the CSV to extract the date columns, skipping the first two and last column.
    /// </summary>
    /// <param name="headerLine">The header line of the CSV file.</param>
    /// <returns>A list of containing date columns in header line.</returns>
    private static List<string?> ParseHeaderRow(string headerLine)
    {
        using var parser = new TextFieldParser(new System.IO.StringReader(headerLine));
        parser.SetDelimiters(",");
        parser.HasFieldsEnclosedInQuotes = true;

        string[] fields = parser.ReadFields();

        return fields.ToList();
    }

    /// <summary>
    /// Transforms time values in the format of "Xh Ym Zs" to total minutes, rounding up seconds to the nearest minute.
    /// </summary>
    /// <param name="totals">A dictionary containing time values in the format of "Xh Ym Zs".</param>
    /// <returns>A dictionary with the same keys but with time values converted to total minutes.</returns>
    private static Dictionary<string, string> TransformValuesToMinutes(Dictionary<string, string> totals)
    {
        var transformedValues = totals.ToDictionary(
            kv => kv.Key,
            kv =>
            {
                var value = kv.Value;
                var hours = Regex.Match(value, @"(\d+)h");
                var minutes = Regex.Match(value, @"(\d+)m");
                var seconds = Regex.Match(value, @"(\d+)s");

                int totalMinutes = 0;

                if (minutes.Success)
                {
                    totalMinutes += int.Parse(minutes.Groups[1].Value);
                }

                if (seconds.Success)
                {
                    totalMinutes += (int.Parse(seconds.Groups[1].Value) + 59) / 60; // round up to the nearest minute
                }

                if (hours.Success)
                {
                    totalMinutes += int.Parse(hours.Groups[1].Value) * 60;
                }

                return totalMinutes.ToString();
            }
       );

        return transformedValues;
    }


    /// <summary>
    /// Gets the content of a local CSV file for testing purposes. This method reads the content of the specified file and returns it as a string. If there is an error reading the file, it logs the error and returns null.
    /// </summary>
    /// <param name="filePath">The path to the local CSV file.</param>
    /// <returns>The content of the CSV file as a string, or null if an error occurs.</returns>

    private static async Task<string?> GetAttachmentContentLocalFile(string filePath = "C:\\tmp\\testfile.csv")
    {
        try
        {
            using var reader = new StreamReader(filePath);
            return await reader.ReadToEndAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reading file: {ex.Message}");
            return null;
        }
    }


    /// <summary>
    /// Gets the content of a CSV file from an HTTP request. This method reads the request body, deserializes it to extract the base64-encoded content of the CSV file, decodes it, and returns the content as a string. If there is an error during this process, it logs the error and returns null.
    /// </summary>
    /// <param name="req">The HTTP request containing the CSV file.</param>
    /// <param name="log">The logger to log errors.</param>
    /// <returns>The content of the CSV file as a string, or null if an error occurs.</returns>
    public static async Task<string?> GetAttachmentContent(HttpRequest req, ILogger log)
    {
        try
        {
            string body = await new StreamReader(req.Body).ReadToEndAsync();

            dynamic? attachment = JsonConvert.DeserializeObject(body);

            if (attachment == null)
            {
                log.LogError("Error: Request body is empty or not in the expected format.");
                return null;
            }

            string bytes = attachment.contentBytes;
            byte[] bytesread = Convert.FromBase64String(bytes);
            Stream stream = new MemoryStream(bytesread);
            var reader = new StreamReader(stream);
            var csvContent = await reader.ReadToEndAsync();

            return csvContent;
        }
        catch (Exception ex)
        {
            log.LogError(ex, $"Error when getting attachment content: {ex.Message}");
        }
        return null;
    }
}