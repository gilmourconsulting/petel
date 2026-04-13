using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PetelATH.Api.Services;
using Microsoft.EntityFrameworkCore;
using PetelATH.Api.Data;

namespace PetelATH.Api.Services
{
    /// <summary>
    /// Background service that loads system attributes into memory cache at application startup
    /// Following system attributes pattern from coding guidelines
    /// </summary>
    public class SystemAttributeLoaderHostedService : IHostedService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly SystemAttributeCache _cache;
        private readonly ILogger<SystemAttributeLoaderHostedService> _logger;

        public SystemAttributeLoaderHostedService(
            IServiceProvider serviceProvider,
            SystemAttributeCache cache,
            ILogger<SystemAttributeLoaderHostedService> logger)
        {
            _serviceProvider = serviceProvider;
            _cache = cache;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting SystemAttributeLoader at {Time}", DateTime.UtcNow);
            
            try
            {
                await LoadAttributesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load system attributes at startup");
                // Don't throw - let app continue even if attributes fail to load
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        /// <summary>
        /// Load system attributes from database into cache
        /// Public method allows manual reload via controller endpoint
        /// </summary>
        public async Task LoadAttributesAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            
            try
            {
                var attributes = await dbContext.SystemAttributes
                    .AsNoTracking()
                    .ToListAsync();
                
                _cache.LoadAttributes(attributes);
                
                _logger.LogInformation(
                    "Successfully loaded {Count} system attributes", 
                    attributes.Count);
                
                // Log summary by type
                var typeGroups = attributes.GroupBy(a => a.ValueType)
                    .Select(g => $"{g.Key}: {g.Count()}")
                    .ToList();
                _logger.LogDebug("Attributes by type: {Types}", string.Join(", ", typeGroups));

                                // ✅ Load alert definitions (part of system attributes)
                await LoadAlertDefinitionsAsync(dbContext);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading system attributes from database");
                throw;
            }
        }
        
        /// <summary>
        /// Load alert definitions into memory cache
        /// Called as part of system attributes loading
        /// </summary>
        private async Task LoadAlertDefinitionsAsync(AppDbContext dbContext)
        {
            try
            {
                _logger.LogInformation("🔔 Loading alert definitions...");

                // Load alert types
                var alertTypes = await dbContext.AlertTypes
                    .AsNoTracking()
                    .ToListAsync();
                
                AlertDefinitionsCache.AlertTypes = alertTypes.ToDictionary(a => (int)a.Id, a => a);
                _logger.LogInformation("✅ Loaded {Count} alert types", alertTypes.Count);

                // Load alert statuses
                var alertStatuses = await dbContext.AlertStatuses
                    .AsNoTracking()
                    .ToListAsync();
                
                AlertDefinitionsCache.AlertStatuses = alertStatuses.ToDictionary(a => (int)a.Id, a => a);
                _logger.LogInformation("✅ Loaded {Count} alert statuses", alertStatuses.Count);

                // Load alert levels
                var alertLevels = await dbContext.AlertLevels
                    .AsNoTracking()
                    .ToListAsync();
                
                AlertDefinitionsCache.AlertLevels = alertLevels.ToDictionary(a => (int)a.Id, a => a);
                _logger.LogInformation("✅ Loaded {Count} alert levels", alertLevels.Count);

                _logger.LogInformation("✅ Alert definitions loaded successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error loading alert definitions");
                throw;
            }
        }
    }
}