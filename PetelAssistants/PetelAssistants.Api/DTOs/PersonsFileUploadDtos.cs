namespace PetelAssistants.Api.DTOs
{
    public class PersonFileRow
    {
        public int RowNumber { get; set; }
        public string IdNumber { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
    }

    public class PersonsFileProcessingResult
    {
        public int Created { get; set; }
        public int Skipped { get; set; }
        public List<string> Errors { get; set; } = new();
    }

    public class PersonsFileUploadRequest
    {
        public IFormFile File { get; set; } = null!;
        public string? MappingJson { get; set; }
    }

    public class PersonsFilePreviewRequest
    {
        public IFormFile File { get; set; } = null!;
    }
}
