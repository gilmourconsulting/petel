using System.Text.Json.Serialization;

namespace PetelAssistants.Api.DTOs.Meitar
{
    public class MeitarDataQueryRequest
    {
        [JsonPropertyName("symbolList")]
        public List<string> SymbolList { get; set; } = new();

        [JsonPropertyName("fileName")]
        public string FileName { get; set; } = string.Empty;

        [JsonPropertyName("filters")]
        public List<MeitarDataQueryFilter>? Filters { get; set; }

        [JsonPropertyName("periodList")]
        public List<string>? PeriodList { get; set; }
    }

    public class MeitarDataQueryFilter
    {
        [JsonPropertyName("field")]
        public string Field { get; set; } = string.Empty;

        [JsonPropertyName("valueList")]
        public List<string> ValueList { get; set; } = new();
    }
}
