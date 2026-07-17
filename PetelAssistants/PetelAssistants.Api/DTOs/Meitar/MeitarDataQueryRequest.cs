using System.Text.Json.Serialization;

namespace PetelAssistants.Api.DTOs.Meitar
{
    public class MeitarDataQueryRequest
    {
        [JsonPropertyName("symbolList")]
        public List<string> SymbolList { get; set; } = new();

        [JsonPropertyName("fileName")]
        public string FileName { get; set; } = string.Empty;

        [JsonPropertyName("filterField")]
        public string FilterField { get; set; } = string.Empty;

        [JsonPropertyName("filterValueList")]
        public List<string> FilterValueList { get; set; } = new();
    }
}
