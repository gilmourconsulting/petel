using Microsoft.EntityFrameworkCore;

using PetelApp.Api.Data;
using PetelApp.Api.DTOs;

using PetelApp.Api.Session;

using Microsoft.Extensions.Options;
using PetelApp.Api.Configuration;


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
        private readonly SecuritySettings _securitySettings;

        private readonly JwtTokenService _jwtTokenService;

        private readonly SystemAttributeCache _systemAttributeCache;

        public AuthService(
            AppDbContext context,
            UserSessionService sessionService,
            ActionAuthorizationService actionAuthService,
            ILogger<AuthService> logger,
            IOptions<SecuritySettings> securitySettings,
            JwtTokenService jwtTokenService,
    SystemAttributeCache systemAttributeCache)
        {
            _context = context;
            _sessionService = sessionService;
            _actionAuthService = actionAuthService;
            _logger = logger;
            _securitySettings = securitySettings.Value;
            _jwtTokenService = jwtTokenService;
            _systemAttributeCache = systemAttributeCache;
        }

        private int GetMaxPasswordAttempts()
        {
            try
            {
                var attribute = _systemAttributeCache.GetAttributeByName("Security_MaxPasswordAttempts");
                if (attribute != null && int.TryParse(attribute.Value, out int maxAttempts))
                {
                    return maxAttempts;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read MaxPasswordAttempts from cache, using default");
            }

            // Fallback to configuration value
            return _securitySettings.MaxPasswordAttempts;
        }

        private bool GetOtpEnabled()
        {
            try
            {
                var attribute = _systemAttributeCache.GetAttributeByName("Security_OtpEnabled");
                if (attribute != null)
                {
                    return attribute.Value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                           attribute.Value.Equals("1", StringComparison.OrdinalIgnoreCase);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read OtpEnabled from cache, using default");
            }

            // Fallback to configuration value
            return _securitySettings.OtpEnabled;
        }

        private int GetPasswordExpirationMonths()
        {
            try
            {
                var attribute = _systemAttributeCache.GetAttributeByName("Security_PasswordExpirationMonths");
                if (attribute != null && int.TryParse(attribute.Value, out int months))
                {
                    return months;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read PasswordExpirationMonths from cache, using default");
            }

            // Fallback to configuration value
            return _securitySettings.PasswordExpirationMonths;
        }

        


        /// <summary>
        /// Check if user is locked and handle failed login attempts
        /// </summary>
        private async Task<(bool IsLocked, string? Message)> CheckUserLockStatusAsync(User user)
        {
            if (user.IsLocked)
            {
                _logger.LogWarning("Login attempt for locked user {UserId}", user.Id);
                return (true, "חשבון המשתמש נעול. אנא פנה למנהל המערכת");
            }

            return (false, null);
        }


        /// <summary>
        /// Record failed password attempt and lock user if threshold exceeded
        /// </summary>
        private async Task<bool> HandleFailedPasswordAttemptAsync(User user)
        {
            user.FailedPasswordAttempts++;
            user.LastFailedAttempt = DateTime.UtcNow;

            bool wasLocked = false;

            int maxAttempts = GetMaxPasswordAttempts();  // ✅ Get from cache dynamically

            if (user.FailedPasswordAttempts >= maxAttempts)
            {
                user.IsLocked = true;
                user.LockedAt = DateTime.UtcNow;
                wasLocked = true;
                _logger.LogWarning("User {UserId} locked after {Attempts} failed password attempts (max: {MaxAttempts})",
                    user.Id, user.FailedPasswordAttempts, maxAttempts);
            }

            await _context.SaveChangesAsync();

            return wasLocked;
        }


        /// <summary>
        /// Reset failed attempt counters on successful login
        /// </summary>
        private async Task ResetFailedAttemptsAsync(User user)
        {
            if (user.FailedPasswordAttempts > 0 || user.FailedOtpAttempts > 0)
            {
                user.FailedPasswordAttempts = 0;
                user.FailedOtpAttempts = 0;
                user.LastFailedAttempt = null;
                await _context.SaveChangesAsync();
            }
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

                // ✅ Check if user is locked
                var (isLocked, lockMessage) = await CheckUserLockStatusAsync(user);
                if (isLocked)
                {
                    return new LoginResponseDto
                    {
                        Success = false,
                        Message = lockMessage!
                    };
                }

                // Verify password using BCrypt (Security Patterns)
                if (!BCrypt.Net.BCrypt.Verify(loginRequest.Password, user.PasswordHash))
                {
                    _logger.LogWarning("Login failed: Invalid password for user {Username}", loginRequest.Username);

                    // ✅ Calculate remaining attempts BEFORE incrementing
                    int maxAttempts = GetMaxPasswordAttempts();
                    int remainingAttempts = maxAttempts - user.FailedPasswordAttempts - 1;

                    // ✅ Track failed attempt and check if user got locked
                    bool wasLocked = await HandleFailedPasswordAttemptAsync(user);

                    if (wasLocked)
                    {
                        // User was just locked on this attempt
                        return new LoginResponseDto
                        {
                            Success = false,
                            Message = "חשבון המשתמש נעול. אנא פנה למנהל המערכת"
                        };
                    }

                    // User not locked yet - show remaining attempts
                    string message = $"שם משתמש או סיסמה שגויים. נותרו {remainingAttempts} ניסיונות";

                    return new LoginResponseDto
                    {
                        Success = false,
                        Message = message
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

                // ✅ Reset failed attempts on successful password verification
                await ResetFailedAttemptsAsync(user);

                // Update last login timestamp
                user.LastLogin = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                // ✅ Check OTP status BEFORE password expiration
                // System OTP enabled = ALL users must use OTP
                // System OTP disabled = Only users with otp_enabled flag use OTP
                if (GetOtpEnabled() || user.OtpEnabled)
                {
                    if (!user.OtpVerified)
                    {
                        // Case B: OTP enabled but not set up yet - prompt setup
                        _logger.LogInformation("User {Username} needs to complete OTP setup", loginRequest.Username);

                        return new LoginResponseDto
                        {
                            Success = false,
                            RequiresOtpSetup = true,
                            TempToken = GenerateTempToken(user.Id),
                            Message = "יש להגדיר אימות דו-שלבי"
                        };
                    }
                    else
                    {
                        // Case C: OTP enabled and verified - prompt code
                        // ✅ Store user ID in temp token for later password expiration check
                        _logger.LogInformation("User {Username} requires OTP code verification", loginRequest.Username);

                        return new LoginResponseDto
                        {
                            Success = false,
                            RequiresOtp = true,
                            TempToken = GenerateTempToken(user.Id),
                            Message = "נדרש קוד אימות דו-שלבי"
                        };
                    }
                }

                // ✅ NO OTP - Check password expiration now and complete login
                var (isExpired, expirationMessage) = CheckPasswordExpiration(user);
                if (isExpired)
                {
                    _logger.LogInformation("User {Username} requires password change: {Reason}",
                        loginRequest.Username, expirationMessage);

                    return new LoginResponseDto
                    {
                        Success = false,
                        RequiresPasswordChange = true,
                        TempToken = GenerateTempToken(user.Id),
                        PasswordExpirationMessage = expirationMessage,
                        Message = "נדרש שינוי סיסמה"
                    };
                }

                // Case A: No OTP, password not expired - complete login
                var userFullName = $"{user.FirstName} {user.LastName}".Trim();
                var sessionId = await CompleteLoginAsync(user, userEntity);

                // Return token only (Frontend Token-Only Storage pattern)
                return new LoginResponseDto
                {
                    Success = true,
                    Message = "התחברות הצליחה",
                    Token = sessionId,
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
        /// Update user password and reset expiration flags
        /// </summary>
        public async Task UpdateUserPasswordAsync(User user, string newPasswordHash)
        {
            user.PasswordHash = newPasswordHash;
            user.PasswordChangedAt = DateTime.UtcNow;
            user.PasswordChangeRequired = false;
            user.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Check if password has expired
        /// </summary>
        private (bool IsExpired, string? Message) CheckPasswordExpiration(User user)
        {
            // Check if expiration is enabled
            var expirationMonths = GetPasswordExpirationMonths();
            if (expirationMonths <= 0)
            {
                return (false, null);
            }

            // Check if admin forced password change
            if (user.PasswordChangeRequired)
            {
                return (true, "מנהל המערכת דורש החלפת סיסמה");
            }

            // Check if password is expired by age
            if (user.IsPasswordExpired(expirationMonths))
            {
                var daysSinceChange = (DateTime.UtcNow - user.PasswordChangedAt).Days;
                return (true, $"הסיסמה פגה תוקף ({daysSinceChange} ימים מאז שינוי אחרון)");
            }

            return (false, null);
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
        /// Generate temporary JWT token for OTP verification (valid for 10 minutes)
        /// ✅ NOW USING CENTRALIZED JWT TOKEN SERVICE
        /// </summary>
        private string GenerateTempToken(int userId)
        {
            return _jwtTokenService.GenerateTempOtpToken(userId);
        }

        /// <summary>
        /// Complete login process by creating full session with roles and actions
        /// Called by both regular login and OTP validation
        /// </summary>
        public async Task<string> CompleteLoginAsync(User user, Entity userEntity)
        {
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

            var userRoles = await _context.UserRoles
                .AsNoTracking()
                .Where(ur => ur.UserId == user.Id && ur.IsActive)
                .ToListAsync();

            _logger.LogInformation("Found {Count} user_roles records", userRoles.Count);

            var userRoleIds = userRoles.Select(ur => ur.RoleId).ToList();

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

            _logger.LogInformation("User {Username} (ID: {UserId}) completed login to entity {EntityId}",
                user.Username, user.Id, user.EntityId);

            return sessionId;
        }

        public async Task<User?> ValidateUserAsync(int userId)
        {
            return await _context.Users
                .Include(u => u.Entity)
                    .ThenInclude(e => e.EntityType)
                .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);
        }
    }
}

