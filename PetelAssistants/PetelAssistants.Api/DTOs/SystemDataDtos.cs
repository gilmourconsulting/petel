namespace PetelAssistants.Api.DTOs
{
    public class SystemAttributeAdminDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string ValueType { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    public class CreateSystemAttributeRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string? ValueType { get; set; }
        public string? Description { get; set; }
    }

    public class UpdateSystemAttributeRequest
    {
        public string Value { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? ValueType { get; set; }
    }

    public class EntityTypeDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsActive { get; set; }
    }

    public class CreateEntityTypeRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    public class UpdateEntityTypeRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class CreateHebrewYearRequest
    {
        public string YearName { get; set; } = string.Empty;
        public DateOnly? StartDate { get; set; }
        public DateOnly? EndDate { get; set; }
        public bool IsCurrent { get; set; }
        public bool IsPrevious { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class CreateMinistryParticipationOptionRequest
    {
        public decimal Percentage { get; set; }
        public int DisplayOrder { get; set; }
    }

    public class UpdateMinistryParticipationOptionRequest
    {
        public decimal Percentage { get; set; }
        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class MeitarDataFilterValueDto
    {
        public int Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string FilterField { get; set; } = string.Empty;
        public string FilterValue { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public int DisplayOrder { get; set; }
    }

    public class CreateMeitarDataFilterValueRequest
    {
        public string FileName { get; set; } = string.Empty;
        public string FilterField { get; set; } = string.Empty;
        public string FilterValue { get; set; } = string.Empty;
        public int DisplayOrder { get; set; }
    }

    public class UpdateMeitarDataFilterValueRequest
    {
        public string FileName { get; set; } = string.Empty;
        public string FilterField { get; set; } = string.Empty;
        public string FilterValue { get; set; } = string.Empty;
        public int DisplayOrder { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class MeitarTopicDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? PositionType { get; set; }
        public int? AssistantTypeId { get; set; }
        public bool IsActive { get; set; }
    }

    public class CreateMeitarTopicRequest
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? PositionType { get; set; }
        public int? AssistantTypeId { get; set; }
    }

    public class UpdateMeitarTopicRequest
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? PositionType { get; set; }
        public int? AssistantTypeId { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
