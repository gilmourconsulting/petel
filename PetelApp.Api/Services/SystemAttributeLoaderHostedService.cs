using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PetelApp.Api.Services;

namespace PetelApp.Api.Services
{
    /// <summary>
    /// Background service for system attributes loading following system attributes pattern
    /// Loads dynamic configuration at startup for educational institutions
    /// </summary>
    public class SystemAttributeLoaderHostedService : IHostedService
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

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var attributeService = scope.ServiceProvider.GetRequiredService<SystemAttributeService>();
            await attributeService.LoadAttributesAsync();
            _logger.LogInformation("System attributes loaded at startup.");
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}