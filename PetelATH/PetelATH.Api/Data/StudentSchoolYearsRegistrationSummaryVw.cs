using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace PetelATH.Api.Data
{
    [Table("student_school_years_registration_summary_vw")]
    public class StudentSchoolYearsRegistrationSummaryVw
    {
        public int SchoolId { get; set; } // EntityId for tenant scoping
        public int SchoolYearId { get; set; }
        public string SchoolGrade { get; set; } = string.Empty;
        public string SchoolTrack { get; set; } = string.Empty;
        public int Registered { get; set; }
        // Add other properties as needed
    }
}