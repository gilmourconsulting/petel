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
    [FromQuery] int? entityId = null,
    [FromQuery] int? yearId = null)
{
    try
    {
        var session = GetCurrentSession();
        
        // Use session values if not provided in query
        int effectiveEntityId = entityId ?? 
            (int.TryParse(session.GetProperty("SelectedSchoolId"), out var sessionSchoolId) 
                ? sessionSchoolId 
                : int.Parse(session.EntityId));
        
        int? effectiveYearId = yearId ?? 
            (int.TryParse(session.GetProperty("SelectedYearId"), out var sessionYearId) 
                ? sessionYearId 
                : (int?)null);

        _logger.LogInformation("Fetching documents for entityId: {EntityId}, yearId: {YearId}", 
            effectiveEntityId, effectiveYearId);

        var query = _context.Documents
            .Include(d => d.DocumentLinks)
            .Include(d => d.DocumentType)
            .Where(d => d.DocumentLinks.Any(dl => dl.EntityId == effectiveEntityId));

        if (effectiveYearId.HasValue)
        {
            query = query.Where(d => d.DocumentType.YearId == effectiveYearId.Value);
        }

        var documents = await query
            .Where(d => d.IsLastVersion)
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => new
            {
                d.Id,
                d.Description,
                DocumentType = d.DocumentType.Name,
                DocumentTypeId = d.DocumentTypeId,
                StatusName = _context.Set<DocumentStatusType>()
                    .Where(s => s.Id == d.StatusId)
                    .Select(s => s.Name)
                    .FirstOrDefault() ?? "לא מוגדר",
                CreatedAt = d.CreatedAt,
                FileSize = d.FileBlob != null ? d.FileBlob.Length : 0,
                HasFile = d.FileBlob != null
            })
            .ToListAsync();

        _logger.LogInformation("Retrieved {Count} documents", documents.Count);
        return Ok(documents);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error retrieving documents");
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
                var session = GetCurrentSession();

                // ✅ Check if this is a view request (from header instead of query string)
                var isViewMode = Request.Headers.ContainsKey("X-View-Mode") &&
                                 Request.Headers["X-View-Mode"] == "inline";

                if (isViewMode)
                {
                    _logger.LogInformation("Document view access: {DocumentId}", id);
                }

                var document = await _context.Documents
                    .Include(d => d.DocumentType)
                    .FirstOrDefaultAsync(d => d.Id == id);

                if (document == null)
                {
                    return NotFound(new { error = "מסמך לא נמצא" });
                }

                if (document.FileBlob == null || document.FileBlob.Length == 0)
                {
                    return NotFound(new { error = "אין קובץ מצורף למסמך" });
                }

                // Determine content type based on file extension
                var contentType = document.FileEncoding?.ToLower() switch
                {
                    "pdf" => "application/pdf",
                    "doc" => "application/msword",
                    "docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                    "xls" => "application/vnd.ms-excel",
                    "xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    "jpg" or "jpeg" => "image/jpeg",
                    "png" => "image/png",
                    "gif" => "image/gif",
                    "txt" => "text/plain",
                    "html" => "text/html",
                    _ => "application/octet-stream"
                };

                // ✅ Use saved filename
                var fileName = !string.IsNullOrEmpty(document.FileName)
                    ? document.FileName
                    : !string.IsNullOrEmpty(document.Description)
                        ? $"{document.Description}.{document.FileEncoding}"
                        : $"document_{document.Id}.{document.FileEncoding}";

                // ✅ Use inline for view mode, attachment for download
                var disposition = isViewMode ? "inline" : "attachment";

                Response.Headers.Append("Content-Disposition", $"{disposition}; filename=\"{fileName}\"");
                Response.Headers.Append("X-Content-Type-Options", "nosniff");

                _logger.LogInformation("Document {Action}: {DocumentId}, FileName: {FileName}, Size: {Size}KB",
                    disposition == "inline" ? "viewed" : "downloaded",
                    id,
                    fileName,
                    document.FileBlob.Length / 1024);

                return File(document.FileBlob, contentType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error accessing document {DocumentId}", id);
                return StatusCode(500, new { error = "שגיאה בגישה למסמך" });
            }
        }

/// <summary>
/// Get all document types
/// </summary>
[HttpGet("types")]
public async Task<IActionResult> GetDocumentTypes()
{
    try
    {
        var documentTypes = await _context.Set<DocumentType>()
            .OrderBy(dt => dt.Name)
            .Select(dt => new
            {
                dt.Id,
                dt.Name,
                dt.Level,
                dt.YearId
            })
            .ToListAsync();

        _logger.LogInformation("Retrieved {Count} document types", documentTypes.Count);
        return Ok(documentTypes);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error retrieving document types");
        return StatusCode(500, new { error = "שגיאה בטעינת סוגי מסמכים" });
    }
}

        /// <summary>
        /// Get document status types
        /// </summary>
        [HttpGet("status-types")]
        public async Task<IActionResult> GetDocumentStatusTypes()
        {
            try
            {
                var statusTypes = await _context.Set<DocumentStatusType>()
                    .OrderBy(s => s.Id)
                    .Select(s => new
                    {
                        s.Id,
                        s.Name
                    })
                    .ToListAsync();

                return Ok(statusTypes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving document status types");
                return StatusCode(500, new { error = "שגיאה בטעינת סוגי סטטוס" });
            }
        }

 /// <summary>
/// Upload new document or replace existing
/// </summary>
[HttpPost("upload")]
public async Task<IActionResult> UploadDocument(
    [FromForm] IFormFile file,
    [FromForm] string? description,
    [FromForm] int documentTypeId,
    [FromForm] int statusId,
    [FromForm] int entityId,
    [FromForm] int? yearId = null,
    [FromForm] long? existingDocumentId = null,
    [FromForm] bool replaceExisting = false)
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

        // Get file extension and original filename
        var fileExtension = Path.GetExtension(file.FileName).TrimStart('.');
        var originalFileName = file.FileName;

        // Read file to byte array
        byte[] fileBytes;
        using (var memoryStream = new MemoryStream())
        {
            await file.CopyToAsync(memoryStream);
            fileBytes = memoryStream.ToArray();
        }

        Document document;

        // ✅ ALWAYS handle as replacement if existingDocumentId is provided
        if (existingDocumentId.HasValue)
        {
            // Get existing document
            var existingDoc = await _context.Documents
                .Include(d => d.DocumentLinks)
                .FirstOrDefaultAsync(d => d.Id == existingDocumentId.Value);

            if (existingDoc == null)
            {
                return NotFound(new { error = "מסמך קיים לא נמצא" });
            }

            // ✅ Mark existing document as not last version
            existingDoc.IsLastVersion = false;
            _context.Documents.Update(existingDoc);

            // ✅ Create new document with incremented version
            document = new Document
            {
                MasterDocumentId = existingDoc.MasterDocumentId ?? existingDoc.Id, // ✅ Preserve or set master
                Description = !string.IsNullOrWhiteSpace(description) ? description : existingDoc.Description,
                DocumentTypeId = existingDoc.DocumentTypeId, // ✅ Preserve document type
                StatusId = statusId,
                FileBlob = fileBytes,
                FileEncoding = fileExtension,
                FileName = originalFileName,
                Version = existingDoc.Version + 1, // ✅ Increment version
                IsLastVersion = true
            };

            _context.Documents.Add(document);
            await _context.SaveChangesAsync();

            // ✅ Copy document links from existing document
            foreach (var existingLink in existingDoc.DocumentLinks)
            {
                var newLink = new DocumentLink
                {
                    DocumentId = document.Id,
                    EntityId = existingLink.EntityId,
                    SchoolStudentId = existingLink.SchoolStudentId
                };
                _context.DocumentLinks.Add(newLink);
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Document replaced. Old: {OldId} (v{OldVer}), New: {NewId} (v{NewVer}), Master: {MasterId}",
                existingDocumentId, existingDoc.Version, document.Id, document.Version, document.MasterDocumentId);

            return Ok(new
            {
                id = document.Id,
                description = document.Description,
                version = document.Version,
                fileSize = fileBytes.Length,
                fileEncoding = fileExtension,
                fileName = originalFileName,
                masterDocumentId = document.MasterDocumentId,
                message = "המסמך הועלה בהצלחה"
            });
        }
        else
        {
            // ✅ NEW document upload - should never happen via upload button
            // Upload button always passes existingDocumentId
            _logger.LogWarning("Upload called without existingDocumentId - this should not happen");
            return BadRequest(new { error = "חסר מזהה מסמך קיים" });
        }
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