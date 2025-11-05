// PetelApp.Api/Data/Person.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PetelApp.Api.Data
{
    /// <summary>
    /// Person entity representing individuals (principals, inspectors, contact persons)
    /// Maps to petel_schema.persons table
    /// </summary>
    [Table("persons", Schema = "petel_schema")]
    public class Person
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("first_name")]
        [MaxLength(100)]
        public string? FirstName { get; set; }

        [Column("last_name")]
        [MaxLength(100)]
        public string? LastName { get; set; }

        [Column("email")]
        [MaxLength(255)]
        public string? Email { get; set; }


        [Column("phone_number_prefix")]
        public string? PhoneNumberPrefix { get; set; }

        [Column("phone_number")]
        public string? PhoneNumber { get; set; }

         [Column("position")]
        public string? Position { get; set; }

        /// <summary>
        /// Helper property to get full name
        /// </summary>
        [NotMapped]
        public string FullName => $"{FirstName} {LastName}".Trim();
    }
}
