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

# Stop the app service first to force a clean deployment
Write-Host "`n⏸️  Stopping app service..." -ForegroundColor Yellow
& "C:\Program Files\Microsoft SDKs\Azure\CLI2\wbin\az.cmd" webapp stop `
    --resource-group $rg `
    --name $appName
Start-Sleep -Seconds 5

# Deploy using config-zip (more reliable than deploy)
Write-Host "📤 Uploading deployment package..." -ForegroundColor Yellow
& "C:\Program Files\Microsoft SDKs\Azure\CLI2\wbin\az.cmd" webapp deployment source config-zip `
    --resource-group $rg `
    --name $appName `
    --src "deploy-$Environment.zip"

# Start the app service
Write-Host "▶️  Starting app service..." -ForegroundColor Yellow
& "C:\Program Files\Microsoft SDKs\Azure\CLI2\wbin\az.cmd" webapp start `
    --resource-group $rg `
    --name $appName

Write-Host "`n✅ Deployment completed!" -ForegroundColor Green
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
