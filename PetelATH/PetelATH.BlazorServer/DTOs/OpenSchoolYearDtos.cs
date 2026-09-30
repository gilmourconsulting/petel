namespace PetelATH.BlazorServer.DTOs
{
    public class OpenSchoolYearPreviewDto
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public int TargetYearId { get; set; }
        public string TargetYearName { get; set; } = string.Empty;
        public int? PreviousYearId { get; set; }
        public string? PreviousYearName { get; set; }
        public bool AttributeTypesReady { get; set; }
        public List<OpenSchoolYearSchoolDto> Schools { get; set; } = new();
    }

    public class OpenSchoolYearSchoolDto
    {
        public int SchoolEntityId { get; set; }
        public int SourceSchoolYearId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Symbol { get; set; }
        public bool AlreadyOpened { get; set; }
        public List<OpenSchoolYearClassDto> Classes { get; set; } = new();

        public bool Selected { get; set; }
        public bool Expanded { get; set; }
        public string NewLevel { get; set; } = string.Empty;
        public string NewClassNumber { get; set; } = string.Empty;
        public int NewHour { get; set; } = 13;
        public int NewMinute { get; set; }
        public int NewCharacterizationId { get; set; }
    }

    public class OpenSchoolYearClassDto
    {
        public string Level { get; set; } = string.Empty;
        public string ClassNumber { get; set; } = string.Empty;
        public TimeOnly? EndHour { get; set; }
        public int? CharacterizationId { get; set; }
        public string? CharacterizationName { get; set; }
    }

    public class OpenSchoolYearRequest
    {
        public List<OpenSchoolYearSchoolRequest> Schools { get; set; } = new();
    }

    public class OpenSchoolYearSchoolRequest
    {
        public int SchoolEntityId { get; set; }
        public List<OpenSchoolYearClassRequest> Classes { get; set; } = new();
    }

    public class OpenSchoolYearClassRequest
    {
        public string Level { get; set; } = string.Empty;
        public string ClassNumber { get; set; } = string.Empty;
        public TimeOnly? EndHour { get; set; }
        public int? CharacterizationId { get; set; }
    }

    public class OpenSchoolYearResultDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int SchoolsCreated { get; set; }
        public int ClassesCreated { get; set; }
        public int AttributesCopied { get; set; }
        public List<SkippedSchoolAttributeDto> SkippedAttributes { get; set; } = new();
    }

    public class SkippedSchoolAttributeDto
    {
        public int SchoolEntityId { get; set; }
        public string SchoolName { get; set; } = string.Empty;
        public string AttributeName { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
    }
}
