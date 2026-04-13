using Microsoft.EntityFrameworkCore;
using PetelATH.Api.Data;
using System.Globalization;


namespace PetelATH.Api.Services
{

    //private readonly GlobalFunctions _globalFunctions;
    public class StudentsFileProcessor
    {

        private readonly GlobalFunctions _globalFunctions;
        private readonly AppDbContext _context;
        private readonly ILogger<StudentsFileProcessor> _logger;
        private readonly StudentService _studentService;

        public StudentsFileProcessor(
            AppDbContext context,
            ILogger<StudentsFileProcessor> logger,
            GlobalFunctions globalFunctions,
            StudentService studentService)
        {
            _context = context;
            _logger = logger;
            _globalFunctions = globalFunctions;
            _studentService = studentService;
        }

        /// <summary>
        /// Process student rows from uploaded file.
        /// </summary>
        public async Task<ProcessingResult> ProcessStudentRowsAsync(
            List<StudentFileRow> rows,
            int schoolId,
            int schoolYearId,
            string userId)
        {

            _logger.LogInformation("=== STARTING FILE PROCESSING === RowCount={RowCount}, SchoolId={SchoolId}, SchoolYearId={SchoolYearId}, UserId={UserId}",
    rows.Count, schoolId, schoolYearId, userId);
            var result = new ProcessingResult();

            foreach (var row in rows)
            {
                try
                {
                    await ProcessSingleStudentAsync(row, schoolId, schoolYearId, userId, result);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing student {IdNumber}", row.IdNumber);
                    result.Errors.Add($"{row.IdNumber} - שגיאת עיבוד: {ex.Message}");
                }
            }

            return result;
        }

        private async Task ProcessSingleStudentAsync(
          StudentFileRow row,
          int schoolId,
          int schoolYearId,
          string userId,
          ProcessingResult result)
        {
            _logger.LogInformation("Processing student record: {IdNumber}", row.IdNumber);

            // Check if ID validation is enabled via system attribute
            bool validateIdChecksum = await ShouldValidateIdNumberAsync();

            // Validate data format
            var (isValid, formatError) = ValidateRowFormat(row, validateIdChecksum);
            if (!isValid)
            {
                result.Errors.Add($"{row.IdNumber} - שגיאת פורמט: {formatError}");
                _logger.LogInformation("Error in student record: {IdNumber}- שגיאת פורמט: {formatError}", row.IdNumber, formatError);
                return;
            }

            // Resolve class ID from class name using GlobalFunctions
            var classId = await _globalFunctions.GetClassIdByName(row.Class, schoolYearId);

            if (classId == null)
            {
                result.Errors.Add($"{row.IdNumber} - כיתה '{row.Class}' לא נמצאה בשנת הלימודים");
                _logger.LogWarning("Class not found: {ClassName} in school year {SchoolYearId} for student {IdNumber}",
                    row.Class, schoolYearId, row.IdNumber);
                return;
            }

            _logger.LogInformation("Resolved class '{ClassName}' to ID {ClassId} for student {IdNumber}",
                row.Class, classId, row.IdNumber);

            // ✅ NEW: Resolve sending council ID from name or numeric ID
            var councilId = await ResolveCouncilIdAsync(row.SendingCouncil, result, row.IdNumber);

            // Check if resolution failed (error already added to result)
            if (councilId == null && !string.IsNullOrWhiteSpace(row.SendingCouncil) && row.SendingCouncil != "99999")
            {
                // Council was provided but couldn't be resolved - skip this student
                _logger.LogWarning("Council resolution failed for student {IdNumber}, skipping", row.IdNumber);
                return;
            }

            // ✅ Load all students for this school year and compare in memory (encryption prevents DB search)
            var allStudentsInYear = await _context.SchoolStudents
                 .Where(s => s.IsLastVersion == true && s.SchoolYearId == schoolYearId)
                 .ToListAsync();
            
            // Find matching student by comparing decrypted IdNumber in memory
            var existingStudent = allStudentsInYear
                .FirstOrDefault(s => s.IdNumber == row.IdNumber);

            if (existingStudent == null)
            {
                // Create new record - pass resolved councilId
                await CreateNewStudentAsync(row, schoolId, schoolYearId, userId, classId.Value, councilId);
                result.Created++;
                _logger.LogInformation("Created new student record: {IdNumber}", row.IdNumber);
            }
            else
            {
                // Check if data has changed - pass resolved councilId
                bool hasChanges = HasDataChanged(existingStudent, row, classId.Value, councilId);

                if (!hasChanges)
                {
                    result.Unchanged.Add($"{row.IdNumber} - נתונים לא השתנו");
                    _logger.LogInformation("Student data unchanged: {IdNumber}", row.IdNumber);
                }
                else
                {
                    // Update existing record and create new version - pass resolved councilId
                    await UpdateStudentWithNewVersionAsync(existingStudent, row, userId, classId.Value, councilId);
                    result.Updated++;
                    _logger.LogInformation("Updated student record: {IdNumber}, new version {Version}",
                        row.IdNumber, existingStudent.Version + 1);
                }
            }
        }

        private (bool isValid, string? error) ValidateRowFormat(StudentFileRow row, bool validateIdChecksum = false)
        {
            // Validate ID number (9 digits)
            if (string.IsNullOrWhiteSpace(row.IdNumber) || row.IdNumber.Length != 9 || !row.IdNumber.All(char.IsDigit))
                return (false, "מספר תעודת זהות לא תקין");

            // Validate Israeli ID checksum if enabled
            if (validateIdChecksum && !IsValidIsraeliId(row.IdNumber))
                return (false, "מספר תעודת זהות לא תקין - ספרת ביקורת שגויה");

            // Validate first name
            if (string.IsNullOrWhiteSpace(row.FirstName))
                return (false, "שם פרטי חסר");

            // Validate last name
            if (string.IsNullOrWhiteSpace(row.LastName))
                return (false, "שם משפחה חסר");

            // ✅ Gender is optional - validate only if provided
            if (!string.IsNullOrWhiteSpace(row.Gender) && !new[] { "1", "2", "99", "זכר", "נקבה" }.Contains(row.Gender))
                return (false, "מין לא תקין");

            // Validate class
            if (string.IsNullOrWhiteSpace(row.Class))
                return (false, "כיתה חסרה");

            // Validate dates (expecting day-month-year format: DD/MM/YYYY)
           /* var hebrewCulture = CultureInfo.GetCultureInfo("he-IL");
            var culture = CultureInfo.InvariantCulture;
            if (!DateTime.TryParse(row.StartDate, out _))
                return (false, $"תאריך התחלה לא תקין: '{row.StartDate}'");

            if (!DateTime.TryParse(row.EndDate, out _))
                return (false, $"תאריך סיום לא תקין: '{row.EndDate}'");*/

            // Validate disability category (integer or empty for none)
            if (!string.IsNullOrWhiteSpace(row.DisabilityCategory) && !int.TryParse(row.DisabilityCategory, out _))
                return (false, "קטגוריית נכות לא תקינה");

            // ✅ Address fields - only HouseNumber length validation if provided
            if (!string.IsNullOrWhiteSpace(row.HouseNumber) && row.HouseNumber.Trim().Length > 6)
                return (false, "מספר בית ארוך מדי (מקסימום 6 תווים)");

            // Validate city (required)
            if (string.IsNullOrWhiteSpace(row.City))
                return (false, "עיר חסרה");

            // ✅ Postcode is optional - no validation needed

            // Validate sending council (integer or 99999 for none)
            if (string.IsNullOrWhiteSpace(row.SendingCouncil))
                return (false, "רשות שולחת לא תקינה");

            return (true, null);
        }

        private bool HasDataChanged(SchoolStudent existing, StudentFileRow row, int classId, int? councilId)
        {
            var hebrewCulture = CultureInfo.GetCultureInfo("he-IL");
            var rowGender = ParseGender(row.Gender);
            var rowDisabilityCategory = string.IsNullOrWhiteSpace(row.DisabilityCategory) ? null : (int?)int.Parse(row.DisabilityCategory);
            // var rowSendingCouncil = row.SendingCouncil == "99999" ? null : (int?)int.Parse(row.SendingCouncil);

            return existing.FirstName != row.FirstName ||
                   existing.LastName != row.LastName ||
                   existing.Gender != rowGender ||
                   existing.ClassId != classId ||
                   existing.StartDate?.ToString("yyyy-MM-dd") != DateTime.Parse(row.StartDate, hebrewCulture).ToString("yyyy-MM-dd") ||
                   existing.EndDate?.ToString("yyyy-MM-dd") != DateTime.Parse(row.EndDate, hebrewCulture).ToString("yyyy-MM-dd") ||
                   existing.DisabilityCategory != rowDisabilityCategory ||
                   existing.Street != (row.Street ?? string.Empty) || // ✅ Optional field
                   existing.HouseNumber != (row.HouseNumber?.Trim() ?? string.Empty) || // ✅ Optional field
                   existing.City != row.City ||
                   existing.PostCode != (row.PostCode ?? string.Empty) || // ✅ Optional field
                   existing.SendingCouncil != councilId;
        }

        private async Task CreateNewStudentAsync(
            StudentFileRow row,
            int schoolId,
            int schoolYearId,
            string userId,
            int classId,
            int? councilId)
        {
            var studentId = await _studentService.CreateNewStudentAsync(
                schoolYearId,
                row.IdNumber,
                student =>
                {
                    // Configure all fields
                    var hebrewCulture = CultureInfo.GetCultureInfo("he-IL");

                    student.FirstName = row.FirstName;
                    student.LastName = row.LastName;
                    student.Gender = ParseGender(row.Gender);
                    student.ClassId = classId;

                    // Parse dates with Israeli culture
                    student.StartDate = DateOnly.FromDateTime(DateTime.Parse(row.StartDate, hebrewCulture));
                    student.EndDate = DateOnly.FromDateTime(DateTime.Parse(row.EndDate, hebrewCulture));

                    student.DisabilityCategory = string.IsNullOrWhiteSpace(row.DisabilityCategory)
                        ? null
                        : (int?)int.Parse(row.DisabilityCategory);
                    student.Street = row.Street ?? string.Empty; // ✅ Optional field
                    student.HouseNumber = row.HouseNumber?.Trim() ?? string.Empty; // ✅ Optional field
                    student.City = row.City;
                    student.PostCode = row.PostCode ?? string.Empty; // ✅ Optional field
                    student.SendingCouncil = councilId;
                    student.StatusId = 1;
                });

            if (!studentId.HasValue)
            {
                _logger.LogError("❌ Failed to create student {IdNumber}", row.IdNumber);
            }
        }

        private async Task<int?> ResolveCouncilIdAsync(
                string councilValue,
                ProcessingResult result,
                string studentIdNumber)
        {
            if (string.IsNullOrWhiteSpace(councilValue) || councilValue == "99999")
                return null;

            // Try numeric ID first (backwards compatibility)
            if (int.TryParse(councilValue, out int numericId))
            {
                return numericId;
            }

            // Try as council name
            var councilId = await _globalFunctions.GetCouncilByName(councilValue);
            if (councilId != null)
            {
                _logger.LogInformation("Resolved council '{Name}' to ID {Id}",
                    councilValue, councilId);
                return councilId;
            }

            // Not found
            result.Errors.Add($"{studentIdNumber} - רשות שולחת '{councilValue}' לא נמצאה במערכת");
            return null;
        }

        private async Task UpdateStudentWithNewVersionAsync(
            SchoolStudent existing,
            StudentFileRow row,
            string userId,
            int classId,
            int? councilId)
        {
            var newVersionId = await _studentService.CreateNewStudentVersionAsync(
                existing.Id,
                newVersion =>
                {
                    // Update all fields from file
                    var hebrewCulture = CultureInfo.GetCultureInfo("he-IL");

                    newVersion.FirstName = row.FirstName;
                    newVersion.LastName = row.LastName;
                    newVersion.Gender = ParseGender(row.Gender);
                    newVersion.ClassId = classId;

                    // Parse dates with Israeli culture
                    newVersion.StartDate = DateOnly.FromDateTime(DateTime.Parse(row.StartDate, hebrewCulture));
                    newVersion.EndDate = DateOnly.FromDateTime(DateTime.Parse(row.EndDate, hebrewCulture));

                    newVersion.DisabilityCategory = string.IsNullOrWhiteSpace(row.DisabilityCategory)
                        ? null
                        : (int?)int.Parse(row.DisabilityCategory);
                    newVersion.Street = row.Street ?? string.Empty; // ✅ Optional field
                    newVersion.HouseNumber = row.HouseNumber?.Trim() ?? string.Empty; // ✅ Optional field
                    newVersion.City = row.City;
                    newVersion.PostCode = row.PostCode ?? string.Empty; // ✅ Optional field
                    newVersion.SendingCouncil = councilId;
                    newVersion.StatusId = 1;
                    // Note: Cost is NOT updated here - it's preserved from existing version
                });

            if (!newVersionId.HasValue)
            {
                _logger.LogError("❌ Failed to create new version for student {IdNumber}", existing.IdNumber);
            }
        }

        private int? ParseGender(string? gender)
        {
            // ✅ Default to 99 (unknown) for null/empty values
            if (string.IsNullOrWhiteSpace(gender))
                return 99;

            return gender.ToUpper() switch
            {
                "1" => 1,
                "2" => 2,
                "99" => 99,
                "זכר" => 1,
                "נקבה" => 2,
                _ => 99 // Default unknown for unrecognized values
            };
        }

        /// <summary>
        /// Check if ID number validation is enabled via system attribute.
        /// </summary>
        private async Task<bool> ShouldValidateIdNumberAsync()
        {
            try
            {
                var attribute = await _context.SystemAttributes
                    .AsNoTracking()
                    .FirstOrDefaultAsync(a => a.Name == "validate_israeli_id_checksum");

                if (attribute != null && bool.TryParse(attribute.Value, out bool isEnabled))
                {
                    _logger.LogInformation("Israeli ID validation enabled: {IsEnabled}", isEnabled);
                    return isEnabled;
                }

                _logger.LogInformation("Israeli ID validation attribute not found, defaulting to false");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking ID validation setting, defaulting to false");
                return false;
            }
        }

        /// <summary>
        /// Validates Israeli ID number using checksum algorithm.
        /// Uses the Luhn-like algorithm for Israeli ID validation.
        /// </summary>
        private bool IsValidIsraeliId(string idNumber)
        {
            if (string.IsNullOrWhiteSpace(idNumber) || idNumber.Length != 9 || !idNumber.All(char.IsDigit))
                return false;

            int sum = 0;
            for (int i = 0; i < 9; i++)
            {
                int digit = int.Parse(idNumber[i].ToString());
                
                // Multiply by 1 or 2 alternately (1 for even positions, 2 for odd positions)
                int multipliedValue = digit * ((i % 2) + 1);
                
                // If result is greater than 9, subtract 9
                if (multipliedValue > 9)
                    multipliedValue -= 9;
                
                sum += multipliedValue;
            }

            // Valid if sum is divisible by 10
            bool isValid = sum % 10 == 0;
            
            if (!isValid)
            {
                _logger.LogWarning("Invalid Israeli ID checksum for {IdNumber}, sum={Sum}", idNumber, sum);
            }
            
            return isValid;
        }
    }

    /// <summary>
    /// Represents a student row from the uploaded file.
    /// </summary>
    public class StudentFileRow
    {
        public required string IdNumber { get; set; }
        public required string FirstName { get; set; }
        public required string LastName { get; set; }
        public string? Gender { get; set; } // ✅ Optional - defaults to 99 (unknown)
        public required string Class { get; set; }
        public required string StartDate { get; set; }
        public required string EndDate { get; set; }
        public string? DisabilityCategory { get; set; }
        public string? Street { get; set; } // ✅ Optional
        public string? HouseNumber { get; set; } // ✅ Optional
        public required string City { get; set; }
        public string? PostCode { get; set; } // ✅ Optional
        public required string SendingCouncil { get; set; }
    }

    /// <summary>
    /// Processing result summary.
    /// </summary>
    public class ProcessingResult
    {
        public int Created { get; set; }
        public int Updated { get; set; }
        public List<string> Unchanged { get; set; } = new();
        public List<string> Errors { get; set; } = new();

        public int TotalProcessed => Created + Updated + Unchanged.Count;
        public int TotalErrors => Errors.Count;
    }
}