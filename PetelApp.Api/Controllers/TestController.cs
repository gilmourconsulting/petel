// PetelApp.Api/Controllers/TestController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetelApp.Api.Data;
using PetelApp.Api.Services;
using System.Security.Cryptography;
using System.Text;

namespace PetelApp.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TestController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly TenantService _tenantService;
        private readonly ILogger<TestController> _logger;

        public TestController(AppDbContext context, TenantService tenantService, ILogger<TestController> logger)
        {
            _context = context;
            _tenantService = tenantService;
            _logger = logger;
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

        [HttpGet("system-version")]
        public async Task<IActionResult> GetSystemVersion()
        {
            try
            {
                _logger.LogInformation("Getting system version from database");
                
                var versionAttribute = await _context.SystemAttributes
                    .Where(s => s.Id == 1)
                    .FirstOrDefaultAsync();

                if (versionAttribute == null)
                {
                    _logger.LogWarning("No system attribute found with id = 1");
                    return Ok(new { 
                        version = "1.0",
                        timestamp = DateTime.UtcNow,
                        source = "default",
                        message = "No version record found in database"
                    });
                }

                var version = versionAttribute.Value ?? "1.0";
                _logger.LogInformation("System version retrieved: {Version}", version);

                return Ok(new { 
                    version = version,
                    timestamp = DateTime.UtcNow,
                    source = "database",
                    description = versionAttribute.Description,
                    valueType = versionAttribute.ValueType
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting system version");
                return StatusCode(500, new { 
                    version = "1.0",
                    timestamp = DateTime.UtcNow,
                    source = "fallback",
                    error = ex.Message
                });
            }
        }

        [HttpGet("debug-system-version")]
        public async Task<IActionResult> DebugSystemVersion()
        {
            try
            {
                _logger.LogInformation("Debug system version query");
                
                // Check if SystemAttributes table exists and has data
                var allAttributes = await _context.SystemAttributes.ToListAsync();
                
                var systemAttribute = await _context.SystemAttributes
                    .Where(s => s.Id == 1)
                    .FirstOrDefaultAsync();

                return Ok(new { 
                    found = systemAttribute != null,
                    systemAttribute = systemAttribute != null ? new {
                        systemAttribute.Id,
                        systemAttribute.Description,
                        systemAttribute.Value,
                        systemAttribute.ValueType,
                        systemAttribute.CreatedAt,
                        systemAttribute.UpdatedAt
                    } : null,
                    allAttributesCount = allAttributes.Count,
                    allAttributes = allAttributes.Select(a => new {
                        a.Id,
                        a.Description,
                        a.Value,
                        a.ValueType,
                        a.CreatedAt,
                        a.UpdatedAt
                    }),
                    message = systemAttribute != null ? "System attribute found" : "No system attribute found with id = 1"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in debug system version");
                return StatusCode(500, new { 
                    error = ex.Message, 
                    stackTrace = ex.StackTrace 
                });
            }
        }

        [HttpPost("test-tenant-context")]
        public IActionResult TestTenantContext([FromBody] object data)
        {
            try
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in test tenant context");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("test-password")]
        public IActionResult TestPassword([FromBody] TestPasswordRequest request)
        {
            try
            {
                var computedHash = HashPassword(request.Password);
                var isMatch = request.StoredHash == computedHash;
                
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
                    bcryptMatch = isBCryptMatch,
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
            try
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking user {Username} in entity {EntityId}", username, entityId);
                return StatusCode(500, new { error = ex.Message });
            }
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
            using (var sha256 = SHA256.Create())
            {
                var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
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