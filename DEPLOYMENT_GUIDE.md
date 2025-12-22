# Deployment Guide - Petel Application

## Overview
This guide covers deploying the Petel Educational Management System to Azure App Service environments (Test, Staging, Production).

## Critical Deployment Issues & Solutions

### Issue 1: Hardcoded localhost URLs
**Problem**: Frontend files may contain hardcoded `localhost:5082` URLs that work in development but fail in deployed environments with `ERR_BLOCKED_BY_CLIENT` errors.

**Solution**: All API URLs must use the centralized `AppConfig` system:
```javascript
// ❌ WRONG - Hardcoded URL
const response = await fetch('http://localhost:5082/api/session/property/key');

// ❌ WRONG - Incorrect AppConfig property
this.baseUrl = window.AppConfig?.baseUrl || 'http://localhost:5082';

// ✅ CORRECT - Use AppConfig.apiBaseUrl
this.baseUrl = window.AppConfig?.apiBaseUrl || 'http://localhost:5082';

// ✅ CORRECT - Use AppConfig.getApiUrl() helper
const response = await fetch(AppConfig.getApiUrl('session/property/key'));
```

**Files to check before deployment**:
- `session-manager.js` - Must use `AppConfig.apiBaseUrl`
- `config.js` - Must load `ENV_CONFIG` correctly
- Any page-specific JavaScript with API calls
- `index.html` - Must load `env-config.js` BEFORE other scripts

### Issue 2: Environment Configuration Not Copied
**Problem**: Deployment package contains `env-config.js` with development localhost URL instead of environment-specific URL.

**Solution**: Deployment script MUST copy the correct environment config file:

```powershell
# ✅ For TEST environment
Copy-Item -Path "..\petelapp-frontend\public\env-test-config.js" `
    -Destination "$wwwrootPath\env-config.js" -Force

# ✅ For STAGING environment
Copy-Item -Path "..\petelapp-frontend\public\env-staging-config.js" `
    -Destination "$wwwrootPath\env-config.js" -Force

# ✅ For PRODUCTION environment
Copy-Item -Path "..\petelapp-frontend\public\env-production-config.js" `
    -Destination "$wwwrootPath\env-config.js" -Force
```

### Issue 3: Script File Extension Mismatch
**Problem**: `Generate deploy package.cmd` file contains PowerShell code but has `.cmd` extension, causing execution failures.

**Solution**: Use PowerShell scripts with `.ps1` extension or run PowerShell commands directly.

## Deployment Procedure

### Prerequisites
- Azure CLI installed and authenticated: `az login`
- .NET 9 SDK installed
- Access to target Azure App Service
- Correct environment config files in `petelapp-frontend/public/`

### Step 1: Clean Previous Build
```powershell
Set-Location "c:\dev\PetelFullApp\PetelApp.Api"

# Remove old artifacts
Remove-Item -Path ".\publish" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -Path ".\deploy-test.zip" -Force -ErrorAction SilentlyContinue
```

### Step 2: Publish Backend
```powershell
# Build and publish .NET application
dotnet clean
dotnet publish -c Release -o .\publish
```

**Verify**: Check that `.\publish\PetelApp.Api.dll` exists

### Step 3: Copy Frontend Files
```powershell
$wwwrootPath = ".\publish\wwwroot"

# Create wwwroot directory
if (Test-Path $wwwrootPath) {
    Remove-Item -Path $wwwrootPath -Recurse -Force
}
New-Item -Path $wwwrootPath -ItemType Directory -Force | Out-Null

# Copy all frontend files
Copy-Item -Path "..\petelapp-frontend\public\*" `
    -Destination $wwwrootPath -Recurse -Force

# ⚠️ CRITICAL: Copy correct environment config
# For TEST:
Copy-Item -Path "..\petelapp-frontend\public\env-test-config.js" `
    -Destination "$wwwrootPath\env-config.js" -Force

# For STAGING:
# Copy-Item -Path "..\petelapp-frontend\public\env-staging-config.js" `
#     -Destination "$wwwrootPath\env-config.js" -Force

# For PRODUCTION:
# Copy-Item -Path "..\petelapp-frontend\public\env-production-config.js" `
#     -Destination "$wwwrootPath\env-config.js" -Force
```

### Step 4: Verify Environment Configuration
```powershell
# ✅ CRITICAL VERIFICATION - Must show deployed environment URL, NOT localhost
Write-Host "🔍 Verifying env-config.js..." -ForegroundColor Cyan
Get-Content "$wwwrootPath\env-config.js"

# Expected output for TEST:
# window.ENV_CONFIG = {
#     API_BASE_URL: 'https://petel-test-api-ahafcqfnh6drcdbd.israelcentral-01.azurewebsites.net/api',
#     ENVIRONMENT: 'test'
# };
```

**If you see `localhost:5082` here, STOP! The wrong config was copied.**

### Step 5: Create Deployment Package
```powershell
Push-Location ".\publish"

# Create ZIP with tar (preserves Unix paths for Linux App Service)
& tar.exe -a -c -f "..\deploy-test.zip" *

Pop-Location

# Verify ZIP created
if (Test-Path ".\deploy-test.zip") {
    $size = (Get-Item ".\deploy-test.zip").Length / 1MB
    Write-Host "✅ deploy-test.zip created ($([math]::Round($size, 2)) MB)" -ForegroundColor Green
}
```

### Step 6: Deploy to Azure
```powershell
# Deploy to TEST environment
az webapp deploy `
    --resource-group petel-test-rg `
    --name petel-test-api `
    --src-path deploy-test.zip `
    --type zip

# Deploy to STAGING environment
# az webapp deploy `
#     --resource-group petel-staging-rg `
#     --name petel-staging-api `
#     --src-path deploy-staging.zip `
#     --type zip

# Deploy to PRODUCTION environment
# az webapp deploy `
#     --resource-group petel-prod-rg `
#     --name petel-prod-api `
#     --src-path deploy-prod.zip `
#     --type zip
```

### Step 7: Post-Deployment Verification

1. **Check deployment status**:
```powershell
az webapp browse --resource-group petel-test-rg --name petel-test-api
```

2. **Verify environment config in browser**:
   - Open browser developer console (F12)
   - Navigate to application
   - Type: `window.ENV_CONFIG.API_BASE_URL`
   - **Must show deployed URL, NOT localhost!**

3. **Test API connectivity**:
   - Login to application
   - Check browser console for API calls
   - **Should see calls to deployed API URL**
   - **NO `ERR_BLOCKED_BY_CLIENT` errors**

4. **Check backend logs** (if issues occur):
```powershell
az webapp log tail --resource-group petel-test-rg --name petel-test-api
```

## Environment-Specific Configuration Files

### Test Environment
**File**: `petelapp-frontend/public/env-test-config.js`
```javascript
window.ENV_CONFIG = {
    API_BASE_URL: 'https://petel-test-api-ahafcqfnh6drcdbd.israelcentral-01.azurewebsites.net/api',
    ENVIRONMENT: 'test'
};
```

### Staging Environment
**File**: `petelapp-frontend/public/env-staging-config.js`
```javascript
window.ENV_CONFIG = {
    API_BASE_URL: 'https://staging-api.petel-system.co.il/api',
    ENVIRONMENT: 'staging'
};
```

### Production Environment
**File**: `petelapp-frontend/public/env-production-config.js`
```javascript
window.ENV_CONFIG = {
    API_BASE_URL: 'https://api.petel-system.co.il/api',
    ENVIRONMENT: 'production'
};
```

## Complete PowerShell Deployment Script

Save as `Deploy-ToAzure.ps1`:

```powershell
param(
    [Parameter(Mandatory=$true)]
    [ValidateSet('test', 'staging', 'production')]
    [string]$Environment
)

Set-Location "c:\dev\PetelFullApp\PetelApp.Api"

Write-Host "🚀 Deploying to $Environment environment..." -ForegroundColor Cyan

# Step 1: Clean
Write-Host "`n🧹 Cleaning previous build..." -ForegroundColor Yellow
Remove-Item -Path ".\publish" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -Path ".\deploy-$Environment.zip" -Force -ErrorAction SilentlyContinue

# Step 2: Publish backend
Write-Host "`n📦 Publishing backend..." -ForegroundColor Yellow
dotnet clean -v quiet
dotnet publish -c Release -o .\publish -v quiet

if (-not (Test-Path ".\publish\PetelApp.Api.dll")) {
    Write-Host "❌ Backend publish failed!" -ForegroundColor Red
    exit 1
}
Write-Host "✅ Backend published" -ForegroundColor Green

# Step 3: Copy frontend
Write-Host "`n📁 Copying frontend files..." -ForegroundColor Yellow
$wwwrootPath = ".\publish\wwwroot"

if (Test-Path $wwwrootPath) {
    Remove-Item -Path $wwwrootPath -Recurse -Force
}
New-Item -Path $wwwrootPath -ItemType Directory -Force | Out-Null

Copy-Item -Path "..\petelapp-frontend\public\*" -Destination $wwwrootPath -Recurse -Force

# Copy environment-specific config
$envConfigFile = "..\petelapp-frontend\public\env-$Environment-config.js"
if (-not (Test-Path $envConfigFile)) {
    Write-Host "❌ Environment config not found: $envConfigFile" -ForegroundColor Red
    exit 1
}

Copy-Item -Path $envConfigFile -Destination "$wwwrootPath\env-config.js" -Force
Write-Host "✅ Copied env-$Environment-config.js as env-config.js" -ForegroundColor Green

# Step 4: Verify configuration
Write-Host "`n🔍 Verifying environment configuration..." -ForegroundColor Yellow
$configContent = Get-Content "$wwwrootPath\env-config.js" -Raw
if ($configContent -match "localhost") {
    Write-Host "❌ ERROR: env-config.js contains 'localhost'!" -ForegroundColor Red
    Write-Host $configContent
    exit 1
}
Write-Host $configContent -ForegroundColor Cyan

# Step 5: Create ZIP
Write-Host "`n📦 Creating deployment package..." -ForegroundColor Yellow
Push-Location ".\publish"
& tar.exe -a -c -f "..\deploy-$Environment.zip" *
Pop-Location

if (-not (Test-Path ".\deploy-$Environment.zip")) {
    Write-Host "❌ Failed to create ZIP package!" -ForegroundColor Red
    exit 1
}

$size = (Get-Item ".\deploy-$Environment.zip").Length / 1MB
Write-Host "✅ Package created: deploy-$Environment.zip ($([math]::Round($size, 2)) MB)" -ForegroundColor Green

# Step 6: Deploy to Azure
Write-Host "`n🌐 Deploying to Azure..." -ForegroundColor Yellow

$resourceGroups = @{
    'test' = 'petel-test-rg'
    'staging' = 'petel-staging-rg'
    'production' = 'petel-prod-rg'
}

$appNames = @{
    'test' = 'petel-test-api'
    'staging' = 'petel-staging-api'
    'production' = 'petel-prod-api'
}

$rg = $resourceGroups[$Environment]
$appName = $appNames[$Environment]

az webapp deploy `
    --resource-group $rg `
    --name $appName `
    --src-path "deploy-$Environment.zip" `
    --type zip

if ($LASTEXITCODE -eq 0) {
    Write-Host "`n✅ Deployment completed successfully!" -ForegroundColor Green
    Write-Host "`n🔗 Application URL: https://$appName.azurewebsites.net" -ForegroundColor Cyan
    Write-Host "`n⚠️  VERIFY: Open browser console and check window.ENV_CONFIG.API_BASE_URL" -ForegroundColor Yellow
} else {
    Write-Host "`n❌ Deployment failed!" -ForegroundColor Red
    exit 1
}
```

## Usage

```powershell
# Deploy to test
.\Deploy-ToAzure.ps1 -Environment test

# Deploy to staging
.\Deploy-ToAzure.ps1 -Environment staging

# Deploy to production
.\Deploy-ToAzure.ps1 -Environment production
```

## Troubleshooting

### Issue: Still seeing localhost errors after deployment
**Diagnosis**:
1. Open browser console
2. Type `window.ENV_CONFIG`
3. Check `API_BASE_URL` value

**If localhost appears**:
- Deployment package was created with wrong env-config.js
- Redeploy with correct environment parameter

### Issue: Session manager errors
**Check**: `session-manager.js` line 6 must be:
```javascript
this.baseUrl = window.AppConfig?.apiBaseUrl || 'http://localhost:5082';
```
NOT:
```javascript
this.baseUrl = window.AppConfig?.baseUrl || 'http://localhost:5082';
```

### Issue: 401 Unauthorized errors
**Cause**: Backend CORS settings or authentication misconfiguration
**Solution**: Check `appsettings.json` CORS settings match frontend domain

### Issue: Database connection errors
**Check**: Verify Azure PostgreSQL connection string in App Service Configuration:
```powershell
az webapp config connection-string list `
    --resource-group petel-test-rg `
    --name petel-test-api
```

## Rollback Procedure

If deployment fails or causes issues:

```powershell
# View deployment history
az webapp deployment list-publishing-profiles `
    --resource-group petel-test-rg `
    --name petel-test-api

# Restart app service
az webapp restart `
    --resource-group petel-test-rg `
    --name petel-test-api
```

## Pre-Deployment Checklist

- [ ] Backend builds without errors (`dotnet build`)
- [ ] All tests pass (`dotnet test`)
- [ ] Environment-specific config files exist and have correct URLs
- [ ] Database migrations applied to target environment
- [ ] CORS settings configured for frontend domain
- [ ] Session manager uses `AppConfig.apiBaseUrl` (not `baseUrl`)
- [ ] No hardcoded localhost URLs in JavaScript files
- [ ] Backend `appsettings.json` has correct connection string
- [ ] Azure CLI authenticated (`az account show`)

## Post-Deployment Checklist

- [ ] Application loads without errors
- [ ] `window.ENV_CONFIG.API_BASE_URL` shows deployed URL (NOT localhost)
- [ ] Login works successfully
- [ ] Session management working (no 401 errors)
- [ ] No `ERR_BLOCKED_BY_CLIENT` errors in console
- [ ] API calls going to correct backend URL
- [ ] Database queries working
- [ ] Background jobs running (Hangfire dashboard)

## Additional Resources

- **Azure CLI Documentation**: https://docs.microsoft.com/en-us/cli/azure/
- **App Service Deployment**: https://docs.microsoft.com/en-us/azure/app-service/deploy-zip
- **.NET Core Deployment**: https://docs.microsoft.com/en-us/aspnet/core/host-and-deploy/

## Contact

For deployment issues or questions, refer to this guide first, then contact the development team.
