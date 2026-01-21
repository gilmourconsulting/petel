##############################################
# Deploy API to Azure Test Environment
##############################################

param(
    [switch]$SkipBuild
)

# Configuration
$ResourceGroup = "petel-test-rg"
$ApiAppName = "petel-test-api"
$Location = "israelcentral"
$RuntimeVersion = "DOTNETCORE:8.0"
$AppServicePlan = "petel-test-plan"

# Paths
$RootPath = "c:\dev\PetelFullApp"
$ApiProjectPath = "$RootPath\PetelApp.Api"
$ApiPublishPath = "$ApiProjectPath\bin\Release\net8.0\publish"
$ApiZipPath = "$RootPath\petel-api-package.zip"

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "Petel API - Test Environment Deployment" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

# Verify Prerequisites
Write-Host "Verifying Prerequisites" -ForegroundColor Yellow
Write-Host "=======================" -ForegroundColor Yellow

# Check Azure CLI
try {
    $azAccount = az account show 2>&1 | ConvertFrom-Json
    Write-Host "SUCCESS: Azure CLI authenticated as $($azAccount.user.name)" -ForegroundColor Green
} catch {
    Write-Host "ERROR: Azure CLI not authenticated. Run 'az login' first" -ForegroundColor Red
    exit 1
}

# Verify paths
if (-not (Test-Path $ApiProjectPath)) {
    Write-Host "ERROR: API project not found at $ApiProjectPath" -ForegroundColor Red
    exit 1
}
Write-Host "SUCCESS: Project paths verified" -ForegroundColor Green
Write-Host ""

# Build API
if (-not $SkipBuild) {
    Write-Host "Building API Application" -ForegroundColor Yellow
    Write-Host "========================" -ForegroundColor Yellow
    
    # Clean previous publish
    if (Test-Path $ApiPublishPath) {
        Remove-Item -Path $ApiPublishPath -Recurse -Force
    }
    
    Push-Location $ApiProjectPath
    try {
        Write-Host "Building API project..." -ForegroundColor Cyan
        dotnet publish -c Release -o "$ApiPublishPath"
        
        if ($LASTEXITCODE -ne 0) {
            Write-Host "ERROR: API build failed" -ForegroundColor Red
            Pop-Location
            exit 1
        }
        
        Write-Host "SUCCESS: API built successfully" -ForegroundColor Green
    } finally {
        Pop-Location
    }
    Write-Host ""
} else {
    Write-Host "Skipping build - using existing publish folder" -ForegroundColor Yellow
    if (-not (Test-Path $ApiPublishPath)) {
        Write-Host "ERROR: Publish folder not found at $ApiPublishPath" -ForegroundColor Red
        exit 1
    }
    Write-Host ""
}

# Create API Package
Write-Host "Creating API Deployment Package" -ForegroundColor Yellow
Write-Host "===============================" -ForegroundColor Yellow

if (Test-Path $ApiZipPath) {
    Remove-Item -Path $ApiZipPath -Force
}

Push-Location $ApiPublishPath
try {
    Write-Host "Creating zip package..." -ForegroundColor Cyan
    tar -czf $ApiZipPath *
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: Failed to create API package" -ForegroundColor Red
        Pop-Location
        exit 1
    }
    
    $zipSize = (Get-Item $ApiZipPath).Length / 1MB
    Write-Host "SUCCESS: API package created ($($zipSize.ToString('F2')) MB)" -ForegroundColor Green
} finally {
    Pop-Location
}
Write-Host ""

# Ensure App Service Exists
Write-Host "Verifying Azure App Service" -ForegroundColor Yellow
Write-Host "===========================" -ForegroundColor Yellow

$appExists = az webapp show --name $ApiAppName --resource-group $ResourceGroup 2>$null
if (-not $appExists) {
    Write-Host "App Service not found. Creating..." -ForegroundColor Cyan
    
    # Check if App Service Plan exists
    $planExists = az appservice plan show --name $AppServicePlan --resource-group $ResourceGroup 2>$null
    if (-not $planExists) {
        Write-Host "Creating App Service Plan..." -ForegroundColor Cyan
        az appservice plan create `
            --name $AppServicePlan `
            --resource-group $ResourceGroup `
            --location $Location `
            --is-linux `
            --sku B1
    }
    
    # Create App Service
    Write-Host "Creating App Service for API..." -ForegroundColor Cyan
    az webapp create `
        --name $ApiAppName `
        --resource-group $ResourceGroup `
        --plan $AppServicePlan `
        --runtime $RuntimeVersion
    
    Write-Host "SUCCESS: App Service created" -ForegroundColor Green
} else {
    Write-Host "SUCCESS: App Service exists" -ForegroundColor Green
}
Write-Host ""

# Deploy API
Write-Host "Deploying API to Azure" -ForegroundColor Yellow
Write-Host "======================" -ForegroundColor Yellow

Write-Host "Uploading package to Azure..." -ForegroundColor Cyan
az webapp deploy `
    --resource-group $ResourceGroup `
    --name $ApiAppName `
    --src-path $ApiZipPath `
    --type zip `
    --async false

if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: API deployment failed" -ForegroundColor Red
    exit 1
}

Write-Host "SUCCESS: API deployed successfully" -ForegroundColor Green
Write-Host ""

# Verify Deployment
Write-Host "Verifying API Deployment" -ForegroundColor Yellow
Write-Host "========================" -ForegroundColor Yellow

Write-Host "Waiting for app to start..." -ForegroundColor Cyan
Start-Sleep -Seconds 30

$apiUrl = "https://$ApiAppName.azurewebsites.net"
try {
    $response = Invoke-WebRequest -Uri $apiUrl -Method GET -TimeoutSec 30 -UseBasicParsing
    Write-Host "SUCCESS: API responding (Status: $($response.StatusCode))" -ForegroundColor Green
} catch {
    Write-Host "WARNING: API health check returned: $($_.Exception.Message)" -ForegroundColor Yellow
    Write-Host "This may be normal if API requires authentication" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "============================================" -ForegroundColor Green
Write-Host "API Deployment Complete!" -ForegroundColor Green
Write-Host "============================================" -ForegroundColor Green
Write-Host ""
Write-Host "API URL: $apiUrl" -ForegroundColor Cyan
Write-Host ""
Write-Host "To verify deployment manually:" -ForegroundColor Yellow
Write-Host "  az webapp browse --name $ApiAppName --resource-group $ResourceGroup" -ForegroundColor White
Write-Host ""
Write-Host "To view logs:" -ForegroundColor Yellow
Write-Host "  az webapp log tail --name $ApiAppName --resource-group $ResourceGroup" -ForegroundColor White
Write-Host ""
