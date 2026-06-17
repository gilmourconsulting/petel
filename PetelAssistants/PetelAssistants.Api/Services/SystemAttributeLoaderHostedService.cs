using Microsoft.EntityFrameworkCore;
using PetelAssistants.Api.Data;

namespace PetelAssistants.Api.Services
{
    /// <summary>
    /// Loads system attributes from database into in-memory cache at application startup.
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
            _logger.LogInformation("Starting SystemAttributeLoaderHostedService");

            try
            {
                await LoadAttributesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                // Do not fail API startup if cache warmup fails.
                _logger.LogError(ex, "Failed to load system attributes at startup");
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public async Task LoadAttributesAsync(CancellationToken cancellationToken = default)
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var attributes = await dbContext.SystemAttributes
                .AsNoTracking()
                .Select(a => new { a.Name, a.Value })
                .ToListAsync(cancellationToken);

            _cache.Load(attributes.Select(a => (a.Name, a.Value)));

            _logger.LogInformation("Loaded {Count} system attributes from database", attributes.Count);
        }
    }
}
