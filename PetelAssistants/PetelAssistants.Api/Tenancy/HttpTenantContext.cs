using Petel.Core.Session;

namespace PetelAssistants.Api.Tenancy
{
    /// <summary>
    /// Resolves the current tenant EntityId from the JWT in the Authorization header.
    /// Registered as scoped — one instance per HTTP request.
    /// Returns 0 when no valid session is found (unauthenticated requests such as login).
    /// </summary>
    public class HttpTenantContext : ITenantContext
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly UserSessionService _sessionService;

        public HttpTenantContext(IHttpContextAccessor httpContextAccessor, UserSessionService sessionService)
        {
            _httpContextAccessor = httpContextAccessor;
            _sessionService = sessionService;
        }

        public int EntityId
        {
            get
            {
                var header = _httpContextAccessor.HttpContext?
                    .Request.Headers["Authorization"].ToString();
                var token = header?.Replace("Bearer ", "").Trim();
                if (string.IsNullOrEmpty(token))
                    return 0;

                var session = _sessionService.GetUserSession(token);
                return int.TryParse(session?.EntityId, out var id) ? id : 0;
            }
        }
    }
}
