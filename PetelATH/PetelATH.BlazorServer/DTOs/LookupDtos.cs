namespace PetelATH.BlazorServer.DTOs
{
    /// <summary>
    /// Common shape for a configuration lookup row shown on the Settings screen.
    /// Fields not relevant to a particular table stay null.
    /// </summary>
    public class LookupItemDto
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? Object { get; set; }
        public int? SortOrder { get; set; }
        public bool? IsCredit { get; set; }
        public bool IsActive { get; set; } = true;
    }

    /// <summary>
    /// Writable fields sent when creating/updating a lookup row.
    /// </summary>
    public class LookupSaveRequest
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? Object { get; set; }
        public int? SortOrder { get; set; }
        public bool? IsCredit { get; set; }
        public bool? IsActive { get; set; }
    }

    /// <summary>
    /// Standard write-operation response from the lookups API.
    /// </summary>
    public class LookupApiResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
    }
}
