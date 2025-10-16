using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PetelApp.Api.Data
{
    [Table("school_students", Schema = "petel_schema")]
    public class SchoolStudent
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("id_number")]
        public string? IdNumber { get; set; }

        [Column("class_id")]
        public int? ClassId { get; set; }

        [Column("start_date")]
        public DateTime? StartDate { get; set; }

        [Column("end_date")]
        public DateTime? EndDate { get; set; }

        [Column("first_name")]
        public string? FirstName { get; set; }

        [Column("last_name")]
        public string? LastName { get; set; }

        [Column("gender")]
        public int? Gender { get; set; }

        [Column("street")]
        public string? Street { get; set; }

        [Column("house_number")]
        public string? HouseNumber { get; set; }

        [Column("city")]
        public string? City { get; set; }

        [Column("post_code")]
        public string? PostCode { get; set; }

        [Column("sending_council")]
        public int? SendingCouncil { get; set; }

        [Column("disability_category")]
        public int? DisabilityCategory { get; set; }

        [Column("school_year_id")]
        public int SchoolYearId { get; set; }

        [Column("is_last_version")]
        public bool IsLastVersion { get; set; }
    }
}