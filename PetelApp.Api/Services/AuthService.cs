using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PetelApp.Api.Data;
using PetelApp.Api.DTOs;
using PetelApp.Api.Models.DTOs;
using PetelApp.Api.Session;
using BCrypt.Net;

namespace PetelApp.Api.Services
{
    /// <summary>
    /// Authentication service implementation following Authentication & Session Management pattern
    /// </summary>
    public class AuthService : IAuthService
    {
        private readonly AppDbContext _context;
        private readonly UserSessionService _sessionService;
        private readonly ActionAuthorizationService _actionAuthService;
        private readonly ILogger<AuthService> _logger;

        public AuthService(
            AppDbContext context,
            UserSessionService sessionService,
            ActionAuthorizationService actionAuthService,
            ILogger<AuthService> logger)
        {
            _context = context;
            _sessionService = sessionService;
            _actionAuthService = actionAuthService;
            _logger = logger;
        }

        /// <summary>
        /// Login user and create session following Authentication & Session Management
        /// Implements Frontend Token-Only Storage pattern
        /// </summary>
        public async Task<LoginResponseDto> LoginAsync(LoginRequestDto loginRequest)
        {
            // Validate request
            if (string.IsNullOrEmpty(loginRequest.Username) || string.IsNullOrEmpty(loginRequest.Password))
            {
                return new LoginResponseDto
                {
                    Success = false,
                    Message = "שם משתמש או סיסמה חסרים"
                };
            }

            try
            {
                // Get user with entity relationship (Entity-Based Request Flow)
                var user = await _context.Users
                    .Include(u => u.Entity)
                        .ThenInclude(e => e!.EntityType)
                    .FirstOrDefaultAsync(u => u.Username == loginRequest.Username && u.IsActive);

                if (user == null)
                {
                    _logger.LogWarning("Login failed: User {Username} not found or inactive", loginRequest.Username);
                    return new LoginResponseDto
                    {
                        Success = false,
                        Message = "שם משתמש או סיסמה שגויים"
                    };
                }

                // Verify password using BCrypt (Security Patterns)
                if (!BCrypt.Net.BCrypt.Verify(loginRequest.Password, user.PasswordHash))
                {
                    _logger.LogWarning("Login failed: Invalid password for user {Username}", loginRequest.Username);
                    return new LoginResponseDto
                    {
                        Success = false,
                        Message = "שם משתמש או סיסמה שגויים"
                    };
                }

                // Validate entity ID matches (Entity-Based Request Flow)
                if (user.EntityId != loginRequest.EntityId)
                {
                    _logger.LogWarning("Login failed: Entity mismatch for user {Username}. Expected {EntityId}, got {RequestEntityId}",
                        loginRequest.Username, user.EntityId, loginRequest.EntityId);
                    return new LoginResponseDto
                    {
                        Success = false,
                        Message = "המשתמש אינו שייך לישות שנבחרה"
                    };
                }

                // Get entity details
                var userEntity = user.Entity;
                if (userEntity == null)
                {
                    _logger.LogError("Login failed: Entity {EntityId} not found for user {Username}",
                        user.EntityId, loginRequest.Username);
                    return new LoginResponseDto
                    {
                        Success = false,
                        Message = "ישות לא נמצאה"
                    };
                }

                // Update last login timestamp
                user.LastLogin = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                // Create UserFullName from FirstName + LastName
                var userFullName = $"{user.FirstName} {user.LastName}".Trim();

                 // Create session following Entity-Based Request Flow
                var sessionId = _sessionService.CreateSessionWithFullData(
                    userId: user.Id.ToString(),
                    username: user.Username,
                    userFullName: userFullName,
                    entityId: user.EntityId.ToString(),
                    entityName: userEntity.Name,
                    entityTypeId: userEntity.EntityTypeId.ToString(),
                    entityTypeName: userEntity.EntityType?.Name ?? "",
                    lastLogin: user.LastLogin
                );

                // Load user roles into session

                  _logger.LogInformation("Getting roles for user {UserId}", user.Id);

// ✅ DIAGNOSTIC VERSION - Load full UserRole objects to see what's happening
var userRoles = await _context.UserRoles
    .AsNoTracking()
    .Where(ur => ur.UserId == user.Id && ur.IsActive)
    .ToListAsync();

_logger.LogInformation("Found {Count} user_roles records", userRoles.Count);

// Log each role record
foreach (var ur in userRoles)
{
    _logger.LogInformation("UserRole: Id={Id}, UserId={UserId}, RoleId={RoleId}, IsActive={IsActive}", 
        ur.Id, ur.UserId, ur.RoleId, ur.IsActive);
}

// Extract role IDs
var userRoleIds = userRoles.Select(ur => ur.RoleId).ToArray().ToList();

_logger.LogInformation("Extracted {Count} role IDs: {RoleIds}", 
    userRoleIds.Count, 
    string.Join(", ", userRoleIds));

                    

                var session = _sessionService.GetUserSession(sessionId);
                if (session != null)
                {
                    session.Roles = userRoleIds;
                    _logger.LogInformation("Loaded {RoleCount} roles for user {UserId}", userRoleIds.Count, user.Id);
                    // Load user actions into session
                    var userActions = await _actionAuthService.GetUserActionsAsync(user.Id);
                    session.SetProperty("UserActions", System.Text.Json.JsonSerializer.Serialize(userActions));
                    _logger.LogInformation("Loaded {ActionCount} actions for user {UserId}", userActions.Count, user.Id);
                }

                _logger.LogInformation("User {Username} (ID: {UserId}) logged in successfully to entity {EntityId}",
                    loginRequest.Username, user.Id, user.EntityId);

                // After password verification:
                if (user.OtpEnabled && user.OtpVerified)
                {
                    // Return requires OTP
                    return new LoginResponseDto
                    {
                        Success = false,
                        RequiresOtp = true,
                        TempToken = GenerateTempToken(user.Id),
                        Message = "נדרש קוד אימות דו-שלבי"
                    };
                }

                // Return token only (Frontend Token-Only Storage pattern)
                return new LoginResponseDto
                {
                    Success = true,
                    Message = "התחברות הצליחה",
                    Token = sessionId, // Frontend stores ONLY this token
                    User = new UserDto
                    {
                        Id = user.Id,
                        Username = user.Username,
                        FirstName = user.FirstName,
                        LastName = user.LastName,
                        FullName = userFullName,
                        Email = user.Email,
                        Phone = user.Phone,
                        LastLogin = user.LastLogin
                    },
                    Entity = new EntityDto
                    {
                        Id = userEntity.Id,
                        Name = userEntity.Name,
                        EntityTypeId = userEntity.EntityTypeId,
                        EntityTypeName = userEntity.EntityType?.Name ?? ""
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login for username {Username}", loginRequest.Username);
                return new LoginResponseDto
                {
                    Success = false,
                    Message = "שגיאה במהלך ההתחברות"
                };
            }
        }

        /// <summary>
        /// Verify user password following Security Patterns
        /// </summary>
        public async Task<bool> VerifyPasswordAsync(User user, string password)
        {
            if (user == null || string.IsNullOrEmpty(password))
                return false;

            return await Task.Run(() => BCrypt.Net.BCrypt.Verify(password, user.PasswordHash));
        }

        /// <summary>
        /// Hash password for storage following Security Patterns
        /// </summary>
        public async Task<string> HashPasswordAsync(string password)
        {
            return await Task.Run(() => BCrypt.Net.BCrypt.HashPassword(password, 12));
        }

        /// <summary>
        /// Validate user credentials following Authentication & Session Management
        /// </summary>
        public async Task<User?> ValidateUserAsync(string username, string password, int entityId)
        {
            var user = await _context.Users
                .Include(u => u.Entity)
                    .ThenInclude(e => e.EntityType)
                .FirstOrDefaultAsync(u => u.Username == username && u.EntityId == entityId && u.IsActive);

            if (user == null || !await VerifyPasswordAsync(user, password))
                return null;

            return user;
        }

                /// <summary>
        /// Generate temporary token for OTP verification
        /// </summary>
        private string GenerateTempToken(int userId)
        {
            // Simple temporary token: userId + timestamp + random guid
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var guid = Guid.NewGuid().ToString("N");
            return $"{userId}_{timestamp}_{guid}";
        }
    }
}