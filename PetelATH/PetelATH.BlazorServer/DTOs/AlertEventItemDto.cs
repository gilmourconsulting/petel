using System.Text.Json.Serialization;

namespace PetelATH.BlazorServer.DTOs;

public class AlertEventItemDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
    
    [JsonPropertyName("alertType")]
    public int AlertType { get; set; }
    
    [JsonPropertyName("alertLevel")]
    public int AlertLevel { get; set; }
    
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
    
    [JsonPropertyName("status")]
    public int Status { get; set; }
    
    [JsonPropertyName("userId")]
    public int UserId { get; set; }
    
    [JsonPropertyName("isEvent")]
    public bool IsEvent { get; set; }
    
    [JsonPropertyName("eventDate")]
    public DateTime? EventDate { get; set; }
    
    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }
    
    [JsonPropertyName("linkId")]
    public long LinkId { get; set; }
    
    [JsonPropertyName("entityId")]
    public int EntityId { get; set; }
    
    [JsonPropertyName("createdByEntityName")]
    public string? CreatedByEntityName { get; set; }
}
