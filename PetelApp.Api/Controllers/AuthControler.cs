[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
[HttpPost("login")]
public async Task<IActionResult> Login([FromBody] LoginRequest request)
{
    // Get tenant ID from header or request
    var tenantId = Request.Headers["X-Tenant-ID"].FirstOrDefault() 
                   ?? request.TenantId.ToString();

    // Validate user exists in this tenant
    var user = await _userService.ValidateUserAsync(
        request.Username, 
        request.Password, 
        int.Parse(tenantId)
    );

    if (user == null)
        return Unauthorized("Invalid credentials or tenant access denied");

    // Generate tenant-specific JWT token
    var token = _tokenService.GenerateToken(user, tenantId);
    
    return Ok(new { 
        success = true, 
        token = token,
        tenantId = tenantId,
        tenantName = user.Entity.Name
    });
}
}

public class LoginRequest
{
    public string Username { get; set; }
    public string Password { get; set; }
    public int EntityId { get; set; }
}

