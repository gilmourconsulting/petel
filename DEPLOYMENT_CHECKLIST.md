# Petel Application - Automated Deployment Checklist

## 🎯 Purpose
This document provides a comprehensive checklist for fully automated deployments to Azure environments (test, staging, production).

## ⚠️ Critical Issues Found (December 2024)

### Issue 1: Missing Environment Configuration Files
**Problem**: Only `appsettings.test.json` exists. Missing `appsettings.Staging.json` and `appsettings.Production.json`
**Impact**: Staging and production deployments will fail or use wrong configuration
**Status**: ❌ CRITICAL - Must create before deploying to staging/production

### Issue 2: PowerShell Script Syntax Error
**Problem**: Line 60 in Deploy-ToAzure.ps1 has parsing error: `Write-Host "✅ Environment config verified (no localhost)" -ForegroundColor Green`
**Impact**: Script fails during verification step
**Status**: ✅ FIXED in updated script

### Issue 3: No Post-Deployment Health Check
**Problem**: Script doesn't verify the deployed application is actually running
**Impact**: Deployment may succeed but application is broken
**Status**: ✅ ADDED automated health check

### Issue 4: No Automatic Cache Clearing
**Problem**: Azure App Service may cache old files, causing "still has previous version" issue
**Impact**: Deployment completes but old code still runs
**Status**: ✅ ADDED automatic restart and cache clear

### Issue 5: Missing Deployment Validation
**Problem**: No verification that correct environment config was deployed
**Impact**: Wrong API URLs, wrong database connections
**Status**: ✅ ADDED post-deployment validation

## 📋 Pre-Deployment Requirements

### Backend Configuration Files Required
- [x] `appsettings.json` (base configuration)
- [x] `appsettings.Development.json` (local development)
- [x] `appsettings.test.json` (test environment)
- [ ] `appsettings.Staging.json` (staging environment) - **MISSING**
- [ ] `appsettings.Production.json` (production environment) - **MISSING**

### Frontend Configuration Files Required
- [x] `env-config.js` (default/development)
- [x] `env-test-config.js` (test environment)
- [x] `env-staging-config.js` (staging environment)
- [x] `env-production-config.js` (production environment)

### Azure Resources Required
- [x] Test: Resource Group `petel-test-rg`, App Service `petel-test-api`
- [ ] Staging: Resource Group `petel-staging-rg`, App Service `petel-staging-api` - **VERIFY**
- [ ] Production: Resource Group `petel-prod-rg`, App Service `petel-prod-api` - **VERIFY**

## 🚀 Deployment Process

### Automated Steps (No Manual Intervention)
1. ✅ Clean previous build artifacts
2. ✅ Build and publish .NET backend
3. ✅ Copy frontend files to wwwroot
4. ✅ Copy environment-specific config
5. ✅ Verify no localhost references
6. ✅ Create deployment ZIP package
7. ✅ Deploy to Azure App Service
8. ✅ Restart App Service (clear cache)
9. ✅ Wait for application warm-up
10. ✅ Health check - verify application is responding
11. ✅ Configuration check - verify correct environment
12. ✅ Report SUCCESS or FAILURE

### Expected Duration
- Test environment: ~3-5 minutes
- Staging environment: ~3-5 minutes
- Production environment: ~5-7 minutes (with additional validation)

## ✅ Success Criteria

### Deployment Success
- Exit code: 0
- Azure CLI reports: "Deployment has completed successfully"
- App Service status: "Running"
- Health check endpoint returns: HTTP 200

### Configuration Success
- `window.ENV_CONFIG.API_BASE_URL` matches expected environment URL
- No localhost references in deployed files
- Backend appsettings matches environment

### Application Success
- Login page loads successfully
- API endpoints respond correctly
- No console errors in browser

## ❌ Failure Scenarios

### Build Failures
- Missing dependencies: Check NuGet packages
- Compilation errors: Check recent code changes
- Missing files: Verify all files are committed

### Deployment Failures
- Azure CLI not authenticated: Run `az login`
- Resource not found: Verify resource group and app service names
- Permission denied: Check Azure RBAC permissions

### Runtime Failures
- 500 errors: Check Application Insights logs
- Database connection failed: Verify connection string in Azure portal
- Configuration errors: Verify appsettings files are correct

## 🔍 Post-Deployment Validation

### Automatic Checks (Performed by Script)
1. ✅ HTTP health check on root URL
2. ✅ Verify ENV_CONFIG in browser console
3. ✅ Check for CORS errors
4. ✅ Verify login page loads

### Manual Checks (Optional)
1. Login with test user
2. Navigate through main features
3. Check browser console for errors
4. Verify database connections
5. Check Application Insights for exceptions

## 📝 Rollback Procedure

If deployment fails:
1. Azure App Service maintains previous deployment
2. Use Azure Portal → Deployment Center → Deployment History
3. Select previous successful deployment
4. Click "Redeploy"
5. Alternative: Re-run script with last known good commit

## 🔒 Security Checklist

- [ ] Connection strings not in source control
- [ ] Passwords stored in Azure Key Vault
- [ ] CORS configured for environment-specific origins only
- [ ] HTTPS enforced
- [ ] OTP enabled in test/production
- [ ] Application Insights logs no sensitive data

## 📞 Troubleshooting

### "Still has previous version" Issue
**Cause**: Azure App Service cache not cleared
**Solution**: Updated script includes automatic restart and cache clear

### "localhost" in deployed files
**Cause**: Wrong env-config.js copied
**Solution**: Updated script verifies correct file before deployment

### Health check fails
**Cause**: Application not fully started
**Solution**: Updated script includes 30-second warm-up period

### Database connection errors
**Cause**: Wrong connection string or firewall rules
**Solution**: Verify appsettings.{Environment}.json and Azure firewall rules

## 📚 Required Files for Each Environment

### Test Environment
- Backend: `appsettings.test.json` ✅
- Frontend: `env-test-config.js` ✅
- Database: `petel-test-db` ✅
- App Service: `petel-test-api` ✅

### Staging Environment
- Backend: `appsettings.Staging.json` ❌ **CREATE THIS**
- Frontend: `env-staging-config.js` ✅
- Database: `petel-staging-db` ❓ **VERIFY**
- App Service: `petel-staging-api` ❓ **VERIFY**

### Production Environment
- Backend: `appsettings.Production.json` ❌ **CREATE THIS**
- Frontend: `env-production-config.js` ✅
- Database: `petel-prod-db` ❓ **VERIFY**
- App Service: `petel-prod-api` ❓ **VERIFY**
