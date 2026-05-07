namespace Petel.Core.Excel
{
    /// <summary>
    /// Describes a single exportable field on an entity.
    /// Used by the Blazor query builder to render column/filter pickers.
    /// </summary>
    public class ExcelFieldDescriptor
    {
        /// <summary>Internal field key (matches property name or query alias).</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Hebrew display label for the UI.</summary>
        public string LabelHe { get; set; } = string.Empty;

        /// <summary>
        /// Data type hint used by the filter editor.
        /// "text" | "number" | "date" | "boolean" | "enum"
        /// </summary>
        public string Type { get; set; } = "text";

        /// <summary>True when this field may be used as a filter condition.</summary>
        public bool IsFilterable { get; set; } = true;

        /// <summary>True when this field may be used for sorting.</summary>
        public bool IsSortable { get; set; } = true;

        /// <summary>
        /// Optional: for enum fields, provide label-value pairs.
        /// Format: [{Value: "0", Label: "לא פעיל"}, ...]
        /// </summary>
        public List<EnumOption>? EnumOptions { get; set; }

        public class EnumOption
        {
            public string Value { get; set; } = string.Empty;
            public string Label { get; set; } = string.Empty;
        }
    }

    /// <summary>
    /// Describes a complete exportable entity (e.g. Students, Schools, Transactions).
    /// </summary>
    public class ExcelEntityDescriptor
    {
        /// <summary>Internal entity name key (e.g. "Students", "Schools").</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Hebrew label for the UI.</summary>
        public string LabelHe { get; set; } = string.Empty;

        /// <summary>
        /// When true, this entity may appear in reports with AllowCrossYear=true.
        /// Server enforces this rule — financial/account entities only.
        /// </summary>
        public bool IsAccountEntity { get; set; } = false;

        /// <summary>All exportable fields for this entity.</summary>
        public List<ExcelFieldDescriptor> Fields { get; set; } = new();
    }
}
