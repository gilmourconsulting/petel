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
        public StudentsFileUploadController(
            UserSessionService userSessionService,
            ILogger<StudentsFileUploadController> logger,
            AppDbContext context,
            StudentsFileProcessor fileProcessor,
    GlobalFunctions globalFunctions)
        : base(userSessionService, logger)
        {
            _context = context;
            _fileProcessor = fileProcessor;
            _globalFunctions = globalFunctions;
        }

        /// <summary>
        /// Upload a students file for a specific school (entity) and school year (form upload).
        /// Can use either IDs or natural keys (school symbol + Hebrew year).
        /// </summary>
        [HttpPost("upload")]
        [RequestSizeLimit(10_000_000)] // 10MB limit, adjust as needed
        public async Task<IActionResult> UploadStudentsFile(
            [FromForm] IFormFile file,
            [FromForm] int? schoolId = null,
            [FromForm] int? schoolYearId = null,
            [FromForm] string? schoolSymbol = null,
            [FromForm] string? hebrewYear = null,
            [FromForm] string? mappingJson = null)
        {
            var session = GetCurrentSession();

            // ✅ Check for null session
            if (session == null)
            {
                _logger.LogError("No valid session found");
                return Unauthorized(new { success = false, message = "לא נמצאה הפעלה פעילה. אנא התחבר מחדש." });
            }

            if (file == null || file.Length == 0)
                return BadRequest(new { success = false, message = "No file uploaded." });

            // Resolve school and year IDs - explicitly type the tuple
            var (resolvedSchoolId, resolvedYearId, error) = await ResolveSchoolAndYearAsync(
                schoolId, schoolYearId, schoolSymbol, hebrewYear);

            if (!string.IsNullOrEmpty(error))
                return BadRequest(new { success = false, message = error });



            // Parse mapping if provided
            Dictionary<string, string>? mapping = null;
            if (!string.IsNullOrEmpty(mappingJson))
            {
                try
                {
                    mapping = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(mappingJson);
                }
                catch
                {
                    return BadRequest(new { success = false, message = "Invalid mapping JSON." });
                }
            }

            // Validate file structure
            var (isValid, validationError) = await StudentsFileValidator.ValidateStudentsFileAsync(file, mapping);
            if (!isValid)
                return BadRequest(new { success = false, message = validationError });

            // Parse file into student rows
            var rows = await ParseFileAsync(file, mapping);
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

        private async Task<List<StudentFileRow>> ParseFileAsync(IFormFile file, Dictionary<string, string>? mapping)
        {
            var rows = new List<StudentFileRow>();
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();

            using var stream = file.OpenReadStream();

            if (ext == ".csv")
            {
                using var reader = new StreamReader(stream);
                using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

                csv.Read();
                csv.ReadHeader();
                var headers = csv.HeaderRecord;
                // ✅ Return empty list on error - let caller handle error response
                if (headers == null || headers.Length == 0)
                {
                    _logger.LogWarning("אין שורת כותרות בקובץ ה-CSV");
                    return new List<StudentFileRow>();  // Return empty list
                }



                while (csv.Read())
                {
                    var row = ExtractRowData(csv, headers, mapping);
                    if (row != null)
                        rows.Add(row);
                }
            }
            else if (ext == ".xls" || ext == ".xlsx")
            {
                using var workbook = new XLWorkbook(stream);
                var worksheet = workbook.Worksheets.First();

                var firstRow = worksheet.FirstRowUsed();
                // ✅ Return empty list on error - let caller handle error response
                if (firstRow == null || firstRow.CellsUsed().Count() == 0)
                {
                    _logger.LogWarning("Excel file has no rows or headers");
                    return new List<StudentFileRow>();  // Return empty list
                }
                var headers = firstRow.CellsUsed()
                    .Select(cell => cell.Value.ToString()?.Trim() ?? "")
                    .ToList();

                foreach (var row in worksheet.RowsUsed().Skip(1)) // Skip header row
                {
                    var rowData = ExtractRowData(row, headers, mapping);
                    if (rowData != null)
                        rows.Add(rowData);
                }
            }

            return rows;
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
                    SendingCouncil = GetFieldValue(csv, headers, "sending_counsil", mapping)
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
                var className = GetFieldValue(row, headers, "class", mapping);
                var classLevel = GetFieldValue(row, headers, "class_level", mapping);
                var classNumber = GetFieldValue(row, headers, "class_number", mapping);

                // If class_level and class_number provided, combine them
                if (!string.IsNullOrWhiteSpace(classLevel) && !string.IsNullOrWhiteSpace(classNumber))
                {
                    className = $"{classLevel}{classNumber}";
                }

                return new StudentFileRow
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
                    SendingCouncil = GetFieldValue(row, headers, "sending_counsil", mapping)
                };
            }
            catch
            {
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
            return index >= 0 ? row.Cell(index + 1).Value.ToString()?.Trim() ?? "" : "";
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
        [RequestSizeLimit(10_000_000)]
        public async Task<IActionResult> PreviewFile([FromForm] IFormFile file)
        {
            var session = GetCurrentSession();
            if (session == null)
            {
                return Unauthorized(new { success = false, message = "נדרש אימות" });
            }

            if (file == null || file.Length == 0)
                return BadRequest(new { success = false, message = "No file uploaded." });

            try
            {
                var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                var headers = new List<string>();

                using var stream = file.OpenReadStream();

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
                { "sending_counsil", new[] { "רשות שולחת", "מועצה", "council", "sending_counsil", "רשות" } }
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
                { "sending_counsil", "רשות שולחת" },
                { "ignore", "התעלם" }
            };
        }
    }





}