using Microsoft.EntityFrameworkCore;
using PetelApp.Api.Data;


namespace PetelApp.Api.Services
{

    //private readonly GlobalFunctions _globalFunctions;
    public class StudentsFileProcessor
    {

        private readonly GlobalFunctions _globalFunctions;
        private readonly AppDbContext _context;
        private readonly ILogger<StudentsFileProcessor> _logger;
        private readonly StudentService _studentService;  // ✅ NEW

        public StudentsFileProcessor(
            AppDbContext context, 
            ILogger<StudentsFileProcessor> logger,
            GlobalFunctions globalFunctions,
            StudentService studentService)  // ✅ NEW
        {
            _context = context;
            _logger = logger;
            _globalFunctions = globalFunctions;
            _studentService = studentService;  // ✅ NEW
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

            // Validate data format
            var (isValid, formatError) = ValidateRowFormat(row);
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

            // Retrieve existing record with is_last_version = true for this school year

            var existingStudent = await _context.SchoolStudents
                 .Where(s => s.IdNumber == row.IdNumber &&
                            s.IsLastVersion == true &&
                            s.SchoolYearId == schoolYearId)
                 .FirstOrDefaultAsync();

            if (existingStudent == null)
            {
                // Create new record
                await CreateNewStudentAsync(row, schoolId, schoolYearId, userId, classId.Value);
                result.Created++;
                _logger.LogInformation("Created new student record: {IdNumber}", row.IdNumber);
            }
            else
            {
                // Check if data has changed
                bool hasChanges = HasDataChanged(existingStudent, row, classId.Value);

                if (!hasChanges)
                {
                    result.Unchanged.Add($"{row.IdNumber} - נתונים לא השתנו");
                    _logger.LogInformation("Student data unchanged: {IdNumber}", row.IdNumber);
                }
                else
                {
                    // Update existing record and create new version
                    await UpdateStudentWithNewVersionAsync(existingStudent, row, userId, classId.Value);
                    result.Updated++;
                    _logger.LogInformation("Updated student record: {IdNumber}, new version {Version}",
                        row.IdNumber, existingStudent.Version + 1);
                }
            }
        }

        private (bool isValid, string? error) ValidateRowFormat(StudentFileRow row)
        {
            // Validate ID number (9 digits)
            if (string.IsNullOrWhiteSpace(row.IdNumber) || row.IdNumber.Length != 9 || !row.IdNumber.All(char.IsDigit))
                return (false, "מספר תעודת זהות לא תקין");

            // Validate first name
            if (string.IsNullOrWhiteSpace(row.FirstName))
                return (false, "שם פרטי חסר");

            // Validate last name
            if (string.IsNullOrWhiteSpace(row.LastName))
                return (false, "שם משפחה חסר");

            // Validate gender
            if (string.IsNullOrWhiteSpace(row.Gender) || !new[] { "1", "2", "זכר", "נקבה" }.Contains(row.Gender))
                return (false, "מין לא תקין");

            // Validate class
            if (string.IsNullOrWhiteSpace(row.Class))
                return (false, "כיתה חסרה");

            // Validate dates
            if (!DateTime.TryParse(row.StartDate, out _))
                return (false, "תאריך התחלה לא תקין");

            if (!DateTime.TryParse(row.EndDate, out _))
                return (false, "תאריך סיום לא תקין");

            // Validate disability category (integer or empty for none)
            if (!string.IsNullOrWhiteSpace(row.DisabilityCategory) && !int.TryParse(row.DisabilityCategory, out _))
                return (false, "קטגוריית נכות לא תקינה");

            // Validate address fields
            if (string.IsNullOrWhiteSpace(row.Street))
                return (false, "רחוב חסר");

            if (string.IsNullOrWhiteSpace(row.HouseNumber))
                return (false, "מספר בית חסר");

            if (string.IsNullOrWhiteSpace(row.City))
                return (false, "עיר חסרה");

            if (string.IsNullOrWhiteSpace(row.PostCode))
                return (false, "מיקוד חסר");

            // Validate sending council (integer or 99999 for none)
            if (string.IsNullOrWhiteSpace(row.SendingCouncil) || !int.TryParse(row.SendingCouncil, out _))
                return (false, "מועצה שולחת לא תקינה");

            return (true, null);
        }

        private bool HasDataChanged(SchoolStudent existing, StudentFileRow row, int  classId )
        {
            var rowGender = ParseGender(row.Gender);
            var rowDisabilityCategory = string.IsNullOrWhiteSpace(row.DisabilityCategory) ? null : (int?)int.Parse(row.DisabilityCategory);
            var rowSendingCouncil = row.SendingCouncil == "99999" ? null : (int?)int.Parse(row.SendingCouncil);

            return existing.FirstName != row.FirstName ||
                   existing.LastName != row.LastName ||
                   existing.Gender != rowGender ||
                   existing.ClassId != classId  ||
                   existing.StartDate?.ToString("yyyy-MM-dd") != DateTime.Parse(row.StartDate).ToString("yyyy-MM-dd") ||
                   existing.EndDate?.ToString("yyyy-MM-dd") != DateTime.Parse(row.EndDate).ToString("yyyy-MM-dd") ||
                   existing.DisabilityCategory != rowDisabilityCategory ||
                   existing.Street != row.Street ||
                   existing.HouseNumber != row.HouseNumber ||
                   existing.City != row.City ||
                   existing.PostCode != row.PostCode ||
                   existing.SendingCouncil != rowSendingCouncil;
        }

        private async Task CreateNewStudentAsync(
            StudentFileRow row,
            int schoolId,
            int schoolYearId,
            string userId,
            int classId)
        {
            var studentId = await _studentService.CreateNewStudentAsync(
                schoolYearId,
                row.IdNumber,
                student =>
                {
                    // Configure all fields
                    student.FirstName = row.FirstName;
                    student.LastName = row.LastName;
                    student.Gender = ParseGender(row.Gender);
                    student.ClassId = classId;
                    student.StartDate = DateOnly.Parse(row.StartDate);
                    student.EndDate = DateOnly.Parse(row.EndDate);
                    student.DisabilityCategory = string.IsNullOrWhiteSpace(row.DisabilityCategory) 
                        ? null 
                        : (int?)int.Parse(row.DisabilityCategory);
                    student.Street = row.Street;
                    student.HouseNumber = row.HouseNumber;
                    student.City = row.City;
                    student.PostCode = row.PostCode;
                    student.SendingCouncil = row.SendingCouncil == "99999" 
                        ? null 
                        : (int?)int.Parse(row.SendingCouncil);
                });

            if (studentId.HasValue)
            {
                _logger.LogInformation("✅ Created new student with ID {StudentId}", studentId.Value);
            }
            else
            {
                _logger.LogError("❌ Failed to create student {IdNumber}", row.IdNumber);
            }
        }

        private async Task UpdateStudentWithNewVersionAsync(
            SchoolStudent existing,
            StudentFileRow row,
            string userId,
            int classId)
        {
            var newVersionId = await _studentService.CreateNewStudentVersionAsync(
                existing.Id,
                newVersion =>
                {
                    // Update all fields from file
                    newVersion.FirstName = row.FirstName;
                    newVersion.LastName = row.LastName;
                    newVersion.Gender = ParseGender(row.Gender);
                    newVersion.ClassId = classId;
                    newVersion.StartDate = DateOnly.Parse(row.StartDate);
                    newVersion.EndDate = DateOnly.Parse(row.EndDate);
                    newVersion.DisabilityCategory = string.IsNullOrWhiteSpace(row.DisabilityCategory) 
                        ? null 
                        : (int?)int.Parse(row.DisabilityCategory);
                    newVersion.Street = row.Street;
                    newVersion.HouseNumber = row.HouseNumber;
                    newVersion.City = row.City;
                    newVersion.PostCode = row.PostCode;
                    newVersion.SendingCouncil = row.SendingCouncil == "99999" 
                        ? null 
                        : (int?)int.Parse(row.SendingCouncil);
                    // Note: Cost is NOT updated here - it's preserved from existing version
                });

            if (newVersionId.HasValue)
            {
                _logger.LogInformation(
                    "✅ Updated student: OldId={OldId}, NewId={NewId}, Version={Version}",
                    existing.Id, newVersionId.Value, existing.Version + 1);
            }
            else
            {
                _logger.LogError("❌ Failed to create new version for student {IdNumber}", existing.IdNumber);
            }
        }

        private int? ParseGender(string gender)
        {
            return gender?.ToUpper() switch
            {
                "1" => 1,
                "2" => 2,
                "זכר" => 1,
                "נקבה" => 2,
                _ => 99 // Default unknown
            };
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
        public required string Gender { get; set; }
        public required string Class { get; set; }
        public required string StartDate { get; set; }
        public required string EndDate { get; set; }
        public string? DisabilityCategory { get; set; }
        public required string Street { get; set; }
        public required string HouseNumber { get; set; }
        public required string City { get; set; }
        public required string PostCode { get; set; }
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