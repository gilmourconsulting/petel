using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Petel.Core.Excel;
using PetelATH.Api.Data;
using PetelATH.Api.Models;
using PetelATH.Api.Session;

namespace PetelATH.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ExcelReportTemplatesController : BaseController
    {
        private const long MaxTemplateSizeBytes = 10 * 1024 * 1024; // 10 MB

        private readonly AppDbContext _context;
        private readonly ExcelTemplateService _templateService;

        public ExcelReportTemplatesController(
            AppDbContext context,
            ExcelTemplateService templateService,
            UserSessionService userSessionService,
            ILogger<ExcelReportTemplatesController> logger)
            : base(userSessionService, logger)
        {
            _context = context;
            _templateService = templateService;
        }

        /// <summary>
        /// POST /api/excelreporttemplates/{reportId}/upload
        /// Upload a .xlsx template file and store it in the database.
        /// </summary>
        [HttpPost("{reportId:int}/upload")]
        public async Task<IActionResult> UploadTemplate(int reportId, IFormFile file)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            var report = await _context.ExcelReportDefinitions
                .Include(r => r.Template)
                .FirstOrDefaultAsync(r => r.Id == reportId);

            if (report == null)
                return NotFound(new { success = false, message = "דוח לא נמצא" });

            if (report.ReportType != "template")
                return BadRequest(new { success = false, message = "הדוח אינו מסוג תבנית" });

            if (file == null || file.Length == 0)
                return BadRequest(new { success = false, message = "קובץ תבנית נדרש" });

            if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { success = false, message = "יש להעלות קובץ בפורמט .xlsx בלבד" });

            if (file.Length > MaxTemplateSizeBytes)
                return BadRequest(new { success = false, message = "גודל הקובץ עולה על 10 MB" });

            byte[] templateBytes;
            using (var ms = new MemoryStream())
            {
                await file.CopyToAsync(ms);
                templateBytes = ms.ToArray();
            }

            // Validate that EPPlus can open it
            IReadOnlyList<string> placeholders;
            try
            {
                placeholders = _templateService.ScanPlaceholders(templateBytes);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Uploaded template is not a valid Excel file for report {ReportId}", reportId);
                return BadRequest(new { success = false, message = "הקובץ אינו קובץ Excel תקין" });
            }

            if (report.Template == null)
            {
                var template = new ExcelReportTemplate
                {
                    ReportId = reportId,
                    TemplateFilename = file.FileName,
                    TemplateBlob = templateBytes,
                    CellMappingsJson = null,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.ExcelReportTemplates.Add(template);
            }
            else
            {
                report.Template.TemplateFilename = file.FileName;
                report.Template.TemplateBlob = templateBytes;
                report.Template.UpdatedAt = DateTime.UtcNow;
                // Reset mappings when template is replaced
                report.Template.CellMappingsJson = null;
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Template uploaded for report {ReportId}: {Filename} ({Size} bytes), {PlaceholderCount} placeholders",
                reportId, file.FileName, templateBytes.Length, placeholders.Count);

            return Ok(new
            {
                success = true,
                data = new
                {
                    filename = file.FileName,
                    sizeBytes = templateBytes.Length,
                    placeholders
                }
            });
        }

        /// <summary>
        /// GET /api/excelreporttemplates/{reportId}/download
        /// Download the stored template file.
        /// </summary>
        [HttpGet("{reportId:int}/download")]
        public async Task<IActionResult> DownloadTemplate(int reportId)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            var template = await _context.ExcelReportTemplates
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.ReportId == reportId);

            if (template == null)
                return NotFound(new { success = false, message = "תבנית לא נמצאה" });

            return File(
                template.TemplateBlob,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                template.TemplateFilename ?? $"template_{reportId}.xlsx");
        }

        /// <summary>
        /// GET /api/excelreporttemplates/{reportId}/scan
        /// Scan the stored template and return all {{placeholder}} names found.
        /// </summary>
        [HttpGet("{reportId:int}/scan")]
        public async Task<IActionResult> ScanPlaceholders(int reportId)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            var template = await _context.ExcelReportTemplates
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.ReportId == reportId);

            if (template == null)
                return NotFound(new { success = false, message = "תבנית לא נמצאה" });

            try
            {
                var placeholders = _templateService.ScanPlaceholders(template.TemplateBlob);
                return Ok(new { success = true, data = placeholders });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scanning template for report {ReportId}", reportId);
                return StatusCode(500, new { success = false, message = "שגיאה בסריקת התבנית" });
            }
        }

        /// <summary>
        /// PUT /api/excelreporttemplates/{reportId}/mappings
        /// Save cell mapping configuration (JSON string) for a template.
        /// Format: [{"placeholder":"{{StudentName}}","entityName":"Students","fieldName":"FirstName","isCollection":false}]
        /// </summary>
        [HttpPut("{reportId:int}/mappings")]
        public async Task<IActionResult> SaveMappings(
            int reportId,
            [FromBody] SaveMappingsRequest request)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            var template = await _context.ExcelReportTemplates
                .FirstOrDefaultAsync(t => t.ReportId == reportId);

            if (template == null)
                return NotFound(new { success = false, message = "תבנית לא נמצאה" });

            template.CellMappingsJson = request.CellMappingsJson;
            template.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new { success = true });
        }
    }

    public record SaveMappingsRequest(string? CellMappingsJson);
}
