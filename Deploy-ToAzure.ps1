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

# Check for localhost in env-config.js
if ($configContent -match "localhost") {
    Write-Host "❌ ERROR: env-config.js contains 'localhost'!" -ForegroundColor Red
    Write-Host $configContent
    Write-Host "`n⚠️  This indicates the wrong environment config was copied." -ForegroundColor Yellow
    Write-Host "Expected file: env-$Environment-config.js" -ForegroundColor Yellow
    exit 1
}
Write-Host "✅ Environment config verified (no localhost)" -ForegroundColor Green

# Verify session-manager.js has correct property name
$sessionManagerContent = Get-Content "$wwwrootPath\session-manager.js" | Select-String "apiBaseUrl"
if (-not $sessionManagerContent) {
    Write-Host "⚠️  WARNING: session-manager.js may not be using AppConfig.apiBaseUrl" -ForegroundColor Yellow
}

Write-Host "`nAPI_BASE_URL:" -ForegroundColor Cyan
Get-Content "$wwwrootPath\env-config.js" | Select-String "API_BASE_URL"

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

Write-Host "Resource Group: $rg" -ForegroundColor Cyan
Write-Host "App Service: $appName" -ForegroundColor Cyan

& "C:\Program Files\Microsoft SDKs\Azure\CLI2\wbin\az.cmd" webapp deploy `
    --resource-group $rg `
    --name $appName `
    --src-path "deploy-$Environment.zip" `
    --type zip

if ($LASTEXITCODE -eq 0) {
    Write-Host "`n✅ Deployment completed successfully!" -ForegroundColor Green
    
    # Restart App Service to clear cache
    Write-Host "`n🔄 Restarting App Service to clear cache..." -ForegroundColor Yellow
    & "C:\Program Files\Microsoft SDKs\Azure\CLI2\wbin\az.cmd" webapp restart `
        --resource-group $rg `
        --name $appName | Out-Null
    
    Write-Host "✅ App Service restarted" -ForegroundColor Green
    
    # Wait for warm-up
    Write-Host "`n⏳ Waiting 30 seconds for application to start..." -ForegroundColor Yellow
    Start-Sleep -Seconds 30
    
    # Health check
    Write-Host "🏥 Performing health check..." -ForegroundColor Yellow
    try {
        $healthResponse = Invoke-WebRequest -Uri "https://$appName.azurewebsites.net" -Method GET -TimeoutSec 30 -UseBasicParsing
        if ($healthResponse.StatusCode -eq 200) {
            Write-Host "✅ Health check passed!" -ForegroundColor Green
        }
    } catch {
        Write-Host "⚠️ Health check warning: $($_.Exception.Message)" -ForegroundColor Yellow
    }
    
    Write-Host "`n🔗 Application URL: https://$appName.azurewebsites.net" -ForegroundColor Cyan
    Write-Host "`n📋 Post-Deployment Verification:" -ForegroundColor Yellow
    Write-Host "  1. Open https://$appName.azurewebsites.net in browser" -ForegroundColor White
    Write-Host "  2. Open browser console (F12)" -ForegroundColor White
    Write-Host "  3. Type: window.ENV_CONFIG.API_BASE_URL" -ForegroundColor White
    Write-Host "  4. Verify it shows deployed API URL (NOT localhost)" -ForegroundColor White
    Write-Host "  5. Login and check for ERR_BLOCKED_BY_CLIENT errors" -ForegroundColor White
} else {
    Write-Host "`n❌ Deployment failed!" -ForegroundColor Red
    Write-Host "Check Azure deployment logs for details." -ForegroundColor Yellow
    exit 1
}
