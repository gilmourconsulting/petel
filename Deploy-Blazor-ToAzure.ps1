param(
    [Parameter(Mandatory=$true)]
    [ValidateSet('test', 'staging', 'production')]
    [string]$Environment
)

$ErrorActionPreference = "Stop"
$rootPath = "c:\dev\PetelFullApp"
Set-Location $rootPath

Write-Host "Deploying Blazor Server to $Environment environment..." -ForegroundColor Cyan

# Step 1: Clean previous builds
Write-Host "`nCleaning previous build..." -ForegroundColor Yellow
Remove-Item -Path "$rootPath\PetelApp.Api\publish" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -Path "$rootPath\PetelApp.BlazorServer\publish" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -Path "$rootPath\deploy-blazor-$Environment.zip" -Force -ErrorAction SilentlyContinue

# Step 2: Publish Backend API
Write-Host "`nPublishing backend API..." -ForegroundColor Yellow
Set-Location "$rootPath\PetelApp.Api"
dotnet clean -v quiet
dotnet publish -c Release -o "$rootPath\PetelApp.Api\publish" -v quiet

if (-not (Test-Path "$rootPath\PetelApp.Api\publish\PetelApp.Api.dll")) {
    Write-Host "ERROR: Backend API publish failed!" -ForegroundColor Red
    Set-Location $rootPath
    exit 1
}
Write-Host "SUCCESS: Backend API published" -ForegroundColor Green

# Step 3: Publish Blazor Server
Write-Host "`nPublishing Blazor Server..." -ForegroundColor Yellow
Set-Location "$rootPath\PetelApp.BlazorServer"
dotnet clean -v quiet
dotnet publish -c Release -o "$rootPath\PetelApp.BlazorServer\publish" -v quiet

if (-not (Test-Path "$rootPath\PetelApp.BlazorServer\publish\PetelApp.BlazorServer.dll")) {
    Write-Host "ERROR: Blazor Server publish failed!" -ForegroundColor Red
    Set-Location $rootPath
    exit 1
}
Write-Host "SUCCESS: Blazor Server published" -ForegroundColor Green

# Step 4: Configure environment-specific settings
Write-Host "`nConfiguring environment settings..." -ForegroundColor Yellow

$blazorPublishPath = "$rootPath\PetelApp.BlazorServer\publish"
$appSettingsPath = Join-Path $blazorPublishPath "appsettings.json"

if (Test-Path $appSettingsPath) {
    $appSettings = Get-Content $appSettingsPath -Raw | ConvertFrom-Json
    
    # Environment-specific API URLs
    $apiUrls = @{
        'test' = 'https://petel-test-api.azurewebsites.net'
        'staging' = 'https://petel-staging-api.azurewebsites.net'
        'production' = 'https://petel-prod-api.azurewebsites.net'
    }
    
    if (-not $appSettings.PSObject.Properties['ApiSettings']) {
        $appSettings | Add-Member -MemberType NoteProperty -Name 'ApiSettings' -Value @{}
    }
    
    $appSettings.ApiSettings | Add-Member -MemberType NoteProperty -Name 'BaseUrl' -Value $apiUrls[$Environment] -Force
    
    $appSettings | ConvertTo-Json -Depth 10 | Set-Content $appSettingsPath
    Write-Host "SUCCESS: Configured API URL: $($apiUrls[$Environment])" -ForegroundColor Green
} else {
    Write-Host "WARNING: appsettings.json not found in publish folder" -ForegroundColor Yellow
}

Set-Location $rootPath

# Step 5: Create deployment package
Write-Host "`nCreating deployment package..." -ForegroundColor Yellow
Push-Location $blazorPublishPath
& tar.exe -a -c -f "$rootPath\deploy-blazor-$Environment.zip" *
Pop-Location

if (-not (Test-Path "$rootPath\deploy-blazor-$Environment.zip")) {
    Write-Host "ERROR: Failed to create ZIP package!" -ForegroundColor Red
    exit 1
}

$size = (Get-Item "$rootPath\deploy-blazor-$Environment.zip").Length / 1MB
Write-Host "SUCCESS: Package created: deploy-blazor-$Environment.zip ($([math]::Round($size, 2)) MB)" -ForegroundColor Green

# Step 6: Verify package contents
Write-Host "`nVerifying package contents..." -ForegroundColor Yellow
Write-Host "Checking for old HTML frontend files..." -ForegroundColor Cyan

$tempExtractPath = "$rootPath\temp-verify-$Environment"
New-Item -Path $tempExtractPath -ItemType Directory -Force | Out-Null
Expand-Archive -Path "$rootPath\deploy-blazor-$Environment.zip" -DestinationPath $tempExtractPath -Force

$oldFrontendMarkers = @(Get-ChildItem -Path $tempExtractPath -Recurse -Include "menu.html", "students.html", "schoollist.html" -ErrorAction SilentlyContinue)

if ($oldFrontendMarkers.Count -gt 0) {
    Write-Host "WARNING: Old frontend HTML files detected in package:" -ForegroundColor Yellow
    $oldFrontendMarkers | ForEach-Object { Write-Host "  - $($_.Name)" -ForegroundColor Yellow }
} else {
    Write-Host "SUCCESS: No old frontend HTML files detected" -ForegroundColor Green
}

$blazorFiles = @(Get-ChildItem -Path "$tempExtractPath\wwwroot\_framework" -ErrorAction SilentlyContinue)
if ($blazorFiles.Count -gt 0) {
    Write-Host "SUCCESS: Blazor framework files found ($($blazorFiles.Count) files)" -ForegroundColor Green
} else {
    Write-Host "WARNING: Blazor framework files not found" -ForegroundColor Yellow
}

Remove-Item -Path $tempExtractPath -Recurse -Force -ErrorAction SilentlyContinue
Write-Host "SUCCESS: Package verification complete" -ForegroundColor Green

# Step 7: Deploy to Azure
Write-Host "`nDeploying to Azure..." -ForegroundColor Yellow

$resourceGroups = @{
    'test' = 'petel-test-rg'
    'staging' = 'petel-staging-rg'
    'production' = 'petel-prod-rg'
}

$blazorAppNames = @{
    'test' = 'petel-test-blazor'
    'staging' = 'petel-staging-blazor'
    'production' = 'petel-prod-blazor'
}

$rg = $resourceGroups[$Environment]
$appName = $blazorAppNames[$Environment]

Write-Host "Resource Group: $rg" -ForegroundColor Cyan
Write-Host "App Service: $appName" -ForegroundColor Cyan

# Check if app service exists
Write-Host "`nChecking if App Service exists..." -ForegroundColor Yellow
$appExists = & "C:\Program Files\Microsoft SDKs\Azure\CLI2\wbin\az.cmd" webapp show `
    --resource-group $rg `
    --name $appName `
    --query "name" `
    --output tsv 2>$null

if (-not $appExists) {
    Write-Host "WARNING: App Service '$appName' not found. Creating..." -ForegroundColor Yellow
    
    # Create App Service Plan if needed
    $planName = "petel-$Environment-plan"
    $planExists = & "C:\Program Files\Microsoft SDKs\Azure\CLI2\wbin\az.cmd" appservice plan show `
        --resource-group $rg `
        --name $planName `
        --query "name" `
        --output tsv 2>$null
    
    if (-not $planExists) {
        Write-Host "Creating App Service Plan '$planName'..." -ForegroundColor Yellow
        & "C:\Program Files\Microsoft SDKs\Azure\CLI2\wbin\az.cmd" appservice plan create `
            --resource-group $rg `
            --name $planName `
            --sku B1 `
            --location "West Europe"
    }
    
    # Create Web App with proper runtime escaping
    Write-Host "Creating Web App '$appName'..." -ForegroundColor Yellow
    & "C:\Program Files\Microsoft SDKs\Azure\CLI2\wbin\az.cmd" webapp create `
        --resource-group $rg `
        --plan $planName `
        --name $appName `
        --runtime "DOTNETCORE:8.0"
    
    Write-Host "SUCCESS: App Service created" -ForegroundColor Green
    Start-Sleep -Seconds 5
}

# Stop the app service first to force a clean deployment
Write-Host "`nStopping app service..." -ForegroundColor Yellow
& "C:\Program Files\Microsoft SDKs\Azure\CLI2\wbin\az.cmd" webapp stop `
    --resource-group $rg `
    --name $appName
Start-Sleep -Seconds 5

# Deploy using config-zip
Write-Host "Uploading deployment package..." -ForegroundColor Yellow
& "C:\Program Files\Microsoft SDKs\Azure\CLI2\wbin\az.cmd" webapp deployment source config-zip `
    --resource-group $rg `
    --name $appName `
    --src "$rootPath\deploy-blazor-$Environment.zip"

# Start the app service
Write-Host "Starting app service..." -ForegroundColor Yellow
& "C:\Program Files\Microsoft SDKs\Azure\CLI2\wbin\az.cmd" webapp start `
    --resource-group $rg `
    --name $appName

Write-Host "`nSUCCESS: Deployment completed!" -ForegroundColor Green
Write-Host "SUCCESS: App Service restarted" -ForegroundColor Green

# Wait for warm-up
Write-Host "`nWaiting 30 seconds for application to start..." -ForegroundColor Yellow
Start-Sleep -Seconds 30

# Health check
Write-Host "Performing health check..." -ForegroundColor Yellow
try {
    $healthResponse = Invoke-WebRequest -Uri "https://$appName.azurewebsites.net" -Method GET -TimeoutSec 30 -UseBasicParsing
    if ($healthResponse.StatusCode -eq 200) {
        Write-Host "SUCCESS: Health check passed!" -ForegroundColor Green
    }
} catch {
    Write-Host "WARNING: Health check warning: $($_.Exception.Message)" -ForegroundColor Yellow
}

Write-Host "`nApplication URLs:" -ForegroundColor Cyan
Write-Host "  Blazor Server: https://$appName.azurewebsites.net" -ForegroundColor White
Write-Host "  Backend API:   https://petel-$Environment-api.azurewebsites.net" -ForegroundColor White

Write-Host "`nPost-Deployment Verification:" -ForegroundColor Yellow
Write-Host "  1. Open https://$appName.azurewebsites.net in browser" -ForegroundColor White
Write-Host "  2. Verify Blazor Server UI loads" -ForegroundColor White
Write-Host "  3. Check browser console for any errors" -ForegroundColor White
Write-Host "  4. Test login and navigation" -ForegroundColor White
Write-Host "  5. Verify API calls to backend" -ForegroundColor White

Write-Host "`nNotes:" -ForegroundColor Yellow
Write-Host "  - This deployment includes Blazor Server components only" -ForegroundColor White
Write-Host "  - Old HTML frontend files are NOT included" -ForegroundColor White
Write-Host "  - Backend API must be deployed separately using Deploy-ToAzure.ps1" -ForegroundColor White
