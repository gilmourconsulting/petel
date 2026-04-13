using System.Collections.Generic;
using PetelATH.Api.Data;

namespace PetelATH.Api.Services
{
    /// <summary>
    /// In-memory cache for system attributes - global configuration accessible to all sessions
    /// Loaded at application startup by SystemAttributeLoaderHostedService
    /// </summary>
    public class SystemAttributeCache
    {
        private readonly Dictionary<string, SystemAttribute> _attributesByName;
        private readonly Dictionary<int, List<SystemAttribute>> _attributesByForeignId;
        private DateTime _lastLoadedTime;
        private readonly ILogger<SystemAttributeCache> _logger;
        
        public SystemAttributeCache(ILogger<SystemAttributeCache> logger)
        {
            _attributesByName = new Dictionary<string, SystemAttribute>(StringComparer.OrdinalIgnoreCase);
            _attributesByForeignId = new Dictionary<int, List<SystemAttribute>>();
            _lastLoadedTime = DateTime.MinValue;
            _logger = logger;
        }
        
        /// <summary>
        /// Get all system attributes
        /// </summary>
        public IEnumerable<SystemAttribute> GetAllAttributes()
        {
            return _attributesByName.Values;
        }
        
        /// <summary>
        /// Get system attribute by name (case-insensitive)
        /// </summary>
        public SystemAttribute? GetAttributeByName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;
                
            return _attributesByName.TryGetValue(name, out var attribute) 
                ? attribute 
                : null;
        }
        
        /// <summary>
        /// Get all attributes with a specific foreign_id
        /// Useful for grouping related attributes (e.g., all attributes for a specific entity type)
        /// </summary>
        public IEnumerable<SystemAttribute> GetAttributesByForeignId(int foreignId)
        {
            if (_attributesByForeignId.TryGetValue(foreignId, out var attributes))
            {
                return attributes;
            }
            return Enumerable.Empty<SystemAttribute>();
        }
        
        /// <summary>
        /// Get attributes by value type (e.g., 'string', 'int', 'bool')
        /// </summary>
        public IEnumerable<SystemAttribute> GetAttributesByType(string valueType)
        {
            return _attributesByName.Values
                .Where(a => a.ValueType.Equals(valueType, StringComparison.OrdinalIgnoreCase));
        }
        
        /// <summary>
        /// Load/reload attributes from database
        /// Called by SystemAttributeLoaderHostedService at startup and on demand
        /// </summary>
        public void LoadAttributes(IEnumerable<SystemAttribute> attributes)
        {
            _attributesByName.Clear();
            _attributesByForeignId.Clear();
            
            foreach (var attr in attributes)
            {
                // Index by name (case-insensitive)
                _attributesByName[attr.Name] = attr;
                
                // Index by foreign_id if present
                if (attr.ForeignId.HasValue)
                {
                    if (!_attributesByForeignId.ContainsKey(attr.ForeignId.Value))
                    {
                        _attributesByForeignId[attr.ForeignId.Value] = new List<SystemAttribute>();
                    }
                    _attributesByForeignId[attr.ForeignId.Value].Add(attr);
                }
            }
            
            _lastLoadedTime = DateTime.UtcNow;
            _logger.LogInformation("Loaded {Count} system attributes into cache", attributes.Count());
        }
        
        /// <summary>
        /// Get timestamp of last successful load
        /// </summary>
        public DateTime GetLastLoadedTime()
        {
            return _lastLoadedTime;
        }
        
        /// <summary>
        /// Check if cache has been loaded
        /// </summary>
        public bool IsLoaded()
        {
            return _lastLoadedTime > DateTime.MinValue && _attributesByName.Count > 0;
        }
    }
}