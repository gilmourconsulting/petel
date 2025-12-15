namespace PetelApp.Api.Configuration
{
    public class SecuritySettings
    {
        public bool OtpEnabled { get; set; }
        public string OtpIssuer { get; set; } = "Petel System";
    }
}