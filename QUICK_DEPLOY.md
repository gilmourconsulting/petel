# 🚀 Quick Deployment Reference

## Flexible Deployment Options

### Deploy Both API + Blazor (Default)
```powershell
.\Deploy-ToAzure.ps1 -Environment test
```

### Deploy Only API Backend
```powershell
.\Deploy-ToAzure.ps1 -Environment test -ApiOnly
```

### Deploy Only Blazor Frontend
```powershell
.\Deploy-ToAzure.ps1 -Environment test -BlazorOnly
```

### Skip Build (Use Existing Publish Folder)
```powershell
.\Deploy-ToAzure.ps1 -Environment test -SkipBuild
```

## What Happens
✅ Fully automated - no manual steps
✅ Deploy both or either component
✅ Builds, packages, and deploys
✅ Automatic app service restart
✅ Health check verification
✅ SUCCESS or FAILURE at end
⏱️ Takes ~3-5 minutes (both), ~2 minutes (single component)

## Deployment Environments

| Environment | Command | Status |
|-------------|---------|--------|
| **Test** | `.\Deploy-ToAzure.ps1 -Environment test` | ✅ Ready |
| **Staging** | `.\Deploy-ToAzure.ps1 -Environment staging` | ✅ Ready |
| **Production** | `.\Deploy-ToAzure.ps1 -Environment production` | ✅ Ready |

## Expected Result

### ✅ Success Looks Like
```
╔════════════════════════════════════════════════════════╗
║  ✅ DEPLOYMENT SUCCESS                                 ║
╚════════════════════════════════════════════════════════╝

📊 Deployment Summary:
   Environment:     test
   Duration:        3m 42s
   Health check:    PASSED ✅
```

### ❌ Failure Looks Like
```
❌ FATAL: Azure deployment failed!
[Error details shown here]
Exit code: 1
```

## Use Cases

### Backend API Changes Only
When you've only modified the backend API:
```powershell
.\Deploy-ToAzure.ps1 -Environment test -ApiOnly
```
**Saves time**: ~2 minutes instead of ~5 minutes

### Frontend UI Changes Only
When you've only modified Blazor components:
```powershell
.\Deploy-ToAzure.ps1 -Environment test -BlazorOnly
```
**Saves time**: ~2 minutes instead of ~5 minutes

### Full Stack Changes
When you've changed both frontend and backend:
```powershell
.\Deploy-ToAzure.ps1 -Environment test
```
**Deploys both**: API + Blazor in one command

### Quick Redeploy (No Code Changes)
When you need to redeploy without rebuilding:
```powershell
.\Deploy-ToAzure.ps1 -Environment test -SkipBuild
```
**Saves time**: Uses existing publish folder

## Quick Fixes

### "Still has previous version"
- ✅ Fixed - Script automatically stops/starts App Service
- Forces clean deployment every time

### "Which script should I use?"
- ✅ Use `Deploy-ToAzure.ps1` for ALL deployments
- Old separate scripts (`Deploy-API ToAzure.ps1`, `Deploy-Blazor-ToAzure.ps1`) are now obsolete

## Deployment Architecture

The Petel application consists of **TWO separate Azure App Services**:

```
┌─────────────────────────────────────────────┐
│  User Browser                               │
│  https://petel-test-blazor.azurewebsites.net│
└──────────────────┬──────────────────────────┘
                   │
                   │ Blazor Server UI
                   ▼
┌─────────────────────────────────────────────┐
│  Blazor Server App Service                  │
│  - PetelApp.BlazorServer                    │
│  - Pages, Components, Services              │
│  - Communicates with API                    │
└──────────────────┬──────────────────────────┘
                   │
                   │ HTTP API Calls
                   ▼
┌─────────────────────────────────────────────┐
│  API Backend App Service                    │
│  https://petel-test-api.azurewebsites.net   │
│  - PetelApp.Api                             │
│  - Controllers, Database Logic              │
│  - PostgreSQL Connection                    │
└─────────────────────────────────────────────┘
```

**Important**: Both services must be deployed for the application to work!

## Available Scripts

| Script | Purpose | Recommended |
|--------|---------|-------------|
| `Deploy-ToAzure.ps1` | **Unified deployment** - API and/or Blazor | ✅ **Use This** |
| `Deploy-Complete-ToTest.ps1` | Legacy - Test environment only | ⚠️ Use Deploy-ToAzure instead |
| `Deploy-API ToAzure.ps1` | Legacy - API only (incomplete) | ❌ Don't use alone |
| `Deploy-Blazor-ToAzure.ps1` | Legacy - Blazor only (incomplete) | ❌ Don't use alone |
| `Deploy-Api-ToTest.ps1` | Legacy - Test API only | ⚠️ Use Deploy-ToAzure instead |
| `Deploy-Blazor-ToTest.ps1` | Legacy - Test Blazor only | ⚠️ Use Deploy-ToAzure instead |

### "localhost" in deployed files
- Script will fail immediately
- Check: `petelapp-frontend/public/env-{environment}-config.js`

### Health check fails
- Script waits 30 seconds for warm-up
- Check Azure Portal → Application Insights for errors

## Post-Deployment Verification

1. Open browser to: https://petel-test-api.azurewebsites.net
2. Press F12 (console)
3. Type: `window.ENV_CONFIG.API_BASE_URL`
4. Should see: `https://petel-test-api-ahafcqfnh6drcdbd.israelcentral-01.azurewebsites.net/api`
5. Login and test features

## Rollback

If something goes wrong:
1. Azure Portal → App Service → Deployment Center
2. Click "Deployment History"
3. Select previous successful deployment
4. Click "Redeploy"

## Files Reference

### Required Files (Must Exist)
- ✅ `PetelApp.Api/appsettings.test.json`
- ✅ `petelapp-frontend/public/env-test-config.js`
- ⚠️ `PetelApp.Api/appsettings.Staging.json` (create for staging)
- ⚠️ `PetelApp.Api/appsettings.Production.json` (create for production)

### Generated Files (Automatic)
- `PetelApp.Api/publish/` - Build output
- `PetelApp.Api/deploy-{env}.zip` - Deployment package (~8-10 MB)

## Pro Tips

💡 Run from project root: `c:\dev\PetelFullApp`
💡 Script is idempotent - safe to re-run
💡 Each deployment creates fresh build
💡 Old deployments kept in Azure history
💡 Zero downtime - Azure handles cutover

## Support

- 📖 Full details: `DEPLOYMENT_CHECKLIST.md`
- 📝 Prompt template: `DEPLOYMENT_PROMPT.md`
- 🔧 Fixes applied: `DEPLOYMENT_FIXES_SUMMARY.md`
- 🤖 For future deployments: Just say "Deploy to test"
