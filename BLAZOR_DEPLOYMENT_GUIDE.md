# Blazor Server Test Environment Deployment Guide

## Overview
This guide documents the deployment process for the Petel Educational Management System's Blazor Server application to Azure test environment.

## Architecture
- **Frontend**: Blazor Server (.NET 8.0)
- **Backend**: ASP.NET Core Web API (.NET 8.0)
- **Database**: PostgreSQL (existing, configured in production)
- **Platform**: Azure App Service on Linux

## Deployment URLs
- **Blazor App**: https://petel-test-blazor.azurewebsites.net
- **API**: https://petel-test-api-ahafcqfnh6drcdbd.israelcentral-01.azurewebsites.net
- **Swagger**: https://petel-test-api-ahafcqfnh6drcdbd.israelcentral-01.azurewebsites.net/swagger

## Azure Resources
### Resource Group: `petel-test-rg`
- **App Service Plan**: `petel-test-plan` (B1 Basic, Linux)
- **Blazor App Service**: `petel-test-blazor`
- **API App Service**: `petel-test-api`

## Critical Lessons Learned

### 1. .NET Runtime Version Mismatch
**Problem**: App compiled for .NET 9.0 but Azure only had .NET 8.0 containers available.
**Solution**: 
- Downgraded project to .NET 8.0: `<TargetFramework>net8.0</TargetFramework>`
- Replaced .NET 9 APIs:
  - `app.MapStaticAssets()` → `app.UseStaticFiles()`
  - `@Assets["file.css"]` → Direct path `"file.css"`

### 2. Wrong Runtime Stack (PHP Instead of .NET)
**Problem**: App Service was pulling PHP 8.2 container instead of .NET 8.0.
**Solution**: Deleted and recreated App Service with correct runtime:
```bash
az webapp create --runtime "DOTNETCORE:8.0"
```
**Note**: Changing `linuxFxVersion` config alone doesn't work if app was originally created with wrong stack.

### 3. Port Binding for Azure App Service Linux
**Problem**: App not listening on correct port.
**Solution**: Configure port binding in `Program.cs`:
```csharp
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
```

### 4. HTTPS Redirection Issues
**Problem**: App tried to redirect to HTTPS causing failures.
**Solution**: Disabled HTTPS redirection in production (Azure handles TLS termination):
```csharp
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // DO NOT use: app.UseHttpsRedirection();
    // DO NOT use: app.UseHsts();
}
```

### 5. API URL Configuration
**Problem**: Blazor app using localhost API URL in production.
**Solution**: Created `appsettings.Production.json` with correct API URL:
```json
{
  "ApiSettings": {
    "BaseUrl": "https://petel-test-api-ahafcqfnh6drcdbd.israelcentral-01.azurewebsites.net/api"
  }
}
```

### 6. IP Restrictions Blocking Blazor App
**Problem**: API returned 403 (IP Forbidden) to Blazor app requests.
**Solution**: Added all Blazor outbound IPs to API's IP allow list (31 IPs).

## Deployment Process

### Prerequisites
- Azure CLI installed and authenticated
- .NET 8.0 SDK installed
- PowerShell 5.1 or higher

### Step-by-Step Deployment

#### 1. Build Blazor Server Application
```powershell
cd c:\dev\PetelFullApp\PetelApp.BlazorServer
dotnet publish -c Release -o publish
```

#### 2. Create Deployment Package
```powershell
cd c:\dev\PetelFullApp
Push-Location PetelApp.BlazorServer\publish
tar.exe -a -c -f ..\..\blazor-deploy.zip *
Pop-Location
```

#### 3. Create/Update Blazor App Service
```powershell
# Create (if doesn't exist)
az webapp create --resource-group petel-test-rg `
  --plan petel-test-plan `
  --name petel-test-blazor `
  --runtime "DOTNETCORE:8.0"

# Configure
az webapp config appsettings set `
  --resource-group petel-test-rg `
  --name petel-test-blazor `
  --settings ASPNETCORE_ENVIRONMENT="Production"
```

#### 4. Deploy Blazor Application
```powershell
az webapp deploy `
  --resource-group petel-test-rg `
  --name petel-test-blazor `
  --src-path blazor-deploy.zip `
  --type zip `
  --restart true `
  --timeout 300
```

#### 5. Build and Deploy API
```powershell
cd c:\dev\PetelFullApp\PetelApp.Api
dotnet publish -c Release -o publish
cd c:\dev\PetelFullApp
Push-Location PetelApp.Api\publish
tar.exe -a -c -f ..\..\api-deploy.zip *
Pop-Location

az webapp deploy `
  --resource-group petel-test-rg `
  --name petel-test-api `
  --src-path api-deploy.zip `
  --type zip `
  --timeout 300
```

#### 6. Configure IP Restrictions
```powershell
# Get Blazor outbound IPs
$blazorIps = az webapp show `
  --resource-group petel-test-rg `
  --name petel-test-blazor `
  --query possibleOutboundIpAddresses -o tsv

# Add each IP to API allow list
$ipArray = $blazorIps -split ','
$priority = 300
foreach ($ip in $ipArray) {
  az webapp config access-restriction add `
    --resource-group petel-test-rg `
    --name petel-test-api `
    --rule-name "Allow-Blazor-$priority" `
    --action Allow `
    --ip-address "$ip/32" `
    --priority $priority
  $priority++
}
```

## Verification Steps

### 1. Check Blazor App
```powershell
Invoke-WebRequest "https://petel-test-blazor.azurewebsites.net" -UseBasicParsing
```
Should return 200 and contain "Blazor" in content.

### 2. Check API
```powershell
Invoke-WebRequest "https://petel-test-api-ahafcqfnh6drcdbd.israelcentral-01.azurewebsites.net/api/entities/login" -UseBasicParsing
```
Should return 200 with entity data.

### 3. Check Docker Logs
```powershell
az webapp log download --resource-group petel-test-rg --name petel-test-blazor --log-file logs.zip
```
Look for:
- "Now listening on: http://0.0.0.0:8080" ✅
- "Pulling image: appsvc/dotnetcore:8.0" ✅
- NOT "Pulling image: appsvc/php" ❌

## Troubleshooting

### Issue: 403 (IP Forbidden)
**Symptom**: API returns 403 errors in Blazor logs.
**Solution**: Add Blazor outbound IPs to API IP restrictions (see step 6 above).

### Issue: Placeholder Page Instead of Blazor
**Symptom**: "Your web app is running and waiting for your content"
**Solution**: App Service was created with wrong runtime stack. Delete and recreate with correct runtime.

### Issue: App Won't Start (Exit Code 150)
**Symptom**: Container exits with code 150 after 35 seconds.
**Solution**: .NET version mismatch. Ensure app targets .NET 8.0.

### Issue: "Cannot assign requested address (localhost:5082)"
**Symptom**: Blazor app trying to connect to localhost.
**Solution**: Update `appsettings.Production.json` with correct API URL.

## Configuration Files

### appsettings.Production.json (Blazor)
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ApiSettings": {
    "BaseUrl": "https://petel-test-api-ahafcqfnh6drcdbd.israelcentral-01.azurewebsites.net/api",
    "Timeout": 30
  }
}
```

### Program.cs (Key Sections)
```csharp
// Port binding for Azure
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// NO HTTPS redirection in production
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // app.UseHsts();          // NO!
    // app.UseHttpsRedirection(); // NO!
}
```

## Database Migrations
- **Last Check**: January 21, 2026
- **Status**: No new migrations in last 5 days
- **Action**: Database schema is stable, no changes needed

## Security Notes
- API has IP restrictions enabled
- Only whitelisted IPs (including Blazor app) can access API
- Database connection strings configured in Azure Portal
- All secrets managed via Azure Key Vault

## Monitoring
- Application Insights enabled
- Docker logs available via Azure Portal or CLI
- Log retention: 30 days

## Rollback Procedure
1. Redeploy previous version ZIP file
2. Or use Azure deployment slots for instant rollback
3. Check deployment history: `az webapp deployment list-publishing-profiles`

## Support Contacts
- Azure Subscription: cab259e3-0053-427d-a93a-9330eff7dcd3
- Region: Israel Central

---
**Last Updated**: January 21, 2026  
**Deployment Time**: ~5-7 minutes (both apps)  
**Tested By**: Deployment successful with full functionality
