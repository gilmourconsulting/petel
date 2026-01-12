using DocumentFormat.OpenXml.InkML;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

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
        
        public string? FileName { get; set; }
        public int Version { get; set; }
        public bool IsLastVersion { get; set; }

        public DateTime CreatedAt { get; set; }

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
        
        // ✅ NEW: Fields for conditional document generation
        [Column("object_element_check")]
        [MaxLength(50)]
        public string? ObjectElementCheck { get; set; }
        
        [Column("object_element_value")]
        [MaxLength(50)]
        public string? ObjectElementValue { get; set; }
        
        [Column("created_at")]
        public DateTime? CreatedAt { get; set; }
        
        [Column("user_id")]
        public int? UserId { get; set; }
    }

    public class DocumentLink
    {
        public long Id { get; set; }
        public long DocumentId { get; set; }
        public int? SchoolStudentId { get; set; }
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