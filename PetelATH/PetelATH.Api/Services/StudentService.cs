// PetelATH.Api/Services/StudentService.cs
using Microsoft.EntityFrameworkCore;
using PetelATH.Api.Data;

namespace PetelATH.Api.Services
{
    /// <summary>
    /// Service for managing student records and versioning
    /// </summary>
    public class StudentService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<StudentService> _logger;

        public StudentService(
            AppDbContext context, 
            ILogger<StudentService> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Create a new version of a student record with selective field updates
        /// ✅ CRITICAL: Preserves master_student_id across versions
        /// </summary>
        public async Task<int?> CreateNewStudentVersionAsync(
            int existingStudentId, 
            Action<SchoolStudent> updates)
        {
            try
            {
                var existingStudent = await _context.SchoolStudents
                    .FirstOrDefaultAsync(s => s.Id == existingStudentId);

                if (existingStudent == null)
                {
                    _logger.LogError("Student with ID {StudentId} not found", existingStudentId);
                    return null;
                }

                // Mark existing record as not last version
                existingStudent.IsLastVersion = false;
                _context.SchoolStudents.Update(existingStudent);

                _logger.LogInformation(
                    "📝 Marking existing student as old version: Id={Id}, MasterStudentId={MasterId}, Version={Version}",
                    existingStudent.Id, existingStudent.MasterStudentId, existingStudent.Version);

                // ✅ Create new version - PRESERVING master_student_id
                var newVersion = new SchoolStudent
                {
                    // Id is auto-generated
                    SchoolYearId = existingStudent.SchoolYearId,
                    IdNumber = existingStudent.IdNumber,
                    Version = existingStudent.Version + 1,
                    MasterStudentId = existingStudent.MasterStudentId, // ✅ CRITICAL: Keep same master ID
                    FirstName = existingStudent.FirstName,
                    LastName = existingStudent.LastName,
                    Gender = existingStudent.Gender,
                    ClassId = existingStudent.ClassId,
                    StartDate = existingStudent.StartDate,
                    EndDate = existingStudent.EndDate,
                    DisabilityCategory = existingStudent.DisabilityCategory,
                    Street = existingStudent.Street,
                    HouseNumber = existingStudent.HouseNumber,
                    City = existingStudent.City,
                    PostCode = existingStudent.PostCode,
                    SendingCouncil = existingStudent.SendingCouncil,
                    Cost = existingStudent.Cost,
                    IsLastVersion = true
                };

                // Apply selective updates via callback
                updates?.Invoke(newVersion);

                _context.SchoolStudents.Add(newVersion);
                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "✅ Created new student version: OldId={OldId}, NewId={NewId}, MasterStudentId={MasterId}, Version={Version}",
                    existingStudent.Id, newVersion.Id, newVersion.MasterStudentId, newVersion.Version);

                return newVersion.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error creating new student version for student {StudentId}", 
                    existingStudentId);
                return null;
            }
        }

        /// <summary>
        /// Create a new student record (Version 0)
        /// ✅ NEW: Sets master_student_id to its own id after creation
        /// </summary>
        public async Task<int?> CreateNewStudentAsync(
            int schoolYearId,
            string idNumber,
            Action<SchoolStudent> configure)
        {
            try
            {
                var newStudent = new SchoolStudent
                {
                    SchoolYearId = schoolYearId,
                    IdNumber = idNumber,
                    Version = 0,
                    MasterStudentId = 0, // Temporary - will be updated after save
                    IsLastVersion = true
                };

                // Apply configuration
                configure?.Invoke(newStudent);

                _context.SchoolStudents.Add(newStudent);
                await _context.SaveChangesAsync();

                // ✅ CRITICAL: Set master_student_id to own id for new students
                newStudent.MasterStudentId = newStudent.Id;
                _context.SchoolStudents.Update(newStudent);
                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "✅ Created new student: Id={Id}, MasterStudentId={MasterId}, IdNumber={IdNumber}, Version=0",
                    newStudent.Id, newStudent.MasterStudentId, newStudent.IdNumber);

                return newStudent.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error creating new student {IdNumber}", idNumber);
                return null;
            }
        }

        /// <summary>
        /// Get the latest version of a student by master_student_id
        /// ✅ NEW: Uses master_student_id instead of encrypted id_number
        /// </summary>
        public async Task<SchoolStudent?> GetLatestVersionByMasterIdAsync(int masterStudentId)
        {
            return await _context.SchoolStudents
                .Where(s => s.MasterStudentId == masterStudentId && s.IsLastVersion)
                .FirstOrDefaultAsync();
        }

        /// <summary>
        /// Get all versions of a student by master_student_id
        /// ✅ NEW: Uses master_student_id for history tracking
        /// </summary>
        public async Task<List<SchoolStudent>> GetVersionHistoryByMasterIdAsync(int masterStudentId)
        {
            return await _context.SchoolStudents
                .Where(s => s.MasterStudentId == masterStudentId)
                .OrderBy(s => s.Version)
                .ToListAsync();
        }

        /// <summary>
        /// Get the latest version of a student (legacy method - still needed for encrypted search)
        /// </summary>
        public async Task<SchoolStudent?> GetLatestVersionAsync(string idNumber, int schoolYearId)
        {
            // Load all latest versions for the year and compare in memory (encryption prevents DB search)
            var students = await _context.SchoolStudents
                .Where(s => s.SchoolYearId == schoolYearId && s.IsLastVersion)
                .ToListAsync();
            
            return students.FirstOrDefault(s => s.IdNumber == idNumber);
        }

        /// <summary>
        /// Get all versions of a student (legacy method - still needed for encrypted search)
        /// </summary>
        public async Task<List<SchoolStudent>> GetVersionHistoryAsync(string idNumber, int schoolYearId)
        {
            // Load all students for the year and filter in memory (encryption prevents DB search)
            var students = await _context.SchoolStudents
                .Where(s => s.SchoolYearId == schoolYearId)
                .OrderBy(s => s.Version)
                .ToListAsync();
            
            return students.Where(s => s.IdNumber == idNumber).ToList();
        }

        /// <summary>
        /// Check if data has changed compared to existing student
        /// </summary>
        public bool HasDataChanged(
            SchoolStudent existing,
            SchoolStudent proposed)
        {
            return existing.FirstName != proposed.FirstName ||
                   existing.LastName != proposed.LastName ||
                   existing.Gender != proposed.Gender ||
                   existing.ClassId != proposed.ClassId ||
                   existing.StartDate != proposed.StartDate ||
                   existing.EndDate != proposed.EndDate ||
                   existing.DisabilityCategory != proposed.DisabilityCategory ||
                   existing.Street != proposed.Street ||
                   existing.HouseNumber != proposed.HouseNumber ||
                   existing.City != proposed.City ||
                   existing.PostCode != proposed.PostCode ||
                   existing.SendingCouncil != proposed.SendingCouncil ||
                   existing.Cost != proposed.Cost;
        }
    }
}