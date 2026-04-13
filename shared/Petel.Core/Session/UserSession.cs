using System;
using System.Collections.Generic;
using System.Text.Json.Serialization; // ADD THIS if using JSON attributes

namespace Petel.Core.Session
{
    /// <summary>
    /// User session data - USER-SPECIFIC context and state
    /// 
    /// DO NOT confuse with SystemAttributes:
    /// - UserSession: Per-user, requires auth, changes during session
    /// - SystemAttributes: Global config, no auth, rarely changes
    /// 
    /// Identity Data: Set at login, never modified (UserId, EntityId, etc.)
    /// Session Parameters: Mutable state stored generically (filters, selections, etc.)
    /// </summary>
    public class UserSession
    {
        // IDENTITY DATA - Set at login, never changes
        public string SessionId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string UserFullName { get; set; } = string.Empty;
        public string EntityId { get; set; } = string.Empty;
        public string EntityName { get; set; } = string.Empty;
        public string EntityTypeId { get; set; } = string.Empty;
        public string EntityTypeName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastAccessedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastLogin { get; set; }
        public List<int> Roles { get; set; } = new();

        // GENERIC PROPERTY STORAGE - For session parameters that change during session
        private readonly Dictionary<string, string> _properties = new();

        public void SetProperty(string key, string value)
        {
            _properties[key] = value;
        }

        public string? GetProperty(string key)
        {
            return _properties.TryGetValue(key, out var value) ? value : null;
        }

        public bool HasProperty(string key)
        {
            return _properties.ContainsKey(key);
        }

        public void RemoveProperty(string key)
        {
            _properties.Remove(key);
        }

        public Dictionary<string, string> GetAllProperties()
        {
            return new Dictionary<string, string>(_properties);
        }

        // BACKWARD COMPATIBILITY - Keep AdditionalData for existing code
        public Dictionary<string, string> AdditionalData 
        { 
            get => _properties;
            set 
            {
                _properties.Clear();
                foreach (var kvp in value)
                {
                    _properties[kvp.Key] = kvp.Value;
                }
            }
        }
    }
}