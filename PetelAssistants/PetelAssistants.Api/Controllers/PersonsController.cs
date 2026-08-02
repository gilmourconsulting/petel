using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Petel.Core.Controllers;
using Petel.Core.Session;
using PetelAssistants.Api.Data;
using PetelAssistants.Api.DTOs;
using PetelAssistants.Api.Services;

namespace PetelAssistants.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PersonsController : BaseController
    {
        private readonly PersonService _personService;
        private readonly EntitlementService _entitlementService;
        private readonly AssistDbContext _context;

        public PersonsController(
            PersonService personService,
            EntitlementService entitlementService,
            AssistDbContext context,
            UserSessionService sessionService,
            ILogger<PersonsController> logger)
            : base(sessionService, logger)
        {
            _personService = personService;
            _entitlementService = entitlementService;
            _context = context;
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
        public async Task<IActionResult> GetAll([FromQuery] int? yearId = null)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            var persons = await _personService.ListPersonsAsync();
            await ApplyAllocationFlagsAsync(persons, yearId);
            return Ok(new { success = true, data = persons });
        }

        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] string term, [FromQuery] int? yearId = null)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            var persons = await _personService.SearchPersonsAsync(term ?? string.Empty);
            await ApplyAllocationFlagsAsync(persons, yearId);
            return Ok(new { success = true, data = persons });
        }

        private async Task ApplyAllocationFlagsAsync(List<PersonListItemDto> persons, int? yearId)
        {
            if (yearId is null or <= 0 || persons.Count == 0)
                return;

            var allocatedIds = await _entitlementService.GetAllocatedPersonIdsAsync(yearId.Value);
            foreach (var person in persons)
                person.HasAllocation = allocatedIds.Contains(person.Id);
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

        [HttpGet("{id:int}/allocations")]
        public async Task<IActionResult> GetAllocations(int id, [FromQuery] int? yearId = null)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            if (yearId is <= 0)
                return BadRequest(new { success = false, message = "שנה לא תקינה" });

            var snapshot = await _personService.GetPersonSnapshotAsync(id);
            if (snapshot == null)
                return NotFound(new { success = false, message = "אדם לא נמצא" });

            try
            {
                var items = await _entitlementService.ListAllocationsByPersonAsync(id, yearId);
                return Ok(new { success = true, data = items });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpGet("{id:int}/salaries")]
        public async Task<IActionResult> GetSalaries(int id)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            var snapshot = await _personService.GetPersonSnapshotAsync(id);
            if (snapshot == null)
                return NotFound(new { success = false, message = "אדם לא נמצא" });

            var items = await _context.Salaries
                .AsNoTracking()
                .Where(s => s.MatchedPersonId == id)
                .Select(s => new SalaryListItemDto
                {
                    Id = s.Id,
                    PeriodYear = s.PeriodYear,
                    PeriodMonth = s.PeriodMonth,
                    NationalId = s.NationalId,
                    DepartmentId = s.DepartmentId,
                    DepartmentName = s.DepartmentName,
                    PositionPercentage = s.PositionPercentage,
                    TotalSalary = s.TotalSalary,
                    MatchedPersonId = s.MatchedPersonId,
                    MatchedPersonName = s.MatchedPerson != null
                        ? s.MatchedPerson.Details
                            .Where(d => d.IsLastVersion)
                            .Select(d => (d.FirstName + " " + d.LastName).Trim())
                            .FirstOrDefault()
                        : null,
                    HasIdWarning = s.HasIdWarning,
                    ProcessId = s.ProcessId
                })
                .ToListAsync();

            items = items
                .OrderByDescending(s => s.PeriodYear)
                .ThenByDescending(s => s.PeriodMonth)
                .ToList();

            return Ok(new { success = true, data = items });
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
