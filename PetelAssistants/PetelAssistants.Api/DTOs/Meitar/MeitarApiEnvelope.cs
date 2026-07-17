using System.Text.Json.Serialization;

namespace PetelAssistants.Api.DTOs.Meitar
{
    public class MeitarApiEnvelope<T>
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("data")]
        public T? Data { get; set; }
    }
}
