namespace PetelApp.BlazorServer.DTOs
{
    public class SchoolAttributeDto
    {
        public int Id { get; set; }
        public int AttributeTypeId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string HebrewName { get; set; } = string.Empty;
        public string ValueType { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string DisplayValue { get; set; } = string.Empty;
        public int Version { get; set; }
    }

    public class SchoolAttributeTypeDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string HebrewName { get; set; } = string.Empty;
        public string AttributeValueType { get; set; } = string.Empty; // "Boolean", "Integer", "Decimal", "List", "String"
        public int YearId { get; set; }
        public int SortOrder { get; set; }
        public List<SchoolAttributeTypeValueDto> PossibleValues { get; set; } = new();
    }

    public class SchoolAttributeTypeValueDto
    {
        public int Id { get; set; }
        public string Value { get; set; } = string.Empty;
        public int Sort_Order { get; set; }
    }

    public class SchoolAttributeTypesResponse
    {
        public bool Success { get; set; }
        public List<SchoolAttributeTypeDto> Data { get; set; } = new();
        public string Message { get; set; } = string.Empty;
    }

    public class SchoolAttributesResponse
    {
        public bool Success { get; set; }
        public List<SchoolAttributeDto> Data { get; set; } = new();
        public string? Message { get; set; }
    }

    public class SaveSchoolAttributesRequest
    {
        public int SchoolYearId { get; set; }
        public List<SchoolAttributeValueDto> Attributes { get; set; } = new();
    }

    public class SchoolAttributeValueDto
    {
        public int AttributeTypeId { get; set; }
        public string Value { get; set; } = string.Empty;
    }
}
