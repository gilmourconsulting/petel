using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetelApp.Api.Data;
using PetelApp.Api.Models.DTOs;
using PetelApp.Api.Session;
using PetelApp.Api.Services;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.Text;
using CsvHelper;
using System.Globalization;
using ClosedXML.Excel;

namespace PetelApp.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StudentsFileUploadController : BaseController
    {
        private readonly AppDbContext _context;
        private readonly StudentsFileProcessor _fileProcessor;
        private readonly GlobalFunctions _globalFunctions;
        private readonly AlertService _alertService;
        
        public StudentsFileUploadController(
            UserSessionService userSessionService,
            ILogger<StudentsFileUploadController> logger,
            AppDbContext context,
            StudentsFileProcessor fileProcessor,
            GlobalFunctions globalFunctions,
            AlertService alertService)
        : base(userSessionService, logger)
        {
            _context = context;
            _fileProcessor = fileProcessor;
            _globalFunctions = globalFunctions;
            _alertService = alertService;
        }

        /// <summary>
        /// Upload a students file for a specific school (entity) and school year (form upload).
        /// Can use either IDs or natural keys (school symbol + Hebrew year).
        /// </summary>
 [HttpPost("upload")]
[Consumes("multipart/form-data")]
[RequestSizeLimit(10_000_000)] // 10MB limit, adjust as needed
public async Task<IActionResult> UploadStudentsFile([FromForm] UploadStudentsFileRequest request)
{
    var session = GetCurrentSession();

    // ✅ Check for null session
    if (session == null)
    {
        _logger.LogError("No valid session found");
        return Unauthorized(new { success = false, message = "לא נמצאה הפעלה פעילה. אנא התחבר מחדש." });
    }

    if (request.File == null || request.File.Length == 0)
        return BadRequest(new { success = false, message = "No file uploaded." });

    // Resolve school and year IDs - explicitly type the tuple
    var (resolvedSchoolId, resolvedYearId, error) = await ResolveSchoolAndYearAsync(
        request.SchoolId, request.SchoolYearId, request.SchoolSymbol, request.HebrewYear);

    if (!string.IsNullOrEmpty(error))
        return BadRequest(new { success = false, message = error });

    // Parse mapping if provided
    Dictionary<string, string>? mapping = null;
    if (!string.IsNullOrEmpty(request.MappingJson))
    {
        try
        {
            mapping = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(request.MappingJson);
        }
        catch
        {
            return BadRequest(new { success = false, message = "Invalid mapping JSON." });
        }
    }

    // Validate file structure
    var (isValid, validationError) = await StudentsFileValidator.ValidateStudentsFileAsync(request.File, mapping);
    if (!isValid)
        return BadRequest(new { success = false, message = validationError });

    // Parse file into student rows
    var rows = await ParseFileAsync(request.File, mapping);
    if (rows == null || !rows.Any())
        return BadRequest(new { success = false, message = "No valid student data found in file." });

    _logger.LogInformation(
         "ResolveSchoolAndYearAsync  returned with: schoolId={SchoolId}, schoolYearId={SchoolYearId}",
         resolvedSchoolId, resolvedYearId);

    if (!resolvedSchoolId.HasValue || !resolvedYearId.HasValue)
    {
        _logger.LogError("Failed to resolve school or year IDs. SchoolId={SchoolId}, YearId={YearId}",
            resolvedSchoolId, resolvedYearId);
        return BadRequest(new { success = false, message = "Failed to resolve school or year information." });
    }

    // Process student data
    var result = await _fileProcessor.ProcessStudentRowsAsync(
        rows,
        resolvedSchoolId.Value,
        resolvedYearId.Value,
        session.UserId);

    // ✅ Create alert for successful file upload using AlertService
    try
    {
        var school = await _context.Entities
            .Where(e => e.Id == resolvedSchoolId.Value)
            .FirstOrDefaultAsync();

        if (school != null)
        {
            var alertDescription = $"קובץ חדש הועלה לבית ספר {school.Name}";
            
            await _alertService.CreateSchoolAlertAsync(
                description: alertDescription,
                schoolId: resolvedSchoolId.Value,
                userId: int.Parse(session.UserId),
                isEvent: false
            );
        }
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "⚠️ Failed to create alert for file upload, but file processing succeeded");
        // Don't fail the request if alert creation fails
    }

    return Ok(new
    {
        success = true,
        message = "File processed successfully.",
        created = result.Created,
        updated = result.Updated,
        unchanged = result.Unchanged.Count,
        errors = result.Errors.Count,
        details = new
        {
            unchangedList = result.Unchanged,
            errorList = result.Errors
        }
    });
}

        /// <summary>
        /// API endpoint for uploading students file (for automation/integration).
        /// Can use either IDs or natural keys (school symbol + Hebrew year).
        /// </summary>
        [HttpPost("upload-api")]
        public async Task<IActionResult> UploadStudentsFileApi([FromBody] StudentsFileUploadDto dto)
        {
            var session = GetCurrentSession();

            // ✅ Check for null session
            if (session == null)
            {
                _logger.LogError("No valid session found");
                return Unauthorized(new { success = false, message = "לא נמצאה הפעלה פעילה. אנא התחבר מחדש." });
            }

            if (dto == null || string.IsNullOrEmpty(dto.FileBase64))
                return BadRequest(new { success = false, message = "No file data provided." });

            // Resolve school and year IDs
            var (resolvedSchoolId, resolvedYearId, error) = await ResolveSchoolAndYearAsync(
                dto.SchoolId, dto.SchoolYearId, dto.SchoolSymbol, dto.HebrewYear);

            if (!string.IsNullOrEmpty(error))
                return BadRequest(new { success = false, message = error });

            if (!resolvedSchoolId.HasValue || !resolvedYearId.HasValue)
            {
                _logger.LogError("Failed to resolve school or year IDs. SchoolId={SchoolId}, YearId={YearId}",
                    resolvedSchoolId, resolvedYearId);
                return BadRequest(new { success = false, message = "Failed to resolve school or year information." });
            }

            // Decode base64 and create temporary file stream
            byte[] fileBytes = Convert.FromBase64String(dto.FileBase64);
            using var stream = new MemoryStream(fileBytes);
            var formFile = new FormFile(stream, 0, fileBytes.Length, "file", dto.FileName ?? "students.csv");

            var (isValid, validationError) = await StudentsFileValidator.ValidateStudentsFileAsync(formFile, dto.Mapping);
            if (!isValid)
                return BadRequest(new { success = false, message = validationError });

            // Parse file into student rows
            stream.Position = 0; // Reset stream position
            var rows = await ParseFileAsync(formFile, dto.Mapping);
            if (rows == null || !rows.Any())
                return BadRequest(new { success = false, message = "No valid student data found in file." });

            // Process student data
            var result = await _fileProcessor.ProcessStudentRowsAsync(
                rows,
                resolvedSchoolId.Value,
                resolvedYearId.Value,
                session.UserId);

        // ✅ Create alert for successful file upload using AlertService
        try
        {
            var school = await _context.Entities
                .Where(e => e.Id == resolvedSchoolId.Value)
                .FirstOrDefaultAsync();

            if (school != null)
            {
                var alertDescription = $"קובץ חדש הועלה לבית ספר {school.Name}";
                
                await _alertService.CreateSchoolAlertAsync(
                    description: alertDescription,
                    schoolId: resolvedSchoolId.Value,
                    userId: int.Parse(session.UserId),
                    isEvent: false
                );
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "⚠️ Failed to create alert for file upload, but file processing succeeded");
            // Don't fail the request if alert creation fails
        }

        return Ok(new
        {
            success = true,
            message = "File processed successfully via API.",
            created = result.Created,
            updated = result.Updated,
            unchanged = result.Unchanged.Count,
            errors = result.Errors.Count,
                details = new
                {
                    unchangedList = result.Unchanged,
                    errorList = result.Errors
                }
            });
        }

 
private Task<List<StudentFileRow>> ParseFileAsync(IFormFile file, Dictionary<string, string>? mapping)
{
    _logger.LogInformation("📄 Starting file parse: FileName={FileName}, Size={Size} bytes", 
        file.FileName, file.Length);
    
    var rows = new List<StudentFileRow>();
    var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
    _logger.LogInformation("File extension: {Extension}", ext);

    using var stream = file.OpenReadStream();

    if (ext == ".csv")
    {
        using var reader = new StreamReader(stream);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

        csv.Read();
        csv.ReadHeader();
        
        var headers = csv.HeaderRecord ?? Array.Empty<string>();
        if (headers == null || headers.Length == 0)
        {
            _logger.LogWarning("⚠️ CSV file has no headers");
            return Task.FromResult(rows);
        }

        _logger.LogInformation("CSV headers: {Headers}", string.Join(", ", headers));

        while (csv.Read())
        {
            var row = ExtractRowData(csv, headers, mapping);
            if (row != null)
            {
                rows.Add(row);
            }
        }
    }
    else if (ext == ".xls" || ext == ".xlsx")
    {
        using var package = new XLWorkbook(stream);
        var worksheet = package.Worksheets.FirstOrDefault();
        
        if (worksheet == null)
        {
            _logger.LogWarning("⚠️ Excel file has no worksheets");
            return Task.FromResult(rows);
        }

        var firstRow = worksheet.Row(1);
        if (firstRow == null || firstRow.CellsUsed().Count() == 0)
        {
            _logger.LogWarning("⚠️ Excel file has no headers");
            return Task.FromResult(rows);
        }

        var headers = firstRow.CellsUsed()
            .Select(c => c.GetValue<string>().Trim())
            .ToList();

        _logger.LogInformation("Excel headers: {Headers}", string.Join(", ", headers));

        foreach (var row in worksheet.RowsUsed().Skip(1))
        {
            var studentRow = ExtractRowData(row, headers, mapping);
            if (studentRow != null)
            {
                rows.Add(studentRow);
            }
        }
    }

    _logger.LogInformation("✅ Parsed {Count} student rows from file", rows.Count);
    return Task.FromResult(rows);
}

        // handle class_level and class_number

        private StudentFileRow? ExtractRowData(CsvReader csv, string[] headers, Dictionary<string, string>? mapping)
        {

            if (headers == null || headers.Length == 0)
            {
                _logger.LogWarning("ExtractRowData called with null or empty headers");
                return null;
            }
            try
            {
                var className = GetFieldValue(csv, headers, "class", mapping);
                var classLevel = GetFieldValue(csv, headers, "class_level", mapping);
                var classNumber = GetFieldValue(csv, headers, "class_number", mapping);

                // If class_level and class_number provided, combine them
                if (!string.IsNullOrWhiteSpace(classLevel) && !string.IsNullOrWhiteSpace(classNumber))
                {
                    className = $"{classLevel}{classNumber}";
                }

                return new StudentFileRow
                {
                    IdNumber = GetFieldValue(csv, headers, "id_number", mapping),
                    FirstName = GetFieldValue(csv, headers, "first_name", mapping),
                    LastName = GetFieldValue(csv, headers, "last_name", mapping),
                    Gender = GetFieldValue(csv, headers, "gender", mapping),
                    Class = className,
                    StartDate = GetFieldValue(csv, headers, "start_date", mapping),
                    EndDate = GetFieldValue(csv, headers, "end_date", mapping),
                    DisabilityCategory = GetFieldValue(csv, headers, "disability_category", mapping),
                    Street = GetFieldValue(csv, headers, "street", mapping),
                    HouseNumber = GetFieldValue(csv, headers, "house_number", mapping),
                    City = GetFieldValue(csv, headers, "city", mapping),
                    PostCode = GetFieldValue(csv, headers, "post_code", mapping),
                    SendingCouncil = GetFieldValue(csv, headers, "sending_council", mapping)
                };
            }
            catch
            {
                return null;
            }
        }

        private StudentFileRow? ExtractRowData(IXLRow row, List<string> headers, Dictionary<string, string>? mapping)
        {
            try
            {
                _logger.LogWarning("🔍 ExtractRowData called for Excel row");
                var className = GetFieldValue(row, headers, "class", mapping);
                var classLevel = GetFieldValue(row, headers, "class_level", mapping);
                var classNumber = GetFieldValue(row, headers, "class_number", mapping);

                // If class_level and class_number provided, combine them
                if (!string.IsNullOrWhiteSpace(classLevel) && !string.IsNullOrWhiteSpace(classNumber))
                {
                    className = $"{classLevel}{classNumber}";
                }

                var studentRow = new StudentFileRow
                {
                    IdNumber = GetFieldValue(row, headers, "id_number", mapping),
                    FirstName = GetFieldValue(row, headers, "first_name", mapping),
                    LastName = GetFieldValue(row, headers, "last_name", mapping),
                    Gender = GetFieldValue(row, headers, "gender", mapping),
                    Class = className,
                    StartDate = GetFieldValue(row, headers, "start_date", mapping),
                    EndDate = GetFieldValue(row, headers, "end_date", mapping),
                    DisabilityCategory = GetFieldValue(row, headers, "disability_category", mapping),
                    Street = GetFieldValue(row, headers, "street", mapping),
                    HouseNumber = GetFieldValue(row, headers, "house_number", mapping),
                    City = GetFieldValue(row, headers, "city", mapping),
                    PostCode = GetFieldValue(row, headers, "post_code", mapping),
                    SendingCouncil = GetFieldValue(row, headers, "sending_council", mapping)
                };
                
                _logger.LogWarning("✅ StudentFileRow created successfully");
                return studentRow;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error extracting row data from Excel");
                return null;
            }
        }
        private string GetFieldValue(CsvReader csv, string[] headers, string fieldName, Dictionary<string, string>? mapping)
        {
            var headerName = mapping != null && mapping.ContainsKey(fieldName) ? mapping[fieldName] : fieldName;
            var index = Array.IndexOf(headers, headerName);
            return index >= 0 ? csv.GetField(index)?.Trim() ?? "" : "";
        }

 private string GetFieldValue(IXLRow row, List<string> headers, string fieldName, Dictionary<string, string>? mapping)
{
    var headerName = mapping != null && mapping.ContainsKey(fieldName) ? mapping[fieldName] : fieldName;
    var index = headers.IndexOf(headerName);
    
    if (index < 0)
    {
        _logger.LogDebug("Field '{FieldName}' not found in headers", fieldName);
        return "";
    }
    
    var cell = row.Cell(index + 1);
    
    // Log cell details for debugging
    _logger.LogDebug("📋 Field '{FieldName}': DataType={DataType}, RawValue='{RawValue}'", 
        fieldName, cell.DataType, cell.Value);
    
    // ✅ Handle date fields specially - can be DateTime, Text, or Number format
    if (fieldName == "start_date" || fieldName == "end_date")
    {
        _logger.LogDebug("🔍 Processing date field: '{FieldName}'", fieldName);
        
        try
        {
            // ✅ Declare hebrewCulture ONCE at the top of the try block
            var hebrewCulture = CultureInfo.GetCultureInfo("he-IL");
            
            // Case 1: Excel DateTime format (formatted as date in Excel)
            if (cell.DataType == XLDataType.DateTime)
            {
                if (cell.TryGetValue<DateTime>(out DateTime dateValue))
                {
                    var formattedDate = dateValue.ToString("dd/MM/yyyy");
                    _logger.LogDebug("✅ DateTime format for '{FieldName}': {DateTime} → '{FormattedDate}'",
                        fieldName, dateValue, formattedDate);
                    return formattedDate;
                }
            }
            
            // Case 2: Number format (Excel stores dates as numbers - days since 1900-01-01)
            if (cell.DataType == XLDataType.Number)
            {
                if (cell.TryGetValue<double>(out double numericValue))
                {
                    // Excel date serial number (e.g., 45535 = 2024-09-01)
                    var dateValue = DateTime.FromOADate(numericValue);
                    var formattedDate = dateValue.ToString("dd/MM/yyyy");
                    _logger.LogDebug("✅ Number format for '{FieldName}': {Number} → {DateTime} → '{FormattedDate}'",
                        fieldName, numericValue, dateValue, formattedDate);
                    return formattedDate;
                }
            }
            
            // Case 3: Text format (already formatted as dd/MM/yyyy or similar)
            if (cell.DataType == XLDataType.Text)
            {
                var textValue = cell.GetString().Trim();
                
                // Validate it's a parseable date (reuse hebrewCulture)
                if (DateTime.TryParse(textValue, hebrewCulture, DateTimeStyles.None, out DateTime parsedDate))
                {
                    // Re-format to ensure consistent dd/MM/yyyy format
                    var formattedDate = parsedDate.ToString("dd/MM/yyyy");
                    _logger.LogDebug("✅ Text format for '{FieldName}': '{Text}' → '{FormattedDate}'",
                        fieldName, textValue, formattedDate);
                    return formattedDate;
                }
                else
                {
                    _logger.LogWarning("⚠️ Text format for '{FieldName}' is not a valid date: '{Text}'",
                        fieldName, textValue);
                    return textValue; // Return as-is for validation to catch
                }
            }
            
            // Case 4: General format (try both numeric and text parsing)
            var cellValueString = cell.Value.ToString()?.Trim() ?? "";
            
            // Try as numeric first (Excel general format with date number)
            if (double.TryParse(cellValueString, NumberStyles.Any, CultureInfo.InvariantCulture, out double generalNumeric))
            {
                // Check if it's a reasonable Excel date serial number (between 1900 and 2100)
                if (generalNumeric > 0 && generalNumeric < 73050) // Excel dates for years 1900-2100
                {
                    var dateValue = DateTime.FromOADate(generalNumeric);
                    var formattedDate = dateValue.ToString("dd/MM/yyyy");
                    _logger.LogDebug("✅ General-as-number format for '{FieldName}': {Number} → {DateTime} → '{FormattedDate}'",
                        fieldName, generalNumeric, dateValue, formattedDate);
                    return formattedDate;
                }
            }
            
            // Try as text date (reuse hebrewCulture)
            if (DateTime.TryParse(cellValueString, hebrewCulture, DateTimeStyles.None, out DateTime generalDate))
            {
                var formattedDate = generalDate.ToString("dd/MM/yyyy");
                _logger.LogDebug("✅ General-as-text format for '{FieldName}': '{Text}' → '{FormattedDate}'",
                    fieldName, cellValueString, formattedDate);
                return formattedDate;
            }
            
            _logger.LogError("❌ Failed to parse date for '{FieldName}': DataType={DataType}, Value='{Value}'",
                fieldName, cell.DataType, cellValueString);
            return cellValueString; // Return as-is for validation to catch
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error extracting date from Excel cell for '{FieldName}'", fieldName);
            return ""; // Return empty string on exception
        }
    }
    
    // Non-date fields: return as string
    var stringValue = cell.Value.ToString()?.Trim() ?? "";
    _logger.LogDebug("Field '{FieldName}' returning string value: '{StringValue}'", fieldName, stringValue);
    return stringValue;
}

        /// <summary>
        /// Resolves school and year IDs from either direct IDs or natural keys.
        /// Priority: Direct IDs first, then natural keys (symbol + Hebrew year).
        /// </summary>
        private async Task<(int? schoolId, int? yearId, string? error)> ResolveSchoolAndYearAsync(
            int? schoolId,
            int? schoolYearId,
            string? schoolSymbol,
            string? hebrewYear)
        {
            // Debug log received parameters
            _logger.LogInformation(
                "ResolveSchoolAndYearAsync called with: schoolId={SchoolId}, schoolYearId={SchoolYearId}, schoolSymbol={SchoolSymbol}, hebrewYear={HebrewYear}",
                schoolId, schoolYearId, schoolSymbol, hebrewYear);

            int? resolvedSchoolId = schoolId;
            int? resolvedYearId = null;

            // STEP 1: If schoolYearId provided directly, use it and we're done
            if (schoolYearId.HasValue)
            {
                resolvedYearId = schoolYearId;
                _logger.LogInformation("Using provided schoolYearId={SchoolYearId}", schoolYearId);

                // schoolYearId is sufficient - no need for school resolution
                return (resolvedSchoolId, resolvedYearId, null);
            }

            // STEP 2: No schoolYearId - need to resolve it from school + Hebrew year
            // First resolve school ID if not provided
            if (!resolvedSchoolId.HasValue && !string.IsNullOrEmpty(schoolSymbol))
            {
                var entity = await _context.Entities
                    .FirstOrDefaultAsync(e => e.Symbol == schoolSymbol);

                if (entity == null)
                    return (null, null, $"School with symbol '{schoolSymbol}' not found.");

                resolvedSchoolId = entity.Id;
                _logger.LogInformation("Resolved schoolId={ResolvedSchoolId} from symbol={SchoolSymbol}",
                    resolvedSchoolId, schoolSymbol);
            }

            // Must have school ID to proceed with Hebrew year lookup
            if (!resolvedSchoolId.HasValue)
                return (null, null, "School ID or school symbol must be provided when using Hebrew year.");

            // STEP 3: Resolve schoolYearId from Hebrew year + school
            if (!string.IsNullOrEmpty(hebrewYear))
            {
                // Get year ID from hebrew_years table
                var hebrewYearRecord = await _context.Set<HebrewYear>()
                    .FirstOrDefaultAsync(y => y.HebrewYearText == hebrewYear);

                if (hebrewYearRecord == null)
                    return (null, null, $"Hebrew year '{hebrewYear}' not found in hebrew_years table.");

                var yearId = hebrewYearRecord.Id;

                // Get school year ID from schoolyear table
                var schoolYear = await _context.SchoolYears
                    .FirstOrDefaultAsync(sy => sy.YearId == yearId && sy.SchoolId == resolvedSchoolId);

                if (schoolYear == null)
                {
                    _logger.LogWarning(
                        "School year not found for schoolId={SchoolId}, yearId={YearId}, hebrewYear={HebrewYear}",
                        resolvedSchoolId, yearId, hebrewYear);
                    return (null, null, $"School year not found for school '{resolvedSchoolId}' and Hebrew year '{hebrewYear}'.");
                }

                resolvedYearId = schoolYear.Id;
                _logger.LogInformation(
                    "Resolved schoolYearId={ResolvedYearId} from hebrewYear={HebrewYear} and schoolId={SchoolId}",
                    resolvedYearId, hebrewYear, resolvedSchoolId);

                return (resolvedSchoolId, resolvedYearId, null);
            }

            // No way to resolve schoolYearId
            return (null, null, "School year ID or Hebrew year must be provided.");
        }


          /// <summary>
    /// Preview file and get column headers for mapping
    /// </summary>
    [HttpPost("preview")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(10_000_000)]
    public async Task<IActionResult> PreviewFile([FromForm] PreviewFileRequest request)
    {
        var session = GetCurrentSession();
        if (session == null)
        {
            return Unauthorized(new { success = false, message = "נדרש אימות" });
        }
    
        if (request.File == null || request.File.Length == 0)
            return BadRequest(new { success = false, message = "No file uploaded." });
    
        try
        {
            var ext = Path.GetExtension(request.File.FileName).ToLowerInvariant();
            var headers = new List<string>();
    
            using var stream = request.File.OpenReadStream();
    
            if (ext == ".csv")
            {
                using var reader = new StreamReader(stream);
                using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
                csv.Read();
                csv.ReadHeader();
                headers = csv.HeaderRecord?.ToList() ?? new List<string>();
            }
            else if (ext == ".xls" || ext == ".xlsx")
            {
                using var workbook = new XLWorkbook(stream);
                var worksheet = workbook.Worksheets.First();
                var firstRow = worksheet.FirstRowUsed();
                if (firstRow == null || firstRow.CellsUsed().Count() == 0)
                    return BadRequest(new 
                    { 
                        success = false, 
                        message = "לא נמצאו נתוני תלמידים תקינים בקובץ או הקובץ ריק" 
                    });
                headers = firstRow.CellsUsed()
                    .Select(cell => cell.Value.ToString()?.Trim() ?? "")
                    .ToList();
            }
            else
            {
                return BadRequest(new { success = false, message = "Unsupported file format. Please use CSV, XLS, or XLSX." });
            }
    
            // Generate suggested mappings
            var suggestedMappings = GenerateSuggestedMappings(headers);
    
            return Ok(new
            {
                success = true,
                headers = headers,
                suggestedMappings = suggestedMappings,
                availableFields = GetAvailableStudentFields()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error previewing file");
            return StatusCode(500, new { success = false, message = "Error reading file: " + ex.Message });
        }
    }

        /// <summary>
        /// Generate suggested field mappings based on header names
        /// </summary>
        private Dictionary<string, string> GenerateSuggestedMappings(List<string> headers)
        {
            var mappings = new Dictionary<string, string>();
            var fieldMappings = new Dictionary<string, string[]>
            {
                { "id_number", new[] { "תעודת זהות", "ת.ז", "תז", "מספר זהות", "id", "id_number", "מזהה" } },
                { "first_name", new[] { "שם פרטי", "שם", "first_name", "firstname", "שם התלמיד" } },
                { "last_name", new[] { "שם משפחה", "משפחה", "last_name", "lastname", "שם משפחת התלמיד" } },
                { "gender", new[] { "מין", "gender", "מגדר" } },
                { "class", new[] { "כיתה", "class", "שם כיתה", "כיתה שם" } },
                { "class_level", new[] { "שכבה", "רמה", "level", "class_level", "מס' שכבה" } },
                { "class_number", new[] { "מספר כיתה", "class_number", "מס' כיתה" } },
                { "start_date", new[] { "תאריך התחלה", "התחלה", "start_date", "start", "תאריך כניסה" } },
                { "end_date", new[] { "תאריך סיום", "סיום", "end_date", "end", "תאריך יציאה" } },
                { "disability_category", new[] { "קטגוריית נכות", "נכות", "disability", "disability_category", "קטגוריה" } },
                { "street", new[] { "רחוב", "street", "שם רחוב" } },
                { "house_number", new[] { "מספר בית", "בית", "house_number", "מס' בית" } },
                { "city", new[] { "עיר", "city", "יישוב" } },
                { "post_code", new[] { "מיקוד", "postcode", "post_code", "מספר מיקוד" } },
                { "sending_council", new[] { "רשות שולחת", "מועצה", "council", "sending_council", "רשות",
                            "שם רשות",
                            "שם מועצה" } }
            };

            foreach (var header in headers)
            {
                var normalizedHeader = header.Trim().ToLower();

                foreach (var field in fieldMappings)
                {
                    if (field.Value.Any(pattern =>
                        normalizedHeader.Contains(pattern.ToLower()) ||
                        pattern.ToLower().Contains(normalizedHeader)))
                    {
                        mappings[header] = field.Key;
                        break;
                    }
                }
            }

            return mappings;
        }

        /// <summary>
        /// Get list of available student fields for mapping
        /// </summary>
        private Dictionary<string, string> GetAvailableStudentFields()
        {
            return new Dictionary<string, string>
            {
                { "id_number", "תעודת זהות" },
                { "first_name", "שם פרטי" },
                { "last_name", "שם משפחה" },
                { "gender", "מין" },
                { "class", "כיתה (שם מלא)" },
                { "class_level", "שכבה" },
                { "class_number", "מספר כיתה" },
                { "start_date", "תאריך התחלה" },
                { "end_date", "תאריך סיום" },
                { "disability_category", "קטגוריית נכות" },
                { "street", "רחוב" },
                { "house_number", "מספר בית" },
                { "city", "עיר" },
                { "post_code", "מיקוד" },
                { "sending_council", "רשות שולחת" },
                { "ignore", "התעלם" }
            };
        }
    }





}

/// <summary>
/// Request model for uploading students file with form data
/// </summary>
public class UploadStudentsFileRequest
{
    public IFormFile File { get; set; } = null!;
    public int? SchoolId { get; set; }
    public int? SchoolYearId { get; set; }
    public string? SchoolSymbol { get; set; }
    public string? HebrewYear { get; set; }
    public string? MappingJson { get; set; }
}

/// <summary>
/// Request model for previewing file with form data
/// </summary>
public class PreviewFileRequest
{
    public IFormFile File { get; set; } = null!;
}