namespace PetelATH.BlazorServer.Models;

public class AlertDto
{
    public int Id { get; set; }
    public int AlertType { get; set; }  // int FK to alert_types, not string
    public int AlertLevel { get; set; }
    public string Description { get; set; } = string.Empty;
    public int Status { get; set; }  // Status is int, not string
    public int UserId { get; set; }
    public bool IsEvent { get; set; }
    public DateTime? EventDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public long LinkId { get; set; }
}