# Production-Ready Rate Limiting Configuration

## Overview

The system now implements a comprehensive, production-ready rate limiting solution with environment-specific configurations and advanced features.

## Environment Configuration

### Development
- **Rate Limiting**: ❌ Disabled
- **Purpose**: Allow unlimited requests during development and testing

### Test Environment 
- **Rate Limiting**: ✅ Enabled (High Limits)
- **Login Attempts**: 50 per 15 minutes
- **OTP Validation**: 30 per 15 minutes  
- **General API**: 300 per minute, 5000 per hour
- **IP Whitelist**: localhost (127.0.0.1, ::1)

### Production Environment
- **Rate Limiting**: ✅ Enabled (Strict Limits)
- **Login Attempts**: 10 per 15 minutes
- **OTP Validation**: 5 per 15 minutes
- **API Operations**:
  - POST: 60 per minute
  - PUT: 40 per minute  
  - DELETE: 20 per minute
  - GET: 120 per minute
- **Total**: 2000 requests per hour

## Advanced Features

### 1. **Client-Based Rate Limiting**
Different limits based on user roles:

```json
{
  "ClientId": "*_role_admin",
  "Rules": [
    {
      "Endpoint": "*",
      "Period": "1m", 
      "Limit": 500
    }
  ]
}
```

### 2. **Role-Based Limits**
- **Admin Users**: 500 requests/minute
- **Manager Users**: 300 requests/minute  
- **Regular Users**: Standard limits

### 3. **Enhanced Error Handling**
- Clear Hebrew error messages
- Retry-After headers
- Exponential backoff in client
- Rate limit exceeded logging

### 4. **Security Headers**
- X-RateLimit-Exceeded header
- Retry-After timing information
- Client IP and endpoint logging

## Implementation Architecture

### Backend Components

1. **AdvancedRateLimitConfiguration.cs**
   - Configures IP and client-based rate limiting
   - Custom client ID resolution from JWT tokens
   - Redis support for distributed scenarios

2. **CustomClientResolveContributor**
   - Extracts user/role from JWT tokens
   - Creates client IDs like `user_123_role_admin`
   - Falls back to IP-based limiting for anonymous users

3. **RateLimitHeadersMiddleware**
   - Adds debugging headers to responses
   - Logs rate limit exceeded events
   - Sets Retry-After header for 429 responses

### Frontend Components

1. **ApiService Enhancements**
   - Automatic retry with exponential backoff
   - Rate limiting specific error handling
   - Support for temporary token requests (OTP flow)

2. **Method Overloads**
   ```csharp
   PostAsync<T, R>(endpoint, data)                    // Standard
   PostAsync<T, R>(endpoint, data, maxRetries)        // With retries  
   PostAsync<T, R>(endpoint, data, customToken)       // With temp token
   ```

## Benefits

### ✅ **Security**
- Prevents brute force attacks
- Limits authentication attempts
- Protects against DoS attacks

### ✅ **Scalability** 
- Redis support for distributed deployments
- Per-user/role rate limiting
- Configurable limits per environment

### ✅ **User Experience**
- Higher limits for privileged users
- Automatic retry logic in client
- Clear error messages

### ✅ **Monitoring**
- Detailed rate limit logging
- Performance metrics
- Block attempt tracking

## Configuration Examples

### Adding New Rate Limits

```json
{
  "Endpoint": "POST:/api/documents/upload",
  "Period": "1h",
  "Limit": 10
}
```

### Role-Based Overrides

```json
{
  "ClientId": "*_role_superuser",
  "Rules": [
    {
      "Endpoint": "DELETE:/api/*",
      "Period": "1m",
      "Limit": 100
    }
  ]
}
```

### IP Whitelisting

```json
{
  "IpWhitelist": [
    "192.168.1.100",
    "10.0.0.0/8"
  ]
}
```

## Client Usage

### Retry-Enabled Calls
```csharp
// Automatically retry rate-limited requests
var response = await ApiService.PostAsync<Request, Response>(
    "critical/endpoint", 
    request, 
    maxRetries: 2
);
```

### Error Handling
```csharp
try 
{
    await ApiService.PostAsync(endpoint, data);
}
catch (HttpRequestException ex) when (ex.Message.Contains("Rate limit"))
{
    // Handle rate limiting gracefully
    ShowUserFriendlyMessage("מערכת עמוסה, נא נסה שוב בעוד מספר שניות");
}
```

## Future Enhancements

### 🔄 **Redis Support**
- Distributed rate limiting across multiple instances
- Persistent rate limit counters
- Better performance for high-traffic scenarios

### 📊 **Analytics Integration**  
- Rate limit metrics dashboard
- Usage pattern analysis
- Capacity planning data

### 🎯 **Dynamic Limits**
- Time-based limit adjustments
- Load-based scaling
- User behavior analysis

### 🛡️ **Advanced Security**
- GeoIP-based limiting
- Suspicious pattern detection
- Automatic IP blocking

This implementation provides enterprise-grade rate limiting suitable for production environments while maintaining development flexibility and user experience.