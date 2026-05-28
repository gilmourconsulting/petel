namespace Petel.Core.Excel
{
    /// <summary>
    /// Root POCO for the report definition JSON stored in
    /// excel_report_definitions.definition_json.
    ///
    /// Declares what parameters the report accepts and how data sources are
    /// resolved from the entity registry.  The engine uses this at generation
    /// time to fill an Excel template (scalar {{ds.Field}} tokens and
    /// collection blocks {{#ds}} / {{/ds}}).
    /// </summary>
    public class ReportTemplateSchema
    {
        /// <summary>
        /// Runtime parameters the report declares.
        /// The UI reads these to decide which inputs to show the user and
        /// which can be resolved automatically from session context.
        /// </summary>
        public List<ParameterDefinition> Parameters { get; set; } = new();

        /// <summary>Data sources to resolve and inject into the template.</summary>
        public List<DataSourceDefinition> DataSources { get; set; } = new();
    }

    /// <summary>
    /// One parameter declared by the report (e.g. a year picker, council picker).
    /// </summary>
    public class ParameterDefinition
    {
        /// <summary>
        /// Key used in runtimeParams dictionary and in DataSourceDefinition.Filters[].ParamName.
        /// </summary>
        public string Name { get; set; } = "";

        /// <summary>
        /// Control type that the UI renders for this parameter.
        ///
        /// session_entity   – always resolved from the logged-in user's entity (never shown to user)
        /// session_year     – resolved from session; UI can still override it
        /// year_selector    – Hebrew year dropdown
        /// council_selector – Council dropdown
        /// entity_selector  – Entity / organisation dropdown
        /// school_selector  – School dropdown
        /// text             – Free-text input
        /// enum             – Fixed-option select (options defined in OptionsJson)
        /// </summary>
        public string Type { get; set; } = "text";

        /// <summary>Hebrew label shown in the UI parameter modal.</summary>
        public string Label { get; set; } = "";

        /// <summary>When true the report cannot be generated without this value.</summary>
        public bool Required { get; set; } = true;

        /// <summary>
        /// For enum type: JSON array of { value, label } objects.
        /// Ignored for all other types.
        /// </summary>
        public string? OptionsJson { get; set; }

        /// <summary>Optional default value (used when caller does not supply one).</summary>
        public string? DefaultValue { get; set; }
    }

    /// <summary>
    /// One data source resolved by the engine (maps to a registry entity).
    /// </summary>
    public class DataSourceDefinition
    {
        /// <summary>
        /// Logical name used in template placeholders.
        /// e.g. "header" → {{header.Name}}, "students" → {{#students}} / {{students.LastName}}
        /// </summary>
        public string Name { get; set; } = "";

        /// <summary>
        /// Registry entity name as registered in IExcelEntityRegistry.
        /// e.g. "OwnerEntity", "Council", "StudentsWithSchool".
        /// </summary>
        public string Entity { get; set; } = "";

        /// <summary>
        /// "scalar"     – take only the first row; used for header/context data.
        /// "collection" – expand rows in the template using {{#name}} / {{/name}}.
        /// </summary>
        public string Type { get; set; } = "collection";

        /// <summary>
        /// Filters applied in-memory after the entity query.
        /// Uses the same FilterCondition class as ExcelQueryConfig.
        /// ParamName references a key in runtimeParams at generation time.
        /// </summary>
        public List<ExcelQueryConfig.FilterCondition> Filters { get; set; } = new();

        /// <summary>Sort applied to collection data sources before template expansion.</summary>
        public List<ExcelQueryConfig.SortSpec> Sort { get; set; } = new();
    }
}
