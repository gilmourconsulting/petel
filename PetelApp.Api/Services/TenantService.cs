// PetelApp.Api/Services/TenantService.cs
using Microsoft.EntityFrameworkCore;
using PetelApp.Api.Data;

namespace PetelApp.Api.Services
{
    public interface ITenantService
    {
        string? GetCurrentTenantId();
        Task<bool> ValidateTenantAccessAsync(int tenantId, int userId);
        Task<bool> TenantExistsAsync(int tenantId);
        Task<string?> GetTenantNameAsync(int tenantId);
        Task<TenantInfo?> GetTenantInfoAsync(int tenantId);
        Task<TenantInfo?> GetCurrentTenantInfoAsync();
    }

    public class TenantService : ITenantService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly AppDbContext _context;
        private readonly ILogger<TenantService> _logger;

        public TenantService(
            IHttpContextAccessor httpContextAccessor, 
            AppDbContext context,
            ILogger<TenantService> logger)
        {
            _httpContextAccessor = httpContextAccessor;
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Gets the current tenant ID from the HTTP context
        /// </summary>
        /// <returns>Tenant ID as string, or null if not found</returns>
        public string? GetCurrentTenantId()
        {
            var tenantId = _httpContextAccessor.HttpContext?.Items["TenantId"]?.ToString();
            
            if (string.IsNullOrEmpty(tenantId))
            {
                _logger.LogDebug("No tenant ID found in HTTP context");
                return null;
            }

            _logger.LogDebug("Retrieved tenant ID from context: {TenantId}", tenantId);
            return tenantId;
        }

        /// <summary>
        /// Validates if a user has access to a specific tenant
        /// </summary>
        /// <param name="tenantId">The tenant/entity ID</param>
        /// <param name="userId">The user ID</param>
        /// <returns>True if user has access to tenant, false otherwise</returns>
        public async Task<bool> ValidateTenantAccessAsync(int tenantId, int userId)
        {
            try
            {
                _logger.LogDebug("Validating tenant access for user {UserId} in tenant {TenantId}", userId, tenantId);

                // Simple one-to-many relationship validation
                // Check if user exists, is active, and belongs to the specified entity
                var hasAccess = await _context.Users
                    .AnyAsync(u => u.Id == userId && 
                                  u.EntityId == tenantId && 
                                  u.IsActive == true);

                _logger.LogDebug("Tenant access validation result for user {UserId} in tenant {TenantId}: {HasAccess}", 
                    userId, tenantId, hasAccess);

                return hasAccess;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating tenant access for user {UserId} in tenant {TenantId}", userId, tenantId);
                return false;
            }
        }

        /// <summary>
        /// Checks if a tenant exists and is active
        /// </summary>
        /// <param name="tenantId">The tenant/entity ID</param>
        /// <returns>True if tenant exists and is active, false otherwise</returns>
        public async Task<bool> TenantExistsAsync(int tenantId)
        {
            try
            {
                _logger.LogDebug("Checking if tenant {TenantId} exists", tenantId);

                var exists = await _context.Entities
                    .AnyAsync(e => e.Id == tenantId && e.IsActive == true);

                _logger.LogDebug("Tenant {TenantId} exists: {Exists}", tenantId, exists);

                return exists;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if tenant {TenantId} exists", tenantId);
                return false;
            }
        }

        /// <summary>
        /// Gets the name of a tenant
        /// </summary>
        /// <param name="tenantId">The tenant/entity ID</param>
        /// <returns>Tenant name or null if not found</returns>
        public async Task<string?> GetTenantNameAsync(int tenantId)
        {
            try
            {
                _logger.LogDebug("Getting name for tenant {TenantId}", tenantId);

                var tenantName = await _context.Entities
                    .Where(e => e.Id == tenantId && e.IsActive == true)
                    .Select(e => e.Name)
                    .FirstOrDefaultAsync();

                _logger.LogDebug("Tenant {TenantId} name: {TenantName}", tenantId, tenantName ?? "Not found");

                return tenantName;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting name for tenant {TenantId}", tenantId);
                return null;
            }
        }

        /// <summary>
        /// Gets detailed tenant information
        /// </summary>
        /// <param name="tenantId">The tenant/entity ID</param>
        /// <returns>Tenant information or null if not found</returns>
        public async Task<TenantInfo?> GetTenantInfoAsync(int tenantId)
        {
            try
            {
                _logger.LogDebug("Getting detailed info for tenant {TenantId}", tenantId);

                var tenantInfo = await _context.Entities
                    .Where(e => e.Id == tenantId && e.IsActive == true)
                    .Select(e => new TenantInfo
                    {
                        Id = e.Id,
                        Name = e.Name,
                        EntityTypeId = e.EntityTypeId,
                        Address = e.Address,
                        Phone = e.Phone,
                        Email = e.Email,
                        PrincipalName = e.PrincipalName,
                        UserCount = e.Users.Count(u => u.IsActive == true)
                    })
                    .FirstOrDefaultAsync();

                _logger.LogDebug("Tenant {TenantId} info retrieved: {Found}", tenantId, tenantInfo != null);

                return tenantInfo;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting detailed info for tenant {TenantId}", tenantId);
                return null;
            }
        }

        /// <summary>
        /// Gets the current tenant information from context
        /// </summary>
        /// <returns>Current tenant information or null</returns>
        public async Task<TenantInfo?> GetCurrentTenantInfoAsync()
        {
            var tenantIdString = GetCurrentTenantId();
            
            if (string.IsNullOrEmpty(tenantIdString) || !int.TryParse(tenantIdString, out int tenantId))
            {
                _logger.LogDebug("No valid tenant ID in current context");
                return null;
            }

            return await GetTenantInfoAsync(tenantId);
        }
    }

    /// <summary>
    /// Data transfer object for tenant information
    /// </summary>
    public class TenantInfo
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int EntityTypeId { get; set; }
        public string? Address { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? PrincipalName { get; set; }
        public int UserCount { get; set; }
    }
}