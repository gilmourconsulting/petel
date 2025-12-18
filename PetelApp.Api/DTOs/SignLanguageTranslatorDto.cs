namespace PetelApp.Api.DTOs
{
    public class SignLanguageTranslatorDto
    {
        public int Id { get; set; }
        public int SchoolYearId { get; set; }
        public int PersonId { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? NationalId { get; set; }
        public decimal HoursEmployed { get; set; }
    }

    public class CreateSignLanguageTranslatorDto
    {
        public int SchoolYearId { get; set; }
        public int PersonId { get; set; }
        public decimal HoursEmployed { get; set; }
    }

    public class UpdateSignLanguageTranslatorDto
    {
        public decimal HoursEmployed { get; set; }
    }
}