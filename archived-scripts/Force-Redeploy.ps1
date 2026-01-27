param(
    [Parameter(Mandatory=$true)]
    [ValidateSet('test', 'staging', 'production')]
    [string]$Environment
)

$appNames = @{
    'test' = 'petel-test-api'
    'staging' = 'petel-staging-api'
    'production' = 'petel-prod-api'
}

$resourceGroups = @{
    'test' = 'petel-test-rg'
    'staging' = 'petel-staging-rg'
    'production' = 'petel-prod-rg'
}

$rg = $resourceGroups[$Environment]
$appName = $appNames[$Environment]

Write-Host "🔄 Force Redeployment Process for $Environment" -ForegroundColor Cyan
Write-Host ""

# Step 1: Stop the app
Write-Host "1️⃣ Stopping app service..." -ForegroundColor Yellow
& "C:\Program Files\Microsoft SDKs\Azure\CLI2\wbin\az.cmd" webapp stop `
    --resource-group $rg `
    --name $appName
Write-Host "   ✅ App stopped" -ForegroundColor Green
Start-Sleep -Seconds 5

# Step 2: Clear Kudu cache
Write-Host ""
Write-Host "2️⃣ Clearing Kudu deployment cache..." -ForegroundColor Yellow
$kuduBaseUrl = "https://$appName.scm.azurewebsites.net"

Write-Host "   🗑️  Deleting wwwroot contents..." -ForegroundColor White
try {
    # Note: This requires SCM credentials - manual step if automated deletion fails
    Write-Host "   ⚠️  Manual step required: Use Kudu Console to delete wwwroot contents" -ForegroundColor Yellow
    Write-Host "   URL: $kuduBaseUrl/DebugConsole" -ForegroundColor Cyan
} catch {
    Write-Host "   ℹ️  Use Kudu console manually if needed" -ForegroundColor White
}

# Step 3: Restart app service (to clear any in-memory cache)
Write-Host ""
Write-Host "3️⃣ Performing hard restart..." -ForegroundColor Yellow
& "C:\Program Files\Microsoft SDKs\Azure\CLI2\wbin\az.cmd" webapp restart `
    --resource-group $rg `
    --name $appName
Write-Host "   ✅ App restarted" -ForegroundColor Green
Start-Sleep -Seconds 10

# Step 4: Rebuild and redeploy
Write-Host ""
Write-Host "4️⃣ Rebuilding application..." -ForegroundColor Yellow

Set-Location "c:\dev\PetelFullApp\PetelApp.Api"

# Clean everything
Write-Host "   🧹 Deep cleaning..." -ForegroundColor White
Remove-Item -Path ".\bin" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -Path ".\obj" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -Path ".\publish" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -Path ".\deploy-$Environment.zip" -Force -ErrorAction SilentlyContinue

# Rebuild
Write-Host "   🔨 Building..." -ForegroundColor White
dotnet clean -v quiet
dotnet build -c Release -v quiet

# Publish
Write-Host "   📦 Publishing..." -ForegroundColor White
dotnet publish -c Release -o .\publish -v quiet

if (-not (Test-Path ".\publish\PetelApp.Api.dll")) {
    Write-Host "   ❌ Backend publish failed!" -ForegroundColor Red
    exit 1
}

# Get DLL timestamp
$dllTimestamp = (Get-Item ".\publish\PetelApp.Api.dll").LastWriteTime
Write-Host "   ✅ DLL created: $dllTimestamp" -ForegroundColor Green

# Copy frontend
Write-Host "   📁 Copying frontend..." -ForegroundColor White
$wwwrootPath = ".\publish\wwwroot"
Remove-Item -Path $wwwrootPath -Recurse -Force -ErrorAction SilentlyContinue
New-Item -Path $wwwrootPath -ItemType Directory -Force | Out-Null
Copy-Item -Path "..\petelapp-frontend\public\*" -Destination $wwwrootPath -Recurse -Force

# Copy environment config
$envConfigFile = "..\petelapp-frontend\public\env-$Environment-config.js"
Copy-Item -Path $envConfigFile -Destination "$wwwrootPath\env-config.js" -Force
Write-Host "   ✅ Environment config: env-$Environment-config.js" -ForegroundColor Green

# Verify no localhost
$configContent = Get-Content "$wwwrootPath\env-config.js" -Raw
if ($configContent -match "localhost") {
    Write-Host "   ❌ ERROR: Config contains localhost!" -ForegroundColor Red
    exit 1
}

# Create deployment package
Write-Host "   📦 Creating deployment package..." -ForegroundColor White
Push-Location ".\publish"
& tar.exe -a -c -f "..\deploy-$Environment.zip" *
Pop-Location

$zipSize = (Get-Item ".\deploy-$Environment.zip").Length / 1MB
Write-Host "   ✅ Package: $([math]::Round($zipSize, 2)) MB" -ForegroundColor Green

# Step 5: Deploy
Write-Host ""
Write-Host "5️⃣ Deploying to Azure..." -ForegroundColor Yellow

# Stop again
& "C:\Program Files\Microsoft SDKs\Azure\CLI2\wbin\az.cmd" webapp stop `
    --resource-group $rg `
    --name $appName
Start-Sleep -Seconds 5

# Deploy
Write-Host "   📤 Uploading..." -ForegroundColor White
& "C:\Program Files\Microsoft SDKs\Azure\CLI2\wbin\az.cmd" webapp deployment source config-zip `
    --resource-group $rg `
    --name $appName `
    --src "deploy-$Environment.zip" `
    --timeout 600

# Start
Write-Host "   ▶️  Starting..." -ForegroundColor White
& "C:\Program Files\Microsoft SDKs\Azure\CLI2\wbin\az.cmd" webapp start `
    --resource-group $rg `
    --name $appName

Write-Host ""
Write-Host "✅ Force redeployment completed!" -ForegroundColor Green

# Wait and verify
Write-Host ""
Write-Host "6️⃣ Waiting for application startup (45 seconds)..." -ForegroundColor Yellow
Start-Sleep -Seconds 45

# Health check
Write-Host ""
Write-Host "7️⃣ Health check..." -ForegroundColor Yellow
try {
    $healthResponse = Invoke-WebRequest -Uri "https://$appName.azurewebsites.net" -Method GET -TimeoutSec 30 -UseBasicParsing
    Write-Host "   ✅ Status: $($healthResponse.StatusCode)" -ForegroundColor Green
} catch {
    Write-Host "   ⚠️  $($_.Exception.Message)" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Gray
Write-Host ""
Write-Host "🎯 CRITICAL: Clear Browser Cache" -ForegroundColor Red
Write-Host ""
Write-Host "   The new code is deployed, but browsers cache JavaScript files!" -ForegroundColor Yellow
Write-Host ""
Write-Host "   Option 1: Open in PRIVATE/INCOGNITO window" -ForegroundColor White
Write-Host "   Option 2: Hard refresh (Ctrl+Shift+R or Ctrl+F5)" -ForegroundColor White
Write-Host "   Option 3: Clear browser cache completely" -ForegroundColor White
Write-Host ""
Write-Host "   🔗 https://$appName.azurewebsites.net" -ForegroundColor Cyan
Write-Host ""
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Gray
