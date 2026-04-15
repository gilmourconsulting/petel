using Petel.Core.Abstractions;

namespace PetelAssistants.Api.Services
{
    /// <summary>
    /// In-memory cache for PetelAssistants system attributes.
    /// Loaded at startup by SystemAttributeLoaderHostedService.
    /// </summary>
    public class SystemAttributeCache : IAttributeCache
    {
        private readonly Dictionary<string, string> _cache = new(StringComparer.OrdinalIgnoreCase);
        private readonly ILogger<SystemAttributeCache> _logger;

        public SystemAttributeCache(ILogger<SystemAttributeCache> logger)
        {
            _logger = logger;
        }

        public string? GetAttributeValue(string name)
        {
            _cache.TryGetValue(name, out var value);
            return value;
        }

        public void Load(IEnumerable<(string Name, string Value)> attributes)
        {
            _cache.Clear();
            foreach (var (name, value) in attributes)
                _cache[name] = value;
            _logger.LogInformation("Loaded {Count} system attributes", _cache.Count);
        }
    }
}
