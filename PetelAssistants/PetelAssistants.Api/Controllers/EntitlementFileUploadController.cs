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
    public class EntitlementFileUploadController : BaseController
    {
        private readonly EntitlementFileProcessor _processor;

        public EntitlementFileUploadController(
            EntitlementFileProcessor processor,
            UserSessionService sessionService,
            ILogger<EntitlementFileUploadController> logger)
            : base(sessionService, logger)
        {
            _processor = processor;
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
                mapping
            });
        }

        [HttpPut("mapping")]
        public async Task<IActionResult> SaveMapping([FromBody] EntitlementFieldMappingSaveRequest request)
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

            var mappingError = EntitlementFileProcessor.ValidateMapping(mapping);
            if (mappingError != null)
                return BadRequest(new { success = false, message = mappingError });

            int? userId = int.TryParse(session.UserId, out int uid) ? uid : null;
            await _processor.SaveMappingAsync(entityId, userId, mapping);

            return Ok(new { success = true, message = "המיפוי נשמר" });
        }

        [HttpPost("preview")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(10_000_000)]
        public async Task<IActionResult> PreviewFile([FromForm] EntitlementFilePreviewRequest request)
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

                var suggestedMappings = EntitlementFileProcessor.GenerateSuggestedMappings(headers);
                var saved = await _processor.GetSavedMappingAsync();

                Dictionary<string, string>? savedMapping = null;
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
                }

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
                    availableFields = EntitlementFileProcessor.GetAvailableFields(),
                    savedMapping = defaultColumnMappings,
                    hasSavedMapping = savedMapping != null
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error previewing entitlement file");
                return StatusCode(500, new { success = false, message = "שגיאה בקריאת הקובץ: " + ex.Message });
            }
        }

        [HttpPost("upload")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(10_000_000)]
        public async Task<IActionResult> UploadFile([FromForm] EntitlementFileUploadRequest request)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            if (!int.TryParse(session.EntityId, out int entityId))
                return BadRequest(new { success = false, message = "מזהה רשות לא תקין" });

            if (request.File == null || request.File.Length == 0)
                return BadRequest(new { success = false, message = "לא הועלה קובץ" });

            if (request.YearId <= 0)
                return BadRequest(new { success = false, message = "שנה עברית נדרשת" });

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

            var mappingError = EntitlementFileProcessor.ValidateMapping(mapping);
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
                    request.YearId,
                    request.File.FileName,
                    rows);

                if (request.SaveMapping)
                    await _processor.SaveMappingAsync(entityId, userId, mapping);

                return Ok(new
                {
                    success = true,
                    message = "הקובץ עובד בהצלחה",
                    processId = result.ProcessId,
                    created = result.Created,
                    versioned = result.Versioned,
                    skipped = result.Skipped,
                    errors = result.Errors,
                    yearId = request.YearId,
                    orphans = result.Orphans,
                    details = new
                    {
                        errorList = result.ErrorList
                    }
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading entitlement file");
                return StatusCode(500, new { success = false, message = "שגיאה בעיבוד הקובץ: " + ex.Message });
            }
        }

        [HttpPost("cancel-orphans")]
        public async Task<IActionResult> CancelOrphans([FromBody] EntitlementCancelOrphansRequest request)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            if (request.YearId <= 0)
                return BadRequest(new { success = false, message = "שנה עברית נדרשת" });

            if (request.EntitlementIds == null || request.EntitlementIds.Count == 0)
                return BadRequest(new { success = false, message = "לא נבחרו זכאויות לביטול" });

            int? userId = int.TryParse(session.UserId, out int uid) ? uid : null;

            try
            {
                var cancelled = await _processor.CancelOrphansAsync(userId, request.YearId, request.EntitlementIds);
                return Ok(new
                {
                    success = true,
                    message = $"בוטלו {cancelled} זכאויות",
                    cancelled
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling orphan entitlements");
                return StatusCode(500, new { success = false, message = "שגיאה בביטול זכאויות: " + ex.Message });
            }
        }
    }
}
