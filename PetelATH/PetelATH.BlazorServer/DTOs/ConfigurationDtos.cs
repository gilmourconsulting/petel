namespace PetelATH.BlazorServer.DTOs;

public class ConfigurationDto
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ConfigurationType Type { get; set; } = ConfigurationType.String;
}

public class RateLimitConfigurationDto
{
    public bool Enabled { get; set; } = true;
    public int LoginLimit { get; set; } = 10;
    public int OtpLimit { get; set; } = 5;
    public int ApiLimit { get; set; } = 100;
    public int HourlyLimit { get; set; } = 3000;
}

public class SecurityConfigurationDto
{
    public bool OtpEnabled { get; set; } = true;
    public int SessionTimeoutMinutes { get; set; } = 30;
    public int MaxPasswordAttempts { get; set; } = 5;
    public int MaxOtpAttempts { get; set; } = 3;
    public string OtpIssuer { get; set; } = "Petel System";
}

public class MaintenanceConfigurationDto
{
    public bool Enabled { get; set; } = false;
    public string Message { get; set; } = "המערכת נמצאת במצב תחזוקה. אנא נסו שוב מאוחר יותר.";
}

public class ConfigurationUpdateRequestDto
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public class RateLimitUpdateRequestDto
{
    public bool Enabled { get; set; }
    public int LoginLimit { get; set; }
    public int OtpLimit { get; set; }
    public int ApiLimit { get; set; }
    public int HourlyLimit { get; set; }
}

public class MaintenanceToggleRequestDto
{
    public bool Enabled { get; set; }
    public string? Message { get; set; }
}

public class AllConfigurationResponseDto
{
    public List<ConfigurationDto> RateLimit { get; set; } = new();
    public List<ConfigurationDto> Security { get; set; } = new();
    public List<ConfigurationDto> System { get; set; } = new();
}

public enum ConfigurationType
{
    String = 1,
    Integer = 2,
    Boolean = 3,
    Decimal = 4
}