using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Petel.Core.Controllers;
using Petel.Core.Session;
using PetelAssistants.Api.DTOs;
using PetelAssistants.Api.Services;

namespace PetelAssistants.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SalaryFileUploadController : BaseController
    {
        private readonly SalaryFileProcessor _processor;

        public SalaryFileUploadController(
            SalaryFileProcessor processor,
            UserSessionService sessionService,
            ILogger<SalaryFileUploadController> logger)
            : base(sessionService, logger)
        {
            _processor = processor;
        }

        [HttpGet("period-exists")]
        public async Task<IActionResult> PeriodExists([FromQuery] int year, [FromQuery] int month)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            if (month < 1 || month > 12)
                return BadRequest(new { success = false, message = "חודש לא תקין" });

            var (exists, rowCount, totalSalary) = await _processor.GetPeriodStatsAsync(year, month);
            return Ok(new
            {
                success = true,
                exists,
                rowCount,
                totalSalary
            });
        }

        [HttpGet("mapping")]
        public async Task<IActionResult> GetMapping()
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            var saved = await _processor.GetSavedMappingAsync();
            if (saved == null)
                return Ok(new { success = true, hasMapping = false });

            Dictionary<string, string>? mapping = null;
            try
            {
                mapping = JsonSerializer.Deserialize<Dictionary<string, string>>(saved.MappingJson);
            }
            catch (JsonException)
            {
                // ignore corrupt saved map
            }

            return Ok(new
            {
                success = true,
                hasMapping = true,
                mapping,
                idIncludesCheckDigit = saved.IdIncludesCheckDigit
            });
        }

        [HttpPut("mapping")]
        public async Task<IActionResult> SaveMapping([FromBody] SalaryFieldMappingSaveRequest request)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            if (!int.TryParse(session.EntityId, out int entityId))
                return BadRequest(new { success = false, message = "מזהה רשות לא תקין" });

            Dictionary<string, string>? mapping;
            try
            {
                mapping = string.IsNullOrWhiteSpace(request.MappingJson)
                    ? null
                    : JsonSerializer.Deserialize<Dictionary<string, string>>(request.MappingJson);
            }
            catch (JsonException)
            {
                return BadRequest(new { success = false, message = "מיפוי עמודות לא תקין" });
            }

            if (mapping == null || mapping.Count == 0)
                return BadRequest(new { success = false, message = "יש לספק מיפוי עמודות" });

            var mappingError = SalaryFileProcessor.ValidateMapping(mapping);
            if (mappingError != null)
                return BadRequest(new { success = false, message = mappingError });

            int? userId = int.TryParse(session.UserId, out int uid) ? uid : null;
            await _processor.SaveMappingAsync(entityId, userId, mapping, request.IdIncludesCheckDigit);

            return Ok(new { success = true, message = "המיפוי נשמר" });
        }

        [HttpPost("preview")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(10_000_000)]
        public async Task<IActionResult> PreviewFile([FromForm] SalaryFilePreviewRequest request)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            if (request.File == null || request.File.Length == 0)
                return BadRequest(new { success = false, message = "לא הועלה קובץ" });

            try
            {
                var headers = _processor.ReadHeaders(request.File);
                if (headers.Count == 0)
                    return BadRequest(new { success = false, message = "לא נמצאו כותרות בקובץ או הקובץ ריק" });

                var suggestedMappings = SalaryFileProcessor.GenerateSuggestedMappings(headers);
                var saved = await _processor.GetSavedMappingAsync();

                Dictionary<string, string>? savedMapping = null;
                bool? idIncludesCheckDigit = null;
                if (saved != null)
                {
                    try
                    {
                        savedMapping = JsonSerializer.Deserialize<Dictionary<string, string>>(saved.MappingJson);
                    }
                    catch (JsonException)
                    {
                        savedMapping = null;
                    }
                    idIncludesCheckDigit = saved.IdIncludesCheckDigit;
                }

                // Convert saved system→header map into header→system for the UI, filtered to file headers
                Dictionary<string, string>? defaultColumnMappings = null;
                if (savedMapping != null)
                {
                    defaultColumnMappings = new Dictionary<string, string>();
                    foreach (var kvp in savedMapping)
                    {
                        if (string.IsNullOrWhiteSpace(kvp.Value))
                            continue;
                        if (headers.Any(h => string.Equals(h, kvp.Value, StringComparison.OrdinalIgnoreCase)))
                            defaultColumnMappings[kvp.Value] = kvp.Key;
                    }
                }

                return Ok(new
                {
                    success = true,
                    headers,
                    suggestedMappings,
                    availableFields = SalaryFileProcessor.GetAvailableFields(),
                    savedMapping = defaultColumnMappings,
                    idIncludesCheckDigit = idIncludesCheckDigit ?? true,
                    hasSavedMapping = savedMapping != null
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error previewing salary file");
                return StatusCode(500, new { success = false, message = "שגיאה בקריאת הקובץ: " + ex.Message });
            }
        }

        [HttpPost("upload")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(10_000_000)]
        public async Task<IActionResult> UploadFile([FromForm] SalaryFileUploadRequest request)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            if (!int.TryParse(session.EntityId, out int entityId))
                return BadRequest(new { success = false, message = "מזהה רשות לא תקין" });

            if (request.File == null || request.File.Length == 0)
                return BadRequest(new { success = false, message = "לא הועלה קובץ" });

            if (request.PeriodMonth < 1 || request.PeriodMonth > 12)
                return BadRequest(new { success = false, message = "חודש לא תקין" });

            Dictionary<string, string>? mapping;
            try
            {
                mapping = string.IsNullOrWhiteSpace(request.MappingJson)
                    ? null
                    : JsonSerializer.Deserialize<Dictionary<string, string>>(request.MappingJson);
            }
            catch (JsonException)
            {
                return BadRequest(new { success = false, message = "מיפוי עמודות לא תקין" });
            }

            if (mapping == null || mapping.Count == 0)
                return BadRequest(new { success = false, message = "יש לספק מיפוי עמודות" });

            var mappingError = SalaryFileProcessor.ValidateMapping(mapping);
            if (mappingError != null)
                return BadRequest(new { success = false, message = mappingError });

            int? userId = int.TryParse(session.UserId, out int uid) ? uid : null;

            try
            {
                var rows = _processor.ParseFile(request.File, mapping);
                if (rows.Count == 0)
                    return BadRequest(new { success = false, message = "לא נמצאו שורות נתונים בקובץ" });

                var result = await _processor.ProcessUploadAsync(
                    entityId,
                    userId,
                    request.PeriodYear,
                    request.PeriodMonth,
                    request.ReplaceExisting,
                    request.IdIncludesCheckDigit,
                    request.File.FileName,
                    rows);

                if (request.SaveMapping)
                    await _processor.SaveMappingAsync(entityId, userId, mapping, request.IdIncludesCheckDigit);

                return Ok(new
                {
                    success = true,
                    message = "הקובץ עובד בהצלחה",
                    processId = result.ProcessId,
                    created = result.Created,
                    errors = result.Errors,
                    warnings = result.Warnings,
                    totalSalarySum = result.TotalSalarySum,
                    periodYear = request.PeriodYear,
                    periodMonth = request.PeriodMonth,
                    details = new
                    {
                        errorList = result.ErrorList,
                        warningList = result.WarningList
                    }
                });
            }
            catch (PeriodExistsException ex)
            {
                return Conflict(new { success = false, message = ex.Message, periodExists = true });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading salary file");
                return StatusCode(500, new { success = false, message = "שגיאה בעיבוד הקובץ: " + ex.Message });
            }
        }
    }
}
