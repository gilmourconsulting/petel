using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PetelATH.Api.Data;
using PetelATH.Api.Models;
using PetelATH.Api.Session;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PetelATH.Api.Services
{
    /// <summary>
    /// Service for accessing system attributes from cache
    /// Provides business logic layer over SystemAttributeCache
    /// This is a wrapper service - all data comes from SystemAttributeCache singleton
    /// </summary>
    public class SystemAttributeService
    {
        private readonly SystemAttributeCache _cache;
        private readonly ILogger<SystemAttributeService> _logger;

        public SystemAttributeService(
            SystemAttributeCache cache,
            ILogger<SystemAttributeService> logger)
        {
            _cache = cache;
            _logger = logger;
        }

        /// <summary>
        /// Get all system attributes from cache
        /// </summary>
        public IEnumerable<SystemAttribute> GetAllAttributes()
        {
            return _cache.GetAllAttributes();
        }

        /// <summary>
        /// Get system attribute value by name
        /// Returns null if not found
        /// </summary>
        public string? GetAttributeValue(string name)
        {
            var attribute = _cache.GetAttributeByName(name);
            return attribute?.Value;
        }

        /// <summary>
        /// Get system attribute by name
        /// Returns null if not found
        /// </summary>
        public SystemAttribute? GetAttribute(string name)
        {
            return _cache.GetAttributeByName(name);
        }

        /// <summary>
        /// Get attributes grouped by foreign_id
        /// </summary>
        public IEnumerable<SystemAttribute> GetAttributesByForeignId(int foreignId)
        {
            return _cache.GetAttributesByForeignId(foreignId);
        }

        /// <summary>
        /// Get attributes of a specific value type
        /// </summary>
        public IEnumerable<SystemAttribute> GetAttributesByType(string valueType)
        {
            return _cache.GetAttributesByType(valueType);
        }

        /// <summary>
        /// Get attribute value as integer
        /// Returns null if not found or cannot parse
        /// </summary>
        public int? GetAttributeValueAsInt(string name)
        {
            var value = GetAttributeValue(name);
            if (string.IsNullOrEmpty(value))
                return null;

            return int.TryParse(value, out var result) ? result : null;
        }

        /// <summary>
        /// Get attribute value as boolean
        /// Returns false if not found or cannot parse
        /// </summary>
        public bool GetAttributeValueAsBool(string name)
        {
            var value = GetAttributeValue(name);
            if (string.IsNullOrEmpty(value))
                return false;

            return value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                   value.Equals("1", StringComparison.OrdinalIgnoreCase) ||
                   value.Equals("yes", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Get attribute value as decimal
        /// Returns null if not found or cannot parse
        /// </summary>
        public decimal? GetAttributeValueAsDecimal(string name)
        {
            var value = GetAttributeValue(name);
            if (string.IsNullOrEmpty(value))
                return null;

            return decimal.TryParse(value, out var result) ? result : null;
        }

        /// <summary>
        /// Check if attribute exists in cache
        /// </summary>
        public bool AttributeExists(string name)
        {
            return _cache.GetAttributeByName(name) != null;
        }

        /// <summary>
        /// Get all attributes as dictionary (name -> value)
        /// </summary>
        public Dictionary<string, string> GetAllAttributesAsDictionary()
        {
            return _cache.GetAllAttributes()
                .ToDictionary(a => a.Name, a => a.Value);
        }

        /// <summary>
        /// Check if cache is loaded
        /// </summary>
        public bool IsLoaded()
        {
            return _cache.IsLoaded();
        }

        /// <summary>
        /// Get last loaded timestamp
        /// </summary>
        public DateTime GetLastLoadedTime()
        {
            return _cache.GetLastLoadedTime();
        }

        /// <summary>
        /// Get cache statistics
        /// </summary>
        public object GetCacheStatistics()
        {
            var allAttributes = _cache.GetAllAttributes().ToList();
            var typeGroups = allAttributes
                .GroupBy(a => a.ValueType)
                .ToDictionary(g => g.Key, g => g.Count());

            return new
            {
                IsLoaded = _cache.IsLoaded(),
                LastLoaded = _cache.GetLastLoadedTime(),
                TotalAttributes = allAttributes.Count,
                AttributesByType = typeGroups
            };
        }
    }
}