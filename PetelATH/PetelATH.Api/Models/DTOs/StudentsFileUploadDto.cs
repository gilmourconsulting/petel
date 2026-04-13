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
}