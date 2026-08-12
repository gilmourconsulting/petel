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
    public class PersonalEntitlementFileProcessor
    {
        private const string TypeStudentHelp = "student_help";

        private readonly AssistDbContext _context;
        private readonly SharedDbContext _sharedContext;
        private readonly EntitlementService _entitlementService;
        private readonly ILogger<PersonalEntitlementFileProcessor> _logger;

        public PersonalEntitlementFileProcessor(
            AssistDbContext context,
            SharedDbContext sharedContext,
            EntitlementService entitlementService,
            ILogger<PersonalEntitlementFileProcessor> logger)
        {
            _context = context;
            _sharedContext = sharedContext;
            _entitlementService = entitlementService;
            _logger = logger;
        }

        public static Dictionary<string, string> GetAvailableFields() => new()
        {
            { "pupil_id_number", "ת.ז. תלמיד" },
            { "pupil_first_name", "שם פרטי" },
            { "pupil_last_name", "שם משפחה" },
            { "institution_symbol", "סמל מוסד" },
            { "hours", "שעות" },
            { "authority_participation_pct", "השתתפות הרשות" },
            { "start_date", "מתאריך" },
            { "end_date", "עד תאריך" },
            { "ignore", "התעלם" }
        };

        public static string? ValidateMapping(Dictionary<string, string> mapping)
        {
            string[] required =
            [
                "pupil_id_number",
                "pupil_first_name",
                "pupil_last_name",
                "institution_symbol",
                "hours",
                "authority_participation_pct",
                "start_date",
                "end_date"
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
                { "pupil_id_number", ["ת.ז. תלמיד", "ת.ז", "תז תלמיד", "pupil_id", "student_id"] },
                { "pupil_first_name", ["שם פרטי", "first_name", "pupil_first_name"] },
                { "pupil_last_name", ["שם משפחה", "last_name", "pupil_last_name"] },
                { "institution_symbol", ["סמל מוסד", "סמל", "symbol", "institution_symbol"] },
                { "hours", ["שעות", "hours"] },
                { "authority_participation_pct", ["השתתפות הרשות", "השתתפות", "participation"] },
                { "start_date", ["מתאריך", "תאריך התחלה", "start_date"] },
                { "end_date", ["עד תאריך", "תאריך סיום", "end_date"] }
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
                        (normalized.Contains("שם") || normalized.Contains("name") || normalized.Contains("רשות")))
                        continue;
                    if (field.Key == "pupil_first_name" && normalized.Contains("משפחה"))
                        continue;
                    if (field.Key == "pupil_last_name" && normalized.Contains("פרטי"))
                        continue;
                    if (field.Key == "start_date" &&
                        (normalized.Contains("אישור") || normalized.Contains("עד")))
                        continue;
                    if (field.Key == "end_date" && normalized.Contains("מתאריך"))
                        continue;
                    if (field.Key == "hours" &&
                        (normalized.Contains("משרה") || normalized.Contains("שנתי")))
                        continue;

                    if (!mappings.ContainsKey(header))
                        mappings[header] = field.Key;
                    break;
                }
            }

            return mappings;
        }

        public async Task<PersonalEntitlementFieldMapping?> GetSavedMappingAsync()
            => await _context.PersonalEntitlementFieldMappings.AsNoTracking().FirstOrDefaultAsync();

        public async Task SaveMappingAsync(int entityId, int? userId, Dictionary<string, string> mapping)
        {
            var existing = await _context.PersonalEntitlementFieldMappings.FirstOrDefaultAsync();
            var json = JsonSerializer.Serialize(mapping);
            var now = DateTime.UtcNow;

            if (existing == null)
            {
                _context.PersonalEntitlementFieldMappings.Add(new PersonalEntitlementFieldMapping
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

        public List<PersonalEntitlementFileRow> ParseFile(IFormFile file, Dictionary<string, string> mapping)
        {
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            using var stream = file.OpenReadStream();

            if (ext == ".csv")
                return ParseCsv(stream, mapping);

            if (ext is ".xls" or ".xlsx")
                return ParseExcel(stream, mapping);

            throw new InvalidOperationException("פורמט קובץ לא נתמך. יש להשתמש ב-CSV, XLS או XLSX");
        }

        public async Task<PersonalEntitlementFileProcessingResult> ProcessUploadAsync(
            int entityId,
            int? userId,
            int yearId,
            string? fileName,
            List<PersonalEntitlementFileRow> rows)
        {
            var year = await _sharedContext.HebrewYears.AsNoTracking()
                .FirstOrDefaultAsync(y => y.Id == yearId)
                ?? throw new InvalidOperationException("שנה עברית לא נמצאה");

            if (!year.StartDate.HasValue || !year.EndDate.HasValue)
                throw new InvalidOperationException("יש להגדיר תאריכי התחלה וסיום לשנה העברית");

            var studentHelp = await _sharedContext.AssistantTypes.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Name == TypeStudentHelp && t.IsActive)
                ?? throw new InvalidOperationException("סוג סייעת student_help לא נמצא במערכת");

            var institutions = await _context.Institutions.AsNoTracking()
                .Where(i => i.IsActive && i.Symbol != null && i.Symbol != string.Empty)
                .Select(i => new { i.Id, i.Symbol, i.Name })
                .ToListAsync();

            var institutionBySymbol = institutions
                .GroupBy(i => NormalizeSymbol(i.Symbol!), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var existing = await _context.Entitlements
                .Where(e => e.HebrewYearId == yearId
                            && e.AssistantTypeId == studentHelp.Id
                            && e.IsLastVersion
                            && !e.IsCancelled)
                .ToListAsync();

            var existingByPupil = existing
                .Where(e => !string.IsNullOrWhiteSpace(e.PupilIdNumber))
                .GroupBy(e => e.PupilIdNumber!, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

            var now = DateTime.UtcNow;
            var process = new EntitlementUploadProcess
            {
                EntityId = entityId,
                HebrewYearId = yearId,
                FileName = fileName,
                CreatedCount = 0,
                VersionedCount = 0,
                SkippedCount = 0,
                ErrorCount = 0,
                CreatedAt = now,
                UserId = userId,
                UpdatedAt = now,
                UpdateUser = userId
            };
            _context.EntitlementUploadProcesses.Add(process);
            await _context.SaveChangesAsync();

            var result = new PersonalEntitlementFileProcessingResult { ProcessId = process.Id };
            var seenKeys = new HashSet<string>(StringComparer.Ordinal);

            var fileDuplicates = new HashSet<string>(
                rows.Where(r => !r.ParseError)
                    .Select(r => NormalizePupilId(r.PupilIdNumber))
                    .Where(id => !string.IsNullOrEmpty(id))
                    .GroupBy(id => id, StringComparer.Ordinal)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key),
                StringComparer.Ordinal);

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

                    var pupilId = NormalizePupilId(row.PupilIdNumber);
                    if (pupilId.Length != 9 || !pupilId.All(char.IsDigit))
                    {
                        result.Errors++;
                        result.ErrorList.Add($"שורה {row.RowNumber}: תעודת זהות חייבת להכיל בדיוק 9 ספרות");
                        continue;
                    }

                    if (fileDuplicates.Contains(pupilId))
                    {
                        result.Errors++;
                        result.ErrorList.Add($"שורה {row.RowNumber}: ת.ז. {pupilId} מופיעה יותר מפעם אחת בקובץ");
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(row.PupilFirstName) || string.IsNullOrWhiteSpace(row.PupilLastName))
                    {
                        result.Errors++;
                        result.ErrorList.Add($"שורה {row.RowNumber}: שם פרטי ושם משפחה נדרשים");
                        continue;
                    }

                    var symbol = NormalizeSymbol(row.InstitutionSymbol);
                    if (string.IsNullOrEmpty(symbol))
                    {
                        result.Errors++;
                        result.ErrorList.Add($"שורה {row.RowNumber}: סמל מוסד חסר");
                        continue;
                    }

                    if (!institutionBySymbol.TryGetValue(symbol, out var institution))
                    {
                        result.Errors++;
                        result.ErrorList.Add($"שורה {row.RowNumber}: לא נמצא מוסד עם סמל {symbol}");
                        continue;
                    }

                    if (row.Hours <= 0)
                    {
                        result.Errors++;
                        result.ErrorList.Add($"שורה {row.RowNumber}: שעות חייבות להיות גדולות מאפס");
                        continue;
                    }

                    if (row.AuthorityParticipationPct < 0 || row.AuthorityParticipationPct > 100)
                    {
                        result.Errors++;
                        result.ErrorList.Add($"שורה {row.RowNumber}: אחוז השתתפות הרשות חייב להיות בין 0 ל-100");
                        continue;
                    }

                    if (!row.StartDate.HasValue || !row.EndDate.HasValue)
                    {
                        result.Errors++;
                        result.ErrorList.Add($"שורה {row.RowNumber}: תאריכי התחלה וסיום נדרשים");
                        continue;
                    }

                    var weeklyHours = Round2(row.Hours);
                    var ministryPct = Round2(100m - row.AuthorityParticipationPct);
                    if (ministryPct < 0 || ministryPct > 100)
                    {
                        result.Errors++;
                        result.ErrorList.Add($"שורה {row.RowNumber}: אחוז השתתפות משרד מחושב אינו תקין");
                        continue;
                    }

                    var firstName = row.PupilFirstName.Trim();
                    var lastName = row.PupilLastName.Trim();
                    var startDate = row.StartDate.Value;
                    var endDate = row.EndDate.Value;

                    seenKeys.Add(pupilId);

                    if (existingByPupil.TryGetValue(pupilId, out var current))
                    {
                        var exact =
                            current.Hours == weeklyHours &&
                            current.HoursUnit == HoursUnits.Weekly &&
                            current.MinistryParticipationPct == ministryPct &&
                            current.InstitutionId == institution.Id &&
                            current.StartDate == startDate &&
                            current.EndDate == endDate &&
                            string.Equals(current.PupilFirstName, firstName, StringComparison.Ordinal) &&
                            string.Equals(current.PupilLastName, lastName, StringComparison.Ordinal);

                        if (exact)
                        {
                            result.Skipped++;
                            continue;
                        }

                        await _entitlementService.ApplyPersonalUploadVersionAsync(
                            userId,
                            current.Id,
                            institution.Id,
                            weeklyHours,
                            HoursUnits.Weekly,
                            ministryPct,
                            startDate,
                            endDate,
                            firstName,
                            lastName);

                        var refreshed = await _context.Entitlements
                            .FirstAsync(e => e.MasterEntitlementId == current.MasterEntitlementId && e.IsLastVersion);
                        existingByPupil[pupilId] = refreshed;
                        result.Versioned++;
                        continue;
                    }

                    await _entitlementService.CreateEntitlementAsync(
                        entityId,
                        userId,
                        new CreateEntitlementRequest
                        {
                            HebrewYearId = yearId,
                            AssistantTypeId = studentHelp.Id,
                            StartDate = startDate,
                            EndDate = endDate,
                            Hours = weeklyHours,
                            HoursUnit = HoursUnits.Weekly,
                            MinistryParticipationPct = ministryPct,
                            InstitutionId = institution.Id,
                            PupilIdNumber = pupilId,
                            PupilFirstName = firstName,
                            PupilLastName = lastName
                        });

                    var created = await _context.Entitlements
                        .FirstAsync(e => e.HebrewYearId == yearId
                                         && e.AssistantTypeId == studentHelp.Id
                                         && e.PupilIdNumber == pupilId
                                         && e.IsLastVersion
                                         && !e.IsCancelled);
                    existingByPupil[pupilId] = created;
                    result.Created++;
                }
                catch (InvalidOperationException ex)
                {
                    result.Errors++;
                    result.ErrorList.Add($"שורה {row.RowNumber}: {ex.Message}");
                    _logger.LogWarning(ex, "Personal entitlement row {Row} failed", row.RowNumber);
                }
                catch (Exception ex)
                {
                    result.Errors++;
                    result.ErrorList.Add($"שורה {row.RowNumber}: שגיאה בעיבוד השורה");
                    _logger.LogError(ex, "Unexpected error on personal entitlement row {Row}", row.RowNumber);
                }
            }

            var institutionNames = institutions.ToDictionary(i => i.Id, i => i.Name);
            var orphanCandidates = await _context.Entitlements.AsNoTracking()
                .Where(e => e.HebrewYearId == yearId
                            && e.AssistantTypeId == studentHelp.Id
                            && e.IsLastVersion
                            && !e.IsCancelled)
                .ToListAsync();

            foreach (var entitlement in orphanCandidates)
            {
                var key = entitlement.PupilIdNumber;
                if (string.IsNullOrWhiteSpace(key) || seenKeys.Contains(key))
                    continue;

                result.Orphans.Add(new PersonalEntitlementOrphanDto
                {
                    Id = entitlement.Id,
                    PupilIdNumber = entitlement.PupilIdNumber,
                    PupilFirstName = entitlement.PupilFirstName,
                    PupilLastName = entitlement.PupilLastName,
                    InstitutionName = entitlement.InstitutionId.HasValue &&
                                      institutionNames.TryGetValue(entitlement.InstitutionId.Value, out var n)
                        ? n
                        : string.Empty,
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

            var studentHelp = await _sharedContext.AssistantTypes.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Name == TypeStudentHelp);

            if (studentHelp == null)
                return 0;

            var ids = entitlementIds.Distinct().ToList();
            var entitlements = await _context.Entitlements.AsNoTracking()
                .Where(e => ids.Contains(e.Id)
                            && e.HebrewYearId == yearId
                            && e.IsLastVersion
                            && !e.IsCancelled
                            && e.AssistantTypeId == studentHelp.Id)
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

        private static string NormalizeSymbol(string symbol)
        {
            var trimmed = symbol.Trim();
            if (trimmed.EndsWith(".0", StringComparison.Ordinal) &&
                trimmed.Length > 2 &&
                trimmed[..^2].All(char.IsDigit))
                trimmed = trimmed[..^2];
            return trimmed;
        }

        private static string NormalizePupilId(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return string.Empty;

            var digits = new string(raw.Trim().Where(char.IsDigit).ToArray());
            if (digits.Length == 0)
                return string.Empty;
            if (digits.Length < 9)
                return digits.PadLeft(9, '0');
            if (digits.Length > 9)
                return digits[^9..];
            return digits;
        }

        private static decimal Round2(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

        private List<PersonalEntitlementFileRow> ParseExcel(Stream stream, Dictionary<string, string> mapping)
        {
            var rows = new List<PersonalEntitlementFileRow>();
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

        private List<PersonalEntitlementFileRow> ParseCsv(Stream stream, Dictionary<string, string> mapping)
        {
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            var headerLine = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(headerLine))
                return [];

            var delimiter = headerLine.Contains('\t') ? '\t' : ',';
            var headers = SplitCsvLine(headerLine, delimiter)
                .Select(h => h.Trim().Trim('"'))
                .ToList();

            var rows = new List<PersonalEntitlementFileRow>();
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

        private static PersonalEntitlementFileRow? BuildRow(
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

            var pupilId = Get("pupil_id_number");
            var firstName = Get("pupil_first_name");
            var lastName = Get("pupil_last_name");
            var symbol = Get("institution_symbol");
            var hoursRaw = Get("hours");
            var pctRaw = Get("authority_participation_pct");
            var startRaw = Get("start_date");
            var endRaw = Get("end_date");

            if (string.IsNullOrWhiteSpace(pupilId) &&
                string.IsNullOrWhiteSpace(firstName) &&
                string.IsNullOrWhiteSpace(lastName) &&
                string.IsNullOrWhiteSpace(symbol) &&
                string.IsNullOrWhiteSpace(hoursRaw))
                return null;

            var row = new PersonalEntitlementFileRow
            {
                RowNumber = rowNumber,
                PupilIdNumber = pupilId,
                PupilFirstName = firstName,
                PupilLastName = lastName,
                InstitutionSymbol = symbol
            };

            if (!TryParseDecimal(hoursRaw, out var hours))
            {
                row.ParseError = true;
                row.ParseErrorMessage = "שעות אינן מספר תקין";
                return row;
            }

            if (!TryParseParticipationPct(pctRaw, out var pct))
            {
                row.ParseError = true;
                row.ParseErrorMessage = "אחוז השתתפות הרשות אינו מספר תקין";
                return row;
            }

            if (!TryParseDate(startRaw, out var startDate))
            {
                row.ParseError = true;
                row.ParseErrorMessage = "תאריך התחלה אינו תקין";
                return row;
            }

            if (!TryParseDate(endRaw, out var endDate))
            {
                row.ParseError = true;
                row.ParseErrorMessage = "תאריך סיום אינו תקין";
                return row;
            }

            row.Hours = hours;
            row.AuthorityParticipationPct = pct;
            row.StartDate = startDate;
            row.EndDate = endDate;
            return row;
        }

        private static bool TryParseDecimal(string raw, out decimal value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(raw))
                return false;

            var cleaned = raw.Trim().Replace("%", string.Empty, StringComparison.Ordinal).Replace(",", string.Empty);
            return decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out value)
                   || decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.GetCultureInfo("he-IL"), out value);
        }

        /// <summary>
        /// Parses municipality participation. Handles "30%", "30", and Excel fraction 0.30.
        /// </summary>
        private static bool TryParseParticipationPct(string raw, out decimal value)
        {
            value = 0;
            if (!TryParseDecimal(raw, out var parsed))
                return false;

            // Excel percentage cells often surface as 0–1 fractions when not formatted with %
            if (parsed > 0 && parsed <= 1 && !raw.Contains('%', StringComparison.Ordinal))
                parsed *= 100m;

            value = Round2(parsed);
            return true;
        }

        private static bool TryParseDate(string raw, out DateOnly date)
        {
            date = default;
            if (string.IsNullOrWhiteSpace(raw))
                return false;

            var trimmed = raw.Trim();
            string[] formats =
            [
                "dd/MM/yyyy",
                "d/M/yyyy",
                "dd/MM/yy",
                "d/M/yy",
                "yyyy-MM-dd",
                "dd-MM-yyyy",
                "d-M-yyyy"
            ];

            if (DateOnly.TryParseExact(trimmed, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
                return true;

            if (DateOnly.TryParse(trimmed, CultureInfo.GetCultureInfo("he-IL"), DateTimeStyles.None, out date))
                return true;

            if (DateOnly.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
                return true;

            // Excel serial date as number
            if (double.TryParse(trimmed, NumberStyles.Number, CultureInfo.InvariantCulture, out var oa) &&
                oa > 20000 && oa < 80000)
            {
                try
                {
                    date = DateOnly.FromDateTime(DateTime.FromOADate(oa));
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            return false;
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
