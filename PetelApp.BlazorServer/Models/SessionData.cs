namespace PetelApp.BlazorServer.Models
{
    public class SessionData
    {
        public string SessionId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string UserFullName { get; set; } = string.Empty;
        public string EntityId { get; set; } = string.Empty;
        public string EntityName { get; set; } = string.Empty;
        public string EntityTypeId { get; set; } = string.Empty;
        public string EntityTypeName { get; set; } = string.Empty;
        public List<int> Roles { get; set; } = new();
    }

    public class LoginRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public int EntityId { get; set; }
    }

    public class LoginResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public bool RequiresOtp { get; set; }
        public string? TempToken { get; set; }
    }
}
