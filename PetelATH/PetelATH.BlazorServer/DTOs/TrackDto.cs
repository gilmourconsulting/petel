namespace PetelATH.BlazorServer.DTOs
{
    public class TrackDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int YearId { get; set; }
        public string? ExternalCode { get; set; }
        public string[]? AvailableForClasses { get; set; }
    }

    public class TracksResponse
    {
        public bool Success { get; set; }
        public List<TrackDto> Data { get; set; } = new();
        public string? Message { get; set; }
    }
}
