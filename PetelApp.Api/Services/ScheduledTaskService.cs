using PetelApp.Api.Data; 
using Microsoft.EntityFrameworkCore; 

namespace PetelApp.Api.Services;

public interface IScheduledTaskService
{
    Task FetchExternalDataAsync();
    Task ProcessDataCleanupAsync();
    Task SendNotificationsAsync();
}

public class ScheduledTaskService : IScheduledTaskService
{
    private readonly ILogger<ScheduledTaskService> _logger;
    private readonly HttpClient _httpClient;
    private readonly AppDbContext _context;

    public ScheduledTaskService(
        ILogger<ScheduledTaskService> logger,
        HttpClient httpClient,
        AppDbContext context)
    {
        _logger = logger;
        _httpClient = httpClient;
        _context = context;
    }

    public async Task FetchExternalDataAsync()
    {
        try
        {
            _logger.LogInformation("Starting external data fetch at {Time}", DateTime.UtcNow);

            // Example: Call external API
            var response = await _httpClient.GetAsync("https://jsonplaceholder.typicode.com/posts/1");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("Successfully fetched data: {Data}", content);

                // Process and save data to database
                // ... your business logic here
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching external data");
        }
    }

    public async Task ProcessDataCleanupAsync()
    {
        try
        {
            _logger.LogInformation("Starting data cleanup at {Time}", DateTime.UtcNow);

            // Example: Delete old records
            var cutoffDate = DateTime.UtcNow.AddDays(-30);
            var oldProducts = _context.Products.Where(p => p.CreatedAt < cutoffDate);

            _context.Products.RemoveRange(oldProducts);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Data cleanup completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during data cleanup");
        }
    }

    public async Task SendNotificationsAsync()
    {
        try
        {
            _logger.LogInformation("Sending notifications at {Time}", DateTime.UtcNow);

            // Example: Send emails, push notifications, etc.
            var usersToNotify = await _context.Users
                .Where(u => u.LastSync < DateTime.UtcNow.AddHours(-24))
                .ToListAsync();

            foreach (var user in usersToNotify)
            {
                // Send notification logic
                _logger.LogInformation("Notification sent to {Email}", user.Email);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending notifications");
        }
    }
}
