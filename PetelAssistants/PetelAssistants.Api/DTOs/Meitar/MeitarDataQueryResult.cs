namespace PetelAssistants.Api.DTOs.Meitar
{
    public class MeitarDataQueryResult
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? FileName { get; set; }
        public int RowCount { get; set; }
        public IReadOnlyList<System.Text.Json.JsonElement> Rows { get; set; } = Array.Empty<System.Text.Json.JsonElement>();

        public static MeitarDataQueryResult FromResponse(MeitarDataQueryResponse response)
        {
            return new MeitarDataQueryResult
            {
                Success = true,
                FileName = response.FileName,
                RowCount = response.RowCount,
                Rows = response.Rows
            };
        }

        public static MeitarDataQueryResult Failed(string message)
        {
            return new MeitarDataQueryResult
            {
                Success = false,
                Message = message
            };
        }
    }
}
