# Authentication & Session Management

> Canonical: docs/agents/core/auth-security.md


## Authentication & Session Management

### Authentication Flow
1. User logs in via `/api/auth/login` with username/password
2. Backend validates credentials, creates JWT token
3. Blazor frontend stores token via `TokenService` (from `Petel.BlazorCore`)
4. All subsequent API calls include `Authorization: Bearer {token}` header (added automatically by `ApiService`)
5. Backend validates token and retrieves user session from `UserSessionService`

### Session State in Blazor
**Backend**: Properties stored in `UserSession` object via `UserSessionService`
**Frontend**: Inject `SessionStateService` from `Petel.BlazorCore.Services`

```csharp
@inject SessionStateService SessionStateService

// Get full session data (cached 1 min)
var session = await SessionStateService.GetSessionAsync();

// Quick typed accessors
var entityId = SessionStateService.GetEntityId();
var userId   = SessionStateService.GetUserId();
var username = SessionStateService.GetUsername();
var name     = SessionStateService.GetEntityName();

// Force refresh after data change
var session = await SessionStateService.GetSessionAsync(forceRefresh: true);
```

### BaseController Pattern
All API controllers inherit from `BaseController` which provides:
- `GetCurrentSession()` - Retrieves full user session
- `GetSessionProperty(key)` - Gets specific session property
- Automatic EntityId scoping for all queries
- **NO `[Authorize]` attribute** - uses manual session validation

```csharp
public class MyController : BaseController
{
    public async Task<IActionResult> GetData()
    {
        var session = GetCurrentSession();
        if (session == null)
        {
            return Unauthorized(new { success = false, message = "× ×“×¨×© ××™×ž×•×ª" });
        }
        
        var entityId = int.Parse(session.EntityId);
        
        var data = await _context.MyEntities
            .Where(e => e.EntityId == entityId)
            .ToListAsync();
            
        return Ok(data);
    }
}
```

**IMPORTANT**: Controllers do NOT use `[Authorize]` attribute. Session validation is done manually via `GetCurrentSession()` in each endpoint.

### Document Proxy Pattern (IP Restrictions)

**Purpose**: When the API has IP restrictions that only allow server-to-server calls, browsers cannot directly access API endpoints. A proxy endpoint in the Blazor app forwards browser requests to the API.

**Note**: The system uses Azure App Service IP restrictions (Israeli IPs only) for geographic filtering.

**Architecture**:
```
Browser (with user token) â†’ Blazor Proxy (forwards token) â†’ API (validates token) â†’ Document
                             â†‘ Server IP is allowed           â†‘ User auth verified
```

**Benefits**:
- âœ… Maintains security - API still validates user's JWT token
- âœ… Bypasses IP restrictions - Blazor server IP is in API allowlist
- âœ… No code changes in API - uses existing authentication
- âœ… Transparent to frontend - JavaScript still uses normal fetch with Authorization header

**Implementation in Blazor Program.cs**:

```csharp
// Required using statements
using Microsoft.Extensions.Options;
using Petel.BlazorCore.Models;  // ApiSettings lives in Petel.BlazorCore

// In middleware pipeline (after UseAntiforgery())
app.MapGet("/api/documents/{documentId}/proxy", async (
    long documentId, 
    HttpContext httpContext,
    IHttpClientFactory httpClientFactory,
    IOptions<ApiSettings> apiSettings,
    ILogger<Program> logger) =>
{
    try
    {
        logger.LogInformation("ðŸ“¥ Document proxy request for ID: {DocumentId}", documentId);
        
        // âœ… Extract Authorization header from browser request
        if (!httpContext.Request.Headers.TryGetValue("Authorization", out var authHeader) ||
            string.IsNullOrEmpty(authHeader))
        {
            logger.LogWarning("âš ï¸ No authorization header in proxy request");
            return Results.Unauthorized();
        }

        // âœ… Create HTTP client and forward browser's token to API
        var client = httpClientFactory.CreateClient("PetelApi");
        client.DefaultRequestHeaders.Add("Authorization", authHeader.ToString());
        
        var apiUrl = $"{apiSettings.Value.BaseUrl}/Documents/{documentId}/download";
        logger.LogDebug("Proxying request to: {ApiUrl}", apiUrl);
        
        var apiResponse = await client.GetAsync(apiUrl);
        
        if (!apiResponse.IsSuccessStatusCode)
        {
            logger.LogWarning("âš ï¸ API returned {StatusCode} for document {DocumentId}", 
                apiResponse.StatusCode, documentId);
            
            if (apiResponse.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                return Results.Unauthorized();
            
            if (apiResponse.StatusCode == System.Net.HttpStatusCode.NotFound)
                return Results.NotFound(new { error = "×ž×¡×ž×š ×œ× × ×ž×¦×" });
            
            return Results.Problem($"×©×’×™××” ×‘×˜×¢×™× ×ª ×”×ž×¡×ž×š: {apiResponse.StatusCode}");
        }
        
        var content = await apiResponse.Content.ReadAsByteArrayAsync();
        var contentType = apiResponse.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
        
        // âœ… Extract filename from Content-Disposition header
        var fileName = $"document_{documentId}";
        if (apiResponse.Content.Headers.ContentDisposition?.FileName != null)
        {
            fileName = apiResponse.Content.Headers.ContentDisposition.FileName.Trim('"');
        }
        
        logger.LogInformation("âœ… Returning document {DocumentId}, size: {Size} bytes", 
            documentId, content.Length);
        
        return Results.File(content, contentType, fileName);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "âŒ Error proxying document {DocumentId}", documentId);
        return Results.Problem("×©×’×™××” ×‘×˜×¢×™× ×ª ×”×ž×¡×ž×š");
    }
})
.DisableAntiforgery(); // âœ… Required for GET requests from browser
```

**Frontend Integration** (no changes needed):

```javascript
// blazorHelpers.js - existing code works unchanged
viewFileWithAuth: async function (url, token) {
    const response = await fetch(url, {
        method: 'GET',
        headers: {
            'Authorization': `Bearer ${token}` // âœ… Forwarded by proxy
        }
    });
    
    const blob = await response.blob();
    const blobUrl = window.URL.createObjectURL(blob);
    window.open(blobUrl, '_blank');
}

// Blazor component - use proxy URL
var downloadUrl = $"/api/documents/{documentId}/proxy";
await JSRuntime.InvokeVoidAsync("BlazorHelpers.viewFileWithAuth", downloadUrl, token);
```

**API Endpoint** (existing, no changes):

```csharp
// DocumentsController.cs - works as-is
[HttpGet("{id}/download")]
public async Task<IActionResult> DownloadDocument(long id)
{
    var session = GetCurrentSession();
    if (session == null)
        return Unauthorized(new { error = "× ×“×¨×© ××™×ž×•×ª" });

    var document = await _context.Documents.FindAsync(id);
    
    return File(document.FileBlob, contentType, fileName);
}
```

**When to Use This Pattern**:
- âœ… API has IP restrictions (Azure App Service IP filtering)
- âœ… Browser needs to download/view files from API
- âœ… Need to maintain user authentication with JWT tokens
- âœ… Server-to-server calls are allowed in security architecture

**Anti-Patterns**:
```csharp
// âŒ WRONG - Using ApiService in Minimal API endpoint
app.MapGet("/proxy", async (ApiService apiService) =>
{
    var file = await apiService.GetFileAsync(...); // NO! ApiService needs Blazor circuit
});

// âŒ WRONG - Not forwarding Authorization header
var client = httpClientFactory.CreateClient("PetelApi");
var response = await client.GetAsync(url); // NO! Missing user's token

// âœ… CORRECT - Forward browser's Authorization header
client.DefaultRequestHeaders.Add("Authorization", authHeader.ToString());
```

**Troubleshooting**:
- **404 errors**: Verify `UseRouting()` is called before `MapGet()` in Program.cs
- **401 errors**: Check Authorization header is being forwarded correctly
- **403 errors**: Verify Blazor server IP is in API's IP allowlist (Azure App Service IP restrictions)
## Security Implementation

### JWT Token Authentication

**Architecture**: Application uses signed JWT tokens instead of GUID-based session tokens for enhanced security.

**Required Package**:
```xml
<PackageReference Include="System.IdentityModel.Tokens.Jwt" Version="8.15.0" />// Services/JwtTokenService.cs
public class JwtTokenService
{
    private readonly SecuritySettings _securitySettings;
    private readonly ILogger<JwtTokenService> _logger;
    private readonly SymmetricSecurityKey _signingKey;

    public JwtTokenService(
        IOptions<SecuritySettings> securitySettings,
        ILogger<JwtTokenService> logger)
    {
        _securitySettings = securitySettings.Value;
        _logger = logger;
        
        // Initialize signing key from configuration
        var keyBytes = Encoding.UTF8.GetBytes(_securitySettings.Jwt.SecretKey);
        _signingKey = new SymmetricSecurityKey(keyBytes);
    }

    public string GenerateSessionToken(UserSession session)
    {
        var claims = new[]
        {
            new Claim("SessionId", session.SessionId),
            new Claim("UserId", session.UserId.ToString()),
            new Claim("Username", session.Username),
            new Claim("EntityId", session.EntityId)
        };

        var credentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256);
        
        var token = new JwtSecurityToken(
            issuer: _securitySettings.Jwt.Issuer,
            audience: _securitySettings.Jwt.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(_securitySettings.Jwt.ExpirationHours),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public (bool isValid, string? sessionId) ValidateTokenAndGetSessionId(string token)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = _signingKey,
                ValidateIssuer = true,
                ValidIssuer = _securitySettings.Jwt.Issuer,
                ValidateAudience = true,
                ValidAudience = _securitySettings.Jwt.Audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };

            var principal = tokenHandler.ValidateToken(token, validationParameters, out _);
            var sessionId = principal.FindFirst("SessionId")?.Value;

            return (true, sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "JWT token validation failed");
            return (false, null);
        }
    }

    public string GenerateTempOtpToken(string username)
    {
        var claims = new[] { new Claim("Username", username) };
        var credentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256);
        
        var token = new JwtSecurityToken(
            issuer: _securitySettings.Jwt.Issuer,
            audience: _securitySettings.Jwt.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}// appsettings.json
{
  "Security": {
    "Jwt": {
      "SecretKey": "LOADED_FROM_KEY_VAULT",
      "Issuer": "PetelApp",
      "Audience": "PetelAppUsers",
      "ExpirationHours": 8
    }
  }
}// Configuration/SecuritySettings.cs
public class SecuritySettings
{
    public JwtSettings Jwt { get; set; } = new();
    
    public class JwtSettings
    {
        public string SecretKey { get; set; } = string.Empty;
        public string Issuer { get; set; } = "PetelApp";
        public string Audience { get; set; } = "PetelAppUsers";
        public int ExpirationHours { get; set; } = 8;
    }
}// Register JWT service
builder.Services.Configure<SecuritySettings>(
    builder.Configuration.GetSection("Security"));

builder.Services.AddSingleton<JwtTokenService>();

// Initialize JWT service in UserSessionService
var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var jwtService = scope.ServiceProvider.GetRequiredService<JwtTokenService>();
    var sessionService = scope.ServiceProvider.GetRequiredService<UserSessionService>();
    sessionService.SetJwtTokenService(jwtService);
}// Session/UserSessionService.cs
public class UserSessionService
{
    private JwtTokenService? _jwtTokenService;

    public void SetJwtTokenService(JwtTokenService jwtTokenService)
    {
        _jwtTokenService = jwtTokenService;
    }

    public string CreateSessionWithFullData(User user, List<Role> userRoles, int entityId)
    {
        var session = new UserSession
        {
            SessionId = Guid.NewGuid().ToString(),
            UserId = user.Id,
            Username = user.Username,
            EntityId = entityId.ToString(),
            Roles = userRoles,
            LoginTime = DateTime.UtcNow
        };

        _sessions.TryAdd(session.SessionId, session);
        
        // Return JWT token instead of GUID
        return _jwtTokenService?.GenerateSessionToken(session) ?? session.SessionId;
    }

    public UserSession? GetUserSession(string token)
    {
        // Try JWT validation first
        if (_jwtTokenService != null)
        {
            var (isValid, sessionId) = _jwtTokenService.ValidateTokenAndGetSessionId(token);
            if (isValid && sessionId != null && _sessions.TryGetValue(sessionId, out var session))
            {
                return session;
            }
        }
        
        // Fallback to GUID lookup for backward compatibility
        if (_sessions.TryGetValue(token, out var directSession))
        {
            return directSession;
        }

        return null;
    }
}

## Password Expiration & Change Flow

### Overview

When a user's password has expired (or an admin has forced a reset), the login API returns `RequiresPasswordChange: true` with a `TempToken`. The Blazor login page catches this and presents a change-password modal **before** completing login. No redirect or separate page is involved.

### Login Response Fields

```csharp
// LoginResponseDto.cs
public bool RequiresPasswordChange { get; set; }
public string? PasswordExpirationMessage { get; set; }  // Hebrew reason shown in modal
public string? TempToken { get; set; }                  // Short-lived JWT with userId claim
```

### HandleLogin Flow (Login.razor)

```
login response
 â”œâ”€ RequiresPasswordChange â†’ show change-password modal, store TempToken
 â”œâ”€ RequiresOtp            â†’ show email OTP modal (masked email shown, resend button)
 â””â”€ Success                â†’ navigate to /maindashboard
```

**CRITICAL**: `RequiresPasswordChange` is checked **before** OTP so an expired-password user is never accidentally sent to the OTP flow.

### Password Policy â€” Single Regex Attribute

Policy is stored as a **single regex string** in `system_attributes`:

| name | value_type | default value |
|---|---|---|
| `Security_PasswordPolicy` | `string` | `^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{6,20}$` |

**Note**: The `value` column must be `varchar(200)` to hold the regex. The migration SQL widens it:
```sql
ALTER TABLE petel_schema.system_attributes
    ALTER COLUMN value TYPE varchar(200);
```

To change policy without restarting the service:
```sql
UPDATE petel_schema.system_attributes
SET value = '<new-regex>'
WHERE name = 'Security_PasswordPolicy';
```
Then call `POST /api/systemattributes/reload`.

### Password Policy Endpoint (Backend owns interpretation)

```
GET /api/auth/password-policy   (public, no auth required)
```

Returns the regex translated into Hebrew requirement strings:

```json
{
  "requirements": [
    "×‘×™×Ÿ 6 ×œ-20 ×ª×•×•×™×",
    "×œ×¤×—×•×ª ××•×ª ×§×˜× ×” ××—×ª (a-z)",
    "×œ×¤×—×•×ª ××•×ª ×’×“×•×œ×” ××—×ª (A-Z)",
    "×œ×¤×—×•×ª ×¡×¤×¨×” ××—×ª (0-9)",
    "×œ×¤×—×•×ª ×ª×• ×ž×™×•×—×“ ××—×“ (@$!%*?&)"
  ]
}
```

**CRITICAL**: The regex is **never evaluated or interpreted on the frontend**. The Blazor login page calls this endpoint once on load and displays the returned strings as hints. All regex matching happens in `AuthController`.

### AuthController Pattern

```csharp
// GET /api/auth/password-policy
[HttpGet("password-policy")]
public IActionResult GetPasswordPolicy()
{
    const string defaultPolicy = @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{6,20}$";
    var policyAttr = _attributeCache.GetAttributeByName("Security_PasswordPolicy");
    var policyRegex = !string.IsNullOrWhiteSpace(policyAttr?.Value) ? policyAttr.Value : defaultPolicy;
    return Ok(new { requirements = GetPasswordRequirements(policyRegex) });
}

// POST /api/auth/change-expired-password
// Requires: { TempToken, OldPassword, NewPassword }
// TempToken decoded to get userId claim (no active session needed)
// Validates: not empty â†’ regex match â†’ new â‰  old â†’ BCrypt update
// Returns: { success: true/false, message }   (no token â€” user logs in again)
```

`GetPasswordRequirements(string pattern)` is a **private static** helper on `AuthController`. It parses common lookahead patterns from the regex and returns Hebrew strings. It is called in both endpoints above â€” nowhere else.

### Login.razor Pattern

```csharp
// State
private List<string> _passwordRequirements = new();   // loaded from API
private bool _requiresPasswordChange = false;
private string _passwordExpirationMessage = "";
private string? _tempToken;
private string _newPassword = "";
private string _confirmNewPassword = "";
private string _passwordChangeErrorMessage = "";

// On page init (alongside entities, version, env indicator)
await LoadPasswordPolicy();   // calls GET /api/auth/password-policy

// HandleLogin
if (response.RequiresPasswordChange)
{
    _requiresPasswordChange = true;
    _tempToken = response.TempToken;
    _passwordExpirationMessage = response.PasswordExpirationMessage ?? "× ×“×¨×©×ª ×”×—×œ×¤×ª ×¡×™×¡×ž×”";
    return;
}
```

### Change-Password Modal

- Yellow warning banner showing `_passwordExpirationMessage` (the Hebrew reason from the backend)
- Password requirements hint rendered from `_passwordRequirements` without any local interpretation
- New password + confirm password fields with show/hide toggles
- **Local validation only**: empty check and confirm-match check
- **All regex validation is done by the API** â€” the error `message` from the `400 BadRequest` body is displayed directly in the modal (`white-space: pre-line` for multi-line display)
- On success: modal closes, password field cleared, login form shows "×”×¡×™×¡×ž×” ×©×•× ×ª×” ×‘×”×¦×œ×—×”. ×× × ×”×ª×—×‘×¨ ×¢× ×”×¡×™×¡×ž×” ×”×—×“×©×”" â€” user must log in again with the new password

### ChangeExpiredPasswordDto (API)

```csharp
public class ChangeExpiredPasswordDto
{
    public string TempToken { get; set; }    // JWT signed by JwtTokenService, contains userId claim
    public string OldPassword { get; set; }  // Verified via BCrypt before accepting new password
    public string NewPassword { get; set; }  // Validated against Security_PasswordPolicy regex
}
```

### Anti-Patterns to Avoid

```csharp
// âŒ WRONG - Evaluating regex on frontend
if (!Regex.IsMatch(_newPassword, _passwordPolicyRegex)) { ... }  // NO! Backend only.

// âŒ WRONG - Interpreting regex on frontend
private static List<string> GetPasswordRequirements(string pattern) { ... }  // NO! Backend only.

// âŒ WRONG - Hardcoded password minimum length
if (request.NewPassword.Length < 6) { ... }  // NO! Read from Security_PasswordPolicy attribute.

// âŒ WRONG - Multiple separate boolean attributes
Security_PasswordMinLength    // NO! Use single regex attribute
Security_PasswordRequireDigit // NO!
Security_PasswordRequireUppercase // NO!

// âœ… CORRECT - Single regex attribute, interpreted server-side
var policyAttr = _attributeCache.GetAttributeByName("Security_PasswordPolicy");
if (!Regex.IsMatch(request.NewPassword, policyRegex))
{
    var message = "×”×¡×™×¡×ž×” ××™× ×” ×¢×•×ž×“×ª ×‘×“×¨×™×©×•×ª ×”×ž×“×™× ×™×•×ª: " +
                  string.Join(", ", GetPasswordRequirements(policyRegex));
    return BadRequest(new { success = false, message });
}
```

### SQL Migration

See `SQL/add-password-policy-attributes.sql`. Run on all environments once. Uses `ON CONFLICT (name) DO NOTHING` so it is safe to re-run.

## Email OTP (Two-Factor Authentication)

### Overview

Two-factor authentication uses a **server-sent 6-digit code delivered via Gmail SMTP**. There is no authenticator app, no QR code, and no per-user secret. The code is generated server-side, BCrypt-hashed, stored temporarily on the user record, and discarded after use or expiry.

### Architecture

```
Login (username + password) â”€â–º AuthService.LoginAsync()
                                  â””â”€ GetOtpEnabled() == true?
                                       â”œâ”€ NO  â†’ return { Success=true, Token }  (OTP skipped)
                                       â””â”€ YES â†’ generate code â†’ BCrypt hash â†’ store on user
                                                 â†’ SendOtpAsync() via Gmail SMTP
                                                 â†’ return { RequiresOtp=true, TempToken, MaskedEmail }

Browser shows OTP modal â†’ user enters 6 digits â†’ POST /api/otp/validate
  â””â”€ BCrypt.Verify(code, user.EmailOtpCode) && not expired && attempts < max
       â”œâ”€ FAIL â†’ increment EmailOtpAttempts, lock if threshold reached
       â””â”€ PASS â†’ clear OTP fields â†’ CompleteLoginAsync() â†’ return { Success=true, Token }
```

### Feature Flag â€” `Security.OtpEnabled`

OTP is **enabled per environment** via `appsettings.json`:

| Environment | `Security.OtpEnabled` |
|---|---|
| `appsettings.Development.json` | `false` â€” OTP skipped in local dev |
| `appsettings.test.json` | `true` |
| `appsettings.Production.json` | `true` |

The flag can also be overridden at runtime via the `Security_OtpEnabled` system attribute in the database (checked first by `AuthService.GetOtpEnabled()`). The `appsettings` value is the fallback.

```sql
-- Disable OTP at runtime without restarting (only if Security_OtpEnabled attribute exists)
UPDATE petel_schema.system_attributes SET value = 'false' WHERE name = 'Security_OtpEnabled';
POST /api/systemattributes/reload
```

### Email Configuration

```json
// appsettings.json (base â€” placeholder values)
{
  "Email": {
    "SmtpHost": "smtp.gmail.com",
    "SmtpPort": 587,
    "FromAddress": "your@gmail.com",
    "Username": "your@gmail.com",
    "Password": "YOUR_GMAIL_APP_PASSWORD"
  }
}

// appsettings.test.json / appsettings.Production.json
{
  "Email": {
    "SmtpHost": "smtp.gmail.com",
    "SmtpPort": 587,
    "FromAddress": "LOADED_FROM_KEY_VAULT",
    "Username": "LOADED_FROM_KEY_VAULT",
    "Password": "LOADED_FROM_KEY_VAULT"
  }
}
```

`Password` must be a **Gmail App Password** (16 chars), not the Google account password. Generate at: Google Account â†’ Security â†’ 2-Step Verification â†’ App passwords.

### Database Columns (users table)

Three columns were added to `petel_schema.users`:

| Column | Type | Purpose |
|---|---|---|
| `email_otp_code` | `VARCHAR(100) NULL` | BCrypt hash of the pending code |
| `email_otp_expiry` | `TIMESTAMPTZ NULL` | Expiry time (10 min after issue) |
| `email_otp_attempts` | `INTEGER NOT NULL DEFAULT 0` | Failed-attempt counter |

**SQL migration**: `SQL/add-email-otp-columns.sql` â€” idempotent, safe to re-run.

Old TOTP columns (`otp_secret`, `otp_enabled`, `otp_verified`) remain in the table for rollback safety but are no longer used by the application.

### API Endpoints

```
POST /api/otp/send       { TempToken }                    â†’ { Success, MaskedEmail }
POST /api/otp/validate   { TempToken, Code }              â†’ LoginResponse (same as /auth/login success)
POST /api/otp/disable    { TempToken, Password }          â†’ { Success }
GET  /api/otp/status     Authorization: Bearer <token>    â†’ { OtpEnabled }
```

`/otp/send` can be called again to resend a new code (the old hash is overwritten). The Login.razor "×©×œ×— ×©×•×‘" button calls this endpoint.

### DI Registration (Program.cs)

```csharp
// Configuration
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("Email"));

// Email service (singleton â€” stateless SMTP client)
builder.Services.AddSingleton<IEmailService, SmtpEmailService>();
```

### Login.razor State

```csharp
private bool   _requiresOtp   = false;
private string _maskedEmail   = "";      // shown in OTP modal heading
private string? _tempToken    = null;
private string _otpCode       = "";
private string _otpErrorMessage = "";

// HandleLogin â€” after successful password check:
if (response.RequiresOtp)
{
    _requiresOtp  = true;
    _tempToken    = response.TempToken;
    _maskedEmail  = response.MaskedEmail ?? "";
    return;
}

// ResendOtp â€” "×©×œ×— ×©×•×‘" button:
var r = await ApiService.PostAsync<object, SendOtpResponse>("otp/send", new { TempToken = _tempToken });
if (r?.Success == true) _maskedEmail = r.MaskedEmail;
```

### Anti-Patterns to Avoid

```csharp
// âŒ WRONG - OTP in local dev environment
"Security": { "OtpEnabled": true }  // in appsettings.Development.json â€” slows down dev

// âŒ WRONG - Storing plaintext OTP code
user.EmailOtpCode = code;  // NO! Always BCrypt.HashPassword(code, 11)

// âŒ WRONG - Accepting expired codes
// Always check: user.EmailOtpExpiry > DateTime.UtcNow

// âŒ WRONG - Not clearing OTP fields after successful validation
// Always: user.EmailOtpCode = null; user.EmailOtpExpiry = null; user.EmailOtpAttempts = 0;

// âœ… CORRECT - Full OTP validation pattern
if (user.EmailOtpCode == null || user.EmailOtpExpiry == null)
    return fail("×§×•×“ ×œ× × ×ž×¦×");
if (DateTime.UtcNow > user.EmailOtpExpiry)
    return fail("×”×§×•×“ ×¤×’ ×ª×•×§×£");
if (!BCrypt.Net.BCrypt.Verify(request.Code, user.EmailOtpCode))
{
    user.EmailOtpAttempts++;
    if (user.EmailOtpAttempts >= maxAttempts) LockUser(user);
    return fail("×§×•×“ ×©×’×•×™");
}
user.EmailOtpCode = null;
user.EmailOtpExpiry = null;
user.EmailOtpAttempts = 0;
```

