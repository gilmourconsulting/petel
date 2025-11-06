using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetelApp.Api.Data;
using PetelApp.Api.Session;
using PetelApp.Api.Controllers;

namespace PetelApp.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DocumentsController : BaseController
    {
        private readonly AppDbContext _context;

        public DocumentsController(
            AppDbContext context,
            UserSessionService userSessionService,
            ILogger<DocumentsController> logger)
            : base(userSessionService, logger)
        {
            _context = context;
        }

        /// <summary>
        /// Get documents by entity (school) and year
        /// </summary>
        [HttpGet("by-entity")]
        public async Task<IActionResult> GetDocumentsByEntity(
            [FromQuery] int entityId,
            [FromQuery] int? yearId = null)
        {
            try
            {
                var session = GetCurrentSession();
                _logger.LogInformation("Fetching documents for entityId: {EntityId}, yearId: {YearId}", 
                    entityId, yearId);

                var query = _context.Documents
                    .Include(d => d.DocumentLinks)
                    .Include(d => d.DocumentType)
                    .Where(d => d.DocumentLinks.Any(dl => dl.EntityId == entityId));

                if (yearId.HasValue)
                {
                    query = query.Where(d => d.DocumentType.YearId == yearId.Value);
                }

                var documents = await query
                    .Where(d => d.IsLastVersion)
                    .OrderByDescending(d => d.DocumentTypeId)
                    .Select(d => new
                    {
                        d.Id,
                        d.Description,
                        DocumentType = d.DocumentType.Name,
                        d.Version,
                        d.FileEncoding,
                        FileSize = d.FileBlob != null ? d.FileBlob.Length : 0,
                        HasFile = d.FileBlob != null
                    })
                    .ToListAsync();

                _logger.LogInformation("Retrieved {Count} documents", documents.Count);
                return Ok(documents);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving documents for entityId: {EntityId}", entityId);
                return StatusCode(500, new { error = "שגיאה בטעינת המסמכים" });
            }
        }

        /// <summary>
        /// Download document file
        /// </summary>
        [HttpGet("{id}/download")]
        public async Task<IActionResult> DownloadDocument(long id)
        {
            try
            {
                var document = await _context.Documents
                    .Include(d => d.DocumentType)
                    .FirstOrDefaultAsync(d => d.Id == id);

                if (document == null)
                {
                    return NotFound(new { error = "מסמך לא נמצא" });
                }

                if (document.FileBlob == null)
                {
                    return NotFound(new { error = "קובץ המסמך לא נמצא" });
                }

                var fileName = $"{document.Description ?? document.DocumentType.Name}_{document.Version}.{document.FileEncoding}";
                var contentType = GetContentType(document.FileEncoding);

                return File(document.FileBlob, contentType, fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading document {DocumentId}", id);
                return StatusCode(500, new { error = "שגיאה בהורדת המסמך" });
            }
        }

        /// <summary>
        /// Upload new document
        /// </summary>
        [HttpPost("upload")]
        public async Task<IActionResult> UploadDocument(
            [FromForm] IFormFile file,
            [FromForm] string description,
            [FromForm] int documentTypeId,
            [FromForm] int entityId,
            [FromForm] int? yearId = null)
        {
            try
            {
                var session = GetCurrentSession();

                if (file == null || file.Length == 0)
                {
                    return BadRequest(new { error = "לא הועלה קובץ" });
                }

                // Validate document type
                var documentType = await _context.DocumentTypes.FindAsync(documentTypeId);
                if (documentType == null)
                {
                    return BadRequest(new { error = "סוג מסמך לא תקין" });
                }

                // Get file extension
                var fileExtension = Path.GetExtension(file.FileName).TrimStart('.');

                // Read file to byte array
                byte[] fileBytes;
                using (var memoryStream = new MemoryStream())
                {
                    await file.CopyToAsync(memoryStream);
                    fileBytes = memoryStream.ToArray();
                }

                // Create document record
                var document = new Document
                {
                    Description = description,
                    DocumentTypeId = documentTypeId,
                    StatusId = 1, // Active status
                    FileBlob = fileBytes,
                    FileEncoding = fileExtension,
                    Version = 1,
                    IsLastVersion = true
                };

                _context.Documents.Add(document);
                await _context.SaveChangesAsync();

                // Create document link
                var documentLink = new DocumentLink
                {
                    DocumentId = document.Id,
                    EntityId = entityId
                };

                _context.DocumentLinks.Add(documentLink);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Document uploaded successfully: {DocumentId}", document.Id);

                return Ok(new
                {
                    id = document.Id,
                    description = document.Description,
                    message = "המסמך הועלה בהצלחה"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading document");
                return StatusCode(500, new { error = "שגיאה בהעלאת המסמך" });
            }
        }

        /// <summary>
        /// Delete document
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteDocument(long id)
        {
            try
            {
                var session = GetCurrentSession();

                var document = await _context.Documents
                    .Include(d => d.DocumentLinks)
                    .FirstOrDefaultAsync(d => d.Id == id);

                if (document == null)
                {
                    return NotFound(new { error = "מסמך לא נמצא" });
                }

                // Remove document links
                _context.DocumentLinks.RemoveRange(document.DocumentLinks);

                // Remove document
                _context.Documents.Remove(document);

                await _context.SaveChangesAsync();

                _logger.LogInformation("Document deleted successfully: {DocumentId}", id);

                return Ok(new { message = "המסמך נמחק בהצלחה" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting document {DocumentId}", id);
                return StatusCode(500, new { error = "שגיאה במחיקת המסמך" });
            }
        }

        private string GetContentType(string fileExtension)
        {
            return fileExtension.ToLower() switch
            {
                "pdf" => "application/pdf",
                "doc" => "application/msword",
                "docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                "xls" => "application/vnd.ms-excel",
                "xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "jpg" or "jpeg" => "image/jpeg",
                "png" => "image/png",
                "txt" => "text/plain",
                _ => "application/octet-stream"
            };
        }
    }
}