using System.Text;
using ClosedXML.Excel;
using PetelAssistants.Api.DTOs;

namespace PetelAssistants.Api.Services
{
    public class PersonsFileProcessor
    {
        private readonly PersonService _personService;
        private readonly ILogger<PersonsFileProcessor> _logger;

        public PersonsFileProcessor(PersonService personService, ILogger<PersonsFileProcessor> logger)
        {
            _personService = personService;
            _logger = logger;
        }

        public static Dictionary<string, string> GetAvailableFields() => new()
        {
            { "id_number", "תעודת זהות" },
            { "name", "שם מלא" },
            { "first_name", "שם פרטי" },
            { "last_name", "שם משפחה" },
            { "ignore", "התעלם" }
        };

        public static string? ValidateMapping(Dictionary<string, string> mapping)
        {
            if (!mapping.ContainsKey("id_number") || string.IsNullOrWhiteSpace(mapping["id_number"]))
                return "יש למפות את שדה תעודת זהות";

            var hasName = mapping.ContainsKey("name") && !string.IsNullOrWhiteSpace(mapping["name"]);
            var hasFirst = mapping.ContainsKey("first_name") && !string.IsNullOrWhiteSpace(mapping["first_name"]);
            var hasLast = mapping.ContainsKey("last_name") && !string.IsNullOrWhiteSpace(mapping["last_name"]);

            if (hasName && (hasFirst || hasLast))
                return "יש לבחור מיפוי שם אחד: שם מלא, או שם פרטי + שם משפחה — לא שניהם";

            if (!hasName && !(hasFirst && hasLast))
            {
                if (hasFirst && !hasLast)
                    return "מופה שם פרטי בלבד — יש למפות גם שם משפחה, או להשתמש בשם מלא";
                if (!hasFirst && hasLast)
                    return "מופה שם משפחה בלבד — יש למפות גם שם פרטי, או להשתמש בשם מלא";
                return "יש למפות שם מלא, או שם פרטי ושם משפחה";
            }

            var headers = mapping.Values.Where(v => !string.IsNullOrWhiteSpace(v)).ToList();
            if (headers.Count != headers.Distinct(StringComparer.OrdinalIgnoreCase).Count())
                return "לא ניתן למפות את אותה עמודת קובץ לשני שדות מערכת";

            return null;
        }

        public static Dictionary<string, string> GenerateSuggestedMappings(List<string> headers)
        {
            var mappings = new Dictionary<string, string>();
            var fieldPatterns = new Dictionary<string, string[]>
            {
                { "id_number", new[] { "תעודת זהות", "ת.ז", "תז", "מספר זהות", "id", "id_number", "מזהה" } },
                { "name", new[] { "שם מלא", "full name", "fullname", "שם הסייעת", "שם העובד" } },
                { "first_name", new[] { "שם פרטי", "first_name", "firstname", "פרטי" } },
                { "last_name", new[] { "שם משפחה", "last_name", "lastname", "משפחה" } }
            };

            foreach (var header in headers)
            {
                var normalized = header.Trim().ToLowerInvariant();
                foreach (var field in fieldPatterns)
                {
                    if (field.Value.Any(p =>
                            normalized.Contains(p.ToLowerInvariant()) ||
                            p.ToLowerInvariant().Contains(normalized)))
                    {
                        // Prefer exact-ish matches: avoid mapping bare "שם" to first_name when "שם מלא" fits name
                        if (field.Key == "first_name" &&
                            (normalized.Contains("מלא") || normalized.Contains("full")))
                            continue;
                        if (field.Key == "name" &&
                            (normalized.Contains("פרטי") || normalized.Contains("משפחה") ||
                             normalized.Contains("first") || normalized.Contains("last")))
                            continue;

                        mappings[header] = field.Key;
                        break;
                    }
                }
            }

            return mappings;
        }

        public List<string> ReadHeaders(IFormFile file)
        {
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            using var stream = file.OpenReadStream();

            if (ext == ".csv")
                return ReadCsvHeaders(stream);

            if (ext is ".xls" or ".xlsx")
            {
                using var workbook = new XLWorkbook(stream);
                var worksheet = workbook.Worksheets.FirstOrDefault()
                    ?? throw new InvalidOperationException("הקובץ אינו מכיל גיליונות");
                var firstRow = worksheet.FirstRowUsed();
                if (firstRow == null || !firstRow.CellsUsed().Any())
                    throw new InvalidOperationException("לא נמצאו כותרות בקובץ או הקובץ ריק");

                return firstRow.CellsUsed()
                    .Select(c => c.GetValue<string>()?.Trim() ?? string.Empty)
                    .Where(h => !string.IsNullOrEmpty(h))
                    .ToList();
            }

            throw new InvalidOperationException("פורמט קובץ לא נתמך. יש להשתמש ב-CSV, XLS או XLSX");
        }

        public List<PersonFileRow> ParseFile(IFormFile file, Dictionary<string, string> mapping)
        {
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            using var stream = file.OpenReadStream();

            if (ext == ".csv")
                return ParseCsv(stream, mapping);

            if (ext is ".xls" or ".xlsx")
                return ParseExcel(stream, mapping);

            throw new InvalidOperationException("פורמט קובץ לא נתמך. יש להשתמש ב-CSV, XLS או XLSX");
        }

        public async Task<PersonsFileProcessingResult> ProcessRowsAsync(
            int entityId,
            int? userId,
            List<PersonFileRow> rows)
        {
            var result = new PersonsFileProcessingResult();

            foreach (var row in rows)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(row.IdNumber))
                    {
                        result.Errors.Add($"שורה {row.RowNumber}: מספר זהות חסר");
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(row.FirstName))
                    {
                        result.Errors.Add($"שורה {row.RowNumber}: שם פרטי חסר");
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(row.LastName))
                    {
                        result.Errors.Add($"שורה {row.RowNumber}: שם משפחה חסר");
                        continue;
                    }

                    var idNumber = row.IdNumber.Trim();
                    if (await _personService.IdNumberExistsAsync(entityId, idNumber))
                    {
                        result.Skipped++;
                        continue;
                    }

                    await _personService.CreatePersonAsync(entityId, userId, new CreatePersonRequest
                    {
                        IdNumber = idNumber,
                        IdType = 1,
                        FirstName = row.FirstName.Trim(),
                        LastName = row.LastName.Trim()
                    });
                    result.Created++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error importing person row {Row}", row.RowNumber);
                    result.Errors.Add($"שורה {row.RowNumber}: {ex.Message}");
                }
            }

            return result;
        }

        public static (string FirstName, string LastName) SplitFullName(string fullName)
        {
            var trimmed = fullName.Trim();
            var spaceIndex = trimmed.IndexOf(' ');
            if (spaceIndex < 0)
                return (trimmed, "-");

            var first = trimmed[..spaceIndex].Trim();
            var last = trimmed[(spaceIndex + 1)..].Trim();
            if (string.IsNullOrEmpty(last))
                last = "-";
            return (string.IsNullOrEmpty(first) ? "-" : first, last);
        }

        private List<PersonFileRow> ParseExcel(Stream stream, Dictionary<string, string> mapping)
        {
            var rows = new List<PersonFileRow>();
            using var workbook = new XLWorkbook(stream);
            var worksheet = workbook.Worksheets.FirstOrDefault();
            if (worksheet == null)
                return rows;

            var headerRow = worksheet.Row(1);
            var headers = headerRow.CellsUsed()
                .Select(c => c.GetValue<string>()?.Trim() ?? string.Empty)
                .ToList();

            var rowNumber = 1;
            foreach (var excelRow in worksheet.RowsUsed().Skip(1))
            {
                rowNumber++;
                var values = BuildValueMap(headers, i =>
                {
                    var cell = excelRow.Cell(i + 1);
                    return cell.GetFormattedString()?.Trim()
                        ?? cell.GetValue<string>()?.Trim()
                        ?? string.Empty;
                });

                var personRow = BuildPersonRow(rowNumber, values, mapping);
                if (personRow != null)
                    rows.Add(personRow);
            }

            return rows;
        }

        private List<PersonFileRow> ParseCsv(Stream stream, Dictionary<string, string> mapping)
        {
            var rows = new List<PersonFileRow>();
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

            var headerLine = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(headerLine))
                return rows;

            var headers = SplitCsvLine(headerLine);
            var rowNumber = 1;
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                rowNumber++;
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var cells = SplitCsvLine(line);
                var values = BuildValueMap(headers, i => i < cells.Count ? cells[i] : string.Empty);
                var personRow = BuildPersonRow(rowNumber, values, mapping);
                if (personRow != null)
                    rows.Add(personRow);
            }

            return rows;
        }

        private static List<string> ReadCsvHeaders(Stream stream)
        {
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            var headerLine = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(headerLine))
                throw new InvalidOperationException("לא נמצאו כותרות בקובץ או הקובץ ריק");
            return SplitCsvLine(headerLine).Where(h => !string.IsNullOrEmpty(h)).ToList();
        }

        private static Dictionary<string, string> BuildValueMap(List<string> headers, Func<int, string> getCell)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < headers.Count; i++)
            {
                var header = headers[i];
                if (string.IsNullOrEmpty(header) || map.ContainsKey(header))
                    continue;
                map[header] = getCell(i)?.Trim() ?? string.Empty;
            }
            return map;
        }

        private static PersonFileRow? BuildPersonRow(
            int rowNumber,
            Dictionary<string, string> values,
            Dictionary<string, string> mapping)
        {
            string GetMapped(string field)
            {
                if (!mapping.TryGetValue(field, out var header) || string.IsNullOrWhiteSpace(header))
                    return string.Empty;
                return values.TryGetValue(header, out var value) ? value : string.Empty;
            }

            var idNumber = GetMapped("id_number");
            var fullName = GetMapped("name");
            var firstName = GetMapped("first_name");
            var lastName = GetMapped("last_name");

            // Skip completely empty rows
            if (string.IsNullOrWhiteSpace(idNumber) &&
                string.IsNullOrWhiteSpace(fullName) &&
                string.IsNullOrWhiteSpace(firstName) &&
                string.IsNullOrWhiteSpace(lastName))
                return null;

            if (!string.IsNullOrWhiteSpace(fullName))
            {
                var split = SplitFullName(fullName);
                firstName = split.FirstName;
                lastName = split.LastName;
            }

            return new PersonFileRow
            {
                RowNumber = rowNumber,
                IdNumber = idNumber,
                FirstName = firstName,
                LastName = lastName
            };
        }

        private static List<string> SplitCsvLine(string line)
        {
            var result = new List<string>();
            var current = new StringBuilder();
            var inQuotes = false;

            for (var i = 0; i < line.Length; i++)
            {
                var c = line[i];
                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if ((c == ',' || c == ';') && !inQuotes)
                {
                    result.Add(current.ToString().Trim());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }

            result.Add(current.ToString().Trim());
            return result;
        }
    }
}
