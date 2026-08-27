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
    public class SalaryFileProcessor
    {
        private readonly AssistDbContext _context;
        private readonly MonthlyImportComparisonService _comparisonService;
        private readonly ILogger<SalaryFileProcessor> _logger;

        public SalaryFileProcessor(
            AssistDbContext context,
            MonthlyImportComparisonService comparisonService,
            ILogger<SalaryFileProcessor> logger)
        {
            _context = context;
            _comparisonService = comparisonService;
            _logger = logger;
        }

        public static Dictionary<string, string> GetAvailableFields() => new()
        {
            { "national_id", "תעודת זהות" },
            { "department_id", "מזהה מחלקה" },
            { "department_name", "שם מחלקה" },
            { "position_percentage", "אחוז משרה" },
            { "total_salary", "שכר כולל" },
            { "ignore", "התעלם" }
        };

        public static string? ValidateMapping(Dictionary<string, string> mapping)
        {
            string[] required = ["national_id", "department_id", "position_percentage", "total_salary"];
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
                { "national_id", new[] { "תעודת זהות", "ת.ז", "תז", "מספר זהות", "id", "id_number", "national_id", "מזהה" } },
                { "department_id", new[] { "מזהה מחלקה", "קוד מחלקה", "department_id", "dept_id", "מחלקה" } },
                { "department_name", new[] { "שם מחלקה", "department_name", "department", "מחלקה" } },
                { "position_percentage", new[] { "אחוז משרה", "אחוז", "position", "percentage", "משרה" } },
                { "total_salary", new[] { "שכר כולל", "שכר", "total_salary", "salary", "סכום" } }
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
                        // Prefer department_id for code-like headers; department_name for name-like
                        if (field.Key == "department_id" &&
                            (normalized.Contains("שם") || normalized.Contains("name")))
                            continue;
                        if (field.Key == "department_name" &&
                            (normalized.Contains("מזהה") || normalized.Contains("קוד") ||
                             normalized.Contains("_id") || normalized.EndsWith(" id")))
                            continue;
                        if (field.Key == "national_id" &&
                            (normalized.Contains("מחלקה") || normalized.Contains("department")))
                            continue;

                        if (!mappings.ContainsKey(header))
                            mappings[header] = field.Key;
                        break;
                    }
                }
            }

            return mappings;
        }

        public async Task<SalaryFieldMapping?> GetSavedMappingAsync()
            => await _context.SalaryFieldMappings.AsNoTracking().FirstOrDefaultAsync();

        public async Task SaveMappingAsync(
            int entityId,
            int? userId,
            Dictionary<string, string> mapping,
            bool idIncludesCheckDigit)
        {
            var existing = await _context.SalaryFieldMappings.FirstOrDefaultAsync();
            var json = JsonSerializer.Serialize(mapping);
            var now = DateTime.UtcNow;

            if (existing == null)
            {
                _context.SalaryFieldMappings.Add(new SalaryFieldMapping
                {
                    EntityId = entityId,
                    MappingJson = json,
                    IdIncludesCheckDigit = idIncludesCheckDigit,
                    CreatedAt = now,
                    UserId = userId,
                    UpdatedAt = now,
                    UpdateUser = userId
                });
            }
            else
            {
                existing.MappingJson = json;
                existing.IdIncludesCheckDigit = idIncludesCheckDigit;
                existing.UpdatedAt = now;
                existing.UpdateUser = userId;
            }

            await _context.SaveChangesAsync();
        }

        public async Task<(bool Exists, int RowCount, decimal TotalSalary)> GetPeriodStatsAsync(
            int periodYear,
            int periodMonth)
        {
            var query = _context.Salaries.AsNoTracking()
                .Where(s => s.PeriodYear == periodYear && s.PeriodMonth == periodMonth);

            var rowCount = await query.CountAsync();
            if (rowCount == 0)
                return (false, 0, 0m);

            var total = await query.SumAsync(s => s.TotalSalary);
            return (true, rowCount, total);
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

        public List<SalaryFileRow> ParseFile(IFormFile file, Dictionary<string, string> mapping)
        {
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            using var stream = file.OpenReadStream();

            if (ext == ".csv")
                return ParseCsv(stream, mapping);

            if (ext is ".xls" or ".xlsx")
                return ParseExcel(stream, mapping);

            throw new InvalidOperationException("פורמט קובץ לא נתמך. יש להשתמש ב-CSV, XLS או XLSX");
        }

        public async Task<SalaryFileProcessingResult> ProcessUploadAsync(
            int entityId,
            int? userId,
            int periodYear,
            int periodMonth,
            bool replaceExisting,
            bool idIncludesCheckDigit,
            string? fileName,
            List<SalaryFileRow> rows)
        {
            if (periodMonth < 1 || periodMonth > 12)
                throw new InvalidOperationException("חודש לא תקין");

            var (exists, _, _) = await GetPeriodStatsAsync(periodYear, periodMonth);
            if (exists && !replaceExisting)
                throw new PeriodExistsException("קיימים נתוני שכר לתקופה זו. יש לאשר החלפה.");

            var now = DateTime.UtcNow;
            var process = new SalaryUploadProcess
            {
                EntityId = entityId,
                PeriodYear = periodYear,
                PeriodMonth = periodMonth,
                Source = "manual",
                FileName = fileName,
                CreatedAt = now,
                UserId = userId,
                UpdatedAt = now,
                UpdateUser = userId
            };
            _context.SalaryUploadProcesses.Add(process);
            await _context.SaveChangesAsync();

            if (replaceExisting && exists)
            {
                var oldSalaries = await _context.Salaries
                    .Where(s => s.PeriodYear == periodYear && s.PeriodMonth == periodMonth)
                    .ToListAsync();

                if (oldSalaries.Count > 0)
                {
                    var oldIds = oldSalaries.Select(s => s.Id).ToList();
                    var oldWarnings = await _context.SalaryUploadWarnings
                        .Where(w => oldIds.Contains(w.SalaryId))
                        .ToListAsync();
                    _context.SalaryUploadWarnings.RemoveRange(oldWarnings);
                    _context.Salaries.RemoveRange(oldSalaries);
                    await _context.SaveChangesAsync();
                }
            }

            var result = new SalaryFileProcessingResult { ProcessId = process.Id };
            decimal sum = 0;

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

                    if (string.IsNullOrWhiteSpace(row.NationalId))
                    {
                        result.Errors++;
                        result.ErrorList.Add($"שורה {row.RowNumber}: תעודת זהות חסרה");
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(row.DepartmentId))
                    {
                        result.Errors++;
                        result.ErrorList.Add($"שורה {row.RowNumber}: מזהה מחלקה חסר");
                        continue;
                    }

                    var (normalizedId, hasWarning, warningMessage) =
                        IsraeliIdHelper.NormalizeForImport(row.NationalId, idIncludesCheckDigit);

                    if (string.IsNullOrEmpty(normalizedId))
                    {
                        result.Errors++;
                        result.ErrorList.Add($"שורה {row.RowNumber}: תעודת זהות לא תקינה");
                        continue;
                    }

                    var salary = new Salary
                    {
                        EntityId = entityId,
                        PeriodYear = periodYear,
                        PeriodMonth = periodMonth,
                        NationalId = normalizedId,
                        DepartmentId = row.DepartmentId.Trim(),
                        DepartmentName = string.IsNullOrWhiteSpace(row.DepartmentName)
                            ? null
                            : row.DepartmentName.Trim(),
                        PositionPercentage = row.PositionPercentage,
                        TotalSalary = row.TotalSalary,
                        HasIdWarning = hasWarning,
                        ProcessId = process.Id,
                        CreatedAt = now,
                        UserId = userId,
                        UpdatedAt = now,
                        UpdateUser = userId
                    };

                    _context.Salaries.Add(salary);
                    await _context.SaveChangesAsync();

                    result.Created++;
                    sum += row.TotalSalary;

                    if (hasWarning && warningMessage != null)
                    {
                        _context.SalaryUploadWarnings.Add(new SalaryUploadWarning
                        {
                            EntityId = entityId,
                            ProcessId = process.Id,
                            SalaryId = salary.Id,
                            WarningType = "invalid_id_checksum",
                            Message = warningMessage,
                            CreatedAt = now,
                            UserId = userId,
                            UpdatedAt = now,
                            UpdateUser = userId
                        });
                        await _context.SaveChangesAsync();
                        result.Warnings++;
                        result.WarningList.Add($"שורה {row.RowNumber}: {warningMessage}");
                    }
                }
                catch (DbUpdateException ex)
                {
                    _logger.LogWarning(ex, "Duplicate or DB error on salary row {Row}", row.RowNumber);
                    result.Errors++;
                    result.ErrorList.Add($"שורה {row.RowNumber}: רשומה כפולה או שגיאת מסד נתונים");
                    _context.ChangeTracker.Clear();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error importing salary row {Row}", row.RowNumber);
                    result.Errors++;
                    result.ErrorList.Add($"שורה {row.RowNumber}: {ex.Message}");
                }
            }

            // Re-load process in case ChangeTracker was cleared after a row error
            var processToUpdate = await _context.SalaryUploadProcesses.FindAsync(process.Id)
                ?? process;
            processToUpdate.RowCount = result.Created;
            processToUpdate.TotalSalarySum = sum;
            processToUpdate.UpdatedAt = DateTime.UtcNow;
            processToUpdate.UpdateUser = userId;
            if (_context.Entry(processToUpdate).State == EntityState.Detached)
                _context.SalaryUploadProcesses.Update(processToUpdate);
            await _context.SaveChangesAsync();

            await MatchPersonsAndAllocationsForProcessAsync(process.Id, userId);
            await _comparisonService.RebuildSalaryProcessAsync(process.Id, userId);

            result.TotalSalarySum = sum;
            return result;
        }

        /// <summary>
        /// For each salary row in the process, look up persons by national ID (tenant-scoped),
        /// set matched_person_id when found, then match allocations for the salary period
        /// (matched_allocation_id).
        /// </summary>
        private async Task MatchPersonsAndAllocationsForProcessAsync(int processId, int? userId)
        {
            var salaries = await _context.Salaries
                .Where(s => s.ProcessId == processId)
                .ToListAsync();

            await MatchSalariesToPersonsAsync(salaries, userId);
            await MatchSalariesToAllocationsAsync(salaries, userId);
        }

        /// <summary>
        /// Re-runs matching for the salary rows of a period:
        /// person matching for rows that are still unmatched (picks up person records created
        /// after the salary file was uploaded), then allocation matching for all matched rows —
        /// setting matched_allocation_id when an active allocation now overlaps the salary
        /// month, and clearing it when the stored allocation no longer applies.
        /// </summary>
        public async Task<SalaryRecheckResult> RecheckPeriodAsync(int periodYear, int periodMonth, int? userId)
        {
            var salaries = await _context.Salaries
                .Where(s => s.PeriodYear == periodYear && s.PeriodMonth == periodMonth)
                .ToListAsync();

            var newlyMatchedPersons = await MatchSalariesToPersonsAsync(
                salaries.Where(s => s.MatchedPersonId == null).ToList(), userId);

            var (allocationsAdded, allocationsRemoved) =
                await MatchSalariesToAllocationsAsync(salaries, userId);

            var processId = salaries.Select(s => s.ProcessId).DefaultIfEmpty().Max();
            if (processId > 0)
                await _comparisonService.RebuildSalaryProcessAsync(processId, userId);

            return new SalaryRecheckResult
            {
                NewlyMatchedPersons = newlyMatchedPersons,
                AllocationsAdded = allocationsAdded,
                AllocationsRemoved = allocationsRemoved
            };
        }

        /// <summary>
        /// Matches salary rows with a matched person to an active entitlement allocation whose
        /// date range overlaps the row's salary month, storing the result in
        /// matched_allocation_id. A stored allocation that is still valid is kept; one that is
        /// no longer active/overlapping is replaced or cleared. Returns how many rows gained
        /// an allocation (null → value) and how many lost it (value → null).
        /// </summary>
        private async Task<(int Added, int Removed)> MatchSalariesToAllocationsAsync(
            List<Salary> salaries,
            int? userId)
        {
            var matchedRows = salaries.Where(s => s.MatchedPersonId.HasValue).ToList();
            if (matchedRows.Count == 0)
                return (0, 0);

            var personIds = matchedRows
                .Select(s => s.MatchedPersonId!.Value)
                .Distinct()
                .ToList();

            var allocations = await (
                from a in _context.EntitlementAllocations.AsNoTracking()
                join e in _context.Entitlements.AsNoTracking() on a.EntitlementId equals e.Id
                where a.IsActive && e.IsValid && personIds.Contains(a.PersonId)
                select new { a.Id, a.PersonId, a.StartDate, a.EndDate }
            ).ToListAsync();

            var allocationsByPerson = allocations
                .GroupBy(a => a.PersonId)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderBy(a => a.StartDate).ThenBy(a => a.Id).ToList());

            var now = DateTime.UtcNow;
            var added = 0;
            var removed = 0;
            var anyChanged = false;

            foreach (var salary in matchedRows)
            {
                var monthStart = new DateOnly(salary.PeriodYear, salary.PeriodMonth, 1);
                var monthEnd = monthStart.AddMonths(1).AddDays(-1);

                var candidates = allocationsByPerson.TryGetValue(salary.MatchedPersonId!.Value, out var list)
                    ? list.Where(a => a.StartDate <= monthEnd && a.EndDate >= monthStart).ToList()
                    : [];

                // Keep the stored allocation if it is still valid; otherwise take the first
                // (earliest-starting) valid allocation, or null when none exists.
                int? desired = salary.MatchedAllocationId.HasValue &&
                               candidates.Any(a => a.Id == salary.MatchedAllocationId.Value)
                    ? salary.MatchedAllocationId
                    : candidates.FirstOrDefault()?.Id;

                if (desired == salary.MatchedAllocationId)
                    continue;

                if (salary.MatchedAllocationId == null)
                    added++;
                else if (desired == null)
                    removed++;
                // non-null → different non-null: re-pointed, allocation status unchanged

                salary.MatchedAllocationId = desired;
                salary.UpdatedAt = now;
                salary.UpdateUser = userId;
                anyChanged = true;
            }

            if (anyChanged)
                await _context.SaveChangesAsync();

            return (added, removed);
        }

        /// <summary>
        /// Matches the given salary rows to persons by national ID (tenant-scoped) and sets
        /// matched_person_id when found. Compares canonical 9-digit IDs so a leading zero
        /// padded on salary import still matches a person stored without it. Full person
        /// entities are loaded so IdNumber is decrypted by the value converter (do not
        /// project encrypted fields). Returns the number of rows matched.
        /// </summary>
        private async Task<int> MatchSalariesToPersonsAsync(List<Salary> salaries, int? userId)
        {
            if (salaries.Count == 0)
                return 0;

            // Full entities — value converter decrypts IdNumber on materialization
            var persons = await _context.Persons
                .AsNoTracking()
                .ToListAsync();

            var personByCanonicalId = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var person in persons)
            {
                var key = IsraeliIdHelper.ToCanonicalId(person.IdNumber);
                if (string.IsNullOrEmpty(key))
                    continue;
                personByCanonicalId.TryAdd(key, person.Id);
            }

            if (personByCanonicalId.Count == 0)
                return 0;

            var now = DateTime.UtcNow;
            var matched = 0;

            foreach (var salary in salaries)
            {
                var key = IsraeliIdHelper.ToCanonicalId(salary.NationalId);
                if (string.IsNullOrEmpty(key) ||
                    !personByCanonicalId.TryGetValue(key, out var personId))
                    continue;

                salary.MatchedPersonId = personId;
                salary.UpdatedAt = now;
                salary.UpdateUser = userId;
                matched++;
            }

            if (matched > 0)
                await _context.SaveChangesAsync();

            return matched;
        }

        private List<SalaryFileRow> ParseExcel(Stream stream, Dictionary<string, string> mapping)
        {
            var rows = new List<SalaryFileRow>();
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

                var salaryRow = BuildSalaryRow(rowNumber, values, mapping);
                if (salaryRow != null)
                    rows.Add(salaryRow);
            }

            return rows;
        }

        private List<SalaryFileRow> ParseCsv(Stream stream, Dictionary<string, string> mapping)
        {
            var rows = new List<SalaryFileRow>();
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
                var salaryRow = BuildSalaryRow(rowNumber, values, mapping);
                if (salaryRow != null)
                    rows.Add(salaryRow);
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

        private static SalaryFileRow? BuildSalaryRow(
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

            var nationalId = GetMapped("national_id");
            var departmentId = GetMapped("department_id");
            var departmentName = GetMapped("department_name");
            var positionRaw = GetMapped("position_percentage");
            var salaryRaw = GetMapped("total_salary");

            if (string.IsNullOrWhiteSpace(nationalId) &&
                string.IsNullOrWhiteSpace(departmentId) &&
                string.IsNullOrWhiteSpace(positionRaw) &&
                string.IsNullOrWhiteSpace(salaryRaw))
                return null;

            var row = new SalaryFileRow
            {
                RowNumber = rowNumber,
                NationalId = nationalId,
                DepartmentId = departmentId,
                DepartmentName = departmentName
            };

            if (!TryParseDecimal(positionRaw, out var positionPct))
            {
                row.ParseError = true;
                row.ParseErrorMessage = "אחוז משרה לא תקין";
                return row;
            }

            if (!TryParseDecimal(salaryRaw, out var totalSalary))
            {
                row.ParseError = true;
                row.ParseErrorMessage = "שכר כולל לא תקין";
                return row;
            }

            row.PositionPercentage = positionPct;
            row.TotalSalary = totalSalary;
            return row;
        }

        private static bool TryParseDecimal(string? raw, out decimal value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(raw))
                return false;

            var cleaned = raw.Trim()
                .Replace("%", "")
                .Replace(",", "")
                .Replace("₪", "")
                .Replace(" ", "");

            return decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out value)
                || decimal.TryParse(cleaned, NumberStyles.Number, new CultureInfo("he-IL"), out value);
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

    public class PeriodExistsException : InvalidOperationException
    {
        public PeriodExistsException(string message) : base(message) { }
    }
}
