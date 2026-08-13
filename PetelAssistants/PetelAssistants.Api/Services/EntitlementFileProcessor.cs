using System.Globalization;
using System.Text;
using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using PetelAssistants.Api.Data;
using PetelAssistants.Api.DTOs;
using PetelAssistants.Api.Models;

namespace PetelAssistants.Api.Services
{
    public class EntitlementFileProcessor
    {
        private const string SupportAutomatic = "אוטומטית";
        private const string SupportSchoolBoost = "תגבור מוסדי";
        private const string TypeClassHelp = "class_help";
        private const string TypeSchoolHelp = "school_help";

        private readonly AssistDbContext _context;
        private readonly SharedDbContext _sharedContext;
        private readonly EntitlementService _entitlementService;
        private readonly ILogger<EntitlementFileProcessor> _logger;

        public EntitlementFileProcessor(
            AssistDbContext context,
            SharedDbContext sharedContext,
            EntitlementService entitlementService,
            ILogger<EntitlementFileProcessor> logger)
        {
            _context = context;
            _sharedContext = sharedContext;
            _entitlementService = entitlementService;
            _logger = logger;
        }

        public static Dictionary<string, string> GetAvailableFields() => new()
        {
            { "institution_symbol", "סמל מוסד" },
            { "institution_name", "שם מוסד" },
            { "support_type", "סוג תומכת חינוך" },
            { "annual_hours", "שעות הקצאה שנתיות" },
            { "participation_pct", "אחוז השתתפות" },
            { "grade_layer", "שכבה" },
            { "grade_parallel", "מקבילה" },
            { "class_type_code", "סוג כיתה (סיווג)" },
            { "hebrew_year", "שנת לימודים" },
            { "ignore", "התעלם" }
        };

        public static string? ValidateMapping(Dictionary<string, string> mapping)
        {
            string[] required =
            [
                "institution_symbol",
                "support_type",
                "annual_hours",
                "participation_pct"
            ];

            foreach (var field in required)
            {
                if (!mapping.ContainsKey(field) || string.IsNullOrWhiteSpace(mapping[field]))
                    return $"יש למפות את השדה: {GetAvailableFields().GetValueOrDefault(field, field)}";
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
                { "institution_symbol", ["סמל מוסד", "סמל", "symbol", "institution_symbol"] },
                { "institution_name", ["שם מוסד", "שם בית ספר", "institution_name", "מוסד"] },
                { "support_type", ["סוג תומכת חינוך", "תומכת", "support_type"] },
                { "annual_hours", ["שעות הקצאה כיתתיות שנתית", "שעות הקצאה", "שעות", "annual_hours"] },
                { "participation_pct", ["אחוז השתתפות", "השתתפות", "participation"] },
                { "grade_layer", ["שכבה", "grade_layer"] },
                { "grade_parallel", ["מקבילה", "grade_parallel"] },
                { "class_type_code", ["סוג כיתה", "class_type", "סיווג"] },
                { "hebrew_year", ["שנת לימודים", "שנה עברית", "hebrew_year"] }
            };

            foreach (var header in headers)
            {
                var normalized = header.Trim().ToLowerInvariant();
                foreach (var field in fieldPatterns)
                {
                    if (!field.Value.Any(p =>
                            normalized.Contains(p.ToLowerInvariant()) ||
                            p.ToLowerInvariant().Contains(normalized)))
                        continue;

                    if (field.Key == "institution_symbol" &&
                        (normalized.Contains("שם") || normalized.Contains("name")))
                        continue;
                    if (field.Key == "institution_name" &&
                        (normalized.Contains("סמל") || normalized.Contains("symbol")))
                        continue;
                    if (field.Key == "class_type_code" &&
                        normalized.Contains("חריג"))
                        continue;
                    if (field.Key == "annual_hours" &&
                        (normalized.Contains("שיבוץ") || normalized.Contains("קיזוז")))
                        continue;

                    if (!mappings.ContainsKey(header))
                        mappings[header] = field.Key;
                    break;
                }
            }

            return mappings;
        }

        public async Task<EntitlementFieldMapping?> GetSavedMappingAsync()
            => await _context.EntitlementFieldMappings.AsNoTracking().FirstOrDefaultAsync();

        public async Task SaveMappingAsync(int entityId, int? userId, Dictionary<string, string> mapping)
        {
            var existing = await _context.EntitlementFieldMappings.FirstOrDefaultAsync();
            var json = JsonSerializer.Serialize(mapping);
            var now = DateTime.UtcNow;

            if (existing == null)
            {
                _context.EntitlementFieldMappings.Add(new EntitlementFieldMapping
                {
                    EntityId = entityId,
                    MappingJson = json,
                    CreatedAt = now,
                    UserId = userId,
                    UpdatedAt = now,
                    UpdateUser = userId
                });
            }
            else
            {
                existing.MappingJson = json;
                existing.UpdatedAt = now;
                existing.UpdateUser = userId;
            }

            await _context.SaveChangesAsync();
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

        public List<EntitlementFileRow> ParseFile(IFormFile file, Dictionary<string, string> mapping)
        {
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            using var stream = file.OpenReadStream();

            if (ext == ".csv")
                return ParseCsv(stream, mapping);

            if (ext is ".xls" or ".xlsx")
                return ParseExcel(stream, mapping);

            throw new InvalidOperationException("פורמט קובץ לא נתמך. יש להשתמש ב-CSV, XLS או XLSX");
        }

        public async Task<EntitlementFileProcessingResult> ProcessUploadAsync(
            int entityId,
            int? userId,
            int yearId,
            string? fileName,
            List<EntitlementFileRow> rows)
        {
            var year = await _sharedContext.HebrewYears.AsNoTracking()
                .FirstOrDefaultAsync(y => y.Id == yearId)
                ?? throw new InvalidOperationException("שנה עברית לא נמצאה");

            if (!year.StartDate.HasValue || !year.EndDate.HasValue)
                throw new InvalidOperationException("יש להגדיר תאריכי התחלה וסיום לשנה העברית");

            var classHelp = await _sharedContext.AssistantTypes.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Name == TypeClassHelp && t.IsActive)
                ?? throw new InvalidOperationException("סוג סייעת כיתתית לא נמצא");

            var schoolHelp = await _sharedContext.AssistantTypes.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Name == TypeSchoolHelp && t.IsActive)
                ?? throw new InvalidOperationException("סוג סייעת תגבור מוסדית לא נמצא");

            var institutions = await _context.Institutions.AsNoTracking()
                .Where(i => i.IsActive && i.Symbol != null && i.Symbol != "")
                .ToListAsync();
            var institutionBySymbol = institutions
                .GroupBy(i => NormalizeSymbol(i.Symbol!), StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

            var classifications = await _sharedContext.ClassClassifications.AsNoTracking()
                .Where(c => c.IsActive)
                .ToListAsync();

            var existing = await _context.Entitlements
                .Where(e => e.HebrewYearId == yearId && e.IsLastVersion && !e.IsCancelled)
                .Where(e => e.AssistantTypeId == classHelp.Id || e.AssistantTypeId == schoolHelp.Id)
                .ToListAsync();

            var existingByKey = existing
                .GroupBy(e => BuildLookupKey(e.AssistantTypeId, e.InstitutionId, e.SourceInstitutionSymbol, e.ClassName))
                .ToDictionary(g => g.Key, g => g.First());

            var now = DateTime.UtcNow;
            var process = new EntitlementUploadProcess
            {
                EntityId = entityId,
                HebrewYearId = yearId,
                FileName = fileName,
                CreatedAt = now,
                UserId = userId,
                UpdatedAt = now,
                UpdateUser = userId
            };
            _context.EntitlementUploadProcesses.Add(process);
            await _context.SaveChangesAsync();

            var result = new EntitlementFileProcessingResult { ProcessId = process.Id };
            var seenKeys = new HashSet<string>(StringComparer.Ordinal);

            foreach (var row in rows)
            {
                try
                {
                    if (row.ParseError)
                    {
                        result.Errors++;
                        result.ErrorList.Add($"שורה {row.RowNumber}: {row.ParseErrorMessage}");
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(row.HebrewYear) &&
                        !HebrewYearNamesMatch(row.HebrewYear, year.YearName))
                    {
                        result.Errors++;
                        result.ErrorList.Add(
                            $"שורה {row.RowNumber}: שנת לימודים בקובץ ({row.HebrewYear}) אינה תואמת לשנה שנבחרה ({year.YearName})");
                        continue;
                    }

                    var symbol = NormalizeSymbol(row.InstitutionSymbol);
                    institutionBySymbol.TryGetValue(symbol, out var institution);
                    int? institutionId = institution?.Id;

                    var support = row.SupportType.Trim();
                    int assistantTypeId;
                    string? className = null;
                    int? classificationId = null;

                    if (support == SupportAutomatic)
                    {
                        assistantTypeId = classHelp.Id;

                        if (string.IsNullOrWhiteSpace(row.GradeLayer) || string.IsNullOrWhiteSpace(row.GradeParallel))
                        {
                            result.Errors++;
                            result.ErrorList.Add($"שורה {row.RowNumber}: שכבה ומקבילה נדרשות לסייעת כיתתית");
                            continue;
                        }

                        className = $"{row.GradeLayer.Trim()}{row.GradeParallel.Trim()}";

                        if (string.IsNullOrWhiteSpace(row.ClassTypeCode))
                        {
                            result.Errors++;
                            result.ErrorList.Add($"שורה {row.RowNumber}: סוג כיתה (סיווג) חסר");
                            continue;
                        }

                        classificationId = ResolveClassificationId(classifications, row.ClassTypeCode);
                        if (classificationId == null)
                        {
                            result.Errors++;
                            result.ErrorList.Add($"שורה {row.RowNumber}: סוג כיתה לא מוכר: {row.ClassTypeCode}");
                            continue;
                        }
                    }
                    else if (support == SupportSchoolBoost)
                    {
                        assistantTypeId = schoolHelp.Id;
                    }
                    else
                    {
                        result.Errors++;
                        result.ErrorList.Add($"שורה {row.RowNumber}: סוג תומכת חינוך לא מוכר: {support}");
                        continue;
                    }

                    if (row.AnnualHours <= 0)
                    {
                        result.Errors++;
                        result.ErrorList.Add($"שורה {row.RowNumber}: שעות שנתיות חייבות להיות גדולות מאפס");
                        continue;
                    }

                    if (row.ParticipationPct < 0 || row.ParticipationPct > 100)
                    {
                        result.Errors++;
                        result.ErrorList.Add($"שורה {row.RowNumber}: אחוז השתתפות חייב להיות בין 0 ל-100");
                        continue;
                    }

                    var weeklyHours = Round2(row.AnnualHours / 12m);
                    if (weeklyHours <= 0)
                    {
                        result.Errors++;
                        result.ErrorList.Add($"שורה {row.RowNumber}: שעות שבועיות מחושבות אינן תקינות");
                        continue;
                    }

                    var validity = new EntitlementUploadValidity
                    {
                        SourceInstitutionSymbol = string.IsNullOrEmpty(symbol) ? null : symbol
                    };
                    if (!institutionId.HasValue)
                        validity.Reasons.Add(EntitlementInvalidReasons.MissingInstitution);

                    if (!validity.IsValid)
                    {
                        result.Invalid++;
                        result.InvalidList.Add(
                            $"שורה {row.RowNumber}: יובאה כלא תקינה — {EntitlementInvalidReasons.ToHebrewList(validity.ReasonsCsv)}");
                    }

                    var key = BuildLookupKey(assistantTypeId, institutionId, symbol, className);
                    var unmatchedKey = BuildUnmatchedKey(assistantTypeId, symbol, className);
                    seenKeys.Add(key);
                    if (institutionId.HasValue)
                        seenKeys.Add(unmatchedKey);

                    if (!existingByKey.TryGetValue(key, out var current) &&
                        institutionId.HasValue)
                        existingByKey.TryGetValue(unmatchedKey, out current);

                    if (current != null)
                    {
                        var exact =
                            current.Hours == weeklyHours &&
                            current.HoursUnit == HoursUnits.Weekly &&
                            current.MinistryParticipationPct == row.ParticipationPct &&
                            current.ClassClassificationId == classificationId &&
                            current.InstitutionId == institutionId &&
                            current.IsValid == validity.IsValid &&
                            string.Equals(current.InvalidReasons, validity.ReasonsCsv, StringComparison.Ordinal);

                        if (exact)
                        {
                            result.Skipped++;
                            continue;
                        }

                        await _entitlementService.ApplyUploadVersionAsync(
                            userId,
                            current.Id,
                            weeklyHours,
                            HoursUnits.Weekly,
                            row.ParticipationPct,
                            classificationId,
                            institutionId,
                            validity);

                        var refreshed = await _context.Entitlements
                            .FirstAsync(e => e.MasterEntitlementId == current.MasterEntitlementId && e.IsLastVersion);
                        existingByKey.Remove(unmatchedKey);
                        existingByKey[key] = refreshed;
                        result.Versioned++;
                        continue;
                    }

                    var createdId = await _entitlementService.CreateEntitlementAsync(
                        entityId,
                        userId,
                        new CreateEntitlementRequest
                        {
                            HebrewYearId = yearId,
                            AssistantTypeId = assistantTypeId,
                            StartDate = year.StartDate,
                            EndDate = year.EndDate,
                            Hours = weeklyHours,
                            HoursUnit = HoursUnits.Weekly,
                            MinistryParticipationPct = row.ParticipationPct,
                            InstitutionId = institutionId,
                            ClassName = className,
                            ClassClassificationId = classificationId
                        },
                        validity);

                    var created = await _context.Entitlements.FirstAsync(e => e.Id == createdId);
                    existingByKey[key] = created;
                    result.Created++;
                }
                catch (Exception ex)
                {
                    result.Errors++;
                    result.ErrorList.Add($"שורה {row.RowNumber}: {ex.Message}");
                    _logger.LogWarning(ex, "Entitlement upload row {Row} failed", row.RowNumber);
                }
            }

            // Orphans: institutional entitlements for year not present in file keys
            var orphanCandidates = await _context.Entitlements
                .AsNoTracking()
                .Include(e => e.Institution)
                .Where(e => e.HebrewYearId == yearId && e.IsLastVersion && !e.IsCancelled)
                .Where(e => e.AssistantTypeId == classHelp.Id || e.AssistantTypeId == schoolHelp.Id)
                .ToListAsync();

            var typeNameById = new Dictionary<int, string>
            {
                [classHelp.Id] = classHelp.DisplayName,
                [schoolHelp.Id] = schoolHelp.DisplayName
            };

            foreach (var entitlement in orphanCandidates)
            {
                var key = BuildLookupKey(
                    entitlement.AssistantTypeId,
                    entitlement.InstitutionId,
                    entitlement.SourceInstitutionSymbol,
                    entitlement.ClassName);
                if (seenKeys.Contains(key))
                    continue;

                result.Orphans.Add(new EntitlementOrphanDto
                {
                    Id = entitlement.Id,
                    InstitutionName = entitlement.Institution?.Name ?? string.Empty,
                    AssistantTypeName = typeNameById.GetValueOrDefault(entitlement.AssistantTypeId, string.Empty),
                    ClassName = entitlement.ClassName,
                    Hours = entitlement.Hours,
                    HoursUnit = entitlement.HoursUnit,
                    MinistryParticipationPct = entitlement.MinistryParticipationPct
                });
            }

            process.CreatedCount = result.Created;
            process.VersionedCount = result.Versioned;
            process.SkippedCount = result.Skipped;
            process.ErrorCount = result.Errors;
            process.UpdatedAt = DateTime.UtcNow;
            process.UpdateUser = userId;
            await _context.SaveChangesAsync();

            return result;
        }

        public async Task<int> CancelOrphansAsync(int? userId, int yearId, List<int> entitlementIds)
        {
            if (entitlementIds.Count == 0)
                return 0;

            var classHelp = await _sharedContext.AssistantTypes.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Name == TypeClassHelp);
            var schoolHelp = await _sharedContext.AssistantTypes.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Name == TypeSchoolHelp);

            var institutionalTypeIds = new HashSet<int>();
            if (classHelp != null) institutionalTypeIds.Add(classHelp.Id);
            if (schoolHelp != null) institutionalTypeIds.Add(schoolHelp.Id);

            var ids = entitlementIds.Distinct().ToList();
            var entitlements = await _context.Entitlements.AsNoTracking()
                .Where(e => ids.Contains(e.Id)
                            && e.HebrewYearId == yearId
                            && e.IsLastVersion
                            && !e.IsCancelled
                            && institutionalTypeIds.Contains(e.AssistantTypeId))
                .Select(e => e.Id)
                .ToListAsync();

            var cancelled = 0;
            foreach (var id in entitlements)
            {
                await _entitlementService.DeactivateEntitlementAsync(userId, id);
                cancelled++;
            }

            return cancelled;
        }

        private static int? ResolveClassificationId(List<ClassClassification> classifications, string rawCode)
        {
            var code = rawCode.Trim();
            if (!int.TryParse(code, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numeric))
                return null;

            var byId = classifications.FirstOrDefault(c => c.Id == numeric);
            if (byId != null)
                return byId.Id;

            var byForeign = classifications.FirstOrDefault(c => c.ForeignId == numeric);
            return byForeign?.Id;
        }

        private static string BuildLookupKey(
            int assistantTypeId,
            int? institutionId,
            string? sourceSymbol,
            string? className)
            => institutionId.HasValue
                ? $"{assistantTypeId}|{institutionId.Value}|{className ?? string.Empty}"
                : BuildUnmatchedKey(assistantTypeId, sourceSymbol, className);

        private static string BuildUnmatchedKey(int assistantTypeId, string? sourceSymbol, string? className)
            => $"{assistantTypeId}|sym:{sourceSymbol ?? string.Empty}|{className ?? string.Empty}";

        private static string NormalizeSymbol(string symbol)
        {
            var trimmed = symbol.Trim();
            if (trimmed.EndsWith(".0", StringComparison.Ordinal) &&
                trimmed.Length > 2 &&
                trimmed[..^2].All(char.IsDigit))
                trimmed = trimmed[..^2];
            return trimmed;
        }

        private static bool HebrewYearNamesMatch(string fileYear, string systemYear)
        {
            static string Normalize(string value) =>
                value.Trim()
                    .Replace("\"", string.Empty, StringComparison.Ordinal)
                    .Replace("״", string.Empty, StringComparison.Ordinal)
                    .Replace("\u05f4", string.Empty, StringComparison.Ordinal)
                    .Replace("'", string.Empty, StringComparison.Ordinal)
                    .Replace("׳", string.Empty, StringComparison.Ordinal);

            return string.Equals(Normalize(fileYear), Normalize(systemYear), StringComparison.Ordinal);
        }

        private static decimal Round2(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

        private List<EntitlementFileRow> ParseExcel(Stream stream, Dictionary<string, string> mapping)
        {
            var rows = new List<EntitlementFileRow>();
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

                var row = BuildRow(rowNumber, values, mapping);
                if (row != null)
                    rows.Add(row);
            }

            return rows;
        }

        private List<EntitlementFileRow> ParseCsv(Stream stream, Dictionary<string, string> mapping)
        {
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            var headerLine = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(headerLine))
                return [];

            var delimiter = headerLine.Contains('\t') ? '\t' : ',';
            var headers = SplitCsvLine(headerLine, delimiter)
                .Select(h => h.Trim().Trim('"'))
                .ToList();

            var rows = new List<EntitlementFileRow>();
            var rowNumber = 1;
            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();
                rowNumber++;
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var cells = SplitCsvLine(line, delimiter);
                var values = BuildValueMap(headers, i => i < cells.Count ? cells[i].Trim().Trim('"') : string.Empty);
                var row = BuildRow(rowNumber, values, mapping);
                if (row != null)
                    rows.Add(row);
            }

            return rows;
        }

        private static List<string> ReadCsvHeaders(Stream stream)
        {
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            var headerLine = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(headerLine))
                return [];

            var delimiter = headerLine.Contains('\t') ? '\t' : ',';
            return SplitCsvLine(headerLine, delimiter)
                .Select(h => h.Trim().Trim('"'))
                .Where(h => !string.IsNullOrEmpty(h))
                .ToList();
        }

        private static Dictionary<string, string> BuildValueMap(List<string> headers, Func<int, string> getCell)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < headers.Count; i++)
            {
                var header = headers[i];
                if (string.IsNullOrEmpty(header) || values.ContainsKey(header))
                    continue;
                values[header] = getCell(i);
            }
            return values;
        }

        private static EntitlementFileRow? BuildRow(
            int rowNumber,
            Dictionary<string, string> values,
            Dictionary<string, string> mapping)
        {
            string Get(string field)
            {
                if (!mapping.TryGetValue(field, out var header) || string.IsNullOrWhiteSpace(header))
                    return string.Empty;
                return values.TryGetValue(header, out var v) ? v : string.Empty;
            }

            var symbol = Get("institution_symbol");
            var name = Get("institution_name");
            var support = Get("support_type");
            var hoursRaw = Get("annual_hours");
            var pctRaw = Get("participation_pct");

            if (string.IsNullOrWhiteSpace(symbol) &&
                string.IsNullOrWhiteSpace(name) &&
                string.IsNullOrWhiteSpace(support) &&
                string.IsNullOrWhiteSpace(hoursRaw))
                return null;

            var row = new EntitlementFileRow
            {
                RowNumber = rowNumber,
                InstitutionSymbol = symbol,
                InstitutionName = name,
                SupportType = support,
                GradeLayer = NullIfEmpty(Get("grade_layer")),
                GradeParallel = NullIfEmpty(Get("grade_parallel")),
                ClassTypeCode = NullIfEmpty(Get("class_type_code")),
                HebrewYear = NullIfEmpty(Get("hebrew_year"))
            };

            if (!TryParseDecimal(hoursRaw, out var annualHours))
            {
                row.ParseError = true;
                row.ParseErrorMessage = "שעות שנתיות אינן מספר תקין";
                return row;
            }

            if (!TryParseDecimal(pctRaw, out var pct))
            {
                row.ParseError = true;
                row.ParseErrorMessage = "אחוז השתתפות אינו מספר תקין";
                return row;
            }

            row.AnnualHours = annualHours;
            row.ParticipationPct = pct;
            return row;
        }

        private static string? NullIfEmpty(string value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static bool TryParseDecimal(string raw, out decimal value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(raw))
                return false;

            var cleaned = raw.Trim().Replace("%", string.Empty, StringComparison.Ordinal).Replace(",", string.Empty);
            return decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out value)
                   || decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.GetCultureInfo("he-IL"), out value);
        }

        private static List<string> SplitCsvLine(string line, char delimiter)
        {
            var result = new List<string>();
            var current = new StringBuilder();
            var inQuotes = false;

            foreach (var ch in line)
            {
                if (ch == '"')
                {
                    inQuotes = !inQuotes;
                    continue;
                }

                if (ch == delimiter && !inQuotes)
                {
                    result.Add(current.ToString());
                    current.Clear();
                    continue;
                }

                current.Append(ch);
            }

            result.Add(current.ToString());
            return result;
        }
    }
}
