namespace Petel.Core.Excel
{
    /// <summary>
    /// Runtime query configuration built by the UI query builder.
    /// Serialised as JSON into excel_report_queries.fields_json / filters_json / sort_json.
    /// </summary>
    public class ExcelQueryConfig
    {
        /// <summary>Entity name (e.g. "Students").</summary>
        public string EntityName { get; set; } = string.Empty;

        /// <summary>Ordered list of columns to include in the output.</summary>
        public List<SelectedField> Fields { get; set; } = new();

        /// <summary>Filter conditions.</summary>
        public List<FilterCondition> Filters { get; set; } = new();

        /// <summary>Sort specification.</summary>
        public List<SortSpec> Sort { get; set; } = new();

        /// <summary>Name for the Excel worksheet tab.</summary>
        public string SheetName { get; set; } = "נתונים";

        // ─── Nested DTOs ──────────────────────────────────────────────────

        public class SelectedField
        {
            /// <summary>Field key from ExcelFieldDescriptor.Name.</summary>
            public string Field { get; set; } = string.Empty;

            /// <summary>Optional override for the column header in the output.</summary>
            public string? LabelOverride { get; set; }
        }

        public class FilterCondition
        {
            public string Field { get; set; } = string.Empty;

            /// <summary>eq | neq | contains | startswith | gt | gte | lt | lte | in | isnull | isnotnull</summary>
            public string Operator { get; set; } = "eq";

            /// <summary>
            /// Literal value, OR a reference to a runtime parameter key
            /// when the value starts with "@" (e.g. "@school_year_id").
            /// </summary>
            public string? Value { get; set; }

            /// <summary>
            /// When set, the actual value is supplied at runtime via the named parameter.
            /// Matches ExcelReportParameter.ParamName.
            /// </summary>
            public string? ParamName { get; set; }
        }

        public class SortSpec
        {
            public string Field { get; set; } = string.Empty;

            /// <summary>"asc" or "desc"</summary>
            public string Direction { get; set; } = "asc";
        }
    }
}
