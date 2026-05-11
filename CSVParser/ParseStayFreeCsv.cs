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

namespace CSVParser;

public class ParseStayFreeCsv
{
    private readonly ILogger<ParseStayFreeCsv> _logger;

    public ParseStayFreeCsv(ILogger<ParseStayFreeCsv> logger)
    {
        _logger = logger;
    }

    [Function("ParseStayFreeCsv")]
    public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "get","post")] HttpRequest req)
    {
        var debug = req.Query["debug"].ToString() == 1.ToString() ? true : false;

        var csv = debug ? await CsvParser.Utils.GetAttachmentContentLocalFile() : await CsvParser.Utils.GetAttachmentContent(req, _logger);

        if (csv == null)
        {
            return new BadRequestObjectResult("No valid CSV content found in the request.");
        }

        var totalUsageValues = GetTotalUsageValues(csv);

        if (totalUsageValues == null)
        {
            return new BadRequestObjectResult("Failed to parse CSV data.");
        }

        var parsedDataAsJson = CsvParser.Utils.ParseDataAsJson(totalUsageValues);

        return new OkObjectResult(parsedDataAsJson);
    }

    /// <summary>
    /// Parses the CSV data to extract the header row and the total usage row, then creates a dictionary with the header as key and total usage as value, transforming time values to total minutes.
    /// </summary>
    /// <param name="csvData">The CSV data as a string.</param>
    /// <returns>A dictionary with the header as key and total usage as value, with time values converted to total minutes.</returns>
    private static Dictionary<string, string>? GetTotalUsageValues(string csvData)
    {
        try
        {
            // split csv data into lines, remove empty lines
            var lines = csvData.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            // get header row with date values only
            var headerRow = GetHeaderRow(lines[0]);

            // get total usage row
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
    /// Gets the header row from the first line of the CSV data, parsing it to extract only the date values
    /// </summary>
    /// <param name="headerLine">The first line of the CSV data.</param>
    /// <returns>A list of date values from the header row.</returns>
    private static List<string>? GetHeaderRow(string headerLine)
    {
        // parse values
        var headerRow = CsvParser.Utils.ParseCsvRow(headerLine);

        // remove first two columns and last column to get only date values
        headerRow = headerRow?.Skip(2).ToList().Take(headerRow.Count - 3).ToList();

        // convert date format
        headerRow = CsvParser.Utils.ConvertDateFormat(headerRow);

        return headerRow;
    }



    /// <summary>
    /// Parses the CSV data to find the last row containing "Total Usage" and extracts the time values, skipping the first two columns and the last column.
    /// </summary>
    /// <param name="lines">The lines of the CSV file.</param>
    /// <returns>A list of time values from the last "Total Usage" row.</returns>
    private static List<string>? GetTotalUsageRow(string[] lines)
    {
        // find time values on last data row containing "Total Usage" 
        lines = lines.Skip(1).ToArray();
        var totalUsageRow = lines.LastOrDefault(row => row.Contains("Total Usage", StringComparison.OrdinalIgnoreCase))?.Split(',').ToList();

        // remove everything else but usage time values
        return totalUsageRow?.Skip(2).ToList().Take(totalUsageRow.Count - 1).ToList();
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


}