using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PetelApp.Api.Data;
using PetelApp.Api.Models;
using PetelApp.Api.DTOs;
using PetelApp.Api.Models.DTOs;
using PetelApp.Api.Session; // Add this using for UserSession
using BCrypt.Net;

namespace PetelApp.Api.Services
{
    /// <summary>
    /// Authentication service implementation following Authentication & Session Management pattern
    /// </summary>


    public class AuthService : IAuthService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<AuthService> _logger;
        private readonly UserSessionService _userSessionService; // Add UserSessionService

        public AuthService(AppDbContext context, ILogger<AuthService> logger, UserSessionService userSessionService)
        {
            _context = context;
            _logger = logger;
            _userSessionService = userSessionService; // Initialize UserSessionService
        }

        /// <summary>
        /// Authenticate user and create session following Authentication & Session Management
        /// </summary>
        public async Task<UserSession?> AuthenticateAsync(string username, string password, string? entityId = null)
        {
            try
            {
                // Query your User entity from database
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Username == username && u.IsActive);

                if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
                {
                    return null;
                }

                // Get user's entity (school/organization)
                var entity = await _context.Entities
                    .FirstOrDefaultAsync(e => e.Id.ToString() == (entityId ?? user.EntityId.ToString()));

                if (entity == null)
                {
                    return null;
                }

                // Get user roles
                var roles = await GetUserRolesAsync(user.Id.ToString(), entity.Id.ToString());

                // Create session following Entity-Based Request Flow
                var session = new UserSession
                {
                    SessionId = Guid.NewGuid().ToString(),
                    UserId = user.Id.ToString(),
                    UserFullName = user.FullName,
                    EntityId = entity.Id.ToString(),
                    EntityName = entity.Name,
                    EntityTypeId = entity.EntityTypeId.ToString(),
                    Roles = roles,
                    CreatedAt = DateTime.UtcNow,
                    LastAccessedAt = DateTime.UtcNow
                };

                _logger.LogInformation("User {UserId} authenticated successfully for entity {EntityId}",
                   user.Id.ToString(), entity.Id.ToString());

                return session;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Authentication error for username {Username}", username);
                return null;
            }
        }

        /// <summary>
        /// Get user details by user ID
        /// </summary>
        public async Task<UserDto?> GetUserAsync(string userId)
        {
            try
            {
                var user = await _context.Users
                    .Include(u => u.Entity)
                    .FirstOrDefaultAsync(u => u.Id.ToString() == userId);

                if (user == null) return null;

                return new UserDto
                {
                    UserId = user.Id.ToString(),
                    UserFullName = user.FullName,
                    Username = user.Username,
                    Email = user.Email,
                    EntityId = user.EntityId.ToString(),
                    EntityName = user.Entity?.Name ?? string.Empty,
                    IsActive = user.IsActive,
                    CreatedAt = user.CreatedAt,
                    LastLoginAt = user.LastLogin ?? DateTime.Now,
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user {UserId}", userId);
                return null;
            }
        }

        /// <summary>
        /// Validate user credentials
        /// </summary>
        public async Task<bool> ValidateUserCredentialsAsync(string username, string password)
        {
            try
            {
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Username == username && u.IsActive);

                return user != null && BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating credentials for username {Username}", username);
                return false;
            }
        }

        /// <summary>
        /// Get user roles for authorization
        /// </summary>
        public async Task<List<int>> GetUserRolesAsync(string userId, string entityId)
        {
            try
            {
                var roles = await _context.UserRoles
                    .Where(ur => ur.UserId.ToString() == userId && ur.IsActive)
                    .Select(ur => ur.RoleId)
                    .ToListAsync();

                return roles;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving roles for user {UserId} in entity {EntityId}", userId, entityId);
                return new List<int>();
            }
        }

        public async Task<bool> VerifyPasswordAsync(User user, string password)
        {
            if (user == null || string.IsNullOrEmpty(password))
                return false;

            return await Task.Run(() => BCrypt.Net.BCrypt.Verify(password, user.PasswordHash));
        }

        public async Task<string> HashPasswordAsync(string password)
        {
            return await Task.Run(() => BCrypt.Net.BCrypt.HashPassword(password, 12));
        }

        public async Task<User?> ValidateUserAsync(string username, string password, int entityId)
        {
            var user = await _context.Users
                .Include(u => u.Entity)
                .ThenInclude(e => e.EntityType)
                .FirstOrDefaultAsync(u => u.Username == username && u.EntityId == entityId);

            if (user == null || !await VerifyPasswordAsync(user, password))
                return null;

            return user;
        }

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
                // Check if the user exists and is active
                var user = await _context.Users
                    .Include(u => u.Entity)
                    .ThenInclude(e => e!.EntityType)
                    .FirstOrDefaultAsync(u => u.Username == loginRequest.Username && u.IsActive);

                if (user == null)
                {
                    return new LoginResponseDto
                    {
                        Success = false,
                        Message = "שם משתמש או סיסמה שגויים"
                    };
                }

                // Verify the password
                if (!BCrypt.Net.BCrypt.Verify(loginRequest.Password, user.PasswordHash))
                {
                    return new LoginResponseDto
                    {
                        Success = false,
                        Message = "שם משתמש או סיסמה שגויים"
                    };
                }

                // Validate entity ID matches (Entity-Based Request Flow)
                if (user.EntityId != loginRequest.EntityId)
                {
                    _logger.LogWarning("Login failed: Entity mismatch for user {Username}", loginRequest.Username);
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
                    _logger.LogWarning("Login failed: Entity {EntityId} not found for user {Username}",
                        user.EntityId, loginRequest.Username);
                    return new LoginResponseDto
                    {
                        Success = false,
                        Message = "ישות לא נמצאה"
                    };
                }

                // Update user's last login time
                user.LastLogin = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                // Create UserFullName from FirstName + LastName
                var userFullName = $"{user.FirstName} {user.LastName}".Trim();

                // Create session using existing UserSessionService following Entity-Based Request Flow
                var sessionId = _userSessionService.CreateSessionWithFullData(
                    userId: user.Id.ToString(),
                    username: user.Username,
                    userFullName: userFullName,
                    entityId: user.EntityId.ToString(),
                    entityName: userEntity.Name,
                    entityTypeId: userEntity.EntityTypeId.ToString(),
                    entityTypeName: userEntity.EntityType?.Name ?? "",
                    lastLogin: user.LastLogin
                );

                // Get created session for response
                var session = _userSessionService.GetUserSession(sessionId);

                _logger.LogInformation("User {Username} logged in successfully", loginRequest.Username);

                return new LoginResponseDto
                {
                    Success = true,
                    Message = "התחברות הצליחה",
                    Token = sessionId // Frontend Session Token Only pattern
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
    }
}