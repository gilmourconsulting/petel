using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetelApp.Api.Data;
using PetelApp.Api.Session; // Add this using for UserSessionService and UserSession

namespace PetelApp.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HoursBudgetController : BaseController
    {
        private readonly AppDbContext _context;

        public HoursBudgetController(
            UserSessionService userSessionService,
            ILogger<HoursBudgetController> logger,
            AppDbContext context)
            : base(userSessionService, logger)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetHoursBudgets()
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { message = "לא נמצא מושב פעיל" });
                }

                // Entity-Based Request Flow - scope by user's EntityId
                var hoursBudgets = await _context.HoursBudgets
                    .Where(hb => hb.EntityId == session.EntityId)
                    .OrderBy(hb => hb.SchoolYear)
                    .ThenBy(hb => hb.BudgetType)
                    .Select(hb => new
                    {
                        id = hb.Id,
                        entityId = hb.EntityId,
                        schoolYear = hb.SchoolYear,
                        budgetType = hb.BudgetType,
                        allocatedHours = hb.AllocatedHours,
                        usedHours = hb.UsedHours,
                        remainingHours = hb.RemainingHours,
                        department = hb.Department,
                        notes = hb.Notes,
                        createdAt = hb.CreatedAt,
                        updatedAt = hb.UpdatedAt
                    })
                    .ToListAsync();

                _logger.LogInformation("Retrieved {Count} hours budgets for entity {EntityId}", 
                    hoursBudgets.Count, session.EntityId);

                return Ok(new
                {
                    success = true,
                    data = hoursBudgets,
                    totalCount = hoursBudgets.Count,
                    entityId = session.EntityId,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving hours budgets");
                return StatusCode(500, new { 
                    success = false, 
                    message = "שגיאה פנימית בשרת" 
                });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateHoursBudget([FromBody] HoursBudgetRequest request)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { message = "לא נמצא מושב פעיל" });
                }

                var hoursBudget = new HoursBudget
                {
                    EntityId = session.EntityId, // Set from session
                    SchoolYear = request.SchoolYear,
                    BudgetType = request.BudgetType,
                    AllocatedHours = request.AllocatedHours,
                    UsedHours = request.UsedHours ?? 0,
                    RemainingHours = request.AllocatedHours - (request.UsedHours ?? 0),
                    Department = request.Department,
                    Notes = request.Notes,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.HoursBudgets.Add(hoursBudget);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Hours budget created with ID {Id} for entity {EntityId}", 
                    hoursBudget.Id, session.EntityId);

                return CreatedAtAction(nameof(GetHoursBudgets), new { id = hoursBudget.Id }, new
                {
                    success = true,
                    message = "תקציב שעות נוצר בהצלחה",
                    data = hoursBudget,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating hours budget");
                return StatusCode(500, new { 
                    success = false, 
                    message = "שגיאה ביצירת תקציב שעות" 
                });
            }
        }
    }

    public class HoursBudgetRequest
    {
        public string? SchoolYear { get; set; }
        public string? BudgetType { get; set; }
        public decimal AllocatedHours { get; set; }
        public decimal? UsedHours { get; set; }
        public string? Department { get; set; }
        public string? Notes { get; set; }
    }
}
