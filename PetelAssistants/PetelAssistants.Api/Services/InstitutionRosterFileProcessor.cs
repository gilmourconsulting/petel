using System.Text;
using ClosedXML.Excel;
using PetelAssistants.Api.DTOs;

namespace PetelAssistants.Api.Services
{
    public class InstitutionRosterFileProcessor
    {
        private readonly OrgUnitService _orgUnitService;
        private readonly ILogger<InstitutionRosterFileProcessor> _logger;

        public InstitutionRosterFileProcessor(
            OrgUnitService orgUnitService,
            ILogger<InstitutionRosterFileProcessor> logger)
        {
            _orgUnitService = orgUnitService;
            _logger = logger;
        }

        public static Dictionary<string, string> GetAvailableFields() => new()
        {
            { "symbol", "סמל מוסד" },
            { "name", "שם מוסד" },
            { "institution_type", "סוג מוסד" },
            { "school_level", "רמת בית ספר" },
            { "is_special_education", "חינוך מיוחד" },
            { "ignore", "התעלם" }
        };

        public static string? ValidateMapping(Dictionary<string, string> mapping)
        {
            if (!mapping.ContainsKey("name") || string.IsNullOrWhiteSpace(mapping["name"]))
                return "יש למפות את שדה שם מוסד";

            if (!mapping.ContainsKey("institution_type") || string.IsNullOrWhiteSpace(mapping["institution_type"]))
                return "יש למפות את שדה סוג מוסד";

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
                { "symbol", new[] { "סמל מוסד", "סמל", "symbol", "institution_code", "institution symbol" } },
                { "name", new[] { "שם מוסד", "שם בית ספר", "שם הגן", "name", "institution_name", "שם" } },
                { "institution_type", new[] { "סוג מוסד", "סוג", "type", "institution_type", "בית ספר", "גן" } },
                { "school_level", new[] { "רמת בית ספר", "רמה", "שלב חינוך", "school_level", "יסודי", "תיכון" } },
                { "is_special_education", new[] { "חינוך מיוחד", "מיוחד", "special", "is_special_education" } }
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
                        if (field.Key == "name" &&
                            (normalized.Contains("סמל") || normalized.Contains("symbol") ||
                             normalized.Contains("סוג") || normalized.Contains("type") ||
                             normalized.Contains("רמה") || normalized.Contains("level")))
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

        public List<InstitutionRosterFileRow> ParseFile(IFormFile file, Dictionary<string, string> mapping)
        {
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            using var stream = file.OpenReadStream();

            if (ext == ".csv")
                return ParseCsv(stream, mapping);

            if (ext is ".xls" or ".xlsx")
                return ParseExcel(stream, mapping);

            throw new InvalidOperationException("פורמט קובץ לא נתמך. יש להשתמש ב-CSV, XLS או XLSX");
        }

        public async Task<InstitutionRosterUploadResult> ProcessRowsAsync(
            int entityId,
            int? userId,
            int yearId,
            List<InstitutionRosterFileRow> rows)
        {
            _logger.LogInformation(
                "Importing institution roster: {Count} rows for year {YearId} entity {EntityId}",
                rows.Count, yearId, entityId);
            return await _orgUnitService.ImportRosterRowsAsync(entityId, userId, yearId, rows);
        }

        private static List<string> ReadCsvHeaders(Stream stream)
        {
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            var line = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(line))
                return new List<string>();
            return SplitCsvLine(line).Where(h => !string.IsNullOrWhiteSpace(h)).Select(h => h.Trim()).ToList();
        }

        private List<InstitutionRosterFileRow> ParseCsv(Stream stream, Dictionary<string, string> mapping)
        {
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            var headerLine = reader.ReadLine()
                ?? throw new InvalidOperationException("הקובץ ריק");
            var headers = SplitCsvLine(headerLine);
            var headerIndex = BuildHeaderIndex(headers, mapping);

            var rows = new List<InstitutionRosterFileRow>();
            var rowNumber = 1;
            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();
                rowNumber++;
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var cells = SplitCsvLine(line);
                rows.Add(MapRow(rowNumber, cells, headerIndex));
            }

            return rows;
        }

        private List<InstitutionRosterFileRow> ParseExcel(Stream stream, Dictionary<string, string> mapping)
        {
            using var workbook = new XLWorkbook(stream);
            var worksheet = workbook.Worksheets.FirstOrDefault()
                ?? throw new InvalidOperationException("הקובץ אינו מכיל גיליונות");
            var firstRow = worksheet.FirstRowUsed()
                ?? throw new InvalidOperationException("הקובץ ריק");

            var headers = firstRow.CellsUsed()
                .Select(c => c.GetValue<string>()?.Trim() ?? string.Empty)
                .ToList();
            var headerIndex = BuildHeaderIndex(headers, mapping);

            var rows = new List<InstitutionRosterFileRow>();
            var usedRange = worksheet.RangeUsed();
            if (usedRange == null)
                return rows;

            foreach (var row in usedRange.RowsUsed().Skip(1))
            {
                var cells = headers.Select((_, i) =>
                {
                    var cell = row.Cell(i + 1);
                    return cell.IsEmpty() ? string.Empty : cell.GetFormattedString()?.Trim() ?? string.Empty;
                }).ToList();

                if (cells.All(string.IsNullOrWhiteSpace))
                    continue;

                rows.Add(MapRow(row.RowNumber(), cells, headerIndex));
            }

            return rows;
        }

        private static Dictionary<string, int> BuildHeaderIndex(List<string> headers, Dictionary<string, string> mapping)
        {
            // mapping: systemField -> fileHeader
            var index = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in mapping)
            {
                if (string.IsNullOrWhiteSpace(pair.Value) || pair.Key == "ignore")
                    continue;
                var headerIdx = headers.FindIndex(h => string.Equals(h, pair.Value, StringComparison.OrdinalIgnoreCase));
                if (headerIdx >= 0)
                    index[pair.Key] = headerIdx;
            }

            return index;
        }

        private static InstitutionRosterFileRow MapRow(int rowNumber, List<string> cells, Dictionary<string, int> headerIndex)
        {
            string? Get(string field)
            {
                if (!headerIndex.TryGetValue(field, out var idx) || idx < 0 || idx >= cells.Count)
                    return null;
                return cells[idx];
            }

            return new InstitutionRosterFileRow
            {
                RowNumber = rowNumber,
                Symbol = Get("symbol"),
                Name = Get("name"),
                InstitutionType = Get("institution_type"),
                SchoolLevel = Get("school_level"),
                IsSpecialEducation = Get("is_special_education")
            };
        }

        private static List<string> SplitCsvLine(string line)
        {
            var result = new List<string>();
            var sb = new StringBuilder();
            var inQuotes = false;

            for (var i = 0; i < line.Length; i++)
            {
                var c = line[i];
                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        sb.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if ((c == ',' || c == ';') && !inQuotes)
                {
                    result.Add(sb.ToString());
                    sb.Clear();
                }
                else
                {
                    sb.Append(c);
                }
            }

            result.Add(sb.ToString());
            return result;
        }
    }
}
