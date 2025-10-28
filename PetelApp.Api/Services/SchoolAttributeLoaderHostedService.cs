using PetelApp.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace PetelApp.Api.Services;

/// <summary>
/// Background service that loads school attribute types and values at application startup.
/// Similar to SystemAttributeLoaderHostedService pattern.
/// </summary>
public class SchoolAttributeLoaderHostedService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SchoolAttributeLoaderHostedService> _logger;

    public SchoolAttributeLoaderHostedService(
        IServiceProvider serviceProvider,
        ILogger<SchoolAttributeLoaderHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("School Attribute Loader starting...");

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var cache = scope.ServiceProvider.GetRequiredService<SchoolAttributeCache>();

            await cache.LoadAsync(context);

            _logger.LogInformation("School Attribute Loader completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading school attributes at startup");
            // Don't throw - allow application to start even if cache loading fails
        }

        return;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("School Attribute Loader stopping...");
        return Task.CompletedTask;
    }
}