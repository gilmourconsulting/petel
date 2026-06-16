using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Petel.Core.Documents;
using Petel.Core.Excel;
using PetelATH.Api.Data;

namespace PetelATH.Api.Services
{
    public class CouncilWordResult
    {
        public int Created { get; set; }
        public int Updated { get; set; }
        public int Failed  { get; set; }
        public List<string> Log { get; set; } = new();
        public bool Success => Failed == 0 || Created + Updated > 0;
    }

    /// <summary>
    /// Generates per-council Word (.docx) letters and stores them as entity documents.
    /// Uses the Word template (docx + json definition) to structure and populate the data.
    /// Template must be uploaded before running this service.
    /// Re-running replaces the existing document for the same council.
    /// </summary>
    public class CouncilWordGenerationService
    {
        public  const string ReportDefinitionName = "מכתב לרשות תשפו";
        private const string DocumentTypeName     = "מכתב לרשות";

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<CouncilWordGenerationService> _logger;

        public CouncilWordGenerationService(
            IServiceScopeFactory scopeFactory,
            ILogger<CouncilWordGenerationService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        // ── Called by Hangfire background job ─────────────────────────────
        public async Task GenerateForAllCouncils(int entityId, int yearId, int? userId)
            => await RunAsync(entityId, yearId, userId);

        // ── Called synchronously from the controller ──────────────────────
        public async Task<CouncilWordResult> GenerateForAllCouncilsWithResult(
            int entityId, int yearId, int? userId,
            IReadOnlyList<int>? councilFilter = null)
            => await RunAsync(entityId, yearId, userId, councilFilter);

        private async Task<CouncilWordResult> RunAsync(
            int entityId, int yearId, int? userId,
            IReadOnlyList<int>? councilFilter = null)
        {
            var result = new CouncilWordResult();
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

            Log($"═══ התחלת ייצוא Word רשויות — entityId={entityId}, yearId={yearId} ═══");

            using var scope = _scopeFactory.CreateScope();
            var context   = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var docEngine = scope.ServiceProvider.GetRequiredService<DocumentTemplateEngine>();

            // ── 1. Resolve document type ──────────────────────────────────
            var docType = await context.Set<DocumentType>()
                .AsNoTracking()
                .FirstOrDefaultAsync(dt => dt.Name.StartsWith(DocumentTypeName) && dt.YearId == yearId);

            if (docType == null)
            {
                // Also try without year scope (level-based type with null YearId)
                docType = await context.Set<DocumentType>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(dt => dt.Name.StartsWith(DocumentTypeName));
            }

            if (docType == null)
            {
                LogError($"סוג מסמך '{DocumentTypeName}' לא נמצא. יש להריץ SQL/add-council-word-doctype.sql");
                return result;
            }

            // ── 2. Load the report template (REQUIRED) ───────────────────
            // Prefer entity-specific template (entity_id = entityId) over default (entity_id = null)
            var reportDef = await context.ReportDefinitions
                .Include(r => r.Template)
                .AsNoTracking()
                .Where(r => r.Name == ReportDefinitionName &&
                            (r.EntityId == entityId || r.EntityId == null))
                .OrderByDescending(r => r.EntityId.HasValue)   // entity-specific first
                .FirstOrDefaultAsync();

            if (reportDef?.Template?.TemplateBlob == null || string.IsNullOrWhiteSpace(reportDef.DefinitionJson))
            {
                LogError($"תבנית Word '{ReportDefinitionName}' לא נמצאה או חסר הגדרה JSON. יש להעלות את התבנית בדף הדוחות.");
                return result;
            }

            byte[] templateBlob  = reportDef.Template.TemplateBlob;
            string definitionJson = reportDef.DefinitionJson;
            string templateSource = reportDef.EntityId.HasValue ? $"ישות {reportDef.EntityId}" : "ברירת מחדל";
            Log($"נטענה תבנית Word ({templateBlob.Length:N0} bytes, {templateSource}) עם הגדרה JSON");

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
            var councilQuery = context.CouncilSummaryVw
                .AsNoTracking()
                .Where(cs => cs.YearId == yearId &&
                             (cs.OwnerId == entityId ||
                              (cs.OwnerId.HasValue && ownedEntityIds.Contains(cs.OwnerId.Value))));

            if (councilFilter?.Count > 0)
                councilQuery = councilQuery.Where(cs => councilFilter.Contains(cs.CouncilId));

            var councils = await councilQuery
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
            var councilIds = councils.Select(c => c.CouncilId).Distinct().ToList();
            var councilEntityMap = (await context.Entities
                .AsNoTracking()
                .Where(e => e.EntityTypeId == 2 && e.CouncilId.HasValue && councilIds.Contains(e.CouncilId.Value))
                .Select(e => new { e.CouncilId, e.Id })
                .ToListAsync())
                .GroupBy(e => e.CouncilId!.Value)
                .ToDictionary(g => g.Key, g => g.First().Id);
            Log($"נמצאו {councilEntityMap.Count} ישויות רשות (מתוך {councilIds.Count} רשויות)");

            // ── 8. Engine context ─────────────────────────────────────────
            var engineContext = new ExcelEntityContext
            {
                EntityId     = entityId,
                EntityTypeId = "3",
                SchoolYearId = yearId,
            };

            // ── 9. Process each council ───────────────────────────────────
            int total = councils.Count;
            for (int i = 0; i < total; i++)
            {
                var council = councils[i];
                Log($"[{i + 1}/{total}] מתחיל עיבוד רשות: {council.CouncilName} (id={council.CouncilId})");

                byte[] docBytes;
                try
                {
                    var runtimeParams = new Dictionary<string, string>
                    {
                        ["hebrew_year_id"]     = yearId.ToString(),
                        ["sending_council_id"] = council.CouncilId.ToString(),
                    };
                    docBytes = await docEngine.GenerateAsync(
                        templateBlob, definitionJson, engineContext, runtimeParams);

                    Log($"[{i + 1}/{total}] ✅ נוצר Word מתבנית ({docBytes.Length:N0} bytes) עבור {council.CouncilName}");
                }
                catch (Exception ex)
                {
                    LogError($"[{i + 1}/{total}] שגיאה ביצירת Word עבור {council.CouncilName}: {ex.Message}", ex);
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

                        long masterId = existingDoc.MasterDocumentId ?? existingDoc.Id;
                        var newVersion = new Document
                        {
                            Description      = docDescription,
                            DocumentTypeId   = docType.Id,
                            StatusId         = existingDoc.StatusId,
                            FileBlob         = docBytes,
                            FileEncoding     = "docx",
                            FileName         = existingDoc.FileName,
                            Version          = existingDoc.Version + 1,
                            IsLastVersion    = true,
                            MasterDocumentId = masterId,
                            CreatedAt        = DateTime.UtcNow,
                            UserId           = userId,
                        };

                        context.Documents.Add(newVersion);
                        await context.SaveChangesAsync();

                        foreach (var link in existingDoc.DocumentLinks)
                        {
                            context.Set<DocumentLink>().Add(new DocumentLink
                            {
                                DocumentId      = newVersion.Id,
                                EntityId        = link.EntityId,
                                SchoolStudentId = link.SchoolStudentId,
                            });
                        }

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
                            FileBlob         = docBytes,
                            FileEncoding     = "docx",
                            FileName         = $"{council.CouncilName}.docx",
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
