using Microsoft.AspNetCore.Mvc;
using Petel.Core.Controllers;
using Petel.Core.Session;
using PetelAssistants.Api.Services;

namespace PetelAssistants.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PersonalApprovalsPdfController : BaseController
    {
        private const long MaxFileBytes = 20 * 1024 * 1024;
        private readonly PersonalApprovalsPdfParser _parser;

        public PersonalApprovalsPdfController(
            PersonalApprovalsPdfParser parser,
            UserSessionService sessionService,
            ILogger<PersonalApprovalsPdfController> logger)
            : base(sessionService, logger)
        {
            _parser = parser;
        }

        [HttpPost("convert")]
        [RequestSizeLimit(MaxFileBytes)]
        public async Task<IActionResult> Convert(IFormFile? file)
        {
            var session = GetCurrentSession();
            if (session == null)
                return Unauthorized(new { success = false, message = "נדרש אימות" });

            if (file == null || file.Length == 0)
                return BadRequest(new { success = false, message = "יש לבחור קובץ PDF" });

            var ext = Path.GetExtension(file.FileName)?.ToLowerInvariant();
            if (ext != ".pdf")
                return BadRequest(new { success = false, message = "יש להעלות קובץ PDF בלבד" });

            if (file.Length > MaxFileBytes)
                return BadRequest(new { success = false, message = "גודל הקובץ חורג מהמותר (20MB)" });

            await using var stream = file.OpenReadStream();
            var result = _parser.ConvertToExcel(stream, file.FileName);

            if (!result.Success)
                return BadRequest(new
                {
                    success = false,
                    message = result.Message,
                    errorCount = result.ErrorCount,
                    errors = result.Errors
                });

            return Ok(new
            {
                success = true,
                message = result.Message,
                fileName = result.FileName,
                contentBase64 = result.ContentBase64,
                rowCount = result.RowCount,
                errorCount = result.ErrorCount,
                errors = result.Errors
            });
        }
    }
}
