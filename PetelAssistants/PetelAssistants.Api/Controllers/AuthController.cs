using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Petel.Core.Session;
using PetelAssistants.Api.Data;

namespace PetelAssistants.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AssistDbContext _context;
        private readonly SharedDbContext _sharedContext;
        private readonly UserSessionService _sessionService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(
            AssistDbContext context,
            SharedDbContext sharedContext,
            UserSessionService sessionService,
            ILogger<AuthController> logger)
        {
            _context = context;
            _sharedContext = sharedContext;
            _sessionService = sessionService;
            _logger = logger;
        }

        [HttpGet("password-policy")]
        public IActionResult GetPasswordPolicy()
        {
            return Ok(new
            {
                requirements = new[]
                {
                    "בין 6 ל-20 תווים",
                    "לפחות אות קטנה אחת (a-z)",
                    "לפחות אות גדולה אחת (A-Z)",
                    "לפחות ספרה אחת (0-9)",
                    "לפחות תו מיוחד אחד (@$!%*?&)"
                }
            });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            {
                return Ok(new LoginResponseDto
                {
                    Success = false,
                    Message = "שם משתמש או סיסמה חסרים"
                });
            }

            try
            {
                // IgnoreQueryFilters is required here: no session token exists yet during login,
                // so ITenantContext.EntityId == 0 and the global filter would match nothing.
                // The Where clause below enforces the same tenant scoping explicitly.
                var user = await _context.Users
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(u =>
                        u.Username == request.Username &&
                        u.EntityId == request.EntityId &&
                        u.IsActive);

                if (user == null)
                {
                    _logger.LogWarning("Login failed for username {Username} in entity {EntityId}: user not found", request.Username, request.EntityId);
                    return Ok(new LoginResponseDto
                    {
                        Success = false,
                        Message = "שם משתמש או סיסמה שגויים"
                    });
                }

                if (user.IsLocked)
                {
                    return Ok(new LoginResponseDto
                    {
                        Success = false,
                        Message = "חשבון המשתמש נעול. אנא פנה למנהל המערכת"
                    });
                }

                if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
                {
                    _logger.LogWarning("Login failed for username {Username}: invalid password", request.Username);
                    return Ok(new LoginResponseDto
                    {
                        Success = false,
                        Message = "שם משתמש או סיסמה שגויים"
                    });
                }

                user.LastLogin = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                // Fetch entity name from shared_schema (separate context — no navigation property).
                var entity = await _sharedContext.Entities.FindAsync(user.EntityId);

                var fullName = $"{user.FirstName} {user.LastName}".Trim();
                if (string.IsNullOrWhiteSpace(fullName))
                    fullName = user.Username;

                var token = _sessionService.CreateSessionWithFullData(
                    userId: user.Id.ToString(),
                    username: user.Username,
                    userFullName: fullName,
                    entityId: user.EntityId.ToString(),
                    entityName: entity?.Name ?? string.Empty,
                    entityTypeId: entity?.EntityTypeId?.ToString() ?? string.Empty,
                    entityTypeName: string.Empty,
                    lastLogin: user.LastLogin);

                _logger.LogInformation("Login successful for user {UserId} ({Username}) in entity {EntityId}", user.Id, user.Username, user.EntityId);

                return Ok(new LoginResponseDto
                {
                    Success = true,
                    Message = "התחברות בוצעה בהצלחה",
                    Token = token
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Login error for username {Username}", request.Username);
                return StatusCode(500, new LoginResponseDto
                {
                    Success = false,
                    Message = "אירעה שגיאה בעת ההתחברות"
                });
            }
        }

        [HttpPost("forgot-password")]
        public IActionResult ForgotPassword([FromBody] object request)
        {
            return Ok(new
            {
                success = false,
                message = "שירות שכחתי סיסמה עדיין לא הוגדר"
            });
        }

        [HttpPost("reset-password")]
        public IActionResult ResetPassword([FromBody] object request)
        {
            return Ok(new
            {
                success = false,
                message = "שירות איפוס סיסמה עדיין לא הוגדר"
            });
        }

        [HttpPost("change-expired-password")]
        public IActionResult ChangeExpiredPassword([FromBody] object request)
        {
            return Ok(new
            {
                success = false,
                message = "שירות שינוי סיסמה עדיין לא הוגדר"
            });
        }

        public class LoginRequestDto
        {
            public string Username { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
            public int EntityId { get; set; }
        }

        public class LoginResponseDto
        {
            public bool Success { get; set; }
            public string Message { get; set; } = string.Empty;
            public string Token { get; set; } = string.Empty;
            public bool RequiresOtp { get; set; }
            public string? TempToken { get; set; }
            public string? MaskedEmail { get; set; }
            public bool RequiresPasswordChange { get; set; }
            public string? PasswordExpirationMessage { get; set; }
        }
    }
}
