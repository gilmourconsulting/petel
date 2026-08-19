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
    ///   "1" = School (special education type)
    ///   "2" = Council (רשות)
    ///   "3" = Special education network (רשת חינוך מיוחד)
    ///   "4" = School (regular)
    ///   "5" = Network (other)
    ///   "6" = Ownership network (רשת בעלויות) — owns sub-networks which own schools
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
                "Students"               => await QueryStudentsAsync(context, cancellationToken),
                "Schools"                => await QuerySchoolsAsync(context, cancellationToken),
                "SchoolClasses"          => await QuerySchoolClassesAsync(context, cancellationToken),
                "AdditionalStudyPrograms" => await QueryAdditionalStudyProgramsAsync(context, cancellationToken),
                "Transactions"           => await QueryTransactionsAsync(context, cancellationToken),
                "TransactionAccounts"    => await QueryTransactionAccountsAsync(context, cancellationToken),
                // ── Definition-engine entities ────────────────────────────────
                "OwnerEntity"            => await QueryOwnerEntityAsync(context, cancellationToken),
                "Council"                => await QueryCouncilsAsync(cancellationToken),
                "StudentsWithSchool"          => await QueryStudentsWithSchoolAsync(context, cancellationToken),
                "StudentsWithPricingElements" => await QueryStudentsWithPricingElementsAsync(context, cancellationToken),
                "CouncilStats"               => await QueryCouncilStatsAsync(context, cancellationToken),
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
                .Where(s => yearIds.Contains(s.SchoolYearId) && (s.IsLastVersion || s.IncludeInCouncilSummary))
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

        // ─── Definition-Engine Entities ────────────────────────────────────

        /// <summary>
        /// Returns a single-row dictionary for the logged-in entity (council/network/school).
        /// Used as the "header" data source in template reports.
        /// </summary>
        private async Task<List<Dictionary<string, object?>>> QueryOwnerEntityAsync(
            ExcelEntityContext context,
            CancellationToken ct)
        {
            var entity = await _context.Entities
                .Include(e => e.ContactPerson)
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == context.EntityId, ct);

            if (entity == null) return new List<Dictionary<string, object?>>();

            var contactName  = entity.ContactPerson != null
                ? $"{entity.ContactPerson.FirstName} {entity.ContactPerson.LastName}".Trim()
                : string.Empty;
            var contactEmail = entity.ContactPerson?.Email ?? string.Empty;
            var contactPhone = entity.ContactPerson?.PhoneNumber
                ?? entity.ContactPerson?.PhoneNumberPrefix
                ?? string.Empty;

            return new List<Dictionary<string, object?>>
            {
                new()
                {
                    ["Id"]                  = entity.Id,
                    ["Name"]               = entity.Name,
                    ["Symbol"]             = entity.Symbol,
                    ["Address"]            = entity.Address,
                    ["Street"]             = entity.Street,
                    ["City"]               = entity.City,
                    ["PostCode"]           = entity.PostCode,
                    ["Phone"]              = entity.Phone,
                    ["Email"]              = entity.Email,
                    ["TaxNumber"]          = entity.TaxNumber,
                    ["ContactPersonName"]  = contactName,
                    ["ContactPersonEmail"] = contactEmail,
                    ["ContactPersonPhone"] = contactPhone,
                }
            };
        }

        /// <summary>
        /// Returns all councils (filtering is done in-memory by ApplyFilters).
        /// Use filter field="Id" operator="eq" paramName="sending_council_id" to narrow to one.
        /// </summary>
        private async Task<List<Dictionary<string, object?>>> QueryCouncilsAsync(CancellationToken ct)
        {
            var councils = await _context.Councils
                .AsNoTracking()
                .OrderBy(c => c.Name)
                .Select(c => new { c.Id, c.Name, c.CouncilCode, c.LongName })
                .ToListAsync(ct);

            return councils.Select(c => new Dictionary<string, object?>
            {
                ["Id"]          = c.Id,
                ["Name"]        = c.Name,
                ["CouncilCode"] = c.CouncilCode,
                ["LongName"]    = c.LongName,
            }).ToList();
        }

        /// <summary>
        /// Returns one row per council with aggregated stats for the current year+entity scope:
        ///   CouncilId, NumberOfStudents, TotalBasicCost (sum of "בסיסית" pricing element), CurrentDate.
        /// Use filter field="CouncilId" operator="eq" paramName="sending_council_id" to narrow to one.
        /// </summary>
        private async Task<List<Dictionary<string, object?>>> QueryCouncilStatsAsync(
            ExcelEntityContext context,
            CancellationToken ct)
        {
            if (!context.SchoolYearId.HasValue)
                return new List<Dictionary<string, object?>>();

            // ── Resolve school-year IDs for the Hebrew year ───────────────
            var yearIds = await GetSchoolYearIdsAsync(context, ct);
            if (yearIds.Count == 0)
                return new List<Dictionary<string, object?>>();

            // ── Count students per council (scoped view) ──────────────────
            var summaryRows = await _context.CouncilSummaryVw
                .AsNoTracking()
                .Where(cs => cs.YearId == context.SchoolYearId.Value && cs.OwnerId == context.EntityId)
                .Select(cs => new { cs.CouncilId, cs.NumberOfStudents })
                .ToListAsync(ct);

            var studentCountByCouncil = summaryRows.ToDictionary(r => r.CouncilId, r => r.NumberOfStudents);

            // ── Find "בסיסית" pricing element IDs for the Hebrew year ─────
            var basicElementIds = await _context.SpecialNeedsPricingElements
                .AsNoTracking()
                .Where(pe => pe.YearId == context.SchoolYearId.Value &&
                             pe.ElementName == "בסיסית")
                .Select(pe => pe.Id)
                .ToListAsync(ct);

            // ── Build student → council map ───────────────────────────────
            var studentCouncilMap = await _context.SchoolStudents
                .AsNoTracking()
                .Where(s => yearIds.Contains(s.SchoolYearId) && (s.IsLastVersion || s.IncludeInCouncilSummary) && s.SendingCouncil.HasValue)
                .Select(s => new { s.Id, CouncilId = s.SendingCouncil!.Value })
                .ToListAsync(ct);

            var studentToCouncil = studentCouncilMap.ToDictionary(s => s.Id, s => s.CouncilId);

            // ── Sum "בסיסית" prices per council ───────────────────────────
            var basicCostByCouncil = new Dictionary<int, decimal>();
            if (basicElementIds.Count > 0 && studentToCouncil.Count > 0)
            {
                var studentIds = studentToCouncil.Keys.ToList();
                var assignments = await _context.SchoolStudentPricingElements
                    .AsNoTracking()
                    .Where(spe => basicElementIds.Contains(spe.PricingElementId) &&
                                  studentIds.Contains(spe.StudentId))
                    .Select(spe => new { spe.StudentId, spe.Price })
                    .ToListAsync(ct);

                foreach (var a in assignments)
                {
                    if (studentToCouncil.TryGetValue(a.StudentId, out int councilId))
                    {
                        basicCostByCouncil.TryGetValue(councilId, out decimal existing);
                        basicCostByCouncil[councilId] = existing + a.Price;
                    }
                }
            }

            string currentDate = DateTime.Now.ToString("dd/MM/yyyy");

            // ── Build one row per council that has student data ───────────
            return studentCountByCouncil.Keys
                .Select(councilId => new Dictionary<string, object?>
                {
                    ["CouncilId"]          = councilId,
                    ["NumberOfStudents"]   = studentCountByCouncil[councilId],
                    ["TotalBasicCost"]     = (basicCostByCouncil.TryGetValue(councilId, out var cost) ? cost : 0m).ToString("N0"),
                    ["CurrentDate"]        = currentDate,
                })
                .ToList();
        }

        /// <summary>
        /// Students with joined school name and symbol.  Intended for template reports
        /// that need to print school information alongside student data.
        /// Filter by SendingCouncil (int) to scope to a specific council.
        /// </summary>
        private async Task<List<Dictionary<string, object?>>> QueryStudentsWithSchoolAsync(
            ExcelEntityContext context,
            CancellationToken ct)
        {
            var yearIds = await GetSchoolYearIdsAsync(context, ct);

            // Build school-year id -> (name, symbol) lookup.
            // Prefer versioned data from schools table (actual per-year school data),
            // and fallback to entities table only when schools row/symbol is missing.
            var schoolYears = await _context.SchoolYears
                .AsNoTracking()
                .Where(sy => yearIds.Contains(sy.Id))
                .Select(sy => new { sy.Id, sy.SchoolId })
                .ToListAsync(ct);

            var schoolEntityIds = schoolYears.Select(sy => sy.SchoolId).Distinct().ToList();

            var schoolVersionRows = await _context.Schools
                .AsNoTracking()
                .Where(s => schoolEntityIds.Contains(s.EntityId)
                            && yearIds.Contains(s.SchoolYearId))
                .Select(s => new { s.Id, s.EntityId, s.SchoolYearId, s.Version, s.Name, s.Symbol, s.EducationStage })
                .ToListAsync(ct);

            var schoolVersionLookup = schoolVersionRows
                .GroupBy(s => new { s.EntityId, s.SchoolYearId })
                .ToDictionary(
                    g => (g.Key.EntityId, g.Key.SchoolYearId),
                    g => g.OrderByDescending(x => x.Version).ThenByDescending(x => x.Id).First());

            var yearToSchool = schoolYears.ToDictionary(
                sy => sy.Id,
                sy =>
                {
                    if (schoolVersionLookup.TryGetValue((sy.SchoolId, sy.Id), out var sv))
                    {
                        return (
                            Name: sv.Name?.Trim() ?? string.Empty,
                            Symbol: sv.Symbol?.Trim() ?? string.Empty,
                            EducationStage: sv.EducationStage
                        );
                    }

                    return (Name: string.Empty, Symbol: string.Empty, EducationStage: (string?)null);
                });

            // Pre-load class names
            var students = await _context.SchoolStudents
                .AsNoTracking()
                .Where(s => yearIds.Contains(s.SchoolYearId) && (s.IsLastVersion || s.IncludeInCouncilSummary))
                .ToListAsync(ct);

            var classIds = students
                .Where(s => s.ClassId.HasValue)
                .Select(s => s.ClassId!.Value)
                .Distinct()
                .ToList();

            var classInfos = classIds.Count > 0
                ? await _context.SchoolClasses.AsNoTracking()
                    .Where(c => classIds.Contains(c.Id))
                .Select(c => new {
                    c.Id,
                    c.Name,
                    CharacterizationId = c.CharacterizationId,
                    CharacterizationName = c.Characterization != null
                        ? $"{c.Characterization.Name} [{c.CharacterizationId}]"
                        : (string?)null
                })
                    .ToDictionaryAsync(c => c.Id, ct)
                : null;

            // Status lookup
            var statusLookup = await _context.Statuses
                .AsNoTracking()
                .Select(st => new { st.Id, st.Name })
                .ToDictionaryAsync(st => st.Id, st => st.Name, ct);

            // Council lookup
            var councilLookup = await _context.Councils
                .AsNoTracking()
                .Select(c => new { c.Id, c.Name })
                .ToDictionaryAsync(c => c.Id, c => c.Name, ct);

            return students.Select(s =>
            {
                var school = yearToSchool.TryGetValue(s.SchoolYearId, out var si)
                    ? si
                    : (Name: string.Empty, Symbol: string.Empty, EducationStage: (string?)null);
                var statusName = s.StatusId.HasValue && statusLookup.TryGetValue(s.StatusId.Value, out var sn) ? sn : null;
                var councilName = s.SendingCouncil.HasValue && councilLookup.TryGetValue(s.SendingCouncil.Value, out var cn) ? cn : null;
                return new Dictionary<string, object?>
                {
                    ["Id"]                  = s.Id,
                    ["MasterStudentId"]     = s.MasterStudentId,
                    ["FirstName"]           = s.FirstName,
                    ["LastName"]            = s.LastName,
                    ["FullName"]            = $"{s.FirstName} {s.LastName}".Trim(),
                    ["IdNumber"]            = s.IdNumber,
                    ["Gender"]              = s.Gender == 1 ? "זכר" : s.Gender == 2 ? "נקבה" : null,
                    ["DisabilityCategory"]  = s.DisabilityCategory,
                    ["City"]                = s.City,
                    ["StartDate"]           = s.StartDate,
                    ["EndDate"]             = s.EndDate,
                    ["Cost"]                = s.Cost,
                    ["CalculatedMonths"]    = s.EnrollmentMonths,
                    ["Status"]              = statusName,
                    ["SendingCouncil"]      = s.SendingCouncil,
                    ["CouncilName"]         = councilName,
                    ["SchoolYearId"]        = s.SchoolYearId,
                    ["ClassId"]             = s.ClassId,
                    ["ClassName"]           = s.ClassId.HasValue && classInfos != null && classInfos.TryGetValue(s.ClassId.Value, out var ci) ? ci.Name : string.Empty,
                    ["ClassCharacterizationName"] = s.ClassId.HasValue && classInfos != null && classInfos.TryGetValue(s.ClassId.Value, out var ci2) ? ci2.CharacterizationName : null,
                    ["SchoolName"]          = school.Name,
                    ["Symbol"]              = school.Symbol,
                    ["SchoolSymbol"]        = school.Symbol,
                    ["EducationStage"]      = school.EducationStage,
                };
            }).ToList();
        }

        /// <summary>
        /// Students with joined school data AND per-element pricing columns.
        /// Each pricing element defined for the school year produces four dynamic fields
        /// on every student row, keyed by the element's Name (the 'name' DB column, max 50 chars):
        ///   [Name]           — Calculated (prorated) price (decimal, or null if no assignment)
        ///   [Name + "_מלא"]  — Full annual (pre-proration) price (decimal?, or null)
        ///   [Name + "_שעות"] — Hours (decimal?, or null)
        ///   [Name + "_גורם"] — DeterminingFactor (string?, or null)
        ///
        /// Template usage: {{students.בסיסית}}, {{students.בסיסית_מלא}}, {{students.בסיסית_שעות}}, {{students.בסיסית_גורם}}
        ///
        /// When context.SchoolYearId is null, falls back to base student+school fields only (no pricing keys).
        /// </summary>
        private async Task<List<Dictionary<string, object?>>> QueryStudentsWithPricingElementsAsync(
            ExcelEntityContext context,
            CancellationToken ct)
        {
            var yearIds = await GetSchoolYearIdsAsync(context, ct);

            // ── School lookup ──────────────────────────────────────────────
            var schoolYears = await _context.SchoolYears
                .AsNoTracking()
                .Where(sy => yearIds.Contains(sy.Id))
                .Select(sy => new { sy.Id, sy.SchoolId })
                .ToListAsync(ct);

            var schoolEntityIds = schoolYears.Select(sy => sy.SchoolId).Distinct().ToList();

            var schoolVersionRows = await _context.Schools
                .AsNoTracking()
                .Where(s => schoolEntityIds.Contains(s.EntityId) && yearIds.Contains(s.SchoolYearId))
                .Select(s => new { s.Id, s.EntityId, s.SchoolYearId, s.Version, s.Name, s.Symbol })
                .ToListAsync(ct);

            var schoolVersionLookup = schoolVersionRows
                .GroupBy(s => new { s.EntityId, s.SchoolYearId })
                .ToDictionary(
                    g => (g.Key.EntityId, g.Key.SchoolYearId),
                    g => g.OrderByDescending(x => x.Version).ThenByDescending(x => x.Id).First());

            var yearToSchool = schoolYears.ToDictionary(
                sy => sy.Id,
                sy =>
                {
                    if (schoolVersionLookup.TryGetValue((sy.SchoolId, sy.Id), out var sv))
                        return (Name: sv.Name?.Trim() ?? string.Empty, Symbol: sv.Symbol?.Trim() ?? string.Empty);
                    return (Name: string.Empty, Symbol: string.Empty);
                });

            // ── Students + class info ──────────────────────────────────────
            var students = await _context.SchoolStudents
                .AsNoTracking()
                .Where(s => yearIds.Contains(s.SchoolYearId) && (s.IsLastVersion || s.IncludeInCouncilSummary))
                .ToListAsync(ct);

            var classIds = students
                .Where(s => s.ClassId.HasValue)
                .Select(s => s.ClassId!.Value)
                .Distinct()
                .ToList();

            var classInfos = classIds.Count > 0
                ? await _context.SchoolClasses.AsNoTracking()
                    .Where(c => classIds.Contains(c.Id))
                    .Select(c => new
                    {
                        c.Id,
                        c.Name,
                        CharacterizationName = c.Characterization != null
                            ? $"{c.Characterization.Name} [{c.CharacterizationId}]"
                            : (string?)null
                    })
                    .ToDictionaryAsync(c => c.Id, ct)
                : null;

            // ── Pricing element definitions for the year ──────────────────
            var pricingElements = new List<SpecialNeedsPricingElement>();
            if (context.SchoolYearId.HasValue)
            {
                pricingElements = await _context.SpecialNeedsPricingElements
                    .AsNoTracking()
                    .Where(pe => pe.YearId == context.SchoolYearId.Value)
                    .OrderBy(pe => pe.SortOrder)
                    .ToListAsync(ct);

                var duplicateNames = pricingElements
                    .GroupBy(pe => pe.ElementName, StringComparer.OrdinalIgnoreCase)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key)
                    .ToList();

                if (duplicateNames.Count > 0)
                    _logger.LogWarning(
                        "StudentsWithPricingElements: duplicate element Names for year {YearId}: {Names}. Last definition wins.",
                        context.SchoolYearId.Value, string.Join(", ", duplicateNames));
            }

            // ── Student pricing assignments (bulk load) ───────────────────
            // studentId → (pricingElementId → assignment values)
            var assignmentsByStudent =
                new Dictionary<int, Dictionary<int, (decimal Price, decimal FullPrice, decimal? Hours, string? Factor)>>();

            if (pricingElements.Count > 0 && students.Count > 0)
            {
                var studentIds = students.Select(s => s.Id).ToList();
                var assignments = await _context.SchoolStudentPricingElements
                    .AsNoTracking()
                    .Where(spe => studentIds.Contains(spe.StudentId))
                    .ToListAsync(ct);

                foreach (var a in assignments)
                {
                    if (!assignmentsByStudent.TryGetValue(a.StudentId, out var elemDict))
                    {
                        elemDict = new Dictionary<int, (decimal, decimal, decimal?, string?)>();
                        assignmentsByStudent[a.StudentId] = elemDict;
                    }
                    // FullPrice = annual (pre-proration) price; Price = prorated by enrollment months
                    elemDict[a.PricingElementId] = (a.Price, a.FullPrice, a.Hours, a.DeterminingFactor);
                }
            }

            // ── Status + council lookups ──────────────────────────────────
            var statusLookupPe = await _context.Statuses
                .AsNoTracking()
                .Select(st => new { st.Id, st.Name })
                .ToDictionaryAsync(st => st.Id, st => st.Name, ct);

            var councilLookupPe = await _context.Councils
                .AsNoTracking()
                .Select(c => new { c.Id, c.Name })
                .ToDictionaryAsync(c => c.Id, c => c.Name, ct);

            // ── Build rows ────────────────────────────────────────────────
            var rows = new List<Dictionary<string, object?>>(students.Count);
            foreach (var s in students)
            {
                var school = yearToSchool.TryGetValue(s.SchoolYearId, out var si)
                    ? si
                    : (Name: string.Empty, Symbol: string.Empty);

                string className = string.Empty;
                string? characterizationName = null;
                if (s.ClassId.HasValue && classInfos != null &&
                    classInfos.TryGetValue(s.ClassId.Value, out var ci))
                {
                    className = ci.Name;
                    characterizationName = ci.CharacterizationName;
                }

                var statusName = s.StatusId.HasValue && statusLookupPe.TryGetValue(s.StatusId.Value, out var snpe) ? snpe : null;
                var councilName = s.SendingCouncil.HasValue && councilLookupPe.TryGetValue(s.SendingCouncil.Value, out var cnpe) ? cnpe : null;

                var row = new Dictionary<string, object?>
                {
                    ["Id"]                        = s.Id,
                    ["MasterStudentId"]           = s.MasterStudentId,
                    ["FirstName"]                 = s.FirstName,
                    ["LastName"]                  = s.LastName,
                    ["FullName"]                  = $"{s.FirstName} {s.LastName}".Trim(),
                    ["IdNumber"]                  = s.IdNumber,
                    ["Gender"]                    = s.Gender == 1 ? "זכר" : s.Gender == 2 ? "נקבה" : null,
                    ["DisabilityCategory"]        = s.DisabilityCategory,
                    ["City"]                      = s.City,
                    ["StartDate"]                 = s.StartDate,
                    ["EndDate"]                   = s.EndDate,
                    ["Cost"]                      = s.Cost,
                    ["CalculatedMonths"]          = s.EnrollmentMonths,
                    ["Status"]                    = statusName,
                    ["SendingCouncil"]            = s.SendingCouncil,
                    ["CouncilName"]               = councilName,
                    ["SchoolYearId"]              = s.SchoolYearId,
                    ["ClassId"]                   = s.ClassId,
                    ["ClassName"]                 = className,
                    ["ClassCharacterizationName"] = characterizationName,
                    ["SchoolName"]                = school.Name,
                    ["Symbol"]                    = school.Symbol,
                    ["SchoolSymbol"]              = school.Symbol,
                };

                // Append per-element pricing keys (keyed by element.ElementName, i.e. the 'name' column)
                if (pricingElements.Count > 0)
                {
                    assignmentsByStudent.TryGetValue(s.Id, out var elemDict);
                    foreach (var element in pricingElements)
                    {
                        var key = element.ElementName;
                        if (elemDict != null && elemDict.TryGetValue(element.Id, out var assignment))
                        {
                            // Primary key = calculated (prorated) price; full annual price in "_מלא"
                            row[key]                   = assignment.Price;       // prorated by enrollment months
                            row[key + "_מלא"]          = assignment.FullPrice;   // annual (pre-proration) price
                            row[key + "_שעות"]         = assignment.Hours;
                            row[key + "_גורם"]         = assignment.Factor;
                        }
                        else
                        {
                            row[key]                   = null;
                            row[key + "_מלא"]          = null;
                            row[key + "_שעות"]         = null;
                            row[key + "_גורם"]         = null;
                        }
                    }
                }

                rows.Add(row);
            }

            return rows;
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

            // Council/Network — all directly owned entities.
            // We intentionally avoid hard-coding entity type here because production
            // data may contain owned schools with different entity type IDs; the
            // downstream SchoolYears join keeps only valid school entities.
            return await _context.Entities
                .AsNoTracking()
                .Where(e => e.OwnerId == context.EntityId)
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
                },
                // ── Definition-engine entities ──────────────────────────────
                new()
                {
                    Name = "OwnerEntity",
                    LabelHe = "גוף מנהל (כותרת)",
                    IsAccountEntity = false,
                    Fields = new List<ExcelFieldDescriptor>
                    {
                        new() { Name = "Id",                  LabelHe = "מזהה",             Type = "number" },
                        new() { Name = "Name",                LabelHe = "שם הגוף",          Type = "text" },
                        new() { Name = "Symbol",              LabelHe = "סמל",              Type = "text" },
                        new() { Name = "Address",             LabelHe = "כתובת",            Type = "text" },
                        new() { Name = "Street",              LabelHe = "רחוב",             Type = "text" },
                        new() { Name = "City",                LabelHe = "עיר",              Type = "text" },
                        new() { Name = "PostCode",            LabelHe = "מיקוד",            Type = "text" },
                        new() { Name = "Phone",               LabelHe = "טלפון",            Type = "text" },
                        new() { Name = "Email",               LabelHe = "דוא\"ל",           Type = "text" },
                        new() { Name = "TaxNumber",           LabelHe = "ח.פ.",             Type = "text" },
                        new() { Name = "ContactPersonName",   LabelHe = "שם איש קשר",      Type = "text" },
                        new() { Name = "ContactPersonEmail",  LabelHe = "דוא\"ל איש קשר",  Type = "text" },
                        new() { Name = "ContactPersonPhone",  LabelHe = "טלפון איש קשר",   Type = "text" }
                    }
                },
                new()
                {
                    Name = "Council",
                    LabelHe = "רשות",
                    IsAccountEntity = false,
                    Fields = new List<ExcelFieldDescriptor>
                    {
                        new() { Name = "Id",          LabelHe = "מזהה",      Type = "number", IsFilterable = true },
                        new() { Name = "Name",        LabelHe = "שם רשות",  Type = "text" },
                        new() { Name = "CouncilCode", LabelHe = "קוד רשות", Type = "number" }
                    }
                },
                new()
                {
                    Name = "StudentsWithSchool",
                    LabelHe = "תלמידים (עם נתוני בית ספר)",
                    IsAccountEntity = false,
                    Fields = new List<ExcelFieldDescriptor>
                    {
                        new() { Name = "Id",                 LabelHe = "מזהה",            Type = "number" },
                        new() { Name = "MasterStudentId",    LabelHe = "מספר תלמיד",     Type = "number" },
                        new() { Name = "FirstName",          LabelHe = "שם פרטי",        Type = "text" },
                        new() { Name = "LastName",           LabelHe = "שם משפחה",       Type = "text" },
                        new() { Name = "FullName",           LabelHe = "שם מלא",         Type = "text" },
                        new() { Name = "IdNumber",           LabelHe = "ת.ז.",           Type = "text" },
                        new() { Name = "Gender",             LabelHe = "מין",            Type = "text", IsFilterable = true },
                        new() { Name = "DisabilityCategory", LabelHe = "קטגוריית נכות", Type = "number" },
                        new() { Name = "City",               LabelHe = "עיר",            Type = "text" },
                        new() { Name = "StartDate",          LabelHe = "תאריך קליטה",   Type = "date" },
                        new() { Name = "EndDate",            LabelHe = "תאריך סיום",    Type = "date" },
                        new() { Name = "Cost",               LabelHe = "עלות",                 Type = "number" },
                        new() { Name = "CalculatedMonths",   LabelHe = "חודשים מחושבים",     Type = "number" },
                        new() { Name = "Status",             LabelHe = "סטטוס",               Type = "text" },
                        new() { Name = "SendingCouncil",     LabelHe = "רשות שולחת",          Type = "number", IsFilterable = true },
                        new() { Name = "CouncilName",        LabelHe = "שם רשות שולחת",       Type = "text" },
                        new() { Name = "SchoolYearId",       LabelHe = "מזהה שנה",            Type = "number", IsFilterable = true },
                        new() { Name = "ClassId",            LabelHe = "מזהה כיתה",           Type = "number", IsFilterable = true },
                        new() { Name = "ClassName",          LabelHe = "שם כיתה",             Type = "text" },
                        new() { Name = "SchoolName",         LabelHe = "שם בית ספר",          Type = "text" },
                        new() { Name = "SchoolSymbol",       LabelHe = "סמל בית ספר",         Type = "text" },
                        new() { Name = "EducationStage",     LabelHe = "שלב חינוך",           Type = "text" }
                    }
                },
                new()
                {
                    Name = "StudentsWithPricingElements",
                    LabelHe = "תלמידים + מרכיבי תמחור",
                    IsAccountEntity = false,
                    Fields = new List<ExcelFieldDescriptor>
                    {
                        new() { Name = "Id",                 LabelHe = "מזהה",            Type = "number" },
                        new() { Name = "MasterStudentId",    LabelHe = "מספר תלמיד",     Type = "number" },
                        new() { Name = "FirstName",          LabelHe = "שם פרטי",        Type = "text" },
                        new() { Name = "LastName",           LabelHe = "שם משפחה",       Type = "text" },
                        new() { Name = "FullName",           LabelHe = "שם מלא",         Type = "text" },
                        new() { Name = "IdNumber",           LabelHe = "ת.ז.",           Type = "text" },
                        new() { Name = "Gender",             LabelHe = "מין",            Type = "text", IsFilterable = true },
                        new() { Name = "DisabilityCategory", LabelHe = "קטגוריית נכות", Type = "number" },
                        new() { Name = "City",               LabelHe = "עיר",            Type = "text" },
                        new() { Name = "StartDate",          LabelHe = "תאריך קליטה",   Type = "date" },
                        new() { Name = "EndDate",            LabelHe = "תאריך סיום",    Type = "date" },
                        new() { Name = "Cost",               LabelHe = "עלות (סה\"כ)",        Type = "number" },
                        new() { Name = "CalculatedMonths",   LabelHe = "חודשים מחושבים",     Type = "number" },
                        new() { Name = "Status",             LabelHe = "סטטוס",               Type = "text" },
                        new() { Name = "SendingCouncil",     LabelHe = "רשות שולחת",          Type = "number", IsFilterable = true },
                        new() { Name = "CouncilName",        LabelHe = "שם רשות שולחת",       Type = "text" },
                        new() { Name = "SchoolYearId",       LabelHe = "מזהה שנה",            Type = "number", IsFilterable = true },
                        new() { Name = "ClassId",            LabelHe = "מזהה כיתה",           Type = "number", IsFilterable = true },
                        new() { Name = "ClassName",          LabelHe = "שם כיתה",             Type = "text" },
                        new() { Name = "SchoolName",         LabelHe = "שם בית ספר",          Type = "text" },
                        new() { Name = "SchoolSymbol",       LabelHe = "סמל בית ספר",         Type = "text" }
                        // Pricing element fields are dynamic (keyed by element Name — the 'name' DB column).
                        // Template tokens: {{students.בסיסית}}, {{students.בסיסית_שעות}}, {{students.בסיסית_גורם}}
                    }
                }
            };
    }
}
