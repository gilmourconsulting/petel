using System;
using System.Collections.Generic;
using System.Text.Json.Serialization; // ADD THIS if using JSON attributes

namespace PetelApp.Api.Session
{
    /// <summary>
    /// User session model with tenant ID maintained but not enforced
    /// </summary>
    public class UserSession
    {
        public string SessionId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string UserFullName { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        
        // Replace TenantId with EntityId
        public string EntityId { get; set; } = string.Empty;  // User's Entity ID preserved throughout session
        public string EntityName { get; set; } = string.Empty;
        
        public string EntityTypeId { get; set; } = string.Empty;
        public string EntityTypeName { get; set; } = string.Empty;
        public string SelectedSchoolId { get; set; } = string.Empty;
        public string SelectedSchoolName { get; set; } = string.Empty;
        public string SelectedYearId { get; set; } = string.Empty;
        public string SelectedYearType { get; set; } = string.Empty;
        public string SelectedYearValue { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastAccessedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastLogin { get; set; }
        public DateTime LastActivity { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = true;
        public Dictionary<string, string> AdditionalData { get; set; } = new();
        public List<int> Roles { get; set; } = new();
        
        // Support properties for system attributes
        public Dictionary<string, object> SystemAttributes { get; set; } = new();
        public Dictionary<string, object> SystemAttributeForeignIds { get; set; } = new();
        public DateTime? SystemAttributesLastLoaded { get; set; }
        public object? SelectedYear { get; set; }
        public Dictionary<string, string> SessionData { get; set; } = new Dictionary<string, string>();
    }
}