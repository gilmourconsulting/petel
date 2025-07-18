using Microsoft.AspNetCore.Mvc;
using PetelApp.Api.Models;
using PetelApp.Api.Session;

namespace PetelApp.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HoursBudgetController : BaseController
    {
        private readonly UserSessionService _userSessionService;
        private readonly ILogger<HoursBudgetController> _logger;

        public HoursBudgetController(
            UserSessionService userSessionService,
            ILogger<HoursBudgetController> logger)
        {
            _userSessionService = userSessionService;
            _logger = logger;
        }

        [HttpGet("schoolyear/{schoolYearId}")]
        public IActionResult GetHoursBudgetsBySchoolYear(int schoolYearId)
        {
            try
            {
                var userSession = _userSessionService.GetUserSession();
                if (userSession == null)
                {
                    return Unauthorized("User session not found");
                }

                var tenantId = GetTenantId();
                if (string.IsNullOrEmpty(tenantId))
                {
                    return Unauthorized("Invalid tenant context");
                }

                _logger.LogInformation("Fetching hours budgets for school year {SchoolYearId}, tenant {TenantId}", 
                    schoolYearId, tenantId);

                // For now, return mock data - replace with actual database query
                var mockHoursBudgets = GenerateMockHoursBudgets(schoolYearId, int.Parse(tenantId));

                // Store hours budgets in user session for later use
                StoreHoursBudgetsInSession(mockHoursBudgets);

                return Ok(mockHoursBudgets);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching hours budgets for school year {SchoolYearId}", schoolYearId);
                return StatusCode(500, "Error fetching hours budget data");
            }
        }

        [HttpGet("session")]
        public IActionResult GetSessionHoursBudgets()
        {
            try
            {
                var userSession = _userSessionService.GetUserSession();
                if (userSession == null)
                {
                    return Unauthorized("User session not found");
                }

                // Retrieve hours budgets from session (you'll need to extend UserSession class)
                // For now, return empty array
                return Ok(new List<HoursBudget>());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving session hours budgets");
                return StatusCode(500, "Error retrieving session hours budget data");
            }
        }

        private List<HoursBudget> GenerateMockHoursBudgets(int schoolYearId, int tenantId)
        {
            return new List<HoursBudget>
            {
                new HoursBudget
                {
                    Id = 1,
                    Name = "תקציב שעות הוראה",
                    Description = "תקציב שעות הוראה רגילות",
                    SchoolYearId = schoolYearId,
                    TenantId = tenantId,
                    HoursBudgetType = "teaching_hours",
                    AllocatedHours = 1200,
                    UsedHours = 300,
                    RemainingHours = 900,
                    CreatedAt = DateTime.UtcNow.AddDays(-30),
                    UpdatedAt = DateTime.UtcNow,
                    IsActive = true
                },
                new HoursBudget
                {
                    Id = 2,
                    Name = "תקציב שעות פעילויות",
                    Description = "תקציב שעות פעילויות חינוכיות",
                    SchoolYearId = schoolYearId,
                    TenantId = tenantId,
                    HoursBudgetType = "activities",
                    AllocatedHours = 600,
                    UsedHours = 150,
                    RemainingHours = 450,
                    CreatedAt = DateTime.UtcNow.AddDays(-30),
                    UpdatedAt = DateTime.UtcNow,
                    IsActive = true
                },
                new HoursBudget
                {
                    Id = 3,
                    Name = "תקציב שעות תמיכה",
                    Description = "תקציב שעות תמיכה לתלמידים",
                    SchoolYearId = schoolYearId,
                    TenantId = tenantId,
                    HoursBudgetType = "support_hours",
                    AllocatedHours = 400,
                    UsedHours = 100,
                    RemainingHours = 300,
                    CreatedAt = DateTime.UtcNow.AddDays(-30),
                    UpdatedAt = DateTime.UtcNow,
                    IsActive = true
                }
            };
        }

        private void StoreHoursBudgetsInSession(List<HoursBudget> hoursBudgets)
        {
            try
            {
                var userSession = _userSessionService.GetUserSession();
                if (userSession != null)
                {
                    // Update session with hours budget summary
                    userSession.CurrentSchoolYearId = hoursBudgets.FirstOrDefault()?.SchoolYearId;
                    userSession.HoursBudgetsLastLoaded = DateTime.UtcNow;
                    userSession.TotalAllocatedHoursBudget = hoursBudgets.Sum(b => b.AllocatedHours);
                    userSession.TotalUsedHoursBudget = hoursBudgets.Sum(b => b.UsedHours);
                    userSession.TotalRemainingHoursBudget = hoursBudgets.Sum(b => b.RemainingHours);
                    userSession.HoursBudgetCount = hoursBudgets.Count;

                    // Save updated session
                    _userSessionService.SetUserSession(userSession);

                    _logger.LogInformation("Stored {HoursBudgetCount} hours budgets in session for school year {SchoolYearId} with total allocated: {TotalAllocated}", 
                        hoursBudgets.Count, userSession.CurrentSchoolYearId, userSession.TotalAllocatedHoursBudget);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error storing hours budgets in session");
            }
        }
    }
}
