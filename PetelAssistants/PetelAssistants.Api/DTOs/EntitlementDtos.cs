namespace PetelAssistants.Api.DTOs
{
    public class YearDetailDto
    {
        public int Id { get; set; }
        public string YearName { get; set; } = string.Empty;
        public DateOnly? StartDate { get; set; }
        public DateOnly? EndDate { get; set; }
        public bool IsCurrent { get; set; }
        public bool IsPrevious { get; set; }
        public bool IsActive { get; set; }
    }

    public class UpdateHebrewYearRequest
    {
        public DateOnly? StartDate { get; set; }
        public DateOnly? EndDate { get; set; }
        public bool IsCurrent { get; set; }
        public bool IsPrevious { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class AssistantTypeDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; }
        public string? Level { get; set; }
    }

    public class CreateAssistantTypeRequest
    {
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int SortOrder { get; set; }
        public string? Level { get; set; }
    }

    public class UpdateAssistantTypeRequest
    {
        public string DisplayName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;
        public string? Level { get; set; }
    }

    public class OrgUnitDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string OrgUnitType { get; set; } = string.Empty;
        public string? OrgUnitTypeDescription { get; set; }
        public bool IsActive { get; set; }
    }

    public class CreateOrgUnitRequest
    {
        public string Name { get; set; } = string.Empty;
        public string OrgUnitType { get; set; } = string.Empty;
    }

    public class UpdateOrgUnitRequest
    {
        public string Name { get; set; } = string.Empty;
    }

    public class MinistryParticipationOptionDto
    {
        public int Id { get; set; }
        public decimal Percentage { get; set; }
        public int DisplayOrder { get; set; }
    }

    public class EntitlementAllocationDto
    {
        public int Id { get; set; }
        public int EntitlementId { get; set; }
        public int PersonId { get; set; }
        public string PersonFullName { get; set; } = string.Empty;
        public DateOnly StartDate { get; set; }
        public DateOnly EndDate { get; set; }
        public decimal Hours { get; set; }
        public string HoursUnit { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }

    public class CreateEntitlementAllocationRequest
    {
        public int PersonId { get; set; }
        public decimal Hours { get; set; }
        public string HoursUnit { get; set; } = string.Empty;
        public DateOnly? StartDate { get; set; }
        public DateOnly? EndDate { get; set; }
    }

    public class EntitlementListItemDto
    {
        public int Id { get; set; }
        public int HebrewYearId { get; set; }
        public int AssistantTypeId { get; set; }
        public string AssistantTypeName { get; set; } = string.Empty;
        public string? AssistantTypeLevel { get; set; }
        public DateOnly StartDate { get; set; }
        public DateOnly EndDate { get; set; }
        public decimal Hours { get; set; }
        public string HoursUnit { get; set; } = string.Empty;
        public decimal MinistryParticipationPct { get; set; }
        public int? SchoolEntityId { get; set; }
        public string? SchoolName { get; set; }
        public string? OrgUnitType { get; set; }
        public string? ClassName { get; set; }
        // Personal entitlement fields (null for institutional)
        public string? PupilIdNumber { get; set; }
        public string? PupilFirstName { get; set; }
        public string? PupilLastName { get; set; }
        public bool IsActive { get; set; }
        // Allocation status
        public decimal AllocatedHours { get; set; }
        public string AllocationStatus { get; set; } = "none"; // "none" | "partial" | "full"
    }

    public class EntitlementDetailDto : EntitlementListItemDto
    {
    }

    public class CreateEntitlementRequest
    {
        public int HebrewYearId { get; set; }
        public int AssistantTypeId { get; set; }
        public DateOnly? StartDate { get; set; }
        public DateOnly? EndDate { get; set; }
        public decimal Hours { get; set; }
        public string HoursUnit { get; set; } = string.Empty;
        public decimal MinistryParticipationPct { get; set; }
        public int? SchoolEntityId { get; set; }
        public string? ClassName { get; set; }
        // Personal entitlement fields (required when assistant type level == "personal")
        public string? PupilIdNumber { get; set; }
        public string? PupilFirstName { get; set; }
        public string? PupilLastName { get; set; }
    }

    public class UpdateEntitlementRequest
    {
        public int AssistantTypeId { get; set; }
        public DateOnly StartDate { get; set; }
        public DateOnly EndDate { get; set; }
        public decimal Hours { get; set; }
        public string HoursUnit { get; set; } = string.Empty;
        public decimal MinistryParticipationPct { get; set; }
        public int? SchoolEntityId { get; set; }
        public string? ClassName { get; set; }
        // Personal entitlement fields (required when assistant type level == "personal")
        public string? PupilIdNumber { get; set; }
        public string? PupilFirstName { get; set; }
        public string? PupilLastName { get; set; }
    }
}
