using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Petel.Core.Controllers;
using Petel.Core.Session;
using PetelAssistants.Api.DTOs;
using PetelAssistants.Api.Services;

namespace PetelAssistants.Api.Controllers
{
    [ApiController]
    [Route("api/org-units")]
    public class OrgUnitsController : BaseController
    {
        private readonly OrgUnitService _orgUnitService;
        private readonly InstitutionRosterFileProcessor _uploadProcessor;

        public OrgUnitsController(
            OrgUnitService orgUnitService,
            InstitutionRosterFileProcessor uploadProcessor,
            UserSessionService userSessionService,
            ILogger<OrgUnitsController> logger)
            : base(userSessionService, logger)
        {
            _orgUnitService = orgUnitService;
            _uploadProcessor = uploadProcessor;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] int yearId, [FromQuery] string? type)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            if (!int.TryParse(session.EntityId, out int entityId))
                return BadRequest(new { success = false, message = "מזהה רשות לא תקין" });

            if (yearId <= 0)
                return BadRequest(new { success = false, message = "שנה לא תקינה" });

            try
            {
                var units = await _orgUnitService.ListOrgUnitsAsync(entityId, yearId, type);
                return Ok(new { success = true, data = units });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpGet("copy-status")]
        public async Task<IActionResult> GetCopyStatus([FromQuery] int yearId)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            if (yearId <= 0)
                return BadRequest(new { success = false, message = "שנה לא תקינה" });

            try
            {
                var status = await _orgUnitService.GetCopyStatusAsync(yearId);
                return Ok(new { success = true, data = status });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPost("copy-from-previous")]
        public async Task<IActionResult> CopyFromPrevious([FromQuery] int yearId)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            if (!int.TryParse(session.EntityId, out int entityId))
                return BadRequest(new { success = false, message = "מזהה רשות לא תקין" });

            if (yearId <= 0)
                return BadRequest(new { success = false, message = "שנה לא תקינה" });

            int? userId = int.TryParse(session.UserId, out int uid) ? uid : null;

            try
            {
                var (previousYearName, imported) =
                    await _orgUnitService.CopyFromPreviousAsync(entityId, userId, yearId);
                return Ok(new
                {
                    success = true,
                    message = $"הועתקו {imported} מוסדות משנת {previousYearName}",
                    previousYearName,
                    imported
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPost("upload/preview")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(10_000_000)]
        public IActionResult PreviewUpload([FromForm] PersonsFilePreviewRequest request)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            if (request.File == null || request.File.Length == 0)
                return BadRequest(new { success = false, message = "לא הועלה קובץ" });

            try
            {
                var headers = _uploadProcessor.ReadHeaders(request.File);
                if (headers.Count == 0)
                    return BadRequest(new { success = false, message = "לא נמצאו כותרות בקובץ או הקובץ ריק" });

                var suggestedMappings = InstitutionRosterFileProcessor.GenerateSuggestedMappings(headers);

                return Ok(new
                {
                    success = true,
                    headers,
                    suggestedMappings,
                    availableFields = InstitutionRosterFileProcessor.GetAvailableFields()
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error previewing institution roster file");
                return StatusCode(500, new { success = false, message = "שגיאה בקריאת הקובץ: " + ex.Message });
            }
        }

        [HttpPost("upload")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(10_000_000)]
        public async Task<IActionResult> Upload([FromQuery] int yearId, [FromForm] PersonsFileUploadRequest request)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            if (!int.TryParse(session.EntityId, out int entityId))
                return BadRequest(new { success = false, message = "מזהה רשות לא תקין" });

            if (yearId <= 0)
                return BadRequest(new { success = false, message = "שנה לא תקינה" });

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

            // Client sends header -> field; convert to field -> header like persons when needed.
            var fieldToHeader = NormalizeMapping(mapping);
            var mappingError = InstitutionRosterFileProcessor.ValidateMapping(fieldToHeader);
            if (mappingError != null)
                return BadRequest(new { success = false, message = mappingError });

            int? userId = int.TryParse(session.UserId, out int uid) ? uid : null;

            try
            {
                var rows = _uploadProcessor.ParseFile(request.File, fieldToHeader);
                if (rows.Count == 0)
                    return BadRequest(new { success = false, message = "לא נמצאו שורות נתונים בקובץ" });

                var result = await _uploadProcessor.ProcessRowsAsync(entityId, userId, yearId, rows);

                return Ok(new
                {
                    success = true,
                    message = "הקובץ עובד בהצלחה",
                    created = result.Created,
                    linkedExisting = result.LinkedExisting,
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
                _logger.LogError(ex, "Error uploading institution roster file");
                return StatusCode(500, new { success = false, message = "שגיאה בעיבוד הקובץ: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromQuery] int yearId, [FromBody] CreateOrgUnitRequest request)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            if (!int.TryParse(session.EntityId, out int entityId))
                return BadRequest(new { success = false, message = "מזהה רשות לא תקין" });

            if (yearId <= 0)
                return BadRequest(new { success = false, message = "שנה לא תקינה" });

            int? userId = int.TryParse(session.UserId, out int uid) ? uid : null;

            try
            {
                var id = await _orgUnitService.CreateOrgUnitAsync(entityId, userId, yearId, request);
                return Ok(new { success = true, message = "מוסד נוצר בהצלחה", data = new { id } });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromQuery] int yearId, [FromBody] UpdateOrgUnitRequest request)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            if (!int.TryParse(session.EntityId, out int entityId))
                return BadRequest(new { success = false, message = "מזהה רשות לא תקין" });

            if (yearId <= 0)
                return BadRequest(new { success = false, message = "שנה לא תקינה" });

            int? userId = int.TryParse(session.UserId, out int uid) ? uid : null;

            try
            {
                await _orgUnitService.UpdateOrgUnitAsync(entityId, userId, yearId, id, request);
                return Ok(new { success = true, message = "מוסד עודכן בהצלחה" });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPut("{id:int}/remove-from-year")]
        public async Task<IActionResult> RemoveFromYear(int id, [FromQuery] int yearId)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            if (!int.TryParse(session.EntityId, out int entityId))
                return BadRequest(new { success = false, message = "מזהה רשות לא תקין" });

            if (yearId <= 0)
                return BadRequest(new { success = false, message = "שנה לא תקינה" });

            int? userId = int.TryParse(session.UserId, out int uid) ? uid : null;

            try
            {
                await _orgUnitService.RemoveFromYearAsync(entityId, userId, yearId, id);
                return Ok(new { success = true, message = "המוסד הוסר מרשימת השנה" });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Accepts either field→header (persons style) or header→field (suggestedMappings style).
        /// Returns field→header for the processor.
        /// </summary>
        private static Dictionary<string, string> NormalizeMapping(Dictionary<string, string> mapping)
        {
            var available = InstitutionRosterFileProcessor.GetAvailableFields();
            var looksLikeFieldToHeader = mapping.Keys.Any(k => available.ContainsKey(k));
            if (looksLikeFieldToHeader)
                return mapping;

            var inverted = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in mapping)
            {
                if (string.IsNullOrWhiteSpace(pair.Value) || pair.Value == "ignore")
                    continue;
                inverted[pair.Value] = pair.Key;
            }

            return inverted;
        }
    }
}
