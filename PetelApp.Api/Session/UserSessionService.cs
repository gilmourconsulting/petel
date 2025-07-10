using Microsoft.AspNetCore.Http;
using System.Text.Json;

namespace PetelApp.Api.Session
{
    /// <summary>
    /// Service for managing the UserSession object in the current HTTP session.
    /// </summary>
    public class UserSessionService
    {
        private const string SessionKey = "UserSession";
        private readonly IHttpContextAccessor _httpContextAccessor;

        public UserSessionService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public void SetUserSession(UserSession session)
        {
            var json = JsonSerializer.Serialize(session);
            _httpContextAccessor.HttpContext?.Session.SetString(SessionKey, json);
        }

        public UserSession? GetUserSession()
        {
            var json = _httpContextAccessor.HttpContext?.Session.GetString(SessionKey);
            if (string.IsNullOrEmpty(json))
                return null;
            return JsonSerializer.Deserialize<UserSession>(json);
        }

        public void ClearUserSession()
        {
            _httpContextAccessor.HttpContext?.Session.Remove(SessionKey);
        }
    }
}