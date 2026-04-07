namespace PetelApp.BlazorServer.DTOs
{
    public class DocumentDto
    {
        public long Id { get; set; }
        public string DocumentType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string StatusName { get; set; } = string.Empty;
        public string? EntityName { get; set; }
        public DateTime CreatedAt { get; set; }
        public long FileSize { get; set; }
        public int DocumentTypeId { get; set; }
        public int StatusId { get; set; }
        public int? DocumentStatusId { get; set; }
        public int? UserId { get; set; }
        public string? Username { get; set; }
    }

    public class DocumentStatusTypeDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class DocumentTypeItemDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Level { get; set; }
    }
}
