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
    
        public StudentsFileUploadController(
            UserSessionService userSessionService,
            ILogger<StudentsFileUploadController> logger,
            AppDbContext context,
            StudentsFileProcessor fileProcessor)
        : base(userSessionService, logger)
        {
            _context = context;
            _fileProcessor = fileProcessor;
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

            if (dto == null || string.IsNullOrEmpty(dto.FileBase64))
                return BadRequest(new { success = false, message = "No file data provided." });

            // Resolve school and year IDs
            var (resolvedSchoolId, resolvedYearId, error) = await ResolveSchoolAndYearAsync(
                dto.SchoolId, dto.SchoolYearId, dto.SchoolSymbol, dto.HebrewYear);

            if (!string.IsNullOrEmpty(error))
                return BadRequest(new { success = false, message = error });

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

        private StudentFileRow? ExtractRowData(CsvReader csv, string[] headers, Dictionary<string, string>? mapping)
        {
            try
            {
                return new StudentFileRow
                {
                    IdNumber = GetFieldValue(csv, headers, "id_number", mapping),
                    FirstName = GetFieldValue(csv, headers, "first_name", mapping),
                    LastName = GetFieldValue(csv, headers, "last_name", mapping),
                    Gender = GetFieldValue(csv, headers, "gender", mapping),
                    Class = GetFieldValue(csv, headers, "class", mapping),
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
                return new StudentFileRow
                {
                    IdNumber = GetFieldValue(row, headers, "id_number", mapping),
                    FirstName = GetFieldValue(row, headers, "first_name", mapping),
                    LastName = GetFieldValue(row, headers, "last_name", mapping),
                    Gender = GetFieldValue(row, headers, "gender", mapping),
                    Class = GetFieldValue(row, headers, "class", mapping),
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
    }




  
}