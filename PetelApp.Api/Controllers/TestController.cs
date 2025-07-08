// PetelApp.Api/Controllers/TestController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetelApp.Api.Data;
using PetelApp.Api.Services;

namespace PetelApp.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TestController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ITenantService _tenantService;

        public TestController(AppDbContext context, ITenantService tenantService)
        {
            _context = context;
            _tenantService = tenantService;
        }

        [HttpGet]
        public IActionResult Get()
        {
            return Ok(new { 
                message = "API is working!", 
                timestamp = DateTime.Now 
            });
        }

        [HttpGet("database")]
        public async Task<IActionResult> TestDatabase()
        {
            try
            {
                // Try to count entities
                var entityCount = await _context.Entities.CountAsync();
                return Ok(new { 
                    message = "Database connected!", 
                    entityCount = entityCount,
                    timestamp = DateTime.Now 
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { 
                    message = "Database error", 
                    error = ex.Message 
                });
            }
        }

        [HttpPost("test-tenant-context")]
        public IActionResult TestTenantContext([FromBody] object data)
        {
            var contextTenantId = HttpContext.Items["TenantId"]?.ToString();
            var tenantServiceId = _tenantService.GetCurrentTenantId();
            var headers = Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString());
            
            return Ok(new {
                contextTenantId = contextTenantId,
                tenantServiceId = tenantServiceId,
                xTenantIdHeader = Request.Headers.ContainsKey("X-Tenant-ID") ? Request.Headers["X-Tenant-ID"].ToString() : "Not found",
                allHeaders = headers
            });
        }

        [HttpPost("test-password")]
        public IActionResult TestPassword([FromBody] TestPasswordRequest request)
        {
            try
            {
                // Test the current hash method
                var computedHash = HashPassword(request.Password);
                var isMatch = request.StoredHash == computedHash;
                
                // Also test if it's BCrypt
                bool isBCryptMatch = false;
                try
                {
                //    isBCryptMatch = BCrypt.Net.BCrypt.Verify(request.Password, request.StoredHash);
                }
                catch { }
                
                return Ok(new {
                    password = request.Password,
                    storedHash = request.StoredHash,
                    computedSHA256Hash = computedHash,
                    sha256Match = isMatch,
               //     bcryptMatch = isBCryptMatch,
                    hashMethod = isMatch ? "SHA256" : (isBCryptMatch ? "BCrypt" : "Unknown")
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("check-user/{username}/{entityId}")]
        public async Task<IActionResult> CheckUser(string username, int entityId)
        {
            var user = await _context.Users
                .Include(u => u.Entity)
                .Where(u => u.Username == username && u.EntityId == entityId)
                .Select(u => new {
                    u.Id,
                    u.Username,
                    u.EntityId,
                    EntityName = u.Entity.Name,
                    u.IsActive,
                    HasPassword = !string.IsNullOrEmpty(u.PasswordHash),
                    PasswordHashLength = u.PasswordHash.Length
                })
                .FirstOrDefaultAsync();
                
            return Ok(user);
        }

        [HttpGet("entities-mock")]
        public IActionResult GetMockEntities()
        {
            var entities = new[]
            {
                new { id = 1, name = "חברת אלפא בע\"מ" },
                new { id = 2, name = "חברת בטא בע\"מ" },
                new { id = 3, name = "חברת גמא בע\"מ" }
            };
            return Ok(entities);
        }

        private string HashPassword(string password)
        {
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                var hashedBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
                return Convert.ToBase64String(hashedBytes);
            }
        }
    }

    public class TestPasswordRequest
    {
        public string Password { get; set; } = string.Empty;
        public string StoredHash { get; set; } = string.Empty;
    }
}