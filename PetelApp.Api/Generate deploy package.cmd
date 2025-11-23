cd c:\dev\PetelFullApp\PetelApp.Api

# Clean
dotnet clean
Remove-Item -Path ".\publish" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -Path ".\deploy-test.zip" -Force -ErrorAction SilentlyContinue

# Publish
dotnet publish -c Release -o .\publish

# Copy frontend to wwwroot
$wwwrootPath = ".\publish\wwwroot"
if (Test-Path $wwwrootPath) {
    Remove-Item -Path $wwwrootPath -Recurse -Force
}
New-Item -Path $wwwrootPath -ItemType Directory -Force | Out-Null

Copy-Item -Path "..\petelapp-frontend\public\*" -Destination $wwwrootPath -Recurse -Force
Copy-Item -Path "..\petelapp-frontend\public\env-test-config.js" -Destination "$wwwrootPath\env-config.js" -Force

# Verify structure
Write-Host "`nVerifying structure:"
Write-Host "Backend DLL: $(Test-Path '.\publish\PetelApp.Api.dll')"
Write-Host "wwwroot folder: $(Test-Path $wwwrootPath)"
Write-Host "index.html: $(Test-Path "$wwwrootPath\index.html")"

# Create ZIP
Write-Host "`n📦 Creating deployment package with tar..." -ForegroundColor Cyan
Set-Location -Path ".\publish"

$deploymentPackage = "..\deploy-test.zip"

# ✅ Use tar to create ZIP with Unix paths
& tar.exe -a -c -f $deploymentPackage *

if ($LASTEXITCODE -eq 0) {
    Write-Host "  ✅ ZIP created successfully" -ForegroundColor Green
} else {
    Write-Host "  ❌ tar failed with exit code: $LASTEXITCODE" -ForegroundColor Red
    Set-Location -Path ".."
    exit 1
}
