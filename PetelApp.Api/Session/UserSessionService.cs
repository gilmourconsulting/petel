using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text.Json;

namespace PetelApp.Api.Session
{
    /// <summary>
    /// Service for managing the UserSession object in the current HTTP session.
    /// </summary>
    public class UserSessionService
    {
        private readonly ConcurrentDictionary<string, UserSession> _sessions = new();
        private readonly ILogger<UserSessionService> _logger;
        private readonly Timer _cleanupTimer;

        public UserSessionService(ILogger<UserSessionService> logger)
        {
            _logger = logger;
            // Cleanup expired sessions every 30 minutes
            _cleanupTimer = new Timer(CleanupExpiredSessions, null, TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(30));
        }

        public string CreateSession(string userId, string userFullName, string entityId, string entityTypeId = "")
        {
            var sessionId = Guid.NewGuid().ToString();
            var session = new UserSession
            {
                SessionId = sessionId,
                UserId = userId,
                UserFullName = userFullName,
                EntityId = entityId, // Changed from TenantId to EntityId
                EntityTypeId = entityTypeId,
                CreatedAt = DateTime.UtcNow,
                LastAccessedAt = DateTime.UtcNow,
                Roles = new List<string> { "user", "viewer" } // Default roles
            };

            _sessions.TryAdd(sessionId, session);
            _logger.LogInformation("Session created for user {UserId} with session {SessionId}", userId, sessionId);
            
            return sessionId;
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

        public UserSession? GetUserSession(string sessionId)
        {
            if (string.IsNullOrEmpty(sessionId))
                return null;

            if (_sessions.TryGetValue(sessionId, out var session))
            {
                // Update last accessed time
                session.LastAccessedAt = DateTime.UtcNow;
                return session;
            }

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
                switch (key.ToLower())
                {
                    case "selectedschoolid":
                        session.SelectedSchoolId = value;
                        break;
                    case "selectedschoolname":
                        session.SelectedSchoolName = value;
                        break;
                    case "selectedyearid":
                        session.SelectedYearId = value;
                        break;
                    case "selectedyeartype":
                        session.SelectedYearType = value;
                        break;
                    case "selectedyearvalue":
                        session.SelectedYearValue = value;
                        break;
                    default:
                        session.AdditionalData[key] = value;
                        break;
                }
                
                session.LastAccessedAt = DateTime.UtcNow;
                _logger.LogDebug("Session data updated for session {SessionId}: {Key}={Value}", sessionId, key, value);
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
                
                return key.ToLower() switch
                {
                    "userid" => session.UserId,
                    "userfullname" => session.UserFullName,
                    "entityid" => session.EntityId, // Changed from tenantid to entityid
                    "entitytypeid" => session.EntityTypeId,
                    "selectedschoolid" => session.SelectedSchoolId,
                    "selectedschoolname" => session.SelectedSchoolName,
                    "selectedyearid" => session.SelectedYearId,
                    "selectedyeartype" => session.SelectedYearType,
                    "selectedyearvalue" => session.SelectedYearValue,
                    _ => session.AdditionalData.TryGetValue(key, out var value) ? value : null
                };
            }

            return null;
        }

        public Dictionary<string, string> GetAllSessionData(string sessionId)
        {
            if (_sessions.TryGetValue(sessionId, out var session))
            {
                session.LastAccessedAt = DateTime.UtcNow;
                
                var data = new Dictionary<string, string>
                {
                    ["userId"] = session.UserId,
                    ["userFullName"] = session.UserFullName,
                    ["entityId"] = session.EntityId, // Changed from tenantId to entityId
                    ["entityTypeId"] = session.EntityTypeId,
                    ["selectedSchoolId"] = session.SelectedSchoolId,
                    ["selectedSchoolName"] = session.SelectedSchoolName,
                    ["selectedYearId"] = session.SelectedYearId,
                    ["selectedYearType"] = session.SelectedYearType,
                    ["selectedYearValue"] = session.SelectedYearValue
                };

                // Add additional data
                foreach (var kvp in session.AdditionalData)
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

        public bool IsSessionValid(string sessionId)
        {
            if (_sessions.TryGetValue(sessionId, out var session))
            {
                // Check if session is expired (24 hours)
                if (DateTime.UtcNow - session.LastAccessedAt > TimeSpan.FromHours(24))
                {
                    _sessions.TryRemove(sessionId, out _);
                    _logger.LogInformation("Session expired for session {SessionId}", sessionId);
                    return false;
                }

                return true;
            }

            return false;
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
    }
}