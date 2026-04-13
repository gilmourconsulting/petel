namespace PetelATH.BlazorServer.DTOs
{
    public class SchoolInfoDto
    {
        public int SchoolId { get; set; }
        public int EntityId { get; set; }
        public int OwnerId { get; set; }
        public string SchoolName { get; set; } = string.Empty;
    }
}
