namespace Petel.Core.Excel
{
    /// <summary>
    /// Resolved entity context passed to the registry when running a report.
    /// This is NOT the raw session — it is the context derived after resolving
    /// admin entity-selector overrides.
    /// </summary>
    public class ExcelEntityContext
    {
        /// <summary>The entity ID to scope data to (school, council, network, etc.).</summary>
        public int EntityId { get; set; }

        /// <summary>
        /// String EntityTypeId from UserSession.
        /// Known values for ATH: "4" = School, "3" = Council/Network, "1" = System Admin.
        /// </summary>
        public string EntityTypeId { get; set; } = string.Empty;

        /// <summary>
        /// The school year ID to filter by.
        /// Null when the report is allowed to be cross-year AND the user elected
        /// not to select a specific year.
        /// </summary>
        public int? SchoolYearId { get; set; }
    }
}
