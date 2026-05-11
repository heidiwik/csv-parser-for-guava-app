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
        /// Parses the header row of the CSV to extract the date columns
        /// </summary>
        /// <param name="headerLine">The header line of the CSV file.</param>
        /// <returns>A list of containing date columns in header line.</returns>
        public static List<string>? ParseHeaderRow(string headerLine)
        {
            using var parser = new TextFieldParser(new System.IO.StringReader(headerLine));
            parser.SetDelimiters(",");
            parser.HasFieldsEnclosedInQuotes = true;

            string[] fields = parser.ReadFields();

            return fields.ToList();
        }
    }
}
