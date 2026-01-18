namespace PetelApp.BlazorServer.DTOs
{
    public class TrackLevelDto
    {
        public int Id { get; set; }
        public string LevelName { get; set; } = string.Empty;
        public string Level { get; set; } = string.Empty;
        public int MinHours { get; set; }
        public int? MaxHours { get; set; }
        public int TrackId { get; set; }
    }

    public class TrackLevelsResponse
    {
        public bool Success { get; set; }
        public List<TrackLevelDto> Data { get; set; } = new();
        public string? Message { get; set; }
    }
}
