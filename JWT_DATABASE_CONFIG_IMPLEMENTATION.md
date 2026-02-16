# JWT Database-Driven Configuration Implementation

## Overview
JWT configuration (Issuer, Audience, Expiration) is now loaded from the database `system_attributes` table with config file fallback. This allows dynamic JWT settings without redeployment.

## Date Implemented
February 15, 2026

## Changes Made

### 1. Modified JwtTokenService ✅
**File**: `PetelApp.Api/Services/JwtTokenService.cs`

**Changes**:
- Added `SystemAttributeCache` dependency injection
- Added private fields: `_issuer`, `_audience`, `_expirationHours`
- Added three new private methods:
  - `LoadJwtIssuer()` - Loads from database, falls back to config
  - `LoadJwtAudience()` - Loads from database, falls back to config
  - `LoadJwtExpirationHours()` - Loads from database, falls back to config
- Updated token generation to use database values

**Loading Priority**:
1. **Database** (`system_attributes` table) - Primary source
2. **Config file** (`appsettings.json`) - Fallback default
3. **Code defaults** (hard-coded in `SecuritySettings.cs`)

### 2. Created Migration Script ✅
**File**: `SQL/add-jwt-system-attributes.sql`

**Creates/Updates**:
```sql
JWT_Issuer           = 'Petel ATH'
JWT_Audience         = 'PetelAppUsers'
JWT_ExpirationHours  = '8'
JWT_SecretKey        = 'LOADED_FROM_KEY_VAULT' (informational only)
```

**Applied To**:
- ✅ Local Development (localhost)
- ⏳ Test Environment (next deployment)
- ⏳ Production (next deployment)

### 3. Updated Config Files ✅
**File**: `api-app-settings.json`

**Changes**:
- Updated `Security__Jwt__Issuer` to "Petel ATH"
- Updated Azure App Service production settings
- Restarted production API

## Database Schema

### system_attributes Table Structure
```sql
id          INTEGER PRIMARY KEY
name        VARCHAR(50) UNIQUE NOT NULL
description VARCHAR(50)
value       VARCHAR(100) NOT NULL
value_type  VARCHAR(100)
created_at  TIMESTAMP DEFAULT NOW()
updated_at  TIMESTAMP DEFAULT NOW()
```

### JWT Attributes
| ID | Name | Description | Value | Type |
|----|------|-------------|-------|------|
| 207 | JWT_Issuer | JWT Token Issuer | Petel ATH | string |
| 208 | JWT_Audience | JWT Token Audience | PetelAppUsers | string |
| 209 | JWT_ExpirationHours | JWT Expiration (Hours) | 8 | integer |
| 206 | JWT_SecretKey | JWT Secret Key | LOADED_FROM_KEY_VAULT | string |

**Note**: `JWT_SecretKey` is always loaded from Azure Key Vault, never from database.

## Benefits

### ✅ Dynamic Configuration
- Change JWT settings without code changes
- Update via database management UI or SQL
- No redeployment required (just API restart)

### ✅ Multi-Tenant Ready
- Different issuer/audience per environment
- Easy tenant-specific JWT configuration
- Supports white-labeling scenarios

### ✅ Security
- JWT SecretKey still from Key Vault (never in database)
- Audit trail via `updated_at` timestamps
- Centralized configuration management

### ✅ Fallback Safety
- Config file values serve as fallback
- Graceful degradation if database unavailable
- Logged warnings for troubleshooting

## Configuration Flow

```
┌─────────────────────────────────────────────┐
│  JwtTokenService Constructor                │
├─────────────────────────────────────────────┤
│                                             │
│  1. Check SystemAttributeCache              │
│     ├─ Found: Use database value            │
│     └─ Not found: Log warning               │
│                                             │
│  2. Fallback to config file                 │
│     ├─ appsettings.{Environment}.json       │
│     └─ Azure App Service settings           │
│                                             │
│  3. Use code default (SecuritySettings.cs)  │
│                                             │
└─────────────────────────────────────────────┘
```

## Logging

### Startup Logs
```
[Information] Loaded JWT Issuer from database: Petel ATH
[Information] Loaded JWT Audience from database: PetelAppUsers
[Information] Loaded JWT Expiration from database: 8 hours
[Information] JWT Service initialized - Issuer: Petel ATH, Audience: PetelAppUsers, Expiration: 8h
```

### Fallback Logs
```
[Warning] Failed to load JWT Issuer from database, using config fallback
[Information] Using JWT Issuer from config: PetelApp
```

## Deployment Checklist

### Development Environment ✅
- [x] Updated `JwtTokenService.cs`
- [x] Created migration script
- [x] Applied migration to local database
- [x] Verified build succeeds
- [x] JWT values loaded from database

### Test Environment ⏳
- [ ] Deploy API code changes
- [ ] Run migration script: `SQL/add-jwt-system-attributes.sql`
- [ ] Update test database JWT values if needed
- [ ] Restart API
- [ ] Verify JWT tokens have correct issuer

### Production Environment ⏳
- [ ] Deploy API code changes
- [ ] Run migration script: `SQL/add-jwt-system-attributes.sql`
- [ ] Verify production JWT values are correct
- [ ] Restart API
- [ ] Monitor logs for JWT initialization
- [ ] Test login and token generation

## Testing

### Verify Database Loading
```powershell
# Check system_attributes
psql -c "SELECT name, value FROM petel_schema.system_attributes WHERE name LIKE 'JWT_%';"

# Expected output:
# JWT_Issuer          | Petel ATH
# JWT_Audience        | PetelAppUsers
# JWT_ExpirationHours | 8
```

### Verify API Logs
```bash
# Look for JWT initialization log
az webapp log tail --name petel-prod-api --resource-group petel-prod-rg | grep "JWT"

# Expected:
# [Information] Loaded JWT Issuer from database: Petel ATH
# [Information] JWT Service initialized - Issuer: Petel ATH...
```

### Verify Token Claims
```csharp
// Decode JWT token and check issuer/audience
var handler = new JwtSecurityTokenHandler();
var token = handler.ReadJwtToken(tokenString);

Assert.Equal("Petel ATH", token.Issuer);
Assert.Equal("PetelAppUsers", token.Audiences.First());
```

## Updating JWT Configuration

### Via Database (Recommended)
```sql
-- Update JWT Issuer
UPDATE petel_schema.system_attributes
SET value = 'New Issuer Name', updated_at = CURRENT_TIMESTAMP
WHERE name = 'JWT_Issuer';

-- Restart API for changes to take effect
```

### Via Azure App Service (Fallback Only)
```powershell
az webapp config appsettings set `
  --name petel-prod-api `
  --resource-group petel-prod-rg `
  --settings "Security__Jwt__Issuer=Fallback Value"

az webapp restart --name petel-prod-api --resource-group petel-prod-rg
```

## Troubleshooting

### Issue: Old issuer name still appearing in tokens
**Cause**: API not restarted after database update  
**Solution**: Restart API to reload configuration

### Issue: "JWT Issuer from config" log message
**Cause**: Database value not found or empty  
**Solution**: Verify `JWT_Issuer` exists in `system_attributes` table

### Issue: JWT validation failures
**Cause**: Mismatch between issuer in token and validation parameters  
**Solution**: Ensure database and config values are consistent across environments

## Backward Compatibility

### Old GUID Session Tokens ✅
- Still supported via fallback in `ValidateTokenAndGetSessionId()`
- Graceful handling of non-JWT tokens
- No breaking changes for existing sessions

### Config-Only Deployments ✅
- System works if database attributes not present
- Falls back to config file values
- Warnings logged for missing database values

## Security Considerations

### ⚠️ JWT SecretKey
- **NEVER** store in database (security risk)
- Always loaded from Azure Key Vault
- `JWT_SecretKey` attribute is informational only

### ✅ Token Validation
- Issuer and Audience validated on every request
- Signature verification using SecretKey from Key Vault
- Expiration enforced with 5-minute clock skew

### ✅ Audit Trail
- All changes to system_attributes tracked via `updated_at`
- Future: Add `update_user` tracking

## Future Enhancements

1. **Admin UI for JWT Configuration**
   - Add to System Configuration page
   - Real-time validation
   - Restart API from UI

2. **Per-Tenant JWT Settings**
   - Use `foreign_id` to link JWT settings to entities
   - Multi-tenant JWT configuration
   - Tenant-specific issuers

3. **Configuration Versioning**
   - Track changes to JWT settings
   - Rollback capability
   - Configuration history

## References

- **JWT Token Service**: `PetelApp.Api/Services/JwtTokenService.cs`
- **System Attributes Cache**: `PetelApp.Api/Services/SystemAttributeCache.cs`
- **Security Settings**: `PetelApp.Api/Configuration/SecuritySetting.cs`
- **Migration Script**: `SQL/add-jwt-system-attributes.sql`
- **Copilot Instructions**: `.github/copilot-instructions.md` (JWT Token Authentication section)

## Summary

✅ **Development**: Implemented and tested  
⏳ **Test**: Ready for next deployment  
⏳ **Production**: Ready for next deployment  

JWT configuration is now database-driven with robust fallback mechanisms, enabling dynamic configuration without redeployment while maintaining security and backward compatibility.
