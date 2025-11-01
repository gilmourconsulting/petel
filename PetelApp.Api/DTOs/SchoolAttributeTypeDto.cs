namespace PetelApp.Api.DTOs
{
    public class SchoolAttributeTypeDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string HebrewName { get; set; } = string.Empty;
        public string AttributeValueType { get; set; } = string.Empty;
        public int? YearId { get; set; }
        public List<SchoolAttributeTypeValueDto>? PossibleValues { get; set; }
    }

    public class SchoolAttributeTypeValueDto
    {
        public int Id { get; set; }
        public string Value { get; set; } = string.Empty;
        public int Sort_Order { get; set; }
    }
}