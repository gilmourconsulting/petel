using System;

namespace PetelATH.Api.DTOs
{
    public class ActionAuditLogDto
    {
        public string EventType { get; set; } = string.Empty;
        public string ScreenName { get; set; } = string.Empty;
        public string FunctionName { get; set; } = string.Empty;
        public string ActionName { get; set; } = string.Empty;
        public string Result { get; set; } = string.Empty;
        public string? ActionParams { get; set; }
        public string? Description { get; set; }
    }
}