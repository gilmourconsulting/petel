namespace PetelApp.Api.DTOs
{
    public class CreateSchoolDto
    {
        public string Name { get; set; } = string.Empty;
        public int EntityTypeId { get; set; }
        public int OwnerId { get; set; }
    }
}