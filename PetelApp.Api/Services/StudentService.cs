// PetelApp.Api/Services/StudentService.cs
using Microsoft.EntityFrameworkCore;
using PetelApp.Api.Data;

namespace PetelApp.Api.Services
{
    /// <summary>
    /// Service for managing student records and versioning
    /// </summary>
    public class StudentService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<StudentService> _logger;

        public StudentService(AppDbContext context, ILogger<StudentService> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Create a new version of a student record with selective field updates
        /// </summary>
        /// <param name="existingStudentId">ID of the current student version</param>
        /// <param name="updates">Action to apply updates to the new version</param>
        /// <returns>ID of the newly created version, or null if failed</returns>
        public async Task<int?> CreateNewStudentVersionAsync(
            int existingStudentId, 
            Action<SchoolStudent> updates)
        {
            try
            {
                // Get existing student record (not AsNoTracking - we need to update it)
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
                    "📝 Marking existing student as old version: Id={Id}, IdNumber={IdNumber}, Version={Version}",
                    existingStudent.Id, existingStudent.IdNumber, existingStudent.Version);

                // Create new version by copying all fields
                var newVersion = new SchoolStudent
                {
                    // Id is auto-generated
                    SchoolYearId = existingStudent.SchoolYearId,
                    IdNumber = existingStudent.IdNumber,
                    Version = existingStudent.Version + 1,
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
                    "✅ Created new student version: OldId={OldId}, NewId={NewId}, Version={Version}",
                    existingStudent.Id, newVersion.Id, newVersion.Version);

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
                    IsLastVersion = true
                };

                // Apply configuration
                configure?.Invoke(newStudent);

                _context.SchoolStudents.Add(newStudent);
                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "✅ Created new student: Id={Id}, IdNumber={IdNumber}, Version=0",
                    newStudent.Id, newStudent.IdNumber);

                return newStudent.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error creating new student {IdNumber}", idNumber);
                return null;
            }
        }

        /// <summary>
        /// Get the latest version of a student
        /// </summary>
        public async Task<SchoolStudent?> GetLatestVersionAsync(string idNumber, int schoolYearId)
        {
            return await _context.SchoolStudents
                .Where(s => s.IdNumber == idNumber && 
                           s.SchoolYearId == schoolYearId && 
                           s.IsLastVersion)
                .FirstOrDefaultAsync();
        }

        /// <summary>
        /// Get all versions of a student
        /// </summary>
        public async Task<List<SchoolStudent>> GetVersionHistoryAsync(string idNumber, int schoolYearId)
        {
            return await _context.SchoolStudents
                .Where(s => s.IdNumber == idNumber && s.SchoolYearId == schoolYearId)
                .OrderBy(s => s.Version)
                .ToListAsync();
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