using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PetelATH.Api.Configuration;
using System.Collections.Concurrent;
using System.Text.Json;
using PetelATH.Api.Services;

namespace PetelATH.Api.Session
{
    /// <summary>
    /// Service for managing the UserSession object in the current HTTP session.
    /// </summary>
    public class UserSessionService
    {
        private readonly ConcurrentDictionary<string, UserSession> _sessions = new();
        private readonly ILogger<UserSessionService> _logger;
        private readonly Timer _cleanupTimer;
        private readonly SecuritySettings _securitySettings;
        private readonly SystemAttributeCache? _systemAttributeCache;

        private JwtTokenService? _jwtTokenService;

        public UserSessionService(
            ILogger<UserSessionService> logger,
            IOptions<SecuritySettings> securitySettings,
            SystemAttributeCache? systemAttributeCache = null)
        {
            _logger = logger;
            _securitySettings = securitySettings.Value;
            _systemAttributeCache = systemAttributeCache;
            
            _logger.LogInformation("Session timeout configured: {TimeoutMinutes} minutes", 
                GetSessionTimeoutMinutes());
            
            // Cleanup expired sessions every 5 minutes
            _cleanupTimer = new Timer(CleanupExpiredSessions, null, 
                TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
        }


        public void SetJwtTokenService(JwtTokenService jwtTokenService)
        {
            _jwtTokenService = jwtTokenService;
            _logger.LogInformation("JwtTokenService configured for UserSessionService");
        }

        /// <summary>
        /// Get SessionTimeoutMinutes from system attributes cache, fallback to config
        /// </summary>
        private int GetSessionTimeoutMinutes()
        {
            try
            {
                if (_systemAttributeCache != null)
                {
                    var attribute = _systemAttributeCache.GetAttributeByName("Security_SessionTimeoutMinutes");
                    if (attribute != null && int.TryParse(attribute.Value, out int minutes))
                    {
                        _logger.LogDebug("Using SessionTimeoutMinutes from system attributes: {Minutes}", minutes);
                        return minutes;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read SessionTimeoutMinutes from cache, using default");
            }
            
            // Fallback to configuration file
            _logger.LogDebug("Using SessionTimeoutMinutes from config file: {Minutes}", _securitySettings.SessionTimeoutMinutes);
            return _securitySettings.SessionTimeoutMinutes;
        }
        // Add method to create session with complete data

        public string CreateSessionWithFullData(
            string userId, 
            string username,
            string userFullName, 
            string entityId, 
            string entityName,
            string entityTypeId, 
            string entityTypeName,
            DateTime? lastLogin = null)
        {
            // Check for existing active sessions for this user/entity combination
            var existingSessions = GetAllActiveSessions()
                .Where(s => s.UserId == userId && s.EntityId == entityId)
                .ToList();
            
            // Invalidate existing sessions to prevent duplicates
            foreach (var existingSession in existingSessions)
            {
                InvalidateSession(existingSession.SessionId);
                _logger.LogInformation("Invalidated duplicate session {SessionId} for user {UserId}", 
                    existingSession.SessionId, userId);
            }

            // Create new session with ALL required data following Authentication & Session Management
            var sessionId = Guid.NewGuid().ToString();
            var session = new UserSession
            {
                SessionId = sessionId,
                UserId = userId,
                Username = username, // Restored
                UserFullName = userFullName, // Fixed: Use concatenated name from login
                EntityId = entityId,
                EntityName = entityName, // Restored
                EntityTypeId = entityTypeId,
                EntityTypeName = entityTypeName, // Restored
                CreatedAt = DateTime.UtcNow, // Restored
                LastAccessedAt = DateTime.UtcNow,
                LastLogin = lastLogin, // Restored
                Roles = new List<int> { 1, 2 }, // Assuming 1=user, 2=viewer
                AdditionalData = new Dictionary<string, string>()
            };

            _sessions.TryAdd(sessionId, session);
            _logger.LogInformation("Complete session created for user {UserId} ({Username}) with session {SessionId}", 
                userId, username, sessionId);
            
                // ✅ Generate JWT token from session (returned to client)
            if (_jwtTokenService != null)
            {
                var jwtToken = _jwtTokenService.GenerateSessionToken(session);
                _logger.LogInformation("Generated JWT token for session {SessionId}", sessionId);
                return jwtToken; // ✅ Return JWT instead of GUID
            }
            else
            {
                // Fallback during app initialization before JWT service is set
                _logger.LogWarning("JwtTokenService not initialized, returning GUID (initialization only)");
                return sessionId;
            }
        }

        // Keep the simple CreateSession method for backward compatibility
        public string CreateSession(string userId, string userFullName, string entityId, string entityTypeId = "")
        {
            // Call the full method with minimal data
            return CreateSessionWithFullData(
                userId: userId,
                username: "", // Will need to be populated from User table if needed
                userFullName: userFullName,
                entityId: entityId,
                entityName: "",
                entityTypeId: entityTypeId,
                entityTypeName: ""
            );
        }

        public void CreateUserSession(UserSession userSession)
        {
            if (userSession == null)
            {
                _logger.LogWarning("Attempt to create session with null UserSession object");
                return;
            }

            if (string.IsNullOrEmpty(userSession.SessionId))
            {
                userSession.SessionId = Guid.NewGuid().ToString();
            }

            // Set creation and access timestamps
            userSession.CreatedAt = DateTime.UtcNow;
            userSession.LastAccessedAt = DateTime.UtcNow;

            // Add the session to the concurrent dictionary
            _sessions.TryAdd(userSession.SessionId, userSession);
            
            _logger.LogInformation("User session created for user {UserId} with session {SessionId} in entity {EntityId}", 
                userSession.UserId, userSession.SessionId, userSession.EntityId);
        }

        // Also add an overload method for convenience
        public void CreateUserSession(string sessionId, UserSession userSession)
        {
            if (string.IsNullOrEmpty(sessionId))
            {
                _logger.LogWarning("Attempt to create session with null or empty sessionId");
                return;
            }

            userSession.SessionId = sessionId;
            CreateUserSession(userSession);
        }

        /// <summary>
        /// Check if session is valid (exists and not timed out)
        /// </summary>
        private bool IsSessionValid(string sessionId)
        {
            if (!_sessions.TryGetValue(sessionId, out var session))
            {
                return false;
            }

            // Check if session has timed out due to inactivity
            var idleTime = DateTime.UtcNow - session.LastAccessedAt;
            var timeoutMinutes = GetSessionTimeoutMinutes();
            
            if (idleTime.TotalMinutes > timeoutMinutes)
            {
                _logger.LogInformation(
                    "Session {SessionId} timed out. Idle for {IdleMinutes:F1} minutes (timeout: {TimeoutMinutes} minutes)",
                    sessionId, idleTime.TotalMinutes, timeoutMinutes);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Get user session and validate timeout
        /// </summary>
        public UserSession? GetUserSession(string? token)
        {
            if (string.IsNullOrEmpty(token))
            {
                _logger.LogWarning("GetUserSession called with null or empty token");
                return null;
            }

            // Try JWT validation first
            if (_jwtTokenService != null)
            {
                var sessionId = _jwtTokenService.ValidateTokenAndGetSessionId(token);
                
                if (sessionId != null && _sessions.TryGetValue(sessionId, out var sessionFromJwt))
                {
                    // Check if session is still valid (not expired or timed out)
                    if (IsSessionValid(sessionId))
                    {
                        // Update last accessed time
                        sessionFromJwt.LastAccessedAt = DateTime.UtcNow;
                        _logger.LogDebug("Retrieved session {SessionId} via JWT token", sessionId);
                        return sessionFromJwt;
                    }
                    else
                    {
                        // Session timed out, remove it
                        _sessions.TryRemove(sessionId, out _);
                        _logger.LogInformation("Session {SessionId} timed out and removed", sessionId);
                        return null;
                    }
                }
                else
                {
                    _logger.LogWarning("JWT token validation failed or session not found");
                    return null;
                }
            }

            // Fallback: Direct GUID lookup (for backward compatibility)
            if (_sessions.TryGetValue(token, out var session))
            {
                if (IsSessionValid(token))
                {
                    session.LastAccessedAt = DateTime.UtcNow;
                    _logger.LogDebug("Retrieved session via direct GUID lookup (legacy)");
                    return session;
                }
                else
                {
                    _sessions.TryRemove(token, out _);
                    return null;
                }
            }

            _logger.LogWarning("Session not found for provided token");
            return null;
        }

        public UserSession? GetUserSession()
        {
            // This should not be called directly - use GetUserSession(sessionId)
            // Return null or throw exception for invalid usage
            _logger.LogWarning("GetUserSession() called without session ID - this should not happen");
            return null;
        }

        public void SetUserSession(UserSession session)
        {
            if (!string.IsNullOrEmpty(session.SessionId))
            {
                _sessions.AddOrUpdate(session.SessionId, session, (key, oldValue) => session);
                _logger.LogDebug("Session updated for session {SessionId}", session.SessionId);
            }
        }


public bool UpdateSessionData(string sessionId, string key, string value)
{
    if (_sessions.TryGetValue(sessionId, out var session))
    {
        session.SetProperty(key, value);
        session.LastAccessedAt = DateTime.UtcNow;
        _logger.LogDebug("Session property updated for session {SessionId}: {Key}={Value}", sessionId, key, value);
        return true;
    }

    return false;
}

        public Task<bool> UpdateSessionDataAsync(string sessionId, string key, string value)
        {
            return Task.FromResult(UpdateSessionData(sessionId, key, value));
        }

public string? GetSessionData(string sessionId, string key)
{
    if (_sessions.TryGetValue(sessionId, out var session))
    {
        session.LastAccessedAt = DateTime.UtcNow;
        
        // Check identity properties first
        return key.ToLower() switch
        {
            "userid" => session.UserId,
            "userfullname" => session.UserFullName,
            "entityid" => session.EntityId,
            "entitytypeid" => session.EntityTypeId,
            "username" => session.Username,
            "entityname" => session.EntityName,
            _ => session.GetProperty(key) // Use generic storage for everything else
        };
    }

    return null;
}

 // Replace GetAllSessionData method (around line 240)
public Dictionary<string, string> GetAllSessionData(string sessionId)
{
    if (_sessions.TryGetValue(sessionId, out var session))
    {
        session.LastAccessedAt = DateTime.UtcNow;
        
        // Start with identity data
        var data = new Dictionary<string, string>
        {
            ["userId"] = session.UserId,
            ["userFullName"] = session.UserFullName,
            ["entityId"] = session.EntityId,
            ["entityTypeId"] = session.EntityTypeId,
            ["username"] = session.Username,
            ["entityName"] = session.EntityName
        };

        // Add all generic properties
        foreach (var kvp in session.GetAllProperties())
        {
            data[kvp.Key] = kvp.Value;
        }

        return data;
    }

    return new Dictionary<string, string>();
}

        public void InvalidateSession(string sessionId)
        {
            if (string.IsNullOrEmpty(sessionId))
            {
                _logger.LogWarning("Attempt to invalidate session with null or empty sessionId");
                return;
            }

            if (_sessions.TryRemove(sessionId, out var removedSession))
            {
                _logger.LogInformation("Session {SessionId} invalidated for user {UserId}, entity {EntityId}", 
                    sessionId, removedSession.UserId, removedSession.EntityId);
            }
            else
            {
                _logger.LogWarning("Session {SessionId} not found during invalidation attempt", sessionId);
            }
        }

        private void CleanupExpiredSessions(object? state)
        {
            var expiredSessions = _sessions
                .Where(kvp => DateTime.UtcNow - kvp.Value.LastAccessedAt > TimeSpan.FromHours(24))
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var sessionId in expiredSessions)
            {
                _sessions.TryRemove(sessionId, out _);
            }

            if (expiredSessions.Count > 0)
            {
                _logger.LogInformation("Cleaned up {Count} expired sessions", expiredSessions.Count);
            }
        }

        public void Dispose()
        {
            _cleanupTimer?.Dispose();
        }

        /// <summary>
        /// Get all active sessions for debugging purposes following Security Patterns
        /// Only accessible in development environment
        /// </summary>
        /// <returns>Collection of active sessions</returns>
        public IEnumerable<UserSession> GetAllActiveSessions()
        {
            try
            {
                return _sessions.Values.Where(s => IsSessionValid(s.SessionId));
            }
            catch (Exception)
            {
                return new List<UserSession>();
            }
        }

        /// <summary>
        /// Get session count statistics following Entity-Based Request Flow
        /// </summary>
        /// <returns>Session statistics object</returns>
        public object GetSessionStatistics()
        {
            try
            {
                var allSessions = _sessions.Values.ToList();
                var activeSessions = allSessions.Where(s => IsSessionValid(s.SessionId)).ToList();
                
                return new
                {
                    totalSessions = allSessions.Count,
                    activeSessions = activeSessions.Count,
                    expiredSessions = allSessions.Count - activeSessions.Count,
                    sessionsByEntity = activeSessions.GroupBy(s => s.EntityId)
                        .ToDictionary(g => g.Key, g => new { 
                            count = g.Count(), 
                            entityName = g.FirstOrDefault()?.EntityName ?? "Unknown"
                        }),
                    oldestActiveSession = activeSessions.OrderBy(s => s.CreatedAt).FirstOrDefault()?.CreatedAt,
                    newestActiveSession = activeSessions.OrderByDescending(s => s.CreatedAt).FirstOrDefault()?.CreatedAt,
                    mostRecentActivity = activeSessions.OrderByDescending(s => s.LastAccessedAt).FirstOrDefault()?.LastAccessedAt
                };
            }
            catch (Exception)
            {
                return new { error = "Could not retrieve session statistics" };
            }
        }

        /// <summary>
        /// Update session activity timestamp following Authentication & Session Management
        /// </summary>
        /// <param name="sessionId">Session ID to update</param>
        /// <returns>True if session was found and updated</returns>
        public bool UpdateSessionActivity(string sessionId)
        {
            var session = GetUserSession(sessionId);
            if (session != null)
            {
                session.LastAccessedAt = DateTime.UtcNow;
                SetUserSession(session);
                return true;
            }
            return false;
        }
    }
}