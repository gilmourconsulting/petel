using Microsoft.AspNetCore.Mvc;

namespace PetelAssistants.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OtpController : ControllerBase
    {
        [HttpPost("send")]
        public IActionResult Send([FromBody] object request)
        {
            return Ok(new
            {
                success = false,
                message = "שירות OTP עדיין לא הוגדר"
            });
        }

        [HttpPost("validate")]
        public IActionResult Validate([FromBody] object request)
        {
            return Ok(new
            {
                success = false,
                message = "שירות אימות OTP עדיין לא הוגדר"
            });
        }
    }
}
