# Automated Deployment Prompt

## 🎯 Use this prompt for future deployments

```
Deploy the Petel application to [test/staging/production] environment.

Requirements:
1. Run the automated deployment script Deploy-ToAzure.ps1
2. The deployment must be fully automated with no manual intervention
3. Report SUCCESS or FAILURE at the end
4. If deployment fails, provide specific error details and suggested fixes
5. Verify the deployed application is running correctly
6. Confirm the correct environment configuration is active

Expected outcome:
- ✅ Application deployed to Azure
- ✅ Health check passed
- ✅ Correct environment configuration verified
- ✅ No localhost references
- ✅ Application responding to requests

Report format:
- Environment: [test/staging/production]
- Status: [SUCCESS/FAILURE]
- Duration: [X minutes]
- Health Check: [PASS/FAIL]
- Configuration: [VERIFIED/ERROR]
- Issues: [None or list of issues]
```

## 📋 Example Usage

### For Test Environment
```
Deploy the Petel application to test environment.
```

### For Production Environment
```
Deploy the Petel application to production environment.
```

## ⚠️ Important Notes

### Before First Deployment to Staging/Production
1. Create missing appsettings files:
   - `PetelApp.Api/appsettings.Staging.json`
   - `PetelApp.Api/appsettings.Production.json`
2. Verify Azure resources exist
3. Update connection strings
4. Test locally first

### After Deployment
The script will automatically:
- ✅ Build and publish the application
- ✅ Deploy to Azure App Service
- ✅ Restart the service to clear cache
- ✅ Perform health check
- ✅ Verify configuration
- ✅ Report results

### If Deployment Fails
The script will:
- ❌ Report specific error
- 📝 Show log excerpts
- 💡 Suggest remediation steps
- 🔄 Indicate if rollback is needed

## 🔍 Validation Steps (Automatic)

1. **Build Validation**: Verify PetelApp.Api.dll exists
2. **Configuration Validation**: Check for localhost references
3. **Deployment Validation**: Verify Azure CLI success
4. **Health Validation**: HTTP request to deployed URL
5. **Environment Validation**: Check window.ENV_CONFIG matches environment

## 📊 Success Indicators

✅ **All indicators must be green for successful deployment:**

- Build: PetelApp.Api.dll created
- Package: deploy-{env}.zip created (size: ~8-10 MB)
- Azure: "Deployment has completed successfully"
- Restart: App Service restarted successfully
- Health: HTTP 200 response from root URL
- Config: ENV_CONFIG.API_BASE_URL matches environment
- Runtime: No startup errors in logs

## 🚨 Failure Indicators

❌ **Any red indicator requires investigation:**

- Build: Missing DLL or build errors
- Package: ZIP creation failed
- Azure: Deployment timeout or error
- Restart: Service restart failed
- Health: HTTP error or timeout
- Config: localhost found in deployed files
- Runtime: Application errors in logs

## 💡 Quick Fixes

### "Still has previous version"
- Root cause: Azure cache not cleared
- Fix: Script now includes automatic restart

### "localhost" in deployed files
- Root cause: Wrong env-config copied
- Fix: Script now verifies correct file

### Health check timeout
- Root cause: Application slow to start
- Fix: Script now includes 30-second warm-up

### Database connection error
- Root cause: Wrong connection string
- Fix: Verify appsettings.{Environment}.json

## 📝 Deployment Log Template

After deployment, you should see output similar to:

```
🚀 Deploying to test environment...
✅ Build completed: PetelApp.Api.dll (10.2 MB)
✅ Frontend copied: 45 files
✅ Environment config: env-test-config.js → env-config.js
✅ Validation passed: No localhost references found
✅ Package created: deploy-test.zip (8.33 MB)
✅ Azure deployment: Completed successfully
✅ Service restart: Completed in 15s
✅ Health check: HTTP 200 OK
✅ Configuration check: API_BASE_URL correct
✅ Deployment SUCCESS in 3m 42s

🔗 Application URL: https://petel-test-api.azurewebsites.net
```

## 🔄 Rollback Instructions

If you need to rollback:

1. In Azure Portal:
   - Navigate to App Service → Deployment Center
   - View Deployment History
   - Select previous successful deployment
   - Click "Redeploy"

2. Or re-run script with previous git commit:
   ```powershell
   git checkout <previous-commit-hash>
   .\Deploy-ToAzure.ps1 -Environment test
   git checkout main
   ```
