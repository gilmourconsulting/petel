using Microsoft.AspNetCore.Mvc;
using PetelApp.Api.Services;
using PetelApp.Api.Session;

namespace PetelApp.Api.Controllers
{
    /// <summary>
    /// Controller for system attributes management following multi-tenant patterns
    /// Inherits from BaseController for tenant isolation
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class SystemAttributesController : BaseController
    {
        private readonly SystemAttributeService _systemAttributeService;

        public SystemAttributesController(
            SystemAttributeService systemAttributeService,
            UserSessionService userSessionService) : base(userSessionService)
        {
            _systemAttributeService = systemAttributeService;
        }

        [HttpGet]
        public async Task<IActionResult> GetSystemAttributes()
        {
            try
            {
                var attributes = await _systemAttributeService.GetAllAttributesAsync();
                return Ok(attributes);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "שגיאה בטעינת מאפייני המערכת", error = ex.Message });
            }
        }

        [HttpGet("{name}")]
        public async Task<IActionResult> GetSystemAttribute(string name)
        {
            try
            {
                var attribute = await _systemAttributeService.GetAttributeAsync(name);
                if (attribute == null)
                {
                    return NotFound(new { message = $"מאפיין '{name}' לא נמצא" });
                }
                return Ok(attribute);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "שגיאה בטעינת מאפיין המערכת", error = ex.Message });
            }
        }

        [HttpPost("selectYear")]
        public async Task<IActionResult> SelectYear([FromBody] SelectYearRequest request)
        {
            try
            {
                var session = UserSessionService.GetUserSession();
                if (session == null)
                {
                    return Unauthorized(new { message = "לא נמצא מידע על המשתמש בסשן" });
                }

                // Set selected year in session following Authentication & Session Management
                session.SelectedYear = request.YearId;
                session.SelectedYearType = request.YearType;
                
                UserSessionService.SetUserSession(session);

                return Ok(new
                {
                    success = true,
                    message = "שנת הלימודים נבחרה בהצלחה",
                    selectedYear = request.YearId,
                    yearType = request.YearType
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "שגיאה בבחירת שנת הלימודים", error = ex.Message });
            }
        }

        [HttpGet("session/info")]
        public IActionResult GetSessionInfo()
        {
            try
            {
                var session = UserSessionService.GetUserSession();
                if (session == null)
                {
                    return Unauthorized(new { message = "לא נמצא מידע על המשתמש בסשן" });
                }

                return Ok(new
                {
                    systemAttributes = session.SystemAttributes,
                    systemAttributesLastLoaded = session.SystemAttributesLastLoaded,
                    selectedYear = session.SelectedYear,
                    selectedYearType = session.SelectedYearType,
                    selectedYearValue = session.SelectedYearValue,
                    tenantId = GetTenantId()
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "שגיאה בטעינת מידע הסשן", error = ex.Message });
            }
        }
    }

    public class SelectYearRequest
    {
        public string YearType { get; set; } = string.Empty;
        public int YearId { get; set; }
    }
}