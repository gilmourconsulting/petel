using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Petel.Core.Excel;
using PetelATH.Api.Data;

namespace PetelATH.Api.Services
{
    /// <summary>
    /// ATH-specific implementation of IExcelEntityRegistry.
    /// Defines which entities can be exported and handles entity-scoped data retrieval.
    ///
    /// Entity Type IDs (string values from UserSession.EntityTypeId):
    ///   "4" = School
    ///   "3" = Council / Network
    ///   "1" = System Administrator
    ///
    /// Cross-year allowed entity names (account/financial entities):
    ///   Transactions, TransactionAccounts
    /// </summary>
    public class AthExcelEntityRegistry : IExcelEntityRegistry
    {
        private static readonly HashSet<string> AccountEntityNames =
            new(StringComparer.OrdinalIgnoreCase)
            { "Transactions", "TransactionAccounts" };

        private static readonly IReadOnlyList<ExcelEntityDescriptor> _allEntities =
            BuildDescriptors();

        private readonly AppDbContext _context;
        private readonly ILogger<AthExcelEntityRegistry> _logger;

        public AthExcelEntityRegistry(
            AppDbContext context,
            ILogger<AthExcelEntityRegistry> logger)
        {
            _context = context;
            _logger = logger;
        }

        // ─── IExcelEntityRegistry ──────────────────────────────────────────

        public IReadOnlyList<ExcelEntityDescriptor> GetAvailableEntities() => _allEntities;

        public ExcelEntityDescriptor? GetEntityDescriptor(string entityName) =>
            _allEntities.FirstOrDefault(e =>
                string.Equals(e.Name, entityName, StringComparison.OrdinalIgnoreCase));

        public async Task<List<Dictionary<string, object?>>> QueryEntityAsync(
            ExcelQueryConfig queryConfig,
            ExcelEntityContext context,
            Dictionary<string, string> runtimeParams,
            CancellationToken cancellationToken = default)
        {
            var entityName = queryConfig.EntityName;
            _logger.LogInformation(
                "ExcelEntityRegistry: querying {Entity} for EntityId={EntityId} EntityTypeId={TypeId}",
                entityName, context.EntityId, context.EntityTypeId);

            var allRows = entityName switch
            {
                "Students"              => await QueryStudentsAsync(context, cancellationToken),
                "Schools"               => await QuerySchoolsAsync(context, cancellationToken),
                "SchoolClasses"         => await QuerySchoolClassesAsync(context, cancellationToken),
                "AdditionalStudyPrograms" => await QueryAdditionalStudyProgramsAsync(context, cancellationToken),
                "Transactions"          => await QueryTransactionsAsync(context, cancellationToken),
                "TransactionAccounts"   => await QueryTransactionAccountsAsync(context, cancellationToken),
                _ => throw new NotSupportedException(
                    $"Entity '{entityName}' is not supported by AthExcelEntityRegistry.")
            };

            // ── Apply in-memory filters ─────────────────────────────────────
            var filtered = ApplyFilters(allRows, queryConfig.Filters, runtimeParams);

            // ── Apply in-memory sort ────────────────────────────────────────
            var sorted = ApplySort(filtered, queryConfig.Sort);

            // ── Project to requested fields ─────────────────────────────────
            var requestedFields = queryConfig.Fields.Select(f => f.Field).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (requestedFields.Count > 0)
            {
                sorted = sorted
                    .Select(row => row
                        .Where(kv => requestedFields.Contains(kv.Key))
                        .ToDictionary(kv => kv.Key, kv => kv.Value))
                    .ToList();
            }

            return sorted;
        }

        // ─── Entity Queries ────────────────────────────────────────────────

        private async Task<List<Dictionary<string, object?>>> QueryStudentsAsync(
            ExcelEntityContext context,
            CancellationToken ct)
        {
            var yearIds = await GetSchoolYearIdsAsync(context, ct);

            var students = await _context.SchoolStudents
                .AsNoTracking()
                .Where(s => yearIds.Contains(s.SchoolYearId))
                .Select(s => new
                {
                    s.Id,
                    s.MasterStudentId,
                    s.FirstName,
                    s.LastName,
                    s.City,
                    s.HouseNumber,
                    s.PostCode,
                    s.Gender,
                    s.DisabilityCategory,
                    s.StartDate,
                    s.EndDate,
                    s.SchoolYearId,
                    s.ClassId,
                    s.Version,
                    s.IsLastVersion
                })
                .ToListAsync(ct);

            return students.Select(s => new Dictionary<string, object?>
            {
                ["Id"]                 = s.Id,
                ["MasterStudentId"]    = s.MasterStudentId,
                ["FirstName"]          = s.FirstName,
                ["LastName"]           = s.LastName,
                ["City"]               = s.City,
                ["HouseNumber"]        = s.HouseNumber,
                ["PostCode"]           = s.PostCode,
                ["Gender"]             = s.Gender == 1 ? "זכר" : s.Gender == 2 ? "נקבה" : null,
                ["DisabilityCategory"] = s.DisabilityCategory,
                ["StartDate"]          = s.StartDate,
                ["EndDate"]            = s.EndDate,
                ["SchoolYearId"]       = s.SchoolYearId,
                ["ClassId"]            = s.ClassId,
                ["Version"]            = s.Version,
                ["IsLastVersion"]      = s.IsLastVersion
            }).ToList();
        }

        private async Task<List<Dictionary<string, object?>>> QuerySchoolsAsync(
            ExcelEntityContext context,
            CancellationToken ct)
        {
            var entityIds = await GetScopedSchoolEntityIdsAsync(context, ct);
            var yearIds = await GetSchoolYearIdsAsync(context, ct);

            var schools = await _context.Schools
                .AsNoTracking()
                .Where(s => entityIds.Contains(s.EntityId) &&
                            yearIds.Contains(s.SchoolYearId))
                .Select(s => new
                {
                    s.Id,
                    s.EntityId,
                    s.Name,
                    s.City,
                    s.Street,
                    s.SchoolYearId
                })
                .ToListAsync(ct);

            return schools.Select(s => new Dictionary<string, object?>
            {
                ["Id"]          = s.Id,
                ["EntityId"]    = s.EntityId,
                ["Name"]        = s.Name,
                ["City"]        = s.City,
                ["Street"]      = s.Street,
                ["SchoolYearId"] = s.SchoolYearId
            }).ToList();
        }

        private async Task<List<Dictionary<string, object?>>> QuerySchoolClassesAsync(
            ExcelEntityContext context,
            CancellationToken ct)
        {
            var yearIds = await GetSchoolYearIdsAsync(context, ct);

            var classes = await _context.SchoolClasses
                .AsNoTracking()
                .Where(c => yearIds.Contains(c.SchoolYearId))
                .Select(c => new
                {
                    c.Id,
                    c.Name,
                    c.Level,
                    c.ClassNumber,
                    c.SchoolYearId
                })
                .ToListAsync(ct);

            return classes.Select(c => new Dictionary<string, object?>
            {
                ["Id"]          = c.Id,
                ["Name"]        = c.Name,
                ["Level"]       = c.Level,
                ["ClassNumber"] = c.ClassNumber,
                ["SchoolYearId"] = c.SchoolYearId
            }).ToList();
        }

        private async Task<List<Dictionary<string, object?>>> QueryAdditionalStudyProgramsAsync(
            ExcelEntityContext context,
            CancellationToken ct)
        {
            var yearIds = await GetSchoolYearIdsAsync(context, ct);

            var programs = await _context.SchoolAdditionalStudyPrograms
                .AsNoTracking()
                .Where(p => yearIds.Contains(p.SchoolYearId) && p.IsLastVersion)
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.SchoolYearId,
                    p.ClassId,
                    p.WeeklyHours,
                    NumberOfClassStudents = p.NumberOfStudents,
                    p.Cost,
                    p.ApprovedAmount,
                    p.HourlyCost,
                    p.Version
                })
                .ToListAsync(ct);

            return programs.Select(p => new Dictionary<string, object?>
            {
                ["Id"]                   = p.Id,
                ["Name"]                 = p.Name,
                ["SchoolYearId"]         = p.SchoolYearId,
                ["ClassId"]              = p.ClassId,
                ["WeeklyHours"]          = p.WeeklyHours,
                ["NumberOfClassStudents"] = p.NumberOfClassStudents,
                ["Cost"]                 = p.Cost,
                ["ApprovedAmount"]       = p.ApprovedAmount,
                ["HourlyCost"]           = p.HourlyCost,
                ["Version"]              = p.Version
            }).ToList();
        }

        private async Task<List<Dictionary<string, object?>>> QueryTransactionsAsync(
            ExcelEntityContext context,
            CancellationToken ct)
        {
            var accountIds = await GetScopedAccountIdsAsync(context, ct);

            var query = _context.Transactions
                .AsNoTracking()
                .Where(t => accountIds.Contains(t.AccountId));

            if (context.SchoolYearId.HasValue)
                query = query.Where(t => t.SchoolYearId == context.SchoolYearId);

            var txs = await query
                .Select(t => new
                {
                    t.Id,
                    t.AccountId,
                    t.TransactionTypeId,
                    t.TransactionDate,
                    t.Amount,
                    t.Description,
                    t.SchoolYearId,
                    t.RelatedStudentId,
                    t.CreatedAt
                })
                .ToListAsync(ct);

            return txs.Select(t => new Dictionary<string, object?>
            {
                ["Id"]               = t.Id,
                ["AccountId"]        = t.AccountId,
                ["TransactionTypeId"] = t.TransactionTypeId,
                ["TransactionDate"]  = t.TransactionDate,
                ["Amount"]           = t.Amount,
                ["Description"]      = t.Description,
                ["SchoolYearId"]     = t.SchoolYearId,
                ["RelatedStudentId"] = t.RelatedStudentId,
                ["CreatedAt"]        = t.CreatedAt
            }).ToList();
        }

        private async Task<List<Dictionary<string, object?>>> QueryTransactionAccountsAsync(
            ExcelEntityContext context,
            CancellationToken ct)
        {
            var entityIds = await GetScopedSchoolEntityIdsAsync(context, ct);

            var accounts = await _context.TransactionAccounts
                .AsNoTracking()
                .Where(ta => entityIds.Contains(ta.OwnerEntityId))
                .Select(ta => new
                {
                    ta.Id,
                    ta.OwnerEntityId,
                    ta.RelatedEntityId,
                    ta.AccountTypeId,
                    ta.IsActive
                })
                .ToListAsync(ct);

            return accounts.Select(ta => new Dictionary<string, object?>
            {
                ["Id"]            = ta.Id,
                ["OwnerEntityId"] = ta.OwnerEntityId,
                ["RelatedEntityId"] = ta.RelatedEntityId,
                ["AccountTypeId"] = ta.AccountTypeId,
                ["IsActive"]      = ta.IsActive
            }).ToList();
        }

        // ─── Scoping Helpers ───────────────────────────────────────────────

        private async Task<List<int>> GetSchoolYearIdsAsync(
            ExcelEntityContext context,
            CancellationToken ct)
        {
            var schoolEntityIds = await GetScopedSchoolEntityIdsAsync(context, ct);

            var query = _context.SchoolYears
                .AsNoTracking()
                .Where(sy => schoolEntityIds.Contains(sy.SchoolId));

            if (context.SchoolYearId.HasValue)
                query = query.Where(sy => sy.YearId == context.SchoolYearId.Value);

            return await query.Select(sy => sy.Id).ToListAsync(ct);
        }

        private async Task<List<int>> GetScopedSchoolEntityIdsAsync(
            ExcelEntityContext context,
            CancellationToken ct)
        {
            // School user — only their own entity
            if (context.EntityTypeId == "4")
                return new List<int> { context.EntityId };

            // Council/Network — all schools they own
            // EntityTypeId 4 = School in entity_types
            return await _context.Entities
                .AsNoTracking()
                .Where(e => e.EntityTypeId == 4 && e.OwnerId == context.EntityId)
                .Select(e => e.Id)
                .ToListAsync(ct);
        }

        private async Task<List<int>> GetScopedAccountIdsAsync(
            ExcelEntityContext context,
            CancellationToken ct)
        {
            var entityIds = await GetScopedSchoolEntityIdsAsync(context, ct);

            return await _context.TransactionAccounts
                .AsNoTracking()
                .Where(ta => entityIds.Contains(ta.OwnerEntityId))
                .Select(ta => ta.Id)
                .ToListAsync(ct);
        }

        // ─── Filter & Sort ─────────────────────────────────────────────────

        private static List<Dictionary<string, object?>> ApplyFilters(
            List<Dictionary<string, object?>> rows,
            List<ExcelQueryConfig.FilterCondition> filters,
            Dictionary<string, string> runtimeParams)
        {
            if (!filters.Any()) return rows;

            return rows.Where(row =>
            {
                foreach (var filter in filters)
                {
                    var rawValue = row.TryGetValue(filter.Field, out var v) ? v : null;
                    var rowStr = rawValue?.ToString() ?? string.Empty;

                    // Resolve filter value (literal or runtime param)
                    string? filterValue = filter.ParamName != null
                        ? (runtimeParams.TryGetValue(filter.ParamName, out var p) ? p : null)
                        : filter.Value;

                    bool match = filter.Operator switch
                    {
                        "eq"         => string.Equals(rowStr, filterValue, StringComparison.OrdinalIgnoreCase),
                        "neq"        => !string.Equals(rowStr, filterValue, StringComparison.OrdinalIgnoreCase),
                        "contains"   => rowStr.Contains(filterValue ?? "", StringComparison.OrdinalIgnoreCase),
                        "startswith" => rowStr.StartsWith(filterValue ?? "", StringComparison.OrdinalIgnoreCase),
                        "isnull"     => rawValue == null || rowStr == string.Empty,
                        "isnotnull"  => rawValue != null && rowStr != string.Empty,
                        "gt"  => CompareNumeric(rowStr, filterValue) > 0,
                        "gte" => CompareNumeric(rowStr, filterValue) >= 0,
                        "lt"  => CompareNumeric(rowStr, filterValue) < 0,
                        "lte" => CompareNumeric(rowStr, filterValue) <= 0,
                        _ => true
                    };

                    if (!match) return false;
                }
                return true;
            }).ToList();
        }

        private static List<Dictionary<string, object?>> ApplySort(
            List<Dictionary<string, object?>> rows,
            List<ExcelQueryConfig.SortSpec> sortSpecs)
        {
            if (!sortSpecs.Any()) return rows;

            IOrderedEnumerable<Dictionary<string, object?>>? ordered = null;

            foreach (var spec in sortSpecs)
            {
                Func<Dictionary<string, object?>, string> keySelector =
                    row => row.TryGetValue(spec.Field, out var v) ? v?.ToString() ?? "" : "";

                if (ordered == null)
                {
                    ordered = spec.Direction == "desc"
                        ? rows.OrderByDescending(keySelector)
                        : rows.OrderBy(keySelector);
                }
                else
                {
                    ordered = spec.Direction == "desc"
                        ? ordered.ThenByDescending(keySelector)
                        : ordered.ThenBy(keySelector);
                }
            }

            return ordered?.ToList() ?? rows;
        }

        private static int CompareNumeric(string a, string? b)
        {
            if (decimal.TryParse(a, out var da) && decimal.TryParse(b, out var db))
                return da.CompareTo(db);
            return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
        }

        // ─── Static Descriptor Build ───────────────────────────────────────

        private static IReadOnlyList<ExcelEntityDescriptor> BuildDescriptors() =>
            new List<ExcelEntityDescriptor>
            {
                new()
                {
                    Name = "Students",
                    LabelHe = "תלמידים",
                    IsAccountEntity = false,
                    Fields = new List<ExcelFieldDescriptor>
                    {
                        new() { Name = "Id",                 LabelHe = "מזהה",            Type = "number" },
                        new() { Name = "MasterStudentId",    LabelHe = "מספר תלמיד",      Type = "number" },
                        new() { Name = "FirstName",          LabelHe = "שם פרטי",         Type = "text" },
                        new() { Name = "LastName",           LabelHe = "שם משפחה",        Type = "text" },
                        new() { Name = "City",               LabelHe = "עיר",             Type = "text" },
                        new() { Name = "PostCode",           LabelHe = "מיקוד",           Type = "text" },
                        new() { Name = "Gender",             LabelHe = "מין",             Type = "text", IsFilterable = true,
                            EnumOptions = new()
                            {
                                new() { Value = "זכר",  Label = "זכר" },
                                new() { Value = "נקבה", Label = "נקבה" }
                            }
                        },
                        new() { Name = "DisabilityCategory", LabelHe = "קטגוריית נכות",  Type = "number" },
                        new() { Name = "StartDate",          LabelHe = "תאריך קליטה",     Type = "date" },
                        new() { Name = "EndDate",            LabelHe = "תאריך סיום",      Type = "date" },
                        new() { Name = "SchoolYearId",       LabelHe = "מזהה שנה",        Type = "number", IsFilterable = true },
                        new() { Name = "ClassId",            LabelHe = "מזהה כיתה",       Type = "number", IsFilterable = true },
                        new() { Name = "IsLastVersion",      LabelHe = "גרסה אחרונה",     Type = "boolean", IsFilterable = true }
                    }
                },
                new()
                {
                    Name = "Schools",
                    LabelHe = "בתי ספר",
                    IsAccountEntity = false,
                    Fields = new List<ExcelFieldDescriptor>
                    {
                        new() { Name = "Id",          LabelHe = "מזהה",     Type = "number" },
                        new() { Name = "EntityId",    LabelHe = "מזהה גוף", Type = "number" },
                        new() { Name = "Name",        LabelHe = "שם",       Type = "text" },
                        new() { Name = "City",        LabelHe = "עיר",      Type = "text" },
                        new() { Name = "Street",      LabelHe = "רחוב",     Type = "text" },
                        new() { Name = "SchoolYearId",LabelHe = "מזהה שנה", Type = "number", IsFilterable = true }
                    }
                },
                new()
                {
                    Name = "SchoolClasses",
                    LabelHe = "כיתות",
                    IsAccountEntity = false,
                    Fields = new List<ExcelFieldDescriptor>
                    {
                        new() { Name = "Id",          LabelHe = "מזהה",         Type = "number" },
                        new() { Name = "Name",        LabelHe = "שם כיתה",      Type = "text" },
                        new() { Name = "Level",       LabelHe = "שכבה",         Type = "text" },
                        new() { Name = "ClassNumber", LabelHe = "מספר כיתה",    Type = "text" },
                        new() { Name = "SchoolYearId",LabelHe = "מזהה שנה",     Type = "number", IsFilterable = true }
                    }
                },
                new()
                {
                    Name = "AdditionalStudyPrograms",
                    LabelHe = "תוכניות תל\"ן",
                    IsAccountEntity = false,
                    Fields = new List<ExcelFieldDescriptor>
                    {
                        new() { Name = "Id",                   LabelHe = "מזהה",              Type = "number" },
                        new() { Name = "Name",                 LabelHe = "שם תוכנית",         Type = "text" },
                        new() { Name = "SchoolYearId",         LabelHe = "מזהה שנה",          Type = "number", IsFilterable = true },
                        new() { Name = "ClassId",              LabelHe = "מזהה כיתה",         Type = "number", IsFilterable = true },
                        new() { Name = "WeeklyHours",          LabelHe = "שעות שבועיות",      Type = "number" },
                        new() { Name = "NumberOfClassStudents",LabelHe = "מספר תלמידים",      Type = "number" },
                        new() { Name = "Cost",                 LabelHe = "עלות",              Type = "number" },
                        new() { Name = "ApprovedAmount",       LabelHe = "סכום מאושר",        Type = "number" },
                        new() { Name = "HourlyCost",           LabelHe = "עלות שעתית",        Type = "number" }
                    }
                },
                new()
                {
                    Name = "Transactions",
                    LabelHe = "עסקאות",
                    IsAccountEntity = true,
                    Fields = new List<ExcelFieldDescriptor>
                    {
                        new() { Name = "Id",               LabelHe = "מזהה",        Type = "number" },
                        new() { Name = "AccountId",        LabelHe = "מזהה חשבון",  Type = "number", IsFilterable = true },
                        new() { Name = "TransactionTypeId",LabelHe = "סוג עסקה",   Type = "number", IsFilterable = true },
                        new() { Name = "TransactionDate",  LabelHe = "תאריך",       Type = "date",   IsFilterable = true },
                        new() { Name = "Amount",           LabelHe = "סכום",        Type = "number" },
                        new() { Name = "Description",      LabelHe = "תיאור",       Type = "text" },
                        new() { Name = "SchoolYearId",     LabelHe = "מזהה שנה",    Type = "number", IsFilterable = true },
                        new() { Name = "RelatedStudentId", LabelHe = "מזהה תלמיד", Type = "number", IsFilterable = true },
                        new() { Name = "CreatedAt",        LabelHe = "נוצר",        Type = "date" }
                    }
                },
                new()
                {
                    Name = "TransactionAccounts",
                    LabelHe = "חשבונות",
                    IsAccountEntity = true,
                    Fields = new List<ExcelFieldDescriptor>
                    {
                        new() { Name = "Id",             LabelHe = "מזהה",           Type = "number" },
                        new() { Name = "OwnerEntityId",  LabelHe = "מזהה בעלים",     Type = "number", IsFilterable = true },
                        new() { Name = "RelatedEntityId",LabelHe = "מזהה גוף קשור", Type = "number", IsFilterable = true },
                        new() { Name = "AccountTypeId",  LabelHe = "סוג חשבון",      Type = "number", IsFilterable = true },
                        new() { Name = "IsActive",       LabelHe = "פעיל",           Type = "boolean", IsFilterable = true }
                    }
                }
            };
    }
}
