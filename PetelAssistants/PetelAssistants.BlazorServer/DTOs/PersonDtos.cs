namespace PetelAssistants.BlazorServer.DTOs
{
    public class PersonListItemDto
    {
        public int Id { get; set; }
        public string IdNumber { get; set; } = string.Empty;
        public int IdType { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? PhoneSummary { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool HasAllocation { get; set; }
    }

    public class PersonPhoneDto
    {
        public int Id { get; set; }
        public int PhoneTypeId { get; set; }
        public string PhoneTypeCode { get; set; } = string.Empty;
        public string PhoneTypeDisplayName { get; set; } = string.Empty;
        public string? PhoneNumberPrefix { get; set; }
        public string? PhoneNumber { get; set; }
        public bool IsActive { get; set; }
        public DateOnly StartDate { get; set; }
        public DateOnly? EndDate { get; set; }
    }

    public class PersonAddressDto
    {
        public int Id { get; set; }
        public string? Street { get; set; }
        public string? HouseNumber { get; set; }
        public string? City { get; set; }
        public string? PostCode { get; set; }
        public bool IsActive { get; set; }
        public DateOnly StartDate { get; set; }
        public DateOnly? EndDate { get; set; }
    }

    public class PersonSnapshotDto
    {
        public int Id { get; set; }
        public string IdNumber { get; set; } = string.Empty;
        public int IdType { get; set; }
        public int DetailId { get; set; }
        public int Version { get; set; }
        public DateOnly StartDate { get; set; }
        public DateOnly? EndDate { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public int? Gender { get; set; }
        public DateOnly? DateOfBirth { get; set; }
        public string? Email { get; set; }
        public string? Position { get; set; }
        public PersonAddressDto? Address { get; set; }
        public List<PersonPhoneDto> Phones { get; set; } = new();
        public DateTime UpdatedAt { get; set; }
    }

    public class PersonDetailHistoryDto
    {
        public int Id { get; set; }
        public int Version { get; set; }
        public bool IsLastVersion { get; set; }
        public DateOnly StartDate { get; set; }
        public DateOnly? EndDate { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public int? Gender { get; set; }
        public DateOnly? DateOfBirth { get; set; }
        public string? Email { get; set; }
        public string? Position { get; set; }
    }

    public class PersonPhoneInputDto
    {
        public int PhoneTypeId { get; set; }
        public string? PhoneNumberPrefix { get; set; }
        public string? PhoneNumber { get; set; }
    }

    public class PersonAddressInputDto
    {
        public string? Street { get; set; }
        public string? HouseNumber { get; set; }
        public string? City { get; set; }
        public string? PostCode { get; set; }
    }

    public class CreatePersonRequest
    {
        public string IdNumber { get; set; } = string.Empty;
        public int IdType { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public int? Gender { get; set; }
        public DateOnly? DateOfBirth { get; set; }
        public string? Email { get; set; }
        public string? Position { get; set; }
        public DateOnly? EffectiveDate { get; set; }
        public PersonAddressInputDto? Address { get; set; }
        public List<PersonPhoneInputDto> Phones { get; set; } = new();
    }

    public class UpdatePersonRequest
    {
        public string? IdNumber { get; set; }
        public int? IdType { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public int? Gender { get; set; }
        public DateOnly? DateOfBirth { get; set; }
        public string? Email { get; set; }
        public string? Position { get; set; }
        public DateOnly? EffectiveDate { get; set; }
        public PersonAddressInputDto? Address { get; set; }
        public List<PersonPhoneInputDto> Phones { get; set; } = new();
    }

    public class PhoneTypeDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
    }
}
