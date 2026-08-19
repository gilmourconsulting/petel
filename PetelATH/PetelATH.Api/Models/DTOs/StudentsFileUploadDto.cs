using System.Collections.Generic;

namespace PetelATH.Api.Models.DTOs
{
    /// <summary>
    /// DTO for API-based students file upload.
    /// </summary>
    public class StudentsFileUploadDto
    {
        public int? SchoolId { get; set; }
        public int? SchoolYearId { get; set; }
        public string? SchoolSymbol { get; set; }
        public string? HebrewYear { get; set; }
        public string? FileName { get; set; }
        public string? FileBase64 { get; set; }
        public Dictionary<string, string>? Mapping { get; set; }
    }

    public static class StudentUploadPromptType
    {
        public const string ReplaceCouncil = "ReplaceCouncil";
        public const string SplitCouncil = "SplitCouncil";
        public const string SplitCouncilBlocked = "SplitCouncilBlocked";
        public const string SameEndCouncilSplit = "SameEndCouncilSplit";
        public const string VerifyDates = "VerifyDates";
        public const string MultiPeriod = "MultiPeriod";
    }

    public static class SameCouncilApplyMode
    {
        public const string KeepBoth = "KeepBoth";
        public const string Correction = "Correction";
    }

    public class StudentUploadPeriodDto
    {
        public DateOnly? StartDate { get; set; }
        public DateOnly? EndDate { get; set; }
        public int? CouncilId { get; set; }
        public string? CouncilName { get; set; }

        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public int? Gender { get; set; }
        public int ClassId { get; set; }
        public int? DisabilityCategory { get; set; }
        public string Street { get; set; } = string.Empty;
        public string HouseNumber { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string PostCode { get; set; } = string.Empty;
    }

    /// <summary>
    /// Date/council confirmation item returned after the first upload pass
    /// and posted back for accepted changes. One item per student.
    /// </summary>
    public class StudentUploadPendingItem
    {
        public string Type { get; set; } = string.Empty;
        public bool IsBlocked { get; set; }
        public string Question { get; set; } = string.Empty;
        public int ExistingStudentId { get; set; }
        public string IdNumber { get; set; } = string.Empty;
        public string StudentName { get; set; } = string.Empty;

        public DateOnly? ExistingStartDate { get; set; }
        public DateOnly? ExistingEndDate { get; set; }
        public int? ExistingCouncilId { get; set; }
        public string? ExistingCouncilName { get; set; }

        public DateOnly? ProposedStartDate { get; set; }
        public DateOnly? ProposedEndDate { get; set; }
        public int? ProposedCouncilId { get; set; }
        public string? ProposedCouncilName { get; set; }

        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public int? Gender { get; set; }
        public int ClassId { get; set; }
        public int? DisabilityCategory { get; set; }
        public string Street { get; set; } = string.Empty;
        public string HouseNumber { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string PostCode { get; set; } = string.Empty;

        public List<StudentUploadPeriodDto> ExistingPeriods { get; set; } = new();
        public List<StudentUploadPeriodDto> ProposedPeriods { get; set; } = new();
        public List<StudentUploadPeriodDto> SuggestedUpdates { get; set; } = new();

        public bool RequiresSameCouncilChoice { get; set; }
        public string? SameCouncilApplyMode { get; set; }
    }

    public class ConfirmStudentsUploadRequest
    {
        public List<StudentUploadPendingItem> Accepted { get; set; } = new();
    }
}
