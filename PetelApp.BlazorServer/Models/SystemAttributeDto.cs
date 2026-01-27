namespace PetelApp.BlazorServer.Models
{
    /// <summary>
    /// Data transfer object for system attributes
    /// </summary>
    public class SystemAttributeDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string ValueType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int? UpdateUser { get; set; }
        public int? ForeignId { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
