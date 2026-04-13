# Rate Limiting Implementation - Petel System

## Overview

Rate limiting has been implemented using **AspNetCoreRateLimit** to protect the API from abuse and ensure fair resource allocation. The feature is **environment-based** and can be enabled/disabled via configuration.

## Status

✅ **COMPLETED: 2026-02-02**

## Configuration

### Feature Flag

Rate limiting is controlled by a feature flag in `appsettings.json`:

```json
{
  "Features": {
    "RateLimitingEnabled": true
  }
}
```

### Environment Settings

| Environment | Rate Limiting Status |
|-------------|---------------------|
| Development | ❌ Disabled (testing friendly) |
| Test (Azure) | ✅ Enabled |
| Production (Azure) | ✅ Enabled |

### Rate Limits

Current configuration (Test & Production):

| Endpoint | Limit | Period | Rationale |
|----------|-------|--------|-----------|
| `POST:/api/auth/login` | 5 requests | 15 minutes | Prevent brute force attacks |
| `POST:/api/auth/verify-otp` | 3 requests | 15 minutes | Prevent OTP guessing |
| All endpoints (`*`) | 100 requests | 1 minute | General rate limiting |
| All endpoints (`*`) | 1000 requests | 1 hour | Hourly cap |

### Configuration Files

**Base Configuration** - `appsettings.json`:
```json
{
  "IpRateLimiting": {
    "EnableEndpointRateLimiting": true,
    "StackBlockedRequests": false,
    "RealIpHeader": "X-Forwarded-For",
    "ClientIdHeader": "X-ClientId",
    "HttpStatusCode": 429,
    "QuotaExceededMessage": "Rate limit exceeded. Please try again later.",
    "GeneralRules": [
      {
        "Endpoint": "POST:/api/auth/login",
        "Period": "15m",
        "Limit": 5
      },
      {
        "Endpoint": "POST:/api/auth/verify-otp",
        "Period": "15m",
        "Limit": 3
      },
      {
        "Endpoint": "*",
        "Period": "1m",
        "Limit": 100
      },
      {
        "Endpoint": "*",
        "Period": "1h",
        "Limit": 1000
      }
    ]
  }
}
```

**Test Environment** - `appsettings.test.json`:
```json
{
  "Features": {
    "RateLimitingEnabled": true
  },
  "IpRateLimiting": {
    "QuotaExceededMessage": "معدل الطلبات تجاوز الحد المسموح. يرجى المحاولة لاحقاً"
  }
}
```

**Production Environment** - `appsettings.Production.json`:
```json
{
  "Features": {
    "RateLimitingEnabled": true
  },
  "IpRateLimiting": {
    "QuotaExceededMessage": "מכסת הבקשות הושגה. אנא נסה שוב מאוחר יותר"
  }
}
```

## Implementation Details

### 1. Package Installed

```bash
dotnet add package AspNetCoreRateLimit --version 5.0.0
```

### 2. Configuration Class

Created `PetelApp.Api/Configuration/RateLimitConfiguration.cs`:

```csharp
public static class RateLimitConfiguration
{
    public static IServiceCollection AddRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMemoryCache();
        services.Configure<IpRateLimitOptions>(configuration.GetSection("IpRateLimiting"));
        services.Configure<IpRateLimitPolicies>(configuration.GetSection("IpRateLimitPolicies"));
        services.AddInMemoryRateLimiting();
        services.AddSingleton<IRateLimitConfiguration, AspNetCoreRateLimit.RateLimitConfiguration>();
        return services;
    }
}
```

### 3. Program.cs Registration

```csharp
// Service registration (before builder.Build())
var rateLimitingEnabled = builder.Configuration.GetValue<bool>("Features:RateLimitingEnabled", false);
if (rateLimitingEnabled)
{
    builder.Services.AddRateLimiting(builder.Configuration);
}

// Middleware (after app.UseRouting() and security headers)
if (rateLimitingEnabled)
{
    app.UseIpRateLimiting();
}
```

## Testing Rate Limits

### Local Testing (Development)

Rate limiting is **disabled** in development by default. To test locally:

1. Enable in `appsettings.Development.json`:
```json
{
  "Features": {
    "RateLimitingEnabled": true
  }
}
```

2. Start the API:
```bash
dotnet run --project PetelApp.Api
```

3. Test login rate limit (should block after 5 attempts):
```bash
# Attempt 1-5 (should succeed or fail based on credentials)
curl -X POST http://localhost:5082/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"test","password":"wrong"}'

# Attempt 6 (should return 429 Too Many Requests)
curl -X POST http://localhost:5082/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"test","password":"wrong"}'
```

### Azure Test Environment

Rate limiting is **enabled** by default. Test using actual client applications or API testing tools.

**Expected Response on Rate Limit Exceeded:**
```json
HTTP 429 Too Many Requests

"معدل الطلبات تجاوز الحد المسموح. يرجى المحاولة لاحقاً"
```

## Monitoring

### Application Insights (Future)

When Application Insights is integrated, rate limit events will be logged as custom events:

- Event name: `RateLimitExceeded`
- Properties: `Endpoint`, `ClientId`, `IP Address`

### Log Output

Rate limiting events are logged via Serilog:

```
[Warning] Rate limit exceeded for IP 192.168.1.1 on endpoint POST:/api/auth/login
```

## Customization

### Adding Endpoint-Specific Limits

To add a new rate limit rule, update `appsettings.json`:

```json
{
  "IpRateLimiting": {
    "GeneralRules": [
      {
        "Endpoint": "GET:/api/students/export",
        "Period": "5m",
        "Limit": 2
      }
    ]
  }
}
```

### Whitelisting IPs

To whitelist specific IP addresses (e.g., admin IPs), add to configuration:

```json
{
  "IpRateLimiting": {
    "IpWhitelist": [
      "127.0.0.1",
      "::1",
      "192.168.1.10"
    ]
  }
}
```

### Client-Based Rate Limiting

To rate limit per user instead of per IP, use `ClientRateLimiting` instead:

```csharp
services.Configure<ClientRateLimitOptions>(configuration.GetSection("ClientRateLimiting"));
app.UseClientRateLimiting();
```

## Security Benefits

✅ **Brute Force Protection** - Limits login and OTP attempts  
✅ **DDoS Mitigation** - Prevents API overload  
✅ **Fair Resource Allocation** - Ensures all users get equal access  
✅ **SOC 2 Compliance** - Demonstrates security controls  

## SOC 2 Compliance

This implementation satisfies:

- **CC6.1** - Logical and Physical Access Controls
- **CC7.2** - System Monitoring
- **A1.2** - Availability and Recovery

## Troubleshooting

### Rate Limit Not Working

1. **Check feature flag**:
```bash
# In appsettings.json
"Features": { "RateLimitingEnabled": true }
```

2. **Verify middleware order** (must be after `UseRouting()`):
```csharp
app.UseRouting();
app.UseIpRateLimiting();  // ✅ After routing
```

3. **Check real IP header** (for Azure/load balancers):
```json
"RealIpHeader": "X-Forwarded-For"
```

### 429 Errors in Development

If you're getting rate limited during development:

1. Disable rate limiting:
```json
"Features": { "RateLimitingEnabled": false }
```

2. Or increase limits:
```json
"GeneralRules": [
  { "Endpoint": "*", "Period": "1m", "Limit": 1000 }
]
```

## Future Enhancements

- ⏳ Redis-based distributed rate limiting (for multi-instance deployments)
- ⏳ Dynamic rate limit adjustment based on load
- ⏳ User-based rate limiting (per authentication token)
- ⏳ Rate limit dashboard/monitoring UI

## References

- **Package**: [AspNetCoreRateLimit](https://github.com/stefanprodan/AspNetCoreRateLimit)
- **SOC 2 Roadmap**: [SOC2_COMPLIANCE_ROADMAP.md](../SOC2_COMPLIANCE_ROADMAP.md)
- **Security Headers**: [Program.cs](Program.cs)

---

**Last Updated**: February 2, 2026  
**Status**: ✅ Production Ready  
**Owner**: Development Team
