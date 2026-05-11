# CSV Parser for Guava App

An Azure Function (v4, .NET 8) that parses StayFree CSV reports and returns the data as JSON formatted for the Guava App.

## What It Does

- Accepts a CSV file via HTTP POST (base64-encoded in the request body) or reads a local file in debug mode
- Extracts the header row (dates) and the "Total Usage" row from the CSV
- Converts time values (e.g. `2h 15m 30s`) to total minutes
- Reformats dates to `yyyy-MM-dd`
- Returns the parsed data as a JSON array

### Architecture

1. An Azure Logic App receives a StayFree CSV report and sends its content as a base64-encoded HTTP POST to a function
2. The function decodes the CSV, extracts dates and total usage times, converts them to minutes, and returns the result as JSON
3. The Logic App takes the JSON response, generates a CSV file, and forwards the file

### Example Output

```json
[
  { "Date/time": "2026-05-01", "Value": 135 },
  { "Date/time": "2026-05-02", "Value": 90 }
]
```

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Azure Functions Core Tools v4](https://learn.microsoft.com/azure/azure-functions/functions-run-local)

## Getting Started

```bash
cd CSVParser
dotnet restore
func start
```

## Usage

### POST request

Send a POST request with a JSON body containing a `contentBytes` field with the base64-encoded CSV content:

```
POST /api/ParseStayFreeCsv
```

### Debug mode

Append `?debug=1` to read from a local file (`C:\tmp\testfile.csv` by default):

```
GET /api/ParseStayFreeCsv?debug=1
```

## Project Structure

- [ParseStayFreeCsv.cs](CSVParser/ParseStayFreeCsv.cs) — Azure Function endpoint and CSV parsing logic
- [Utils/Utils.cs](CSVParser/Utils/Utils.cs) — Helper methods for CSV reading, row parsing, date formatting, and JSON serialization
- [Program.cs](CSVParser/Program.cs) — Function app host configuration
