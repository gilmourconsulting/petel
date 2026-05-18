namespace PetelATH.Api.Models.DTOs
{
    public class DashboardSummaryDto
    {
        public List<StatItemDto> Stats { get; set; } = new();
        public string EntityTypeName { get; set; } = string.Empty;
        public int? YearId { get; set; }
    }

    public class StatItemDto
    {
        public string Label { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string AccentColor { get; set; } = "#667eea";
        public string? SubLabel { get; set; }
    }
}
