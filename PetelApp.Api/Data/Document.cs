namespace PetelApp.Api.Data
{
    public class Document
    {
        public long Id { get; set; }
        public long? MasterDocumentId { get; set; }
        public string? Description { get; set; }
        public int DocumentTypeId { get; set; }
        public int StatusId { get; set; }
        public byte[]? FileBlob { get; set; }
        public string FileEncoding { get; set; } = string.Empty;
        public int Version { get; set; }
        public bool IsLastVersion { get; set; }

        // Navigation properties
        public DocumentType? DocumentType { get; set; }
        public ICollection<DocumentLink> DocumentLinks { get; set; } = new List<DocumentLink>();
    }

    public class DocumentType
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Level { get; set; } = string.Empty;
        public int? YearId { get; set; }
    }

    public class DocumentLink
    {
        public long Id { get; set; }
        public long DocumentId { get; set; }
        public long? SchoolStudentId { get; set; }
        public long? EntityId { get; set; }

        // Navigation properties
        public Document? Document { get; set; }
    }

    public class DocumentStatusType
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}