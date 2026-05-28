namespace PetelATH.BlazorServer.DTOs
{
    public class ReportDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string ReportType { get; set; } = string.Empty;
        public string Format { get; set; } = "excel";
        public bool AllowCrossYear { get; set; }
        public bool RequiresEntityContext { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; }
        /// <summary>Filename of the uploaded template (.xlsx or .docx), or null if not yet uploaded.</summary>
        public string? TemplateFilename { get; set; }
    }
}
