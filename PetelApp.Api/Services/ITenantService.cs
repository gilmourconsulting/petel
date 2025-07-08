// PetelApp.Api/Services/ITenantService.cs
public interface ITenantService
{
    string GetCurrentTenantId();
    Task<bool> ValidateTenantAccessAsync(int tenantId, int userId);
}

// PetelApp.Api/Services/TenantService.cs
public class TenantService : ITenantService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ApplicationDbContext _context;

    public TenantService(IHttpContextAccessor httpContextAccessor, ApplicationDbContext context)
    {
        _httpContextAccessor = httpContextAccessor;
        _context = context;
    }

    public string GetCurrentTenantId()
    {
        return _httpContextAccessor.HttpContext?.Items["TenantId"]?.ToString();
    }

    public async Task<bool> ValidateTenantAccessAsync(int tenantId, int userId)
    {
        return await _context.UserEntities
            .AnyAsync(ue => ue.UserId == userId && ue.EntityId == tenantId);
    }
}