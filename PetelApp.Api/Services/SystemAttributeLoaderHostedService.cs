using Microsoft.Extensions.Hosting;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

public class SystemAttributeLoaderHostedService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public SystemAttributeLoaderHostedService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<SystemAttributeService>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<SystemAttributeLoaderHostedService>>();
            
            logger.LogInformation("Loading system attributes at startup...");
            await service.LoadAttributesAsync();
            logger.LogInformation("System attributes loaded successfully at startup.");
        }
        catch (Exception ex)
        {
            var logger = _scopeFactory.CreateScope().ServiceProvider.GetRequiredService<ILogger<SystemAttributeLoaderHostedService>>();
            logger.LogError(ex, "Failed to load system attributes at startup");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}