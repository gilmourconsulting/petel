// PetelApp.Api/Controllers/BaseController.cs
using Microsoft.AspNetCore.Mvc;

namespace PetelApp.Api.Controllers
{
    public class BaseController : ControllerBase
    {
        protected string GetTenantId()
        {
            return HttpContext.Items["TenantId"]?.ToString();
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