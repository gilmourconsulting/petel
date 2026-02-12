# Database-Driven Configuration System - Deployment Guide

## Overview
This guide covers the deployment of the new database-driven configuration system that allows runtime management of system settings without requiring application restarts or deployments.

## Features Implemented
✅ **Rate Limiting Configuration** - Dynamic rate limits loaded from database  
✅ **Security Settings** - OTP, session timeouts, password attempt limits  
✅ **Maintenance Mode** - System-wide maintenance mode with custom messages  
✅ **Configuration UI** - Blazor admin interface for configuration management  
✅ **Caching System** - 5-minute cache with manual refresh capabilities  
✅ **REST API** - Full CRUD operations for configuration management  

## Deployment Steps

### 1. Database Schema Updates

**Run the configuration migration:**
```sql
-- Execute: database-driven-config-migration.sql
-- This adds configuration attributes to system_attributes table
-- Location: c:\dev\PetelFullApp\database-driven-config-migration.sql
```

**Add the system configuration menu item:**
```sql
-- Execute: add-system-configuration-menu-item.sql 
-- Location: c:\dev\PetelFullApp\SQL\add-system-configuration-menu-item.sql
```

### 2. Backend Services (Already Integrated)

The following services are automatically registered in `Program.cs`:
- **DatabaseConfigurationService** - Core configuration service with caching
- **DatabaseRateLimitConfiguration** - Dynamic rate limiting from database  
- **ConfigurationController** - REST API endpoints for configuration management

### 3. Frontend Components (Ready to Deploy)

**New Blazor page created:**
- **SystemConfiguration.razor** - Admin UI for configuration management
- **ConfigurationDtos.cs** - Type-safe DTOs for configuration data
- **Route:** `/system-configuration`

### 4. Configuration Migration

**Default configuration values will be automatically inserted:**
```
Rate Limiting:
- RateLimit.Enabled = true
- RateLimit.LoginLimit = 10 (per 15 minutes)
- RateLimit.OtpLimit = 5 (per 15 minutes)  
- RateLimit.ApiLimit = 100 (per minute)
- RateLimit.HourlyLimit = 3000 (per hour)

Security:
- Security.OtpEnabled = true
- Security.SessionTimeoutMinutes = 30
- Security.MaxPasswordAttempts = 5
- Security.MaxOtpAttempts = 3
- Security.OtpIssuer = "Petel System"

System:
- System.MaintenanceMode = false
- System.MaintenanceMessage = "המערכת נמצאת במצב תחזוקה..."
```

## Testing Guide

### 1. Verify Database Migration

**Check that configuration attributes were created:**
```sql
SELECT 
    key,
    value,
    description,
    attribute_type
FROM petel_schema.system_attributes 
WHERE key LIKE 'RateLimit.%' 
   OR key LIKE 'Security.%' 
   OR key LIKE 'System.%'
ORDER BY key;
```

Expected output: ~10 configuration rows

### 2. Test Configuration API

**Get all configuration:**
```http
GET /api/configuration
Authorization: Bearer {your-jwt-token}
```

**Get rate limit configuration:**
```http
GET /api/configuration/rate-limit  
Authorization: Bearer {your-jwt-token}
```

**Update rate limit settings:**
```http
PUT /api/configuration/rate-limit
Authorization: Bearer {your-jwt-token}
Content-Type: application/json

{
  "enabled": true,
  "loginLimit": 15,
  "otpLimit": 8,
  "apiLimit": 150,
  "hourlyLimit": 4000
}
```

**Toggle maintenance mode:**
```http
POST /api/configuration/maintenance
Authorization: Bearer {your-jwt-token}
Content-Type: application/json

{
  "enabled": true,
  "message": "המערכת נמצאת בתחזוקה מתוכננת"
}
```

### 3. Test Configuration UI

**Access the admin interface:**
1. Navigate to `/system-configuration`
2. Verify all three configuration cards load properly:
   - Rate Limiting Configuration
   - Security Configuration  
   - System Configuration
3. Test updating rate limits and verify database persistence
4. Test maintenance mode toggle
5. Verify cache refresh functionality

### 4. Test Rate Limiting

**Test login rate limiting:**
1. Set login limit to 3 attempts per 15 minutes via UI
2. Attempt 4 failed logins rapidly
3. Verify 4th attempt returns 429 Too Many Requests
4. Wait or reset rate limit via admin UI

**Test API rate limiting:**
1. Set API limit to 5 requests per minute via UI
2. Make 6+ API calls rapidly
3. Verify 6th request returns 429 Too Many Requests

### 5. Test Maintenance Mode

**Enable maintenance mode:**
1. Toggle maintenance mode via `/system-configuration`
2. Set custom message
3. Verify all API endpoints return 503 Service Unavailable
4. Verify maintenance message is displayed
5. Admin endpoints should still work

## Environment-Specific Configuration

### Development Environment
```
Rate Limiting: DISABLED (loads from database but ignores limits)
OTP: OPTIONAL 
Maintenance: NEVER enabled automatically
Cache: 1-minute expiry for faster testing
```

### Test Environment  
```
Rate Limiting: GENEROUS limits for testing
LoginLimit: 20 per 15 minutes
ApiLimit: 200 per minute
OTP: ENABLED
Cache: 5-minute expiry
```

### Production Environment
```
Rate Limiting: STRICT security-focused limits
LoginLimit: 5 per 15 minutes
ApiLimit: 50 per minute
HourlyLimit: 2000 per hour
OTP: MANDATORY
Cache: 5-minute expiry
```

## Architecture Benefits

### Runtime Configuration Changes
✅ **No Deployments** - Change rate limits without restarting application  
✅ **Environment Consistency** - Same configuration system across dev/test/prod  
✅ **Audit Trail** - All configuration changes tracked with user/timestamp  
✅ **Rollback Capability** - Easy to revert configuration changes  

### Performance & Caching
✅ **Memory Caching** - 5-minute cache prevents database hits on every request  
✅ **Cache Invalidation** - Manual cache refresh via admin UI  
✅ **Type Safety** - Strongly-typed configuration with validation  
✅ **Batch Loading** - Efficient bulk configuration loading  

### Security & Access Control
✅ **JWT Authentication** - All configuration endpoints require valid JWT  
✅ **Session Validation** - User session validation for all config changes  
✅ **Configuration Encryption** - Sensitive values can be encrypted in database  
✅ **Admin-Only Access** - Configuration UI protected by action security system  

## Monitoring & Troubleshooting

### Configuration Loading Issues
```
Check logs for: "Error loading configuration from database"
Verify: Database connectivity and system_attributes table exists
Solution: Run database migration script
```

### Cache Issues
```
Symptom: Configuration changes not taking effect immediately
Check: Cache expiry time in DatabaseConfigurationService
Solution: Use "Clear Cache" button in admin UI or restart application
```

### Rate Limiting Not Working
```
Check logs for: "Rate limit configuration loading failed"
Verify: RateLimit.Enabled = 'true' in system_attributes table
Solution: Verify database configuration values and restart application
```

### Maintenance Mode Stuck
```
Symptom: All API calls return 503 even after disabling maintenance
Check: System.MaintenanceMode value in database
Solution: Manually set to 'false' in database or use admin UI
```

## Next Phase Enhancements

### Advanced Features (Future)
- **Configuration Templates** - Predefined configuration sets for different scenarios
- **Configuration History** - Track all configuration changes with rollback capability  
- **Environment Sync** - Sync configuration between environments
- **Configuration Validation** - Advanced validation rules for configuration values
- **Real-time Notifications** - WebSocket notifications when configuration changes
- **Configuration Import/Export** - Backup and restore configuration sets

### Security Enhancements
- **Configuration Encryption** - Encrypt sensitive configuration values at rest
- **Role-Based Access** - Different configuration sections for different admin roles
- **Configuration Approval** - Two-person approval for critical configuration changes
- **API Rate Limiting** - Separate rate limits for configuration API endpoints

## Commands Summary

**Deploy database changes:**
```bash
# Run from SQL Management Studio or psql
\i database-driven-config-migration.sql
\i SQL/add-system-configuration-menu-item.sql
```

**Restart application services:**
```bash
# API (will auto-load database configuration)
cd PetelApp.Api && dotnet run

# Blazor Server (configuration UI will be available)
cd PetelApp.BlazorServer && dotnet run
```

**Verify deployment:**
- Check `/system-configuration` page loads
- Verify rate limiting respects database values
- Test maintenance mode toggle
- Check configuration API endpoints

The database-driven configuration system is now ready for production deployment and provides a flexible, secure foundation for runtime system management.