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
    /// Uses the Excel template (xlsx + json definition) to structure and populate the data.
    /// Template must be uploaded before running this service.
    /// Re-running replaces the existing document for the same council.
    /// </summary>
    public class CouncilExcelGenerationService
    {
        public  const string ReportDefinitionName = "נספח 10 - תשפו";
        private const string DocumentTypeName     = "נספח 10";

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

            // ── 1. Resolve document type ──────────────────────────────────
            var docType = await context.Set<DocumentType>()
                .AsNoTracking()
                .FirstOrDefaultAsync(dt => dt.Name.StartsWith(DocumentTypeName) && dt.YearId == yearId);

            if (docType == null)
            {
                LogError($"סוג מסמך '{DocumentTypeName}' לא נמצא. יש להריץ SQL/add-council-excel-doctype.sql");
                return result;
            }

            // ── 2. Load the report template (REQUIRED) ───────────────────
            var reportDef = await context.ReportDefinitions
                .Include(r => r.Template)
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Name == ReportDefinitionName);

            if (reportDef?.Template?.TemplateBlob == null || string.IsNullOrWhiteSpace(reportDef.DefinitionJson))
            {
                LogError($"תבנית Excel '{ReportDefinitionName}' לא נמצאה או חסר הגדרה JSON. יש להעלות את התבנית בדף דוחות Excel.");
                return result;
            }

            byte[] templateBlob = reportDef.Template.TemplateBlob;
            string definitionJson = reportDef.DefinitionJson;
            Log($"נטענה תבנית Excel ({templateBlob.Length:N0} bytes) עם הגדרה JSON");

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

            // ── 6. Pre-load existing docs (tracked) for upsert ───────────
            var existingDocs = await context.Documents
                .Include(d => d.DocumentLinks)
                .Where(d => d.DocumentTypeId == docType.Id &&
                            d.IsLastVersion &&
                            d.DocumentLinks.Any(dl => dl.EntityId == entityId))
                .ToListAsync();

            var existingByName = existingDocs
                .Where(d => d.Description != null)
                .ToDictionary(d => d.Description!, StringComparer.OrdinalIgnoreCase);

            // ── 7. Fetch owner entity name ───────────────────────────────
            var ownerEntity = await context.Entities
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == entityId);
            
            string ownerName = ownerEntity?.Name ?? "לא ידוע";
            Log($"שם גורם בעלות: {ownerName}");

            // ── 7b. Build CouncilId → EntityId map for document linking ──
            // Only council entities (EntityTypeId = 2) carry the council FK
            var councilIds = councils.Select(c => c.CouncilId).Distinct().ToList();
            var councilEntityMap = (await context.Entities
                .AsNoTracking()
                .Where(e => e.EntityTypeId == 2 && e.CouncilId.HasValue && councilIds.Contains(e.CouncilId.Value))
                .Select(e => new { e.CouncilId, e.Id })
                .ToListAsync())
                .GroupBy(e => e.CouncilId!.Value)
                .ToDictionary(g => g.Key, g => g.First().Id);
            Log($"נמצאו {councilEntityMap.Count} ישויות רשות (מתוך {councilIds.Count} רשויות)");

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
                    var runtimeParams = new Dictionary<string, string>
                    {
                        ["hebrew_year_id"]     = yearId.ToString(),
                        ["sending_council_id"] = council.CouncilId.ToString(),
                    };
                    excelBytes = await templateEngine.GenerateAsync(
                        templateBlob, definitionJson, engineContext, runtimeParams);
                    
                    Log($"[{i + 1}/{total}] ✅ נוצר Excel מתבנית ({excelBytes.Length:N0} bytes) עבור {council.CouncilName}");
                }
                catch (Exception ex)
                {
                    LogError($"[{i + 1}/{total}] שגיאה ביצירת Excel עבור {council.CouncilName}: {ex.Message}", ex);
                    result.Failed++;
                    continue;
                }

                try
                {
                    string docDescription = $"{ownerName}-{council.CouncilName}";

                    if (existingByName.TryGetValue(docDescription, out var existingDoc))
                    {
                        // Mark old version as no longer the latest
                        existingDoc.IsLastVersion = false;
                        await context.SaveChangesAsync();

                        // Create a new version document
                        long masterId = existingDoc.MasterDocumentId ?? existingDoc.Id;
                        var newVersion = new Document
                        {
                            Description      = docDescription,
                            DocumentTypeId   = docType.Id,
                            StatusId         = existingDoc.StatusId,
                            FileBlob         = excelBytes,
                            FileEncoding     = "xlsx",
                            FileName         = existingDoc.FileName,
                            Version          = existingDoc.Version + 1,
                            IsLastVersion    = true,
                            MasterDocumentId = masterId,
                            CreatedAt        = DateTime.UtcNow,
                            UserId           = userId,
                        };

                        context.Documents.Add(newVersion);
                        await context.SaveChangesAsync();

                        // Copy existing document links to the new version
                        foreach (var link in existingDoc.DocumentLinks)
                        {
                            context.Set<DocumentLink>().Add(new DocumentLink
                            {
                                DocumentId      = newVersion.Id,
                                EntityId        = link.EntityId,
                                SchoolStudentId = link.SchoolStudentId,
                            });
                        }

                        // Ensure council entity link exists on the new version
                        if (councilEntityMap.TryGetValue(council.CouncilId, out var councilEntityId))
                        {
                            bool alreadyLinked = existingDoc.DocumentLinks.Any(dl => dl.EntityId == councilEntityId);
                            if (!alreadyLinked)
                            {
                                context.Set<DocumentLink>().Add(new DocumentLink
                                {
                                    DocumentId      = newVersion.Id,
                                    EntityId        = councilEntityId,
                                    SchoolStudentId = null,
                                });
                            }
                        }

                        await context.SaveChangesAsync();

                        // Update local lookup so subsequent iterations see the new version
                        existingByName[docDescription] = newVersion;

                        result.Updated++;
                        Log($"[{i + 1}/{total}] ✅ נוצרה גרסה חדשה ({newVersion.Version}) עבור {council.CouncilName} (docId={newVersion.Id})");
                    }
                    else
                    {
                        var document = new Document
                        {
                            Description      = docDescription,
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

                        // Link to owner entity
                        context.Set<DocumentLink>().Add(new DocumentLink
                        {
                            DocumentId      = document.Id,
                            EntityId        = entityId,
                            SchoolStudentId = null,
                        });

                        // Link to council entity (if found in entities table)
                        if (councilEntityMap.TryGetValue(council.CouncilId, out var councilEntityId))
                        {
                            context.Set<DocumentLink>().Add(new DocumentLink
                            {
                                DocumentId      = document.Id,
                                EntityId        = councilEntityId,
                                SchoolStudentId = null,
                            });
                        }
                        else
                        {
                            Log($"[{i + 1}/{total}] ⚠️ לא נמצאה ישות עבור רשות {council.CouncilName} (councilId={council.CouncilId}) — קישור לרשות לא נוצר");
                        }

                        await context.SaveChangesAsync();

                        existingByName[docDescription] = document;
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
