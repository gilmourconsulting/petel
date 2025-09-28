namespace PetelApp.Api.Models.DTOs
{
    /// <summary>
    /// Login response DTO following Authentication & Session Management pattern
    /// </summary>
    public class LoginResponseDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        // NO User property - violates Frontend Session Token Only pattern
    }
}