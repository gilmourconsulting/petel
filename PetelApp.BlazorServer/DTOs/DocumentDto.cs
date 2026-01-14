namespace PetelApp.BlazorServer.DTOs
{
    public class DocumentDto
    {
        public int Id { get; set; }
        public string DocumentType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string StatusName { get; set; } = string.Empty;
        public string? EntityName { get; set; }
        public DateTime CreatedAt { get; set; }
        public long FileSize { get; set; }
        public int DocumentTypeId { get; set; }
        public int StatusId { get; set; }
    }
}
