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
    public class PersonsFileUploadController : BaseController
    {
        private readonly PersonsFileProcessor _processor;

        public PersonsFileUploadController(
            PersonsFileProcessor processor,
            UserSessionService sessionService,
            ILogger<PersonsFileUploadController> logger)
            : base(sessionService, logger)
        {
            _processor = processor;
        }

        [HttpPost("preview")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(10_000_000)]
        public IActionResult PreviewFile([FromForm] PersonsFilePreviewRequest request)
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

                var suggestedMappings = PersonsFileProcessor.GenerateSuggestedMappings(headers);

                return Ok(new
                {
                    success = true,
                    headers,
                    suggestedMappings,
                    availableFields = PersonsFileProcessor.GetAvailableFields()
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error previewing persons file");
                return StatusCode(500, new { success = false, message = "שגיאה בקריאת הקובץ: " + ex.Message });
            }
        }

        [HttpPost("upload")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(10_000_000)]
        public async Task<IActionResult> UploadFile([FromForm] PersonsFileUploadRequest request)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            if (!int.TryParse(session.EntityId, out int entityId))
                return BadRequest(new { success = false, message = "מזהה רשות לא תקין" });

            if (request.File == null || request.File.Length == 0)
                return BadRequest(new { success = false, message = "לא הועלה קובץ" });

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

            var mappingError = PersonsFileProcessor.ValidateMapping(mapping);
            if (mappingError != null)
                return BadRequest(new { success = false, message = mappingError });

            int? userId = int.TryParse(session.UserId, out int uid) ? uid : null;

            try
            {
                var rows = _processor.ParseFile(request.File, mapping);
                if (rows.Count == 0)
                    return BadRequest(new { success = false, message = "לא נמצאו שורות נתונים בקובץ" });

                var result = await _processor.ProcessRowsAsync(entityId, userId, rows);

                return Ok(new
                {
                    success = true,
                    message = "הקובץ עובד בהצלחה",
                    created = result.Created,
                    skipped = result.Skipped,
                    errors = result.Errors.Count,
                    details = new { errorList = result.Errors }
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading persons file");
                return StatusCode(500, new { success = false, message = "שגיאה בעיבוד הקובץ: " + ex.Message });
            }
        }
    }
}
