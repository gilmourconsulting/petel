using Microsoft.EntityFrameworkCore;
using PetelApp.Api.Data;
using Microsoft.Extensions.Logging;

namespace PetelApp.Api.Services
{
    /// <summary>
    /// Service for loading alert-related definitions into memory at startup
    /// Part of system attributes loading process
    /// </summary>
    public class AlertDefinitionsLoader
    {
        private readonly AppDbContext _context;
        private readonly ILogger<AlertDefinitionsLoader> _logger;

        public AlertDefinitionsLoader(
            AppDbContext context,
            ILogger<AlertDefinitionsLoader> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Load all alert definitions (types, statuses, levels) into cache
        /// Called during application startup
        /// </summary>
        public async Task LoadAlertDefinitionsAsync()
        {
            try
            {
                _logger.LogInformation("🔔 Loading alert definitions...");

                // Load alert types
                var alertTypes = await _context.AlertTypes
                    .AsNoTracking()
                    .ToListAsync();
                
                AlertDefinitionsCache.AlertTypes = alertTypes.ToDictionary(a => (int)a.Id, a => a);
                _logger.LogInformation("✅ Loaded {Count} alert types", alertTypes.Count);

                // Load alert statuses
                var alertStatuses = await _context.AlertStatuses
                    .AsNoTracking()
                    .ToListAsync();
                
                AlertDefinitionsCache.AlertStatuses = alertStatuses.ToDictionary(a => (int)a.Id, a => a);
                _logger.LogInformation("✅ Loaded {Count} alert statuses", alertStatuses.Count);

                // Load alert levels
                var alertLevels = await _context.AlertLevels
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

    /// <summary>
    /// In-memory cache for alert definitions
    /// Shared across application lifetime
    /// </summary>
    public static class AlertDefinitionsCache
    {
        public static Dictionary<int, AlertType> AlertTypes { get; set; } = new();
        public static Dictionary<int, AlertStatus> AlertStatuses { get; set; } = new();
        public static Dictionary<int, AlertLevel> AlertLevels { get; set; } = new();

        public static AlertType? GetAlertType(int id) => AlertTypes.GetValueOrDefault(id);
        public static AlertStatus? GetAlertStatus(int id) => AlertStatuses.GetValueOrDefault(id);
        public static AlertLevel? GetAlertLevel(int id) => AlertLevels.GetValueOrDefault(id);

        public static bool IsValidAlertType(int id) => AlertTypes.ContainsKey(id);
        public static bool IsValidAlertStatus(int id) => AlertStatuses.ContainsKey(id);
        public static bool IsValidAlertLevel(int id) => AlertLevels.ContainsKey(id);
    }
}