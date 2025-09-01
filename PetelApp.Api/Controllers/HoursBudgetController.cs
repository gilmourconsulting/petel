using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetelApp.Api.Data;
using PetelApp.Api.Models;
using PetelApp.Api.Session;

namespace PetelApp.Api.Controllers
{
    /// <summary>
    /// Hours budget management following multi-tenant request flow
    /// Inherits from BaseController for tenant isolation
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class HoursBudgetController : BaseController
    {
        private readonly AppDbContext _context;

        public HoursBudgetController(
            AppDbContext context,
            UserSessionService userSessionService) : base(userSessionService)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetHoursBudgets()
        {
            try
            {
                // Validate tenant access following multi-tenant request flow
                ValidateTenantAccess();
                var tenantId = GetTenantId();

                if (string.IsNullOrEmpty(tenantId))
                {
                    return BadRequest(new { message = "חסר זיהוי גוף - אנא התחבר מחדש" });
                }

                if (!int.TryParse(tenantId, out int tenantIdInt))
                {
                    return BadRequest(new { message = "זיהוי גוף לא תקין" });
                }

                // Query database entity following database conventions
                var dbBudgets = await _context.HoursBudgets
                    .Where(hb => hb.SchoolId == tenantIdInt && hb.IsActive)
                    .Include(hb => hb.SchoolYear)
                    .OrderBy(hb => hb.BudgetName)
                    .ToListAsync();

                // Convert to DTOs following project-specific patterns
                var budgetDtos = dbBudgets.Select(db => new HoursBudgetDto
                {
                    Id = db.Id,
                    SchoolId = db.SchoolId,
                    SchoolYearId = db.SchoolYearId,
                    BudgetName = db.BudgetName,
                    AllocatedHours = db.AllocatedHours,
                    UsedHours = db.UsedHours,
                    RemainingHours = db.RemainingHours,
                    IsActive = db.IsActive,
                    CreatedAt = db.CreatedAt,
                    UpdatedAt = db.UpdatedAt,
                    SchoolYearName = db.SchoolYear?.YearName ?? "לא מוגדר"
                }).ToList();

                return Ok(budgetDtos);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "שגיאה בטעינת תקציבי השעות", error = ex.Message });
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetHoursBudget(int id)
        {
            try
            {
                ValidateTenantAccess();
                var tenantId = GetTenantId();

                if (!int.TryParse(tenantId, out int tenantIdInt))
                {
                    return BadRequest(new { message = "זיהוי גוף לא תקין" });
                }

                // Query database entity
                var dbBudget = await _context.HoursBudgets
                    .Include(hb => hb.SchoolYear)
                    .FirstOrDefaultAsync(hb => hb.Id == id && hb.SchoolId == tenantIdInt);

                if (dbBudget == null)
                {
                    return NotFound(new { message = "תקציב שעות לא נמצא" });
                }

                // Convert to DTO
                var budgetDto = new HoursBudgetDto
                {
                    Id = dbBudget.Id,
                    SchoolId = dbBudget.SchoolId,
                    SchoolYearId = dbBudget.SchoolYearId,
                    BudgetName = dbBudget.BudgetName,
                    AllocatedHours = dbBudget.AllocatedHours,
                    UsedHours = dbBudget.UsedHours,
                    RemainingHours = dbBudget.RemainingHours,
                    IsActive = dbBudget.IsActive,
                    CreatedAt = dbBudget.CreatedAt,
                    UpdatedAt = dbBudget.UpdatedAt,
                    SchoolYearName = dbBudget.SchoolYear?.YearName ?? "לא מוגדר"
                };

                return Ok(budgetDto);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "שגיאה בטעינת תקציב השעות", error = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateHoursBudget([FromBody] CreateHoursBudgetRequest request)
        {
            try
            {
                ValidateTenantAccess();
                var tenantId = GetTenantId();

                if (string.IsNullOrEmpty(tenantId))
                {
                    return BadRequest(new { message = "חסר זיהוי גוף - אנא התחבר מחדש" });
                }

                if (!int.TryParse(tenantId, out int tenantIdInt))
                {
                    return BadRequest(new { message = "זיהוי גוף לא תקין" });
                }

                // Create database entity following database conventions
                var dbBudget = new Data.HoursBudget
                {
                    SchoolId = tenantIdInt,
                    SchoolYearId = request.SchoolYearId,
                    BudgetName = request.BudgetName,
                    AllocatedHours = request.AllocatedHours,
                    UsedHours = 0,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.HoursBudgets.Add(dbBudget);
                await _context.SaveChangesAsync();

                // Convert to DTO for response
                var budgetDto = new HoursBudgetDto
                {
                    Id = dbBudget.Id,
                    SchoolId = dbBudget.SchoolId,
                    SchoolYearId = dbBudget.SchoolYearId,
                    BudgetName = dbBudget.BudgetName,
                    AllocatedHours = dbBudget.AllocatedHours,
                    UsedHours = dbBudget.UsedHours,
                    RemainingHours = dbBudget.RemainingHours,
                    IsActive = dbBudget.IsActive,
                    CreatedAt = dbBudget.CreatedAt,
                    UpdatedAt = dbBudget.UpdatedAt
                };

                return CreatedAtAction(nameof(GetHoursBudget), new { id = budgetDto.Id }, budgetDto);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "שגיאה ביצירת תקציב השעות", error = ex.Message });
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateHoursBudget(int id, [FromBody] UpdateHoursBudgetRequest request)
        {
            try
            {
                ValidateTenantAccess();
                var tenantId = GetTenantId();

                if (!int.TryParse(tenantId, out int tenantIdInt))
                {
                    return BadRequest(new { message = "זיהוי גוף לא תקין" });
                }

                // Find existing database entity
                var dbBudget = await _context.HoursBudgets
                    .FirstOrDefaultAsync(hb => hb.Id == id && hb.SchoolId == tenantIdInt);

                if (dbBudget == null)
                {
                    return NotFound(new { message = "תקציב שעות לא נמצא" });
                }

                // Update entity following database conventions
                dbBudget.BudgetName = request.BudgetName;
                dbBudget.AllocatedHours = request.AllocatedHours;
                dbBudget.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return Ok(new { message = "תקציב השעות עודכן בהצלחה" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "שגיאה בעדכון תקציב השעות", error = ex.Message });
            }
        }
    }

    // Request DTOs following project-specific patterns
    public class CreateHoursBudgetRequest
    {
        public int SchoolYearId { get; set; }
        public string BudgetName { get; set; } = string.Empty;
        public decimal AllocatedHours { get; set; }
    }

    public class UpdateHoursBudgetRequest
    {
        public string BudgetName { get; set; } = string.Empty;
        public decimal AllocatedHours { get; set; }
    }
}
