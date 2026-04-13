using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DocumentFormat.OpenXml.Spreadsheet;

namespace PetelATH.Api.Data
{
    /// <summary>
    /// Represents a Hebrew year in the system.
    /// </summary>
    /// 
        [Table("hebrew_years")]
    public class HebrewYear
    {
        [Column("id")]
        public required int Id { get; set; }
        [Column("hebrew_year")]
        public required string HebrewYearText { get; set; }
    }
}