using System.Text.Json;
using System.Text.Json.Serialization;

namespace PetelAssistants.Api.DTOs.Meitar
{
    public class MeitarDataQueryResponse
    {
        [JsonPropertyName("fileName")]
        public string? FileName { get; set; }

        [JsonPropertyName("rowCount")]
        public int RowCount { get; set; }

        [JsonPropertyName("rows")]
        public List<JsonElement> Rows { get; set; } = new();
    }
}
