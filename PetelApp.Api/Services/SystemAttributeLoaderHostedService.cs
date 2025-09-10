using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PetelApp.Api.Services;

namespace PetelApp.Api.Services
{
    /// <summary>
    /// Background service for system attributes loading following system attributes pattern
    /// Loads dynamic configuration at startup for multi-tenant educational SaaS
    /// </summary>
    public class SystemAttributeLoaderHostedService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<SystemAttributeLoaderHostedService> _logger;

        public SystemAttributeLoaderHostedService(
            IServiceProvider serviceProvider,
            ILogger<SystemAttributeLoaderHostedService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Starting System Attributes Loader...");
            
            // Load immediately at startup
            await LoadSystemAttributesAtStartup();

            // Continue periodic loading
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
                    await LoadSystemAttributes();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in periodic system attributes loading");
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                }
            }
        }

        private async Task LoadSystemAttributesAtStartup()
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var systemAttributeService = scope.ServiceProvider.GetRequiredService<SystemAttributeService>();
                
                var attributes = await systemAttributeService.GetAllAttributesListAsync();
                _logger.LogInformation($"Loaded {attributes.Count} system attributes at startup");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load system attributes at startup");
            }
        }

        private async Task LoadSystemAttributes()
        {
            using var scope = _serviceProvider.CreateScope();
            var systemAttributeService = scope.ServiceProvider.GetRequiredService<SystemAttributeService>();
            
            await systemAttributeService.GetAllAttributesAsync();
            _logger.LogInformation("System attributes cache refreshed");
        }
    }
}