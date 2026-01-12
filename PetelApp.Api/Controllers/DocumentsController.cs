using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetelApp.Api.Data;
using PetelApp.Api.Session;
using PetelApp.Api.Controllers;
using DocumentFormat.OpenXml.Bibliography;

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
                // ✅ Check for null session
                if (session == null)
                {
                    _logger.LogError("No valid session found");
                    return Unauthorized(new { success = false, message = "לא נמצאה הפעלה פעילה. אנא התחבר מחדש." });
                }

                // Use session values if not provided in query
                int effectiveEntityId = entityId ??
                    (int.TryParse(session.GetProperty("SelectedSchoolId") ?? "", out var sessionSchoolId)
                        ? sessionSchoolId
                        : int.Parse(session.EntityId));

                int? effectiveYearId = yearId ??
                    (int.TryParse(session.GetProperty("SelectedYearId") ?? "", out var sessionYearId)
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
        /// Get documents by student master ID (works across all student versions)
        /// </summary>
        [HttpGet("by-student-id")]
        public async Task<IActionResult> GetDocumentsByStudentId(
            [FromQuery] long? studentId = null)
        {
            try
            {
                var session = GetCurrentSession();
                // ✅ Check for null session
                if (session == null)
                {
                    _logger.LogError("No valid session found");
                    return Unauthorized(new { success = false, message = "לא נמצאה הפעלה פעילה. אנא התחבר מחדש." });
                }

                // Use session values if not provided in query
                long effectiveStudentId = studentId ??
                    (long.TryParse(session.GetProperty("SelectedStudentId") ?? "", out var sessionStudentId)
                        ? sessionStudentId
                        : throw new Exception("No student ID provided and none in session"));

                _logger.LogInformation("Fetching documents for studentId: {StudentId}", effectiveStudentId);

                // ✅ NEW: Get the master_student_id for the provided student ID
                var student = await _context.SchoolStudents
                    .Where(s => s.Id == effectiveStudentId)
                    .Select(s => new { s.Id, s.MasterStudentId })
                    .FirstOrDefaultAsync();

                if (student == null)
                {
                    _logger.LogWarning("Student not found: {StudentId}", effectiveStudentId);
                    return NotFound(new { success = false, message = "תלמיד לא נמצא" });
                }

                _logger.LogInformation("Resolved student {StudentId} to master_student_id: {MasterStudentId}",
                    effectiveStudentId, student.MasterStudentId);

                // ✅ NEW: Get all student IDs that share this master_student_id (all versions)
                var allStudentVersionIds = await _context.SchoolStudents
                    .Where(s => s.MasterStudentId == student.MasterStudentId)
                    .Select(s => s.Id)
                    .ToListAsync();

                _logger.LogInformation("Found {Count} versions for master_student_id {MasterStudentId}",
                    allStudentVersionIds.Count, student.MasterStudentId);

                // ✅ Query documents linked to ANY version of this student
                var documents = await _context.Documents
                    .Include(d => d.DocumentLinks)
                    .Include(d => d.DocumentType)
                    .Where(d => d.DocumentLinks.Any(dl =>
                        dl.SchoolStudentId.HasValue &&
                        allStudentVersionIds.Contains(dl.SchoolStudentId.Value)))
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
                        HasFile = d.FileBlob != null,
                        // ✅ Include which student version this document is linked to
                        LinkedStudentId = d.DocumentLinks
                            .Where(dl => dl.SchoolStudentId.HasValue &&
                                         allStudentVersionIds.Contains(dl.SchoolStudentId.Value))
                            .Select(dl => dl.SchoolStudentId)
                            .FirstOrDefault(),
                        MasterStudentId = student.MasterStudentId
                    })
                    .ToListAsync();

                _logger.LogInformation("Retrieved {Count} documents for master_student_id {MasterStudentId}",
                    documents.Count, student.MasterStudentId);

                return Ok(documents);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving documents for student");
                return StatusCode(500, new { success = false, error = "שגיאה בטעינת המסמכים" });
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
        /// Get documents for user's entity and all owned entities (excluding schools)
        /// Used for entity-level document management
        /// </summary>
        [HttpGet("by-entity-hierarchy")]
        public async Task<IActionResult> GetDocumentsByEntityHierarchy(
            [FromQuery] int? yearId = null)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    _logger.LogError("No valid session found");
                    return Unauthorized(new { success = false, message = "לא נמצאה הפעלה פעילה. אנא התחבר מחדש." });
                }

                var userEntityId = int.Parse(session.EntityId);

                int? effectiveYearId = yearId ??
                    (int.TryParse(session.GetProperty("SelectedYearId") ?? "", out var sessionYearId)
                        ? sessionYearId
                        : (int?)null);

                _logger.LogInformation("Fetching entity hierarchy documents for entityId: {EntityId}, yearId: {YearId}",
                    userEntityId, effectiveYearId);

                // Get user's entity and all owned entities (excluding schools - entity types 1 and 4)
                var notSchoolTypes = new[] { 1, 4 };

                var entityIds = await _context.Entities
                    .Where(e => e.IsActive &&
                           !notSchoolTypes.Contains(e.EntityTypeId) &&
                           (e.Id == userEntityId || e.OwnerId == userEntityId))
                    .Select(e => new { e.Id, e.Name })
                    .ToListAsync();

                var entityIdList = entityIds.Select(e => e.Id).ToList();

                // Create a dictionary for fast entity name lookup
                var entityIdToNameMap = entityIds.ToDictionary(e => e.Id, e => e.Name);

                _logger.LogInformation("Found {Count} entities in hierarchy (including owner): {EntityIds}",
                    entityIds.Count, string.Join(", ", entityIdList));

                // Query documents for all entities in hierarchy
                var query = _context.Documents
                    .Include(d => d.DocumentLinks)
                    .Include(d => d.DocumentType)
                    .Where(d => d.DocumentLinks.Any(dl => dl.EntityId.HasValue &&
                                                           entityIdList.Contains((int)dl.EntityId.Value)));

                if (effectiveYearId.HasValue)
                {
                    query = query.Where(d => d.DocumentType.YearId == effectiveYearId.Value);
                }

                // ✅ First get the documents with their linked entity IDs
                var documentsWithEntityIds = await query
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
                        HasFile = d.FileBlob != null,
                        // Get the entity ID from document links
                        EntityId = d.DocumentLinks
                            .Where(dl => dl.EntityId.HasValue)
                            .Select(dl => (int)dl.EntityId.Value)
                            .FirstOrDefault()
                    })
                    .ToListAsync();

                // ✅ Then add entity names using the in-memory dictionary
                var documents = documentsWithEntityIds.Select(d => new
                {
                    d.Id,
                    d.Description,
                    d.DocumentType,
                    d.DocumentTypeId,
                    d.StatusName,
                    d.CreatedAt,
                    d.FileSize,
                    d.HasFile,
                    d.EntityId,
                    // Lookup entity name from dictionary
                    EntityName = entityIdToNameMap.TryGetValue(d.EntityId, out var name) ? name : "לא ידוע"
                }).ToList();

                _logger.LogInformation("Retrieved {Count} documents for entity hierarchy", documents.Count);
                return Ok(documents);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving entity hierarchy documents");
                return StatusCode(500, new { error = "שגיאה בטעינת מסמכי הישות" });
            }
        }

        /// <summary>
        /// Generate missing documents for a student based on document types for the year
        /// Level = תלמיד, filtered by category if applicable
        /// </summary>
        [HttpPost("generate-student-documents")]
        public async Task<IActionResult> GenerateStudentDocuments(
            [FromQuery] int studentId,
            [FromQuery] int yearId)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                _logger.LogInformation("Generating documents for student {StudentId} in year {YearId}",
                    studentId, yearId);

                // ✅ Get student with master_student_id
                var student = await _context.SchoolStudents
                    .Where(s => s.Id == studentId)
                    .Select(s => new
                    {
                        s.Id,
                        s.MasterStudentId,
                        s.DisabilityCategory,
                        s.SchoolYearId
                    })
                    .FirstOrDefaultAsync();

                if (student == null)
                {
                    return NotFound(new { success = false, message = "תלמיד לא נמצא" });
                }

                // ✅ Get all document types for student level (תלמיד) in this year
                var documentTypes = await _context.DocumentTypes
                    .Where(dt => dt.YearId == yearId && dt.Level == "תלמיד")
                    .ToListAsync();

                _logger.LogInformation("Found {Count} document types for year {YearId}",
                    documentTypes.Count, yearId);

                // ✅ Get existing documents for this master_student_id
                var allStudentVersionIds = await _context.SchoolStudents
                    .Where(s => s.MasterStudentId == student.MasterStudentId)
                    .Select(s => s.Id)
                    .ToListAsync();

                var existingDocumentTypeIds = await _context.Documents
                    .Include(d => d.DocumentLinks)
                    .Where(d => d.DocumentLinks.Any(dl =>
                        dl.SchoolStudentId.HasValue &&
                        allStudentVersionIds.Contains(dl.SchoolStudentId.Value)))
                    .Where(d => d.IsLastVersion)
                    .Select(d => d.DocumentTypeId)
                    .Distinct()
                    .ToListAsync();

                _logger.LogInformation("Student already has {Count} document types",
                    existingDocumentTypeIds.Count);

                int addedCount = 0;
                int skippedCount = 0;
                int notRequiredCount = 0;

                foreach (var docType in documentTypes)
                {
                    // ✅ Skip if document type already exists for this student
                    if (existingDocumentTypeIds.Contains(docType.Id))
                    {
                        skippedCount++;
                        _logger.LogInformation("Skipping document type {TypeId} - already exists", docType.Id);
                        continue;
                    }

                    // ✅ Check object_element_check filter
                    if (!string.IsNullOrEmpty(docType.ObjectElementCheck))
                    {
                        // If check type is "category", verify student's disability_category matches
                        if (docType.ObjectElementCheck.Equals("category", StringComparison.OrdinalIgnoreCase))
                        {
                            var studentCategory = student.DisabilityCategory?.ToString();
                            if (string.IsNullOrEmpty(studentCategory) ||
                                !studentCategory.Equals(docType.ObjectElementValue, StringComparison.OrdinalIgnoreCase))
                            {
                                notRequiredCount++;
                                _logger.LogInformation(
                                    "Skipping document type {TypeId} - category mismatch (student: {StudentCat}, required: {RequiredCat})",
                                    docType.Id, studentCategory, docType.ObjectElementValue);
                                continue;
                            }
                        }
                    }


                    // ✅ Create new document with default status and truncated description
                    var newDocument = new Document
                    {
                        Description = null,
                        DocumentTypeId = docType.Id,
                        StatusId = 1,  // Default status
                        Version = 0,
                        IsLastVersion = true,
                        CreatedAt = DateTime.UtcNow,
                        MasterDocumentId = null,
                        FileBlob = null,
                        FileEncoding = string.Empty
                    };

                    _context.Documents.Add(newDocument);
                    await _context.SaveChangesAsync();

                    // ✅ Set master document ID after creation
                    await SetMasterDocumentId(newDocument.Id);

                    // ✅ Create document link using master_student_id
                    var documentLink = new DocumentLink
                    {
                        DocumentId = newDocument.Id,
                        SchoolStudentId = studentId,  // Link to current version
                        EntityId = null
                    };

                    _context.Set<DocumentLink>().Add(documentLink);
                    addedCount++;

                    _logger.LogInformation("Created document for type {TypeId} ({TypeName})",
                        docType.Id, docType.Name);
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("Document generation complete: {Added} added, {Skipped} skipped (already exist), {NotRequired} not required",
                    addedCount, skippedCount, notRequiredCount);

                return Ok(new
                {
                    success = true,
                    message = $"נוספו לרשימה {addedCount} מסמכים ",
                    addedCount,
                    skippedCount,
                    notRequiredCount,
                    totalTypes = documentTypes.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating student documents");
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה ביצירת מסמכים",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// Generate missing documents for a school based on document types for the year
        /// Level = בית ספר, filtered by school attribute type if applicable
        /// </summary>
        [HttpPost("generate-school-documents")]
        public async Task<IActionResult> GenerateSchoolDocuments(
            [FromQuery] int schoolId,
            [FromQuery] int yearId,
            [FromQuery] int? schoolYearId = null)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                _logger.LogInformation("Generating documents for school  {SchoolId} in year {YearId}",
                    schoolId, yearId);


                // ✅ Get school attributes for this specific school year
                var schoolAttributes = await _context.SchoolAttributes
                    .Include(sa => sa.SchoolAttributeType)
                    .Where(sa => sa.SchoolYearId == schoolYearId && sa.IsLastVersion)
                    .ToListAsync();

                _logger.LogInformation("Found {Count} school attributes for school_year_id {SchoolYearId}",
                    schoolAttributes.Count, schoolYearId);
                // ✅ Get all document types for school level (בית ספר) in this year
                var documentTypes = await _context.DocumentTypes
                    .Where(dt => dt.YearId == yearId && dt.Level == "בית ספר")
                    .ToListAsync();

                _logger.LogInformation("Found {Count} document types for year {YearId}",
                    documentTypes.Count, yearId);

                // ✅ Get existing documents for this entity
                var existingDocumentTypeIds = await _context.Documents
                    .Include(d => d.DocumentLinks)
                    .Where(d => d.DocumentLinks.Any(dl => dl.EntityId == schoolId))
                    .Where(d => d.IsLastVersion)
                    .Select(d => d.DocumentTypeId)
                    .Distinct()
                    .ToListAsync();

                _logger.LogInformation("School already has {Count} document types",
                    existingDocumentTypeIds.Count);

                int addedCount = 0;
                int skippedCount = 0;
                int notRequiredCount = 0;
                foreach (var docType in documentTypes)
                {
                    // ✅ Skip if document type already exists for this school
                    if (existingDocumentTypeIds.Contains(docType.Id))
                    {
                        skippedCount++;
                        _logger.LogInformation("Skipping document type {TypeId} - already exists", docType.Id);
                        continue;
                    }

                    // ✅ Check object_element_check filter (school attribute type)
                    if (!string.IsNullOrEmpty(docType.ObjectElementCheck))
                    {
                        // Find school attribute with matching type name
                        var matchingAttribute = schoolAttributes
                            .FirstOrDefault(sa =>
                                sa.SchoolAttributeType != null &&
                                sa.SchoolAttributeType.Name.Equals(
                                    docType.ObjectElementCheck,
                                    StringComparison.OrdinalIgnoreCase));

                        if (!string.IsNullOrEmpty(docType.ObjectElementValue))
                        {

                            if (docType.ObjectElementCheck.Equals("additional studies", StringComparison.OrdinalIgnoreCase) ||
                                docType.ObjectElementCheck.Equals("תל\"ן", StringComparison.OrdinalIgnoreCase))
                            {


                                // Check if there are any additional study programs for this school year
                                var hasAdditionalStudies = await _context.SchoolAdditionalStudyPrograms
                                    .AnyAsync(sasp => sasp.SchoolYearId == schoolYearId && sasp.IsLastVersion);

                                if (!hasAdditionalStudies)
                                {
                                    notRequiredCount++;
                                    _logger.LogInformation(
                                        "Document type {TypeId} not required - no additional study programs found for school_year_id {SchoolYearId}",
                                        docType.Id, schoolYearId);
                                    continue;
                                }

                                // If ObjectElementValue is specified, treat it as boolean check
                                if (!string.IsNullOrEmpty(docType.ObjectElementValue))
                                {
                                    bool requiredValue = docType.ObjectElementValue.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                                                         docType.ObjectElementValue == "1";

                                    if (!requiredValue)
                                    {
                                        // Required value is false, but we have programs - skip
                                        notRequiredCount++;
                                        _logger.LogInformation(
                                            "Document type {TypeId} not required - additional studies exist but required value is false",
                                            docType.Id);
                                        continue;
                                    }
                                }
                            }
                            else
                            {
                                var attributeType = matchingAttribute.SchoolAttributeType?.AttributeValueType?.ToLower();
                                var attributeValue = matchingAttribute.Value;
                                var requiredValue = docType.ObjectElementValue;

                                bool valueMatches = false;

                                // ✅ Handle numeric types: 0 = false, non-zero = true
                                if (attributeType == "integer" || attributeType == "decimal")
                                {
                                    // Parse the attribute value as decimal
                                    if (decimal.TryParse(attributeValue, out var numericValue))
                                    {
                                        // Parse required value as boolean (true/false or 1/0)
                                        bool requiredBool = false;
                                        if (bool.TryParse(requiredValue, out var boolValue))
                                        {
                                            requiredBool = boolValue;
                                        }
                                        else if (requiredValue == "1")
                                        {
                                            requiredBool = true;
                                        }

                                        // Compare: 0 = false, non-zero = true
                                        bool actualBool = numericValue != 0;
                                        valueMatches = actualBool == requiredBool;

                                        _logger.LogInformation(
                                            "Document type {TypeId} - numeric attribute check: value={NumericVal}, actual={ActualBool}, required={RequiredBool}, matches={Matches}",
                                            docType.Id, numericValue, actualBool, requiredBool, valueMatches);
                                    }
                                    else
                                    {
                                        _logger.LogWarning(
                                            "Document type {TypeId} - failed to parse numeric attribute value '{Value}'",
                                            docType.Id, attributeValue);
                                    }
                                }
                                else
                                {
                                    // ✅ String comparison for other types
                                    valueMatches = attributeValue?.Equals(requiredValue, StringComparison.OrdinalIgnoreCase) ?? false;
                                }

                                if (!valueMatches)
                                {
                                    notRequiredCount++;
                                    _logger.LogInformation(
                                        "Document type {TypeId} not required - attribute value mismatch (school: {SchoolVal}, required: {RequiredVal}, type: {AttrType})",
                                        docType.Id, attributeValue, requiredValue, attributeType ?? "string");
                                    continue;
                                }
                            }
                        }
                    }

                    // ✅ Create new document with default status and truncated description
                    var newDocument = new Document
                    {
                        Description = null,
                        DocumentTypeId = docType.Id,
                        StatusId = 1,  // Default status
                        Version = 0,
                        IsLastVersion = true,
                        CreatedAt = DateTime.UtcNow,
                        MasterDocumentId = null,
                        FileBlob = null,
                        FileEncoding = string.Empty
                    };

                    _context.Documents.Add(newDocument);
                    await _context.SaveChangesAsync();

                    // ✅ Set master document ID after creation
                    await SetMasterDocumentId(newDocument.Id);

                    // ✅ Create document link to entity
                    var documentLink = new DocumentLink
                    {
                        DocumentId = newDocument.Id,
                        SchoolStudentId = null,
                        EntityId = schoolId
                    };

                    _context.Set<DocumentLink>().Add(documentLink);
                    addedCount++;

                    _logger.LogInformation("Created document for type {TypeId} ({TypeName})",
                        docType.Id, docType.Name);
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("Document generation complete: {Added} added, {Skipped} skipped (already exist), {NotRequired} not required",
                    addedCount, skippedCount, notRequiredCount);

                return Ok(new
                {
                    success = true,
                    message = $"נוספו לרשימה {addedCount} מסמכים ",
                    addedCount,
                    skippedCount,
                    notRequiredCount,  // ✅ NEW: Return filtered count
                    totalTypes = documentTypes.Count
                });

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating school documents");
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה ביצירת מסמכים",
                    error = ex.Message
                });
            }
        }


                /// <summary>
        /// Generate missing documents for an entity based on document types for the year
        /// Level = רשת, no filtering rules
        /// For entity type 6 (networks), generates documents for all owned entities (excluding schools)
        /// </summary>
        [HttpPost("generate-entity-documents")]
        public async Task<IActionResult> GenerateEntityDocuments(
            [FromQuery] int entityId,
            [FromQuery] int yearId)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }
        
                _logger.LogInformation("Generating documents for entity {EntityId} in year {YearId}",
                    entityId, yearId);
        
                // Get the entity to check its type
                var entity = await _context.Entities
                    .Where(e => e.Id == entityId)
                    .Select(e => new { e.Id, e.EntityTypeId, e.Name })
                    .FirstOrDefaultAsync();
        
                if (entity == null)
                {
                    return NotFound(new { success = false, message = "ישות לא נמצאה" });
                }
        
                _logger.LogInformation("Entity {EntityId} is type {EntityTypeId}", entityId, entity.EntityTypeId);
        
                // Determine target entities
                List<int> targetEntityIds;
                
                if (entity.EntityTypeId == 6)
                {
                    // Entity type 6 (network) - get all owned entities (excluding schools - types 1 and 4)
                    var notSchoolTypes = new[] { 1, 4 };
                    
                    targetEntityIds = await _context.Entities
                        .Where(e => e.OwnerId == entityId && 
                                   e.IsActive && 
                                   !notSchoolTypes.Contains(e.EntityTypeId))
                        .Select(e => e.Id)
                        .ToListAsync();
        
                    _logger.LogInformation("Entity type 6 (network) - found {Count} owned entities (excluding schools)",
                        targetEntityIds.Count);
        
                    if (targetEntityIds.Count == 0)
                    {
                        return Ok(new
                        {
                            success = true,
                            message = "לא נמצאו ישויות בנות ליצירת מסמכים",
                            addedCount = 0,
                            skippedCount = 0,
                            totalTypes = 0,
                            targetEntitiesCount = 0
                        });
                    }
                }
                else
                {
                    // For other entity types, generate documents for the entity itself
                    targetEntityIds = new List<int> { entityId };
                    _logger.LogInformation("Entity type {TypeId} - generating documents for the entity itself",
                        entity.EntityTypeId);
                }
        
                // Get all document types for רשת level in this year
                var documentTypes = await _context.DocumentTypes
                    .Where(dt => dt.YearId == yearId && dt.Level == "רשת")
                    .ToListAsync();
        
                _logger.LogInformation("Found {Count} document types with level 'רשת' for year {YearId}",
                    documentTypes.Count, yearId);
        
                if (documentTypes.Count == 0)
                {
                    return Ok(new
                    {
                        success = true,
                        message = "לא נמצאו סוגי מסמכים מסוג 'רשת' לשנה זו",
                        addedCount = 0,
                        skippedCount = 0,
                        totalTypes = 0,
                        targetEntitiesCount = targetEntityIds.Count
                    });
                }
        
                int totalAddedCount = 0;
                int totalSkippedCount = 0;
        
                // Process each target entity
                foreach (var targetEntityId in targetEntityIds)
                {
                    _logger.LogInformation("Processing entity {EntityId}", targetEntityId);
        
                    // Get existing documents for this entity
                    var existingDocumentTypeIds = await _context.Documents
                        .Include(d => d.DocumentLinks)
                        .Where(d => d.DocumentLinks.Any(dl => dl.EntityId == targetEntityId))
                        .Where(d => d.IsLastVersion)
                        .Select(d => d.DocumentTypeId)
                        .Distinct()
                        .ToListAsync();
        
                    _logger.LogInformation("Entity {EntityId} already has {Count} document types",
                        targetEntityId, existingDocumentTypeIds.Count);
        
                    foreach (var docType in documentTypes)
                    {
                        // Skip if document type already exists for this entity
                        if (existingDocumentTypeIds.Contains(docType.Id))
                        {
                            totalSkippedCount++;
                            _logger.LogInformation("Skipping document type {TypeId} for entity {EntityId} - already exists",
                                docType.Id, targetEntityId);
                            continue;
                        }
        
                        // רשת level documents have no filtering rules - always create
                        _logger.LogInformation("Creating document type {TypeId} ({TypeName}) for entity {EntityId}",
                            docType.Id, docType.Name, targetEntityId);
        
                        // Create new document with default status
                        var newDocument = new Document
                        {
                            Description = null,
                            DocumentTypeId = docType.Id,
                            StatusId = 1,  // Default status
                            Version = 0,
                            IsLastVersion = true,
                            CreatedAt = DateTime.UtcNow,
                            MasterDocumentId = null,
                            FileBlob = null,
                            FileEncoding = string.Empty
                        };
        
                        _context.Documents.Add(newDocument);
                        await _context.SaveChangesAsync();
        
                        // Set master document ID after creation
                        await SetMasterDocumentId(newDocument.Id);
        
                        // Create document link to entity
                        var documentLink = new DocumentLink
                        {
                            DocumentId = newDocument.Id,
                            SchoolStudentId = null,
                            EntityId = targetEntityId
                        };
        
                        _context.Set<DocumentLink>().Add(documentLink);
                        totalAddedCount++;
        
                        _logger.LogInformation("Created document {DocumentId} for type {TypeId} ({TypeName}) linked to entity {EntityId}",
                            newDocument.Id, docType.Id, docType.Name, targetEntityId);
                    }
        
                    await _context.SaveChangesAsync();
                }
        
                _logger.LogInformation("Entity document generation complete: {Added} added, {Skipped} skipped across {EntityCount} entities",
                    totalAddedCount, totalSkippedCount, targetEntityIds.Count);
        
                var message = entity.EntityTypeId == 6
                    ? $"נוספו {totalAddedCount} מסמכים ל-{targetEntityIds.Count} ישויות בנות"
                    : $"נוספו לרשימה {totalAddedCount} מסמכים";
        
                return Ok(new
                {
                    success = true,
                    message = message,
                    addedCount = totalAddedCount,
                    skippedCount = totalSkippedCount,
                    totalTypes = documentTypes.Count,
                    targetEntitiesCount = targetEntityIds.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating entity documents");
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה ביצירת מסמכים",
                    error = ex.Message
                });
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
        /// Get document types for selected year
        /// </summary>
        [HttpGet("document-types/{yearId}")]
        public async Task<IActionResult> GetDocumentTypesByYear(int yearId)
        {
            try
            {
                var session = GetCurrentSession();
                if (session == null)
                {
                    return Unauthorized(new { success = false, message = "נדרש אימות" });
                }

                var documentTypes = await _context.DocumentTypes
                    .AsNoTracking()
                    .Where(dt => dt.YearId == yearId)
                    .OrderBy(dt => dt.Name)
                    .Select(dt => new
                    {
                        id = dt.Id,
                        name = dt.Name,
                        level = dt.Level,
                        yearId = dt.YearId
                    })
                    .ToListAsync();

                _logger.LogInformation("Retrieved {Count} document types for year {YearId}", documentTypes.Count, yearId);
                return Ok(documentTypes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving document types for year {YearId}", yearId);
                return StatusCode(500, new
                {
                    success = false,
                    message = "שגיאה בטעינת סוגי מסמכים",
                    error = ex.Message
                });
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
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadDocument([FromForm] UploadDocumentRequest request)
        {
            try
            {
                var session = GetCurrentSession();

                if (request.File == null || request.File.Length == 0)
                {
                    return BadRequest(new { error = "לא הועלה קובץ" });
                }

                // Validate document type
                var documentType = await _context.DocumentTypes.FindAsync(request.DocumentTypeId);
                if (documentType == null)
                {
                    return BadRequest(new { error = "סוג מסמך לא תקין" });
                }

                // Get file extension and original filename
                var fileExtension = Path.GetExtension(request.File.FileName).TrimStart('.');
                var originalFileName = request.File.FileName;

                // Read file to byte array
                byte[] fileBytes;
                using (var memoryStream = new MemoryStream())
                {
                    await request.File.CopyToAsync(memoryStream);
                    fileBytes = memoryStream.ToArray();
                }

                Document document;

                // ✅ ALWAYS handle as replacement if existingDocumentId is provided
                if (request.ExistingDocumentId.HasValue)
                {
                    // Get existing document
                    var existingDoc = await _context.Documents
                        .Include(d => d.DocumentLinks)
                        .FirstOrDefaultAsync(d => d.Id == request.ExistingDocumentId.Value);

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
                        MasterDocumentId = existingDoc.MasterDocumentId ?? existingDoc.Id,
                        Description = !string.IsNullOrWhiteSpace(request.Description) ? request.Description : existingDoc.Description,
                        DocumentTypeId = existingDoc.DocumentTypeId,
                        StatusId = request.StatusId,
                        FileBlob = fileBytes,
                        FileEncoding = fileExtension,
                        FileName = originalFileName,
                        Version = existingDoc.Version + 1,
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
                        request.ExistingDocumentId, existingDoc.Version, document.Id, document.Version, document.MasterDocumentId);

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

        /// <summary>
        /// Set the master document ID for a newly created document.
        /// The document's own ID becomes its master ID for version tracking.
        /// </summary>
        /// <param name="documentId">The ID of the newly created document</param>
        private async Task SetMasterDocumentId(long documentId)
        {
            var document = await _context.Documents.FindAsync(documentId);
            if (document == null)
            {
                _logger.LogWarning("Document {DocumentId} not found when setting master ID", documentId);
                return;
            }

            // Only set master ID if it's null (first version)
            if (document.MasterDocumentId == null)
            {
                document.MasterDocumentId = documentId;
                _context.Documents.Update(document);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Set master_document_id={MasterId} for document {DocumentId}",
                    documentId, documentId);
            }
        }
    }
}



/// <summary>
/// Request model for document upload
/// </summary>
public class UploadDocumentRequest
{
    public IFormFile File { get; set; } = null!;
    public string? Description { get; set; }
    public int DocumentTypeId { get; set; }
    public int StatusId { get; set; }
    public int EntityId { get; set; }
    public int? YearId { get; set; }
    public long? ExistingDocumentId { get; set; }
    public bool ReplaceExisting { get; set; }
}