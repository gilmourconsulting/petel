namespace PetelAssistants.Api.Configuration
{
    public class MeitarApiSettings
    {
        public string BaseUrl { get; set; } = string.Empty;
        public int TimeoutSeconds { get; set; } = 60;
    }
}
