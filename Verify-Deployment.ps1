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

$appName = $appNames[$Environment]
$rg = $resourceGroups[$Environment]
$appUrl = "https://$appName.azurewebsites.net"

Write-Host "🔍 Verifying Deployment for $Environment environment" -ForegroundColor Cyan
Write-Host "App Service: $appName" -ForegroundColor White
Write-Host "URL: $appUrl" -ForegroundColor White
Write-Host ""

# 1. Check if the DLL was updated
Write-Host "1️⃣ Checking deployed DLL timestamp..." -ForegroundColor Yellow
$kuduUrl = "https://$appName.scm.azurewebsites.net/api/vfs/site/wwwroot/PetelApp.Api.dll"

try {
    $response = Invoke-WebRequest -Uri $kuduUrl -Method HEAD -UseBasicParsing
    $lastModified = $response.Headers['Last-Modified']
    Write-Host "   Last Modified: $lastModified" -ForegroundColor White
    
    $deployedTime = [DateTime]::Parse($lastModified)
    $minutesAgo = [math]::Round(((Get-Date) - $deployedTime).TotalMinutes, 0)
    
    if ($minutesAgo -lt 60) {
        Write-Host "   ✅ DLL was updated $minutesAgo minutes ago (recent)" -ForegroundColor Green
    } else {
        Write-Host "   ⚠️  DLL was updated $minutesAgo minutes ago (may be stale)" -ForegroundColor Yellow
    }
} catch {
    Write-Host "   ❌ Could not check DLL timestamp: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""

# 2. Check frontend files
Write-Host "2️⃣ Checking frontend files..." -ForegroundColor Yellow
$frontendFiles = @(
    'env-config.js',
    'index.html',
    'student.html'
)

foreach ($file in $frontendFiles) {
    $fileUrl = "https://$appName.scm.azurewebsites.net/api/vfs/site/wwwroot/$file"
    try {
        $response = Invoke-WebRequest -Uri $fileUrl -Method HEAD -UseBasicParsing
        $lastModified = $response.Headers['Last-Modified']
        $size = $response.Headers['Content-Length']
        $sizeKB = [math]::Round($size / 1KB, 2)
        Write-Host "   $file : $lastModified - $sizeKB KB" -ForegroundColor White
    } catch {
        Write-Host "   ERROR $file : Not found or inaccessible" -ForegroundColor Red
    }
}

Write-Host ""

# 3. Check application status
Write-Host "3️⃣ Checking application status..." -ForegroundColor Yellow
& "C:\Program Files\Microsoft SDKs\Azure\CLI2\wbin\az.cmd" webapp show `
    --resource-group $rg `
    --name $appName `
    --query "{state: state, lastModified: lastModifiedTimeUtc, availabilityState: availabilityState}" `
    --output table

Write-Host ""

# 4. Check recent deployment history
Write-Host "4️⃣ Checking deployment history..." -ForegroundColor Yellow
& "C:\Program Files\Microsoft SDKs\Azure\CLI2\wbin\az.cmd" webapp deployment list `
    --resource-group $rg `
    --name $appName `
    --output table 2>$null | Select-Object -First 5

Write-Host ""

# 5. Test API endpoint
Write-Host "5️⃣ Testing API health..." -ForegroundColor Yellow
try {
    $healthResponse = Invoke-WebRequest -Uri "$appUrl/api/auth/test" -Method GET -TimeoutSec 10 -UseBasicParsing
    Write-Host "   ✅ API is responding (Status: $($healthResponse.StatusCode))" -ForegroundColor Green
} catch {
    Write-Host "   ⚠️  API response: $($_.Exception.Message)" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Gray
Write-Host ""
Write-Host "📋 Manual Verification Steps:" -ForegroundColor Cyan
Write-Host ""
Write-Host "1. Open browser in INCOGNITO/PRIVATE mode:" -ForegroundColor White
Write-Host "   $appUrl" -ForegroundColor Yellow
Write-Host ""
Write-Host "2. Open Developer Tools (F12) → Console" -ForegroundColor White
Write-Host ""
Write-Host "3. Check environment config:" -ForegroundColor White
Write-Host "   window.ENV_CONFIG" -ForegroundColor Yellow
Write-Host ""
Write-Host "4. Verify API calls are going to correct URL (not localhost)" -ForegroundColor White
Write-Host ""
Write-Host "5. Login and test pricing calculation on a student" -ForegroundColor White
Write-Host ""
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Gray
