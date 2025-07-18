using System;
using System.Collections.Generic;

namespace PetelApp.Api.Session
{
    /// <summary>
    /// Holds user-specific session data for the duration of the user's connection.
    /// </summary>
    public class UserSession
    {
        public int UserId { get; set; }
        public string UserFullName { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;
        public int TenantId { get; set; }
        public string TenantName { get; set; } = string.Empty;
        public List<string> Roles { get; set; } = new List<string>();
        public DateTime LoginTime { get; set; } = DateTime.UtcNow;
        public List<int> AllowedActions { get; set; } = new List<int>();

        // Hours Budget session data
        public int? CurrentSchoolYearId { get; set; }
        public DateTime? HoursBudgetsLastLoaded { get; set; }
        public decimal? TotalAllocatedHoursBudget { get; set; }
        public decimal? TotalUsedHoursBudget { get; set; }
        public decimal? TotalRemainingHoursBudget { get; set; }
        public int? HoursBudgetCount { get; set; }

        // Add more properties as needed for your application
    }
}