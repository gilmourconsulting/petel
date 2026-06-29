namespace PetelAssistants.BlazorServer.Models
{
    public class SystemAttributeDto
    {
        public int    Id          { get; set; }
        public string Name        { get; set; } = string.Empty;
        public string Value       { get; set; } = string.Empty;
        public string ValueType   { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public class YearDto
    {
        public int    Id         { get; set; }
        public string YearName   { get; set; } = string.Empty;
        public bool   IsCurrent  { get; set; }
        public bool   IsPrevious { get; set; }
    }

    public class YearContextDto
    {
        public YearDto?       CurrentYear  { get; set; }
        public YearDto?       PreviousYear { get; set; }
        public List<YearDto>  AllYears     { get; set; } = new();
    }

    public class MenuItemDto
    {
        public int    Id        { get; set; }
        public string Name      { get; set; } = string.Empty;
        public string Reference { get; set; } = string.Empty;
        public string Text      { get; set; } = string.Empty;
        public int    SortOrder { get; set; }
    }
}
