# 🚀 Quick Deployment Reference

## One-Line Deployment

```powershell
.\Deploy-ToAzure.ps1 -Environment test
```

## What Happens
✅ Fully automated - no manual steps
✅ 10 steps with clear progress
✅ Automatic cache clearing
✅ Health check verification
✅ SUCCESS or FAILURE at end
⏱️ Takes ~3-5 minutes

## Deployment Environments

| Environment | Command | Status |
|-------------|---------|--------|
| **Test** | `.\Deploy-ToAzure.ps1 -Environment test` | ✅ Ready |
| **Staging** | `.\Deploy-ToAzure.ps1 -Environment staging` | ⚠️ Need appsettings.Staging.json |
| **Production** | `.\Deploy-ToAzure.ps1 -Environment production` | ⚠️ Need appsettings.Production.json |

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

## Quick Fixes

### "Still has previous version"
- ✅ Fixed in updated script
- Script now restarts App Service automatically

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
