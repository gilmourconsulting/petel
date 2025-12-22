# Issues Found and Fixed - Session Manager & Deployment

## Date: December 22, 2025

## Problem Summary
After deployment to Azure, the application was failing with `ERR_BLOCKED_BY_CLIENT` errors because API calls were still trying to access `http://localhost:5082` instead of the deployed API URL.

## Root Causes Identified

### 1. ❌ Incorrect Property Name in session-manager.js
**Location**: Lines 6 in both:
- `petelapp-frontend/public/session-manager.js`
- `PetelApp.Api/session-manager.js`

**Issue**:
```javascript
// ❌ WRONG - Using .baseUrl instead of .apiBaseUrl
this.baseUrl = window.AppConfig?.baseUrl || 'http://localhost:5082';
```

**Why This Failed**:
- `AppConfig` object in `config.js` uses property name `apiBaseUrl`, NOT `baseUrl`
- When deployed, `window.AppConfig?.baseUrl` returned `undefined`
- Fallback to hardcoded `'http://localhost:5082'` was triggered
- All session manager API calls went to localhost instead of deployed API

**Fix Applied**:
```javascript
// ✅ CORRECT - Using .apiBaseUrl property
this.baseUrl = window.AppConfig?.apiBaseUrl || 'http://localhost:5082';
```

### 2. ✅ Verification: AppConfig Structure
From `config.js`:
```javascript
const AppConfig = {
    apiBaseUrl: ENV_CONFIG.API_BASE_URL,  // ✅ Property name is 'apiBaseUrl'
    environment: ENV_CONFIG.ENVIRONMENT,
    
    getApiUrl(endpoint) {
        return `${this.apiBaseUrl}/${endpoint}`;  // ✅ Uses 'apiBaseUrl'
    }
}
```

## Files Modified

### Session Manager Files (Both locations fixed):
1. `c:\dev\PetelFullApp\petelapp-frontend\public\session-manager.js` - Line 6
2. `c:\dev\PetelFullApp\PetelApp.Api\session-manager.js` - Line 6

**Change**: `window.AppConfig?.baseUrl` → `window.AppConfig?.apiBaseUrl`

## Other Hardcoded localhost References Found (No Changes Needed)

### Development/Debug Files (OK to keep localhost):
- `env-config.js` - Development default config (replaced during deployment)
- `config.js` - Fallback default (only used if ENV_CONFIG fails to load)
- `debug.js` - Checks `window.location.hostname === 'localhost'` for debugging
- `bootstrap-config.js` - Legacy file, checks hostname for environment detection

### Files Using AppConfig Correctly:
- ✅ `schooldetails.html` - All API calls use `AppConfig.getApiUrl()`
- ✅ All other HTML pages - Verified using centralized config

## Deployment Process Improvements

### Created Documentation:
1. **`DEPLOYMENT_GUIDE.md`** - Comprehensive deployment documentation
2. **`Deploy-ToAzure.ps1`** - Automated deployment script with validation

### Key Deployment Steps:
1. Clean previous build artifacts
2. Publish .NET backend (Release configuration)
3. Copy frontend files to `wwwroot`
4. **Copy environment-specific config** (e.g., `env-test-config.js` → `env-config.js`)
5. **Verify no localhost in env-config.js**
6. Create ZIP package with tar
7. Deploy to Azure App Service
8. Verify deployment in browser console

## Verification Checklist

### Pre-Deployment:
- [x] `session-manager.js` uses `AppConfig.apiBaseUrl`
- [x] Environment-specific config files exist
- [x] No hardcoded API URLs in application code
- [x] Backend builds successfully

### Post-Deployment:
- [x] Application loads without errors
- [x] Browser console: `window.ENV_CONFIG.API_BASE_URL` shows deployed URL
- [x] No `ERR_BLOCKED_BY_CLIENT` errors
- [x] API calls go to correct backend URL
- [x] Session management working

## Test Results

### Deployment: 2025-12-22 15:46 UTC
- **Status**: ✅ Successful
- **Environment**: Test
- **Resource Group**: petel-test-rg
- **App Service**: petel-test-api
- **Deployment ID**: 934d4a45-e6cc-460c-927f-0573d688bf39

### Verification Commands:
```powershell
# Verify env-config.js in deployment package
Get-Content "c:\dev\PetelFullApp\PetelApp.Api\publish\wwwroot\env-config.js"

# Expected output:
# window.ENV_CONFIG = {
#     API_BASE_URL: 'https://petel-test-api-ahafcqfnh6drcdbd.israelcentral-01.azurewebsites.net/api',
#     ENVIRONMENT: 'test'
# };

# Verify session-manager.js fix
Get-Content "c:\dev\PetelFullApp\PetelApp.Api\publish\wwwroot\session-manager.js" | Select-Object -Skip 5 -First 1

# Expected output:
# this.baseUrl = window.AppConfig?.apiBaseUrl || 'http://localhost:5082';
```

## Impact Analysis

### Before Fix:
- ❌ Session manager API calls: `http://localhost:5082/api/session/...`
- ❌ All session properties requests blocked by browser
- ❌ Pages failed to load user/year/school context
- ❌ Application unusable in deployed environment

### After Fix:
- ✅ Session manager API calls: `https://petel-test-api-ahafcqfnh6drcdbd.israelcentral-01.azurewebsites.net/api/session/...`
- ✅ Session properties loaded successfully
- ✅ Pages load with correct context
- ✅ Application fully functional in deployed environment

## Lessons Learned

1. **Property Name Consistency**: Always verify property names match between configuration object and consumers
2. **Deployment Validation**: Add automated checks for hardcoded URLs before deployment
3. **Testing Requirement**: Test deployment package contents before deploying to production
4. **Console Verification**: Use browser console to verify runtime configuration after deployment

## Future Preventions

### Code Review Checklist:
- [ ] All API calls use `AppConfig.getApiUrl()` or `sessionManager.apiCall()`
- [ ] No direct property access like `AppConfig.baseUrl` (should be `apiBaseUrl`)
- [ ] Environment-specific URLs only in `env-*-config.js` files
- [ ] Deployment script verifies config before creating package

### Automated Tests (To Implement):
```javascript
// Unit test to verify session manager uses correct property
test('SessionManager should use AppConfig.apiBaseUrl', () => {
    window.AppConfig = { apiBaseUrl: 'https://test-api.com' };
    const sm = new SessionManager();
    expect(sm.baseUrl).toBe('https://test-api.com');
});
```

## Related Files

### Documentation:
- `DEPLOYMENT_GUIDE.md` - Complete deployment procedures
- `Deploy-ToAzure.ps1` - Automated deployment script

### Configuration Files:
- `petelapp-frontend/public/env-test-config.js` - Test environment
- `petelapp-frontend/public/env-staging-config.js` - Staging environment
- `petelapp-frontend/public/env-production-config.js` - Production environment
- `petelapp-frontend/public/config.js` - AppConfig object definition

### Fixed Files:
- `petelapp-frontend/public/session-manager.js`
- `PetelApp.Api/session-manager.js`

## Support

For issues with deployment or configuration:
1. Check `DEPLOYMENT_GUIDE.md` troubleshooting section
2. Verify browser console shows correct `window.ENV_CONFIG`
3. Check Azure App Service logs: `az webapp log tail --resource-group petel-test-rg --name petel-test-api`
4. Review this document for common pitfalls
