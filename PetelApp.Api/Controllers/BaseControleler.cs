// PetelApp.Api/Controllers/BaseController.cs
using Microsoft.AspNetCore.Mvc;
using PetelApp.Api.Session;

namespace PetelApp.Api.Controllers
{
    public class BaseController(UserSessionService userSessionService) : ControllerBase
    {
        public UserSessionService UserSessionService { get; } = userSessionService;

        protected string GetTenantId()
        {
            return HttpContext.Items["TenantId"]?.ToString() ?? string.Empty;
        }

        protected bool HasTenantContext()
        {
            return HttpContext.Items.ContainsKey("TenantContext");
        }

        protected void ValidateTenantAccess()
        {
            if (!HasTenantContext())
            {
                throw new UnauthorizedAccessException("Tenant context required");
            }
        }
    }
}