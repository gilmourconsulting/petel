namespace PetelApp.Api.Models.DTOs
{
    /// <summary>
    /// Login request DTO following Authentication & Session Management pattern
    /// </summary>
    public class LoginRequestDto
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public int EntityId { get; set; }
    }
}