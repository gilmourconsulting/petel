namespace PetelATH.BlazorServer.DTOs
{
    public class ExcelReportDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string ReportType { get; set; } = string.Empty;
        public bool AllowCrossYear { get; set; }
        public bool RequiresEntityContext { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; }
        /// <summary>Filename of the uploaded xlsx template, or null if no template has been uploaded yet.</summary>
        public string? TemplateFilename { get; set; }
    }
}
