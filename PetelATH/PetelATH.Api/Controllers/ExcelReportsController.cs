using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Petel.Core.Excel;
using PetelATH.Api.Data;
using PetelATH.Api.Models;
using PetelATH.Api.Session;
using PetelATH.Api.Services;
using System.Text.Json;

namespace PetelATH.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ExcelReportsController : BaseController
    {
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        private readonly AppDbContext _context;
        private readonly IExcelEntityRegistry _registry;
        private readonly ExcelGenerationService _generationService;
        private readonly ExcelTemplateService _templateService;
        private readonly ReportTemplateEngine _templateEngine;

        public ExcelReportsController(
            AppDbContext context,
            IExcelEntityRegistry registry,
            ExcelGenerationService generationService,
            ExcelTemplateService templateService,
            ReportTemplateEngine templateEngine,
            UserSessionService userSessionService,
            ILogger<ExcelReportsController> logger)
            : base(userSessionService, logger)
        {
            _context = context;
            _registry = registry;
            _generationService = generationService;
            _templateService = templateService;
            _templateEngine = templateEngine;
        }

        // ─── Metadata Endpoints ────────────────────────────────────────────

        /// <summary>GET /api/excelreports/entities — list all exportable entities</summary>
        [HttpGet("entities")]
        public IActionResult GetAvailableEntities()
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            var entities = _registry.GetAvailableEntities().Select(e => new
            {
                name = e.Name,
                labelHe = e.LabelHe,
                isAccountEntity = e.IsAccountEntity,
                fieldCount = e.Fields.Count
            });

            return Ok(new { success = true, data = entities });
        }

        /// <summary>GET /api/excelreports/entities/{name}/fields</summary>
        [HttpGet("entities/{name}/fields")]
        public IActionResult GetEntityFields(string name)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            var descriptor = _registry.GetEntityDescriptor(name);
            if (descriptor == null)
                return NotFound(new { success = false, message = $"ישות '{name}' לא נמצאה" });

            return Ok(new { success = true, data = descriptor });
        }

        // ─── CRUD Endpoints ────────────────────────────────────────────────

        /// <summary>GET /api/excelreports — list all reports</summary>
        [HttpGet]
        public async Task<IActionResult> GetReports()
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            var reports = await _context.ExcelReportDefinitions
                .AsNoTracking()
                .Where(r => r.IsActive)
                .OrderBy(r => r.SortOrder).ThenBy(r => r.Name)
                .Select(r => new
                {
                    r.Id, r.Name, r.Description, r.ReportType,
                    r.AllowCrossYear, r.RequiresEntityContext,
                    r.SortOrder, r.IsActive,
                    templateFilename = r.Template != null ? r.Template.TemplateFilename : null
                })
                .ToListAsync();

            return Ok(new { success = true, data = reports });
        }

        /// <summary>GET /api/excelreports/{id}</summary>
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetReport(int id)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            var report = await _context.ExcelReportDefinitions
                .AsNoTracking()
                .Include(r => r.Query)
                .Include(r => r.Parameters)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (report == null)
                return NotFound(new { success = false, message = "דוח לא נמצא" });

            return Ok(new { success = true, data = report });
        }

        /// <summary>POST /api/excelreports — create new report definition</summary>
        [HttpPost]
        public async Task<IActionResult> CreateReport([FromBody] CreateReportRequest request)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            if (string.IsNullOrWhiteSpace(request.Name))
                return BadRequest(new { success = false, message = "שם הדוח נדרש" });

            if (!IsValidReportType(request.ReportType))
                return BadRequest(new { success = false, message = "סוג דוח לא חוקי" });

            int? userId = int.TryParse(session.UserId, out int uid) ? uid : null;

            var report = new ExcelReportDefinition
            {
                Name = request.Name,
                Description = request.Description,
                ReportType = request.ReportType,
                AllowCrossYear = request.AllowCrossYear,
                RequiresEntityContext = request.RequiresEntityContext,
                SortOrder = request.SortOrder,
                IsActive = true,
                RequiredActionId = request.RequiredActionId,
                DefinitionJson = request.DefinitionJson,
                CreatedAt = DateTime.UtcNow,
                CreatedUser = userId,
                UpdatedAt = DateTime.UtcNow,
                UpdateUser = userId
            };

            _context.ExcelReportDefinitions.Add(report);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Excel report created: Id={Id} Name={Name} by UserId={UserId}",
                report.Id, report.Name, session.UserId);

            return Ok(new { success = true, data = new { id = report.Id } });
        }

        /// <summary>PUT /api/excelreports/{id}</summary>
        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateReport(int id, [FromBody] UpdateReportRequest request)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            var report = await _context.ExcelReportDefinitions.FindAsync(id);
            if (report == null)
                return NotFound(new { success = false, message = "דוח לא נמצא" });

            int? userId = int.TryParse(session.UserId, out int uid) ? uid : null;

            if (!string.IsNullOrWhiteSpace(request.Name)) report.Name = request.Name;
            if (request.Description != null) report.Description = request.Description;
            if (request.AllowCrossYear.HasValue) report.AllowCrossYear = request.AllowCrossYear.Value;
            if (request.RequiresEntityContext.HasValue) report.RequiresEntityContext = request.RequiresEntityContext.Value;
            if (request.SortOrder.HasValue) report.SortOrder = request.SortOrder.Value;
            if (request.RequiredActionId.HasValue) report.RequiredActionId = request.RequiredActionId;
            if (request.DefinitionJson != null) report.DefinitionJson = request.DefinitionJson;
            report.UpdatedAt = DateTime.UtcNow;
            report.UpdateUser = userId;

            await _context.SaveChangesAsync();
            return Ok(new { success = true });
        }

        /// <summary>DELETE /api/excelreports/{id} — soft delete</summary>
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteReport(int id)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            var report = await _context.ExcelReportDefinitions.FindAsync(id);
            if (report == null)
                return NotFound(new { success = false, message = "דוח לא נמצא" });

            int? userId = int.TryParse(session.UserId, out int uid) ? uid : null;
            report.IsActive = false;
            report.UpdatedAt = DateTime.UtcNow;
            report.UpdateUser = userId;

            await _context.SaveChangesAsync();
            return Ok(new { success = true });
        }

        // ─── Query Configuration ───────────────────────────────────────────

        /// <summary>PUT /api/excelreports/{id}/query — save or update query config</summary>
        [HttpPut("{id:int}/query")]
        public async Task<IActionResult> SaveQuery(int id, [FromBody] SaveQueryRequest request)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            var report = await _context.ExcelReportDefinitions
                .Include(r => r.Query)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (report == null)
                return NotFound(new { success = false, message = "דוח לא נמצא" });

            if (string.IsNullOrWhiteSpace(request.EntityName))
                return BadRequest(new { success = false, message = "שם הישות נדרש" });

            if (report.Query == null)
            {
                var query = new ExcelReportQuery
                {
                    ReportId = id,
                    EntityName = request.EntityName,
                    FieldsJson = request.FieldsJson,
                    FiltersJson = request.FiltersJson,
                    SortJson = request.SortJson,
                    SqlQuery = request.SqlQuery,
                    SheetName = request.SheetName ?? "נתונים",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.ExcelReportQueries.Add(query);
            }
            else
            {
                report.Query.EntityName = request.EntityName;
                report.Query.FieldsJson = request.FieldsJson;
                report.Query.FiltersJson = request.FiltersJson;
                report.Query.SortJson = request.SortJson;
                report.Query.SqlQuery = request.SqlQuery;
                if (request.SheetName != null) report.Query.SheetName = request.SheetName;
                report.Query.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            return Ok(new { success = true });
        }

        // ─── Parameter Configuration ───────────────────────────────────────

        /// <summary>PUT /api/excelreports/{id}/parameters — replace all parameters</summary>
        [HttpPut("{id:int}/parameters")]
        public async Task<IActionResult> SaveParameters(int id, [FromBody] List<SaveParameterRequest> parameters)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            var exists = await _context.ExcelReportDefinitions.AnyAsync(r => r.Id == id);
            if (!exists)
                return NotFound(new { success = false, message = "דוח לא נמצא" });

            // Replace all parameters for this report
            var existing = _context.ExcelReportParameters.Where(p => p.ReportId == id);
            _context.ExcelReportParameters.RemoveRange(existing);

            foreach (var (p, idx) in parameters.Select((p, i) => (p, i)))
            {
                _context.ExcelReportParameters.Add(new ExcelReportParameter
                {
                    ReportId = id,
                    ParamName = p.ParamName,
                    ParamLabelHe = p.ParamLabelHe,
                    ParamType = p.ParamType,
                    IsRequired = p.IsRequired,
                    DefaultValue = p.DefaultValue,
                    OptionsJson = p.OptionsJson,
                    SortOrder = p.SortOrder ?? idx,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }

            await _context.SaveChangesAsync();
            return Ok(new { success = true });
        }

        // ─── Parameter Schema Endpoint ───────────────────────────────────────

        /// <summary>
        /// GET /api/excelreports/{id}/params
        /// Returns the parameter schema so the UI can build the generation modal.
        /// Combines DB-stored excel_report_parameters with any parameters declared
        /// in definition_json (definition takes precedence for template reports).
        /// </summary>
        [HttpGet("{id:int}/params")]
        public async Task<IActionResult> GetReportParams(int id)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            var report = await _context.ExcelReportDefinitions
                .AsNoTracking()
                .Include(r => r.Parameters)
                .FirstOrDefaultAsync(r => r.Id == id && r.IsActive);

            if (report == null)
                return NotFound(new { success = false, message = "דוח לא נמצא" });

            // If the report has a definition_json, its Parameters[] wins
            if (!string.IsNullOrWhiteSpace(report.DefinitionJson))
            {
                try
                {
                    var def = JsonSerializer.Deserialize<Petel.Core.Excel.ReportDefinition>(
                        report.DefinitionJson,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

                    var defParams = def?.Parameters.Select(p => new
                    {
                        paramName     = p.Name,
                        paramLabelHe  = p.Label,
                        paramType     = p.Type,
                        isRequired    = p.Required,
                        defaultValue  = p.DefaultValue,
                        optionsJson   = p.OptionsJson,
                        sortOrder     = 0
                    }) ?? Enumerable.Empty<object>();

                    return Ok(new { success = true, data = defParams });
                }
                catch (JsonException)
                {
                    // Fall through to DB parameters if JSON is malformed
                }
            }

            // Fall back to DB-stored parameters
            var dbParams = report.Parameters
                .OrderBy(p => p.SortOrder)
                .Select(p => new
                {
                    paramName    = p.ParamName,
                    paramLabelHe = p.ParamLabelHe,
                    paramType    = p.ParamType,
                    isRequired   = p.IsRequired,
                    defaultValue = p.DefaultValue,
                    optionsJson  = p.OptionsJson,
                    sortOrder    = p.SortOrder
                });

            return Ok(new { success = true, data = dbParams });
        }

        // ─── Generation Endpoints ────────────────────────────────────────────

        /// <summary>POST /api/excelreports/{id}/generate — generate and download Excel file</summary>
        [HttpPost("{id:int}/generate")]
        public async Task<IActionResult> GenerateReport(
            int id,
            [FromBody] GenerateReportRequest request,
            CancellationToken ct)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            var runtimeParams = request?.RuntimeParams ?? new Dictionary<string, string>();

            var report = await _context.ExcelReportDefinitions
                .AsNoTracking()
                .Include(r => r.Query)
                .Include(r => r.Template)
                .Include(r => r.Parameters)
                .FirstOrDefaultAsync(r => r.Id == id && r.IsActive, ct);

            if (report == null)
                return NotFound(new { success = false, message = "דוח לא נמצא" });

            if (report.ReportType is "query_builder" or "advanced_sql" && report.Query == null)
                return BadRequest(new { success = false, message = "הדוח אינו מוגדר כראוי – חסר שאילתה" });

            if (report.ReportType == "template" && report.Template == null)
                return BadRequest(new { success = false, message = "הדוח אינו מוגדר כראוי – חסרה תבנית" });

            var entityContext = BuildEntityContext(session, runtimeParams);

            // Cross-year guard: non-account entities MUST have a school year
            if (report.Query != null && !report.AllowCrossYear && entityContext.SchoolYearId == null)
                return BadRequest(new { success = false, message = "יש לבחור שנת לימודים לדוח זה" });

            try
            {
                byte[] fileBytes;
                string fileName;

                if (report.ReportType == "template" && report.Template != null)
                {
                    fileBytes = await GenerateTemplateReportAsync(report, entityContext, runtimeParams, ct);
                    fileName = $"{report.Name}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                }
                else
                {
                    fileBytes = await GenerateQueryReportAsync(report, entityContext, runtimeParams, ct);
                    fileName = $"{report.Name}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                }

                _logger.LogInformation("Excel report generated: Id={Id} Name={Name} by UserId={UserId} Size={Size}B",
                    report.Id, report.Name, session.UserId, fileBytes.Length);

                return File(fileBytes,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    fileName);
            }
            catch (NotSupportedException ex)
            {
                _logger.LogWarning(ex, "Unsupported entity in report {ReportId}", id);
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating Excel report Id={Id}", id);
                return StatusCode(500, new { success = false, message = "שגיאה ביצירת הדוח" });
            }
        }

        /// <summary>POST /api/excelreports/preview — preview first 10 rows as JSON</summary>
        [HttpPost("preview")]
        public async Task<IActionResult> Preview(
            [FromBody] PreviewRequest request,
            CancellationToken ct)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            if (string.IsNullOrWhiteSpace(request.EntityName))
                return BadRequest(new { success = false, message = "שם הישות נדרש" });

            var descriptor = _registry.GetEntityDescriptor(request.EntityName);
            if (descriptor == null)
                return NotFound(new { success = false, message = $"ישות '{request.EntityName}' לא נמצאה" });

            var runtimeParams = request.RuntimeParams ?? new Dictionary<string, string>();
            var entityContext = BuildEntityContext(session, runtimeParams);

            ExcelQueryConfig queryConfig;
            try
            {
                queryConfig = BuildQueryConfig(request);
            }
            catch (JsonException ex)
            {
                return BadRequest(new { success = false, message = $"JSON לא חוקי: {ex.Message}" });
            }

            try
            {
                var allRows = await _registry.QueryEntityAsync(queryConfig, entityContext, runtimeParams, ct);
                var preview = allRows.Take(10).ToList();
                return Ok(new
                {
                    success = true,
                    data = preview,
                    totalRows = allRows.Count,
                    previewRows = preview.Count
                });
            }
            catch (NotSupportedException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in preview for entity {Entity}", request.EntityName);
                return StatusCode(500, new { success = false, message = "שגיאה בהצגה מקדימה" });
            }
        }

        // ─── Private Helpers ───────────────────────────────────────────────

        private ExcelEntityContext BuildEntityContext(
            UserSession session,
            Dictionary<string, string> runtimeParams)
        {
            var entityId = int.TryParse(session.EntityId, out int sid) ? sid : 0;
            var entityTypeId = session.EntityTypeId;

            // System Admin may select a specific entity via runtime param
            if (runtimeParams.TryGetValue("entity_context_selector", out var entityCtxStr)
                && int.TryParse(entityCtxStr, out var entityCtxId))
            {
                entityId = entityCtxId;
                // Resolve entity type for the selected entity — default to "4" (school) for context
                entityTypeId = "4";
            }

            int? schoolYearId = null;
            if (runtimeParams.TryGetValue("school_year_id", out var yearStr)
                && int.TryParse(yearStr, out var yearId))
            {
                schoolYearId = yearId;
            }
            else if (runtimeParams.TryGetValue("hebrew_year_id", out var hebrewYearStr)
                && int.TryParse(hebrewYearStr, out var hebrewYearId))
            {
                // hebrew_year_id is the FK to hebrew_years.id — same value used by
                // GetSchoolYearIdsAsync and SpecialNeedsPricingElements.YearId
                schoolYearId = hebrewYearId;
            }

            return new ExcelEntityContext
            {
                EntityId = entityId,
                EntityTypeId = entityTypeId,
                SchoolYearId = schoolYearId
            };
        }

        private async Task<byte[]> GenerateQueryReportAsync(
            ExcelReportDefinition report,
            ExcelEntityContext entityContext,
            Dictionary<string, string> runtimeParams,
            CancellationToken ct)
        {
            var query = report.Query!;

            var queryConfig = new ExcelQueryConfig
            {
                EntityName = query.EntityName ?? string.Empty,
                SheetName = query.SheetName ?? report.Name,
                Fields = DeserializeJson<List<ExcelQueryConfig.SelectedField>>(query.FieldsJson) ?? new(),
                Filters = DeserializeJson<List<ExcelQueryConfig.FilterCondition>>(query.FiltersJson) ?? new(),
                Sort = DeserializeJson<List<ExcelQueryConfig.SortSpec>>(query.SortJson) ?? new()
            };

            var rows = await _registry.QueryEntityAsync(queryConfig, entityContext, runtimeParams, ct);

            // Build ordered columns for the sheet header
            var descriptor = _registry.GetEntityDescriptor(queryConfig.EntityName);
            var columns = BuildColumnList(queryConfig.Fields, descriptor);

            return _generationService.GenerateFromRows(rows, columns, queryConfig.SheetName);
        }

        private async Task<byte[]> GenerateTemplateReportAsync(
            ExcelReportDefinition report,
            ExcelEntityContext entityContext,
            Dictionary<string, string> runtimeParams,
            CancellationToken ct)
        {
            var template = report.Template!;

            // ── Engine path: definition_json + template blob ──────────────────
            if (!string.IsNullOrWhiteSpace(report.DefinitionJson))
            {
                return await _templateEngine.GenerateAsync(
                    template.TemplateBlob,
                    report.DefinitionJson,
                    entityContext,
                    runtimeParams,
                    ct);
            }

            // ── Legacy path: scalar cell_mappings_json fill only ─────────────
            var merged = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

            foreach (var kv in runtimeParams)
                merged[kv.Key] = kv.Value;

            if (report.Query != null)
            {
                var queryConfig = new ExcelQueryConfig
                {
                    EntityName = report.Query.EntityName ?? string.Empty,
                    SheetName  = report.Query.SheetName ?? report.Name,
                    Fields  = DeserializeJson<List<ExcelQueryConfig.SelectedField>>(report.Query.FieldsJson)  ?? new(),
                    Filters = DeserializeJson<List<ExcelQueryConfig.FilterCondition>>(report.Query.FiltersJson) ?? new(),
                    Sort    = DeserializeJson<List<ExcelQueryConfig.SortSpec>>(report.Query.SortJson)           ?? new()
                };

                var rows = await _registry.QueryEntityAsync(queryConfig, entityContext, runtimeParams, ct);
                if (rows.Count > 0)
                    foreach (var kv in rows[0])
                        merged[kv.Key] = kv.Value;
            }

            return _templateService.FillTemplate(template.TemplateBlob, merged);
        }

        private static IReadOnlyList<(string Key, string Label)> BuildColumnList(
            IReadOnlyList<ExcelQueryConfig.SelectedField> selectedFields,
            ExcelEntityDescriptor? descriptor)
        {
            if (!selectedFields.Any() || descriptor == null)
            {
                // Return all fields from descriptor
                return descriptor?.Fields.Select(f => (f.Name, f.LabelHe)).ToList()
                    ?? new List<(string, string)>();
            }

            var fieldMap = descriptor.Fields.ToDictionary(f => f.Name, f => f.LabelHe,
                StringComparer.OrdinalIgnoreCase);

            return selectedFields.Select(f =>
            {
                var label = f.LabelOverride
                    ?? (fieldMap.TryGetValue(f.Field, out var lbl) ? lbl : f.Field);
                return (f.Field, label);
            }).ToList();
        }

        private static ExcelQueryConfig BuildQueryConfig(PreviewRequest request)
        {
            return new ExcelQueryConfig
            {
                EntityName = request.EntityName,
                SheetName = request.SheetName ?? "תצוגה מקדימה",
                Fields = DeserializeJson<List<ExcelQueryConfig.SelectedField>>(request.FieldsJson) ?? new(),
                Filters = DeserializeJson<List<ExcelQueryConfig.FilterCondition>>(request.FiltersJson) ?? new(),
                Sort = DeserializeJson<List<ExcelQueryConfig.SortSpec>>(request.SortJson) ?? new()
            };
        }

        private static T? DeserializeJson<T>(string? json) where T : class
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            return JsonSerializer.Deserialize<T>(json, _jsonOptions);
        }

        private static bool IsValidReportType(string type) =>
            type is "query_builder" or "advanced_sql" or "template";
    }

    // ─── Request DTOs ──────────────────────────────────────────────────────

    public record CreateReportRequest(
        string Name,
        string? Description,
        string ReportType,
        bool AllowCrossYear = false,
        bool RequiresEntityContext = false,
        int SortOrder = 0,
        int? RequiredActionId = null,
        string? DefinitionJson = null);

    public record UpdateReportRequest(
        string? Name = null,
        string? Description = null,
        bool? AllowCrossYear = null,
        bool? RequiresEntityContext = null,
        int? SortOrder = null,
        int? RequiredActionId = null,
        string? DefinitionJson = null);

    public record SaveQueryRequest(
        string EntityName,
        string? FieldsJson,
        string? FiltersJson,
        string? SortJson,
        string? SqlQuery,
        string? SheetName);

    public record SaveParameterRequest(
        string ParamName,
        string ParamLabelHe,
        string ParamType,
        bool IsRequired = false,
        string? DefaultValue = null,
        string? OptionsJson = null,
        int? SortOrder = null);

    public record PreviewRequest(
        string EntityName,
        string? FieldsJson = null,
        string? FiltersJson = null,
        string? SortJson = null,
        string? SheetName = null,
        Dictionary<string, string>? RuntimeParams = null);

    public class GenerateReportRequest
    {
        /// <summary>
        /// Values supplied by the caller.
        /// Keys must match ParameterDefinition.Name / FilterCondition.ParamName.
        /// Session-auto params (session_entity, session_year) do not need to be here.
        /// </summary>
        public Dictionary<string, string> RuntimeParams { get; set; } = new();
    }
}
