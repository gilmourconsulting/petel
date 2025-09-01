using System;
using System.Collections.Generic;

namespace PetelApp.Api.Session
{
    /// <summary>
    /// Holds user-specific session data for multi-tenant educational SaaS
    /// Follows authentication & session management patterns
    /// </summary>
    public class UserSession
    {
        public int UserId { get; set; }
        public string UserFullName { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;
        public int TenantId { get; set; }
        public string TenantName { get; set; } = string.Empty;
        public int EntityTypeId { get; set; } = 0;
        public string EntityTypeName { get; set; } = string.Empty;
        public List<string> Roles { get; set; } = new List<string>();
        public DateTime LoginTime { get; set; } = DateTime.UtcNow;
        public List<int> AllowedActions { get; set; } = new List<int>();

        // System Attributes session data following system attributes pattern
        public Dictionary<string, string> SystemAttributes { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, int?> SystemAttributeForeignIds { get; set; } = new Dictionary<string, int?>();
        public DateTime? SystemAttributesLastLoaded { get; set; }

        // Selected year properties for system attributes pattern
        public int? SelectedYear { get; set; }
        public string SelectedYearType { get; set; } = string.Empty;
        public string SelectedYearValue { get; set; } = string.Empty;

        // School year context for multi-tenant navigation
        public int? SelectedSchoolId { get; set; }
        public string SelectedSchoolName { get; set; } = string.Empty;

        // Hours Budget session data
        public int? CurrentSchoolYearId { get; set; }
        public DateTime? HoursBudgetsLastLoaded { get; set; }
        public decimal? TotalAllocatedHoursBudget { get; set; }
        public decimal? TotalUsedHoursBudget { get; set; }
        public decimal? TotalRemainingHoursBudget { get; set; }
        public int? HoursBudgetCount { get; set; }
    }
}