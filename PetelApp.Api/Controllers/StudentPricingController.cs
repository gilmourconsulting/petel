using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetelApp.Api.Data;
using PetelApp.Api.Session;

namespace PetelApp.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StudentPricingController : BaseController
    {
        private readonly AppDbContext _context;

        public StudentPricingController(
            AppDbContext context,
            UserSessionService userSessionService,
            ILogger<StudentPricingController> logger)
            : base(userSessionService, logger)
        {
            _context = context;
        }

        [HttpGet("{studentId}")]
        public async Task<IActionResult> GetStudentPricingElements(int studentId)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "Session not found" });
                }

                _logger.LogInformation("📊 Fetching pricing elements for student: {StudentId}", studentId);

                var pricingElements = await _context.SchoolStudentPricingElements
                    .Where(pe => pe.StudentId == studentId)
                    .Join(
                        _context.SpecialNeedsPricingElements,
                        spe => spe.PricingElementId,
                        snpe => snpe.Id,
                        (spe, snpe) => new
                        {
                            spe.Id,
                            spe.StudentId,
                            spe.PricingElementId,
                            PricingElementName = snpe.ElementName,
                            spe.Price
                        })
                    .OrderBy(pe => pe.PricingElementName)
                    .ToListAsync();

                _logger.LogInformation("✅ Found {Count} pricing elements for student {StudentId}", 
                    pricingElements.Count, studentId);

                return Ok(new
                {
                    success = true,
                    data = pricingElements
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error fetching pricing elements for student {StudentId}", studentId);
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בטעינת נתוני תמחור"
                });
            }
        }
    }
}