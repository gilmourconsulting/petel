using Microsoft.AspNetCore.Mvc;
using Petel.Core.Controllers;
using Petel.Core.Session;
using PetelAssistants.Api.DTOs;
using PetelAssistants.Api.Services;

namespace PetelAssistants.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PersonsController : BaseController
    {
        private readonly PersonService _personService;

        public PersonsController(
            PersonService personService,
            UserSessionService sessionService,
            ILogger<PersonsController> logger)
            : base(sessionService, logger)
        {
            _personService = personService;
        }

        [HttpGet("phone-types")]
        public async Task<IActionResult> GetPhoneTypes()
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            var types = await _personService.GetPhoneTypesAsync();
            return Ok(new { success = true, data = types });
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            var persons = await _personService.ListPersonsAsync();
            return Ok(new { success = true, data = persons });
        }

        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] string term)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            var persons = await _personService.SearchPersonsAsync(term ?? string.Empty);
            return Ok(new { success = true, data = persons });
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            var snapshot = await _personService.GetPersonSnapshotAsync(id);
            if (snapshot == null)
                return NotFound(new { success = false, message = "אדם לא נמצא" });

            return Ok(new { success = true, data = snapshot });
        }

        [HttpGet("{id:int}/history")]
        public async Task<IActionResult> GetHistory(int id)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            var snapshot = await _personService.GetPersonSnapshotAsync(id);
            if (snapshot == null)
                return NotFound(new { success = false, message = "אדם לא נמצא" });

            var history = await _personService.GetDetailHistoryAsync(id);
            return Ok(new { success = true, data = history });
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreatePersonRequest request)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            if (!int.TryParse(session.EntityId, out int entityId))
                return BadRequest(new { success = false, message = "מזהה רשות לא תקין" });

            int? userId = int.TryParse(session.UserId, out int uid) ? uid : null;

            try
            {
                var personId = await _personService.CreatePersonAsync(entityId, userId, request);
                if (personId == null)
                    return StatusCode(500, new { success = false, message = "שגיאה ביצירת אדם" });

                var snapshot = await _personService.GetPersonSnapshotAsync(personId.Value);
                return Ok(new { success = true, message = "אדם נוצר בהצלחה", data = snapshot, id = personId });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating person");
                return StatusCode(500, new { success = false, message = "שגיאה ביצירת אדם" });
            }
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdatePersonRequest request)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            if (!int.TryParse(session.EntityId, out int entityId))
                return BadRequest(new { success = false, message = "מזהה רשות לא תקין" });

            int? userId = int.TryParse(session.UserId, out int uid) ? uid : null;

            try
            {
                var updated = await _personService.UpdatePersonAsync(id, entityId, userId, request);
                if (!updated)
                    return NotFound(new { success = false, message = "אדם לא נמצא" });

                var snapshot = await _personService.GetPersonSnapshotAsync(id);
                return Ok(new { success = true, message = "פרטי האדם עודכנו בהצלחה", data = snapshot });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating person {PersonId}", id);
                return StatusCode(500, new { success = false, message = "שגיאה בעדכון אדם" });
            }
        }
    }
}
