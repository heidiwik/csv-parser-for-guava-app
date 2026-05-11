using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic.FileIO;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsvParser
{
    internal class Utils
    {

        /// <summary>
        /// Gets the content of a local CSV file for testing purposes. This method reads the content of the specified file and returns it as a string.
        /// </summary>
        /// <param name="filePath">The path to the local CSV file.</param>
        /// <returns>The content of the CSV file as a string, or null if an error occurs.</returns>
        public static async Task<string?> GetAttachmentContentLocalFile(string filePath = "C:\\tmp\\testfile.csv")
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
        /// Gets the content of a CSV file from an HTTP request. This method reads the request body, deserializes it to extract the base64-encoded content of the CSV file, decodes it, and returns the content as a string.
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

        /// <summary>
        /// Parses the row of the CSV with commas as delimiters and handles quoted fields that may contain commas.
        /// </summary>
        /// <param name="line">The line of the CSV file.</param>
        /// <returns>A list of containing date columns in header line.</returns>
        public static List<string>? ParseCsvRow(string line)
        {
            using var parser = new TextFieldParser(new System.IO.StringReader(line));
            parser.SetDelimiters(",");
            parser.HasFieldsEnclosedInQuotes = true;

            string[] fields = parser.ReadFields();

            return fields.ToList();
        }


        /// <summary>
        /// Parses the data as JSON for Guava App format 
        /// </summary>
        /// <param name="parsedData">The parsed data dictionary.</param>
        /// <returns>An dictionary of JSON strings representing the parsed data.</returns>
        public static string ParseDataAsJson(Dictionary<string, string>? parsedData)
        {
            var objects = parsedData?.Select(kv => new Dictionary<string, object>
            {
                ["Date/time"] = kv.Key,
                ["Value"] = int.TryParse(kv.Value, out var n) ? n : 0
            }).ToArray();

            return JsonConvert.SerializeObject(objects, Newtonsoft.Json.Formatting.Indented);
        }


        /// <summary>
        /// Converts date strings in the input list from formats like "May 2, 2026" to "yyyy-MM-dd" format.
        /// </summary>
        /// <param name="row">A list of strings potentially containing date values to reformat.</param>
        /// <returns>A list of strings with date values reformatted to "yyyy-MM-dd", or null if the input is null.</returns>
        public static List<string>? ConvertDateFormat(List<string>? row, string dateFormat = "yyyy-MM-dd")
        {
            return row?.Select(value =>
            {
                if (DateTime.TryParse(value, out var date))
                {
                    return date.ToString(dateFormat);
                }
                return value;
            }).ToList();
        }
    }
}
