using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Petel.Core.Excel;
using PetelATH.Api.Data;

namespace PetelATH.Api.Services
{
    public class CouncilExcelResult
    {
        public int Created { get; set; }
        public int Updated { get; set; }
        public int Failed  { get; set; }
        public List<string> Log { get; set; } = new();
        public bool Success => Failed == 0 || Created + Updated > 0;
    }

    /// <summary>
    /// Generates per-council student Excel files and stores them as entity documents.
    /// Prefers the "דוח תלמידים לפי רשות שולחת" report template when its blob has been
    /// uploaded — falls back to ExcelGenerationService.GenerateFromRows otherwise.
    /// Re-running replaces the existing document for the same council.
    /// </summary>
    public class CouncilExcelGenerationService
    {
        private const string ReportDefinitionName = "דוח תלמידים לפי רשות שולחת";
        private const string DocumentTypeName     = "Excel תלמידי רשויות";

        private static readonly IReadOnlyList<(string Key, string Label)> FallbackColumns =
        [
            ("IdNumber",   "מספר זהות"),
            ("FirstName",  "שם פרטי"),
            ("LastName",   "שם משפחה"),
            ("Gender",     "מין"),
            ("City",       "עיר"),
            ("SchoolName", "שם בית ספר"),
            ("ClassName",  "כיתה"),
            ("StartDate",  "תאריך התחלה"),
            ("EndDate",    "תאריך סיום"),
            ("Cost",       "עלות"),
        ];

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<CouncilExcelGenerationService> _logger;

        public CouncilExcelGenerationService(
            IServiceScopeFactory scopeFactory,
            ILogger<CouncilExcelGenerationService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        // ── Called by Hangfire background job ─────────────────────────────
        public async Task GenerateForAllCouncils(int entityId, int yearId, int? userId)
            => await RunAsync(entityId, yearId, userId);

        // ── Called synchronously from the controller ──────────────────────
        public async Task<CouncilExcelResult> GenerateForAllCouncilsWithResult(int entityId, int yearId, int? userId)
            => await RunAsync(entityId, yearId, userId);

        private async Task<CouncilExcelResult> RunAsync(int entityId, int yearId, int? userId)
        {
            var result = new CouncilExcelResult();
            void Log(string msg)
            {
                result.Log.Add(msg);
                _logger.LogInformation("{Message}", msg);
            }
            void LogError(string msg, Exception? ex = null)
            {
                result.Log.Add("❌ " + msg);
                if (ex != null) _logger.LogError(ex, "{Message}", msg);
                else            _logger.LogError("{Message}", msg);
            }

            Log($"═══ התחלת ייצוא Excel רשויות — entityId={entityId}, yearId={yearId} ═══");

            using var scope = _scopeFactory.CreateScope();
            var context        = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var templateEngine = scope.ServiceProvider.GetRequiredService<ReportTemplateEngine>();
            var excelService   = scope.ServiceProvider.GetRequiredService<ExcelGenerationService>();

            // ── 1. Resolve document type ──────────────────────────────────
            var docType = await context.Set<DocumentType>()
                .AsNoTracking()
                .FirstOrDefaultAsync(dt => dt.Name == DocumentTypeName);

            if (docType == null)
            {
                LogError($"סוג מסמך '{DocumentTypeName}' לא נמצא. יש להריץ SQL/add-council-excel-doctype.sql");
                return result;
            }

            // ── 2. Try to load the report template (optional) ─────────────
            var reportDef = await context.ExcelReportDefinitions
                .Include(r => r.Template)
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Name == ReportDefinitionName);

            bool useTemplate = reportDef?.Template?.TemplateBlob != null
                               && !string.IsNullOrWhiteSpace(reportDef.DefinitionJson);

            byte[]? templateBlob  = useTemplate ? reportDef!.Template!.TemplateBlob : null;
            string? definitionJson = useTemplate ? reportDef!.DefinitionJson : null;

            if (useTemplate)
                Log($"נטענה תבנית Excel ({templateBlob!.Length:N0} bytes) — מצב תבנית מלאה");
            else
                Log("תבנית Excel לא נמצאה — נוצרת טבלה בסיסית (יש להעלות תבנית בדף דוחות Excel)");

            // ── 3. Resolve school-year IDs for the Hebrew year ────────────
            var schoolYearIds = await context.SchoolYears
                .AsNoTracking()
                .Where(sy => sy.YearId == yearId)
                .Select(sy => sy.Id)
                .ToListAsync();

            if (!schoolYearIds.Any())
            {
                LogError($"לא נמצאו שנות לימוד עבור yearId={yearId}");
                return result;
            }

            // ── 4. Determine owned schools ────────────────────────────────
            var ownedEntityIds = await context.Entities
                .AsNoTracking()
                .Where(e => e.Owner!.Id == entityId)
                .Select(e => e.Id)
                .ToListAsync();

            // ── 5. Load distinct councils in scope ────────────────────────
            var councils = await context.CouncilSummaryVw
                .AsNoTracking()
                .Where(cs => cs.YearId == yearId &&
                             (cs.OwnerId == entityId ||
                              (cs.OwnerId.HasValue && ownedEntityIds.Contains(cs.OwnerId.Value))))
                .GroupBy(cs => new { cs.CouncilId, cs.CouncilName })
                .Select(g => new { g.Key.CouncilId, CouncilName = g.Key.CouncilName ?? "לא ידוע" })
                .OrderBy(c => c.CouncilName)
                .ToListAsync();

            if (!councils.Any())
            {
                Log("לא נמצאו רשויות עם תלמידים בשנה זו — לא נוצרו קבצים.");
                return result;
            }

            Log($"נמצאו {councils.Count} רשויות לעיבוד");

            // ── 6. Pre-load fallback data lookups (used when no template) ─
            Dictionary<int, string> classNames = new();
            Dictionary<int, string> schoolYearToName = new();

            if (!useTemplate)
            {
                var allClassIds = await context.SchoolStudents
                    .AsNoTracking()
                    .Where(s => schoolYearIds.Contains(s.SchoolYearId) && s.IsLastVersion && s.ClassId.HasValue)
                    .Select(s => s.ClassId!.Value).Distinct().ToListAsync();

                if (allClassIds.Count > 0)
                    classNames = await context.SchoolClasses.AsNoTracking()
                        .Where(c => allClassIds.Contains(c.Id))
                        .ToDictionaryAsync(c => c.Id, c => c.Name);

                var syEntities = await context.SchoolYears.AsNoTracking()
                    .Where(sy => schoolYearIds.Contains(sy.Id))
                    .Select(sy => new { sy.Id, sy.SchoolId }).ToListAsync();

                var schoolEntityIds = syEntities.Select(s => s.SchoolId).Distinct().ToList();
                var schoolNameMap = await context.Entities.AsNoTracking()
                    .Where(e => schoolEntityIds.Contains(e.Id))
                    .ToDictionaryAsync(e => e.Id, e => e.Name ?? string.Empty);

                schoolYearToName = syEntities.ToDictionary(
                    sy => sy.Id,
                    sy => schoolNameMap.TryGetValue(sy.SchoolId, out var n) ? n : string.Empty);
            }

            // ── 7. Pre-load existing docs (tracked) for upsert ───────────
            var existingDocs = await context.Documents
                .Include(d => d.DocumentLinks)
                .Where(d => d.DocumentTypeId == docType.Id &&
                            d.IsLastVersion &&
                            d.DocumentLinks.Any(dl => dl.EntityId == entityId))
                .ToListAsync();

            var existingByName = existingDocs
                .Where(d => d.Description != null)
                .ToDictionary(d => d.Description!, StringComparer.OrdinalIgnoreCase);

            // ── 8. Engine context for template path ───────────────────────
            var engineContext = new ExcelEntityContext
            {
                EntityId     = entityId,
                EntityTypeId = "3",    // Council/Network
                SchoolYearId = yearId,
            };

            // ── 9. Process each council ───────────────────────────────────
            int total = councils.Count;
            for (int i = 0; i < total; i++)
            {
                var council = councils[i];
                Log($"[{i + 1}/{total}] מתחיל עיבוד רשות: {council.CouncilName} (id={council.CouncilId})");

                byte[] excelBytes;
                try
                {
                    if (useTemplate)
                    {
                        var runtimeParams = new Dictionary<string, string>
                        {
                            ["hebrew_year_id"]     = yearId.ToString(),
                            ["sending_council_id"] = council.CouncilId.ToString(),
                        };
                        excelBytes = await templateEngine.GenerateAsync(
                            templateBlob!, definitionJson!, engineContext, runtimeParams);
                    }
                    else
                    {
                        // Fallback: build rows manually and use plain table generator
                        var students = await context.SchoolStudents
                            .AsNoTracking()
                            .Where(s => s.SendingCouncil == council.CouncilId &&
                                        schoolYearIds.Contains(s.SchoolYearId) &&
                                        s.IsLastVersion)
                            .ToListAsync();

                        var rows = students.Select(s => new Dictionary<string, object?>
                        {
                            ["IdNumber"]   = s.IdNumber,
                            ["FirstName"]  = s.FirstName,
                            ["LastName"]   = s.LastName,
                            ["Gender"]     = s.Gender == 1 ? "זכר" : s.Gender == 2 ? "נקבה" : null,
                            ["City"]       = s.City,
                            ["SchoolName"] = schoolYearToName.TryGetValue(s.SchoolYearId, out var sn) ? sn : string.Empty,
                            ["ClassName"]  = s.ClassId.HasValue && classNames.TryGetValue(s.ClassId.Value, out var cn) ? cn : string.Empty,
                            ["StartDate"]  = s.StartDate.HasValue ? (object)s.StartDate.Value.ToDateTime(TimeOnly.MinValue) : null,
                            ["EndDate"]    = s.EndDate.HasValue ? (object)s.EndDate.Value.ToDateTime(TimeOnly.MinValue) : null,
                            ["Cost"]       = s.Cost,
                        }).ToList();

                        excelBytes = excelService.GenerateFromRows(rows, FallbackColumns, council.CouncilName);
                        Log($"[{i + 1}/{total}] {students.Count} תלמידים — נוצרה טבלה בסיסית ({excelBytes.Length:N0} bytes)");
                    }

                    if (useTemplate)
                        Log($"[{i + 1}/{total}] נוצר Excel מתבנית ({excelBytes.Length:N0} bytes) עבור {council.CouncilName}");
                }
                catch (Exception ex)
                {
                    LogError($"[{i + 1}/{total}] שגיאה ביצירת Excel עבור {council.CouncilName}: {ex.Message}", ex);
                    result.Failed++;
                    continue;
                }

                try
                {
                    if (existingByName.TryGetValue(council.CouncilName, out var existingDoc))
                    {
                        existingDoc.FileBlob = excelBytes;
                        await context.SaveChangesAsync();
                        result.Updated++;
                        Log($"[{i + 1}/{total}] ✅ עודכן מסמך קיים עבור {council.CouncilName} (docId={existingDoc.Id})");
                    }
                    else
                    {
                        var document = new Document
                        {
                            Description      = council.CouncilName,
                            DocumentTypeId   = docType.Id,
                            StatusId         = 2,
                            FileBlob         = excelBytes,
                            FileEncoding     = "xlsx",
                            FileName         = $"{council.CouncilName}.xlsx",
                            Version          = 0,
                            IsLastVersion    = true,
                            MasterDocumentId = null,
                            CreatedAt        = DateTime.UtcNow,
                            UserId           = userId,
                        };

                        context.Documents.Add(document);
                        await context.SaveChangesAsync();

                        document.MasterDocumentId = document.Id;
                        context.Documents.Update(document);

                        context.Set<DocumentLink>().Add(new DocumentLink
                        {
                            DocumentId      = document.Id,
                            EntityId        = entityId,
                            SchoolStudentId = null,
                        });

                        await context.SaveChangesAsync();

                        existingByName[council.CouncilName] = document;
                        result.Created++;
                        Log($"[{i + 1}/{total}] ✅ נוצר מסמך חדש עבור {council.CouncilName} (docId={document.Id})");
                    }
                }
                catch (Exception ex)
                {
                    LogError($"[{i + 1}/{total}] שגיאה בשמירת מסמך עבור {council.CouncilName}: {ex.Message}", ex);
                    result.Failed++;
                }
            }

            Log($"═══ סיום ייצוא: נוצרו={result.Created}, עודכנו={result.Updated}, נכשלו={result.Failed} ═══");
            return result;
        }
    }
}
