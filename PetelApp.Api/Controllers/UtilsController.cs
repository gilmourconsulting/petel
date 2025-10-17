using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;

namespace PetelApp.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UtilsController : ControllerBase
    {
        /// <summary>
        /// Generates a BCrypt hash for the provided input text.
        /// </summary>
        /// <param name="input">The text to hash.</param>
        /// <returns>The BCrypt hash value.</returns>
        [HttpPost("generate-hash")]
        public ActionResult<string> GenerateHash([FromBody] HashInputDto input)
        {
            if (string.IsNullOrWhiteSpace(input?.Text))
                return BadRequest("Input text is required.");

            // Generate BCrypt hash
            var hash = BCrypt.Net.BCrypt.HashPassword(input.Text);
            return Ok(new { hash });
        }
    }

    public class HashInputDto
    {
        public string Text { get; set; } = string.Empty;
    }
}