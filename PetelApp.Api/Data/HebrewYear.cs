namespace PetelApp.Api.Data
{
    /// <summary>
    /// Represents a Hebrew year in the system.
    /// </summary>
    public class HebrewYear
    {
        public required int Id { get; set; }
        public required string HebrewYearText { get; set; }
    }
}