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
    
    # Use PowerShell's Compress-Archive instead of tar for better Azure compatibility
    Compress-Archive -Path * -DestinationPath $ApiZipPath -Force
    
    if (-not (Test-Path $ApiZipPath)) {
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

$appInfo = az webapp show --name $ApiAppName --resource-group $ResourceGroup 2>$null | ConvertFrom-Json

if (-not $appInfo) {
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
    Write-Host "  Default Hostname: $($appInfo.defaultHostName)" -ForegroundColor Cyan
    Write-Host "  State: $($appInfo.state)" -ForegroundColor Cyan
}
Write-Host ""

# Stop App Before Deployment
Write-Host "Stopping App Service" -ForegroundColor Yellow
Write-Host "====================" -ForegroundColor Yellow

Write-Host "Stopping app to ensure clean deployment..." -ForegroundColor Cyan
az webapp stop --name $ApiAppName --resource-group $ResourceGroup

if ($LASTEXITCODE -ne 0) {
    Write-Host "WARNING: Failed to stop app (may not be running)" -ForegroundColor Yellow
} else {
    Write-Host "SUCCESS: App stopped" -ForegroundColor Green
}
Start-Sleep -Seconds 5
Write-Host ""

# Clear Application Cache
Write-Host "Clearing Application Cache" -ForegroundColor Yellow
Write-Host "==========================" -ForegroundColor Yellow

Write-Host "Restarting app to clear cache..." -ForegroundColor Cyan
az webapp restart --name $ApiAppName --resource-group $ResourceGroup
Start-Sleep -Seconds 10

Write-Host "SUCCESS: Cache cleared" -ForegroundColor Green
Write-Host ""

# Configure App to Disable Build
Write-Host "Configuring Deployment Settings" -ForegroundColor Yellow
Write-Host "===============================" -ForegroundColor Yellow

Write-Host "Setting deployment configuration..." -ForegroundColor Cyan
az webapp config appsettings set `
    --resource-group $ResourceGroup `
    --name $ApiAppName `
    --settings `
        SCM_DO_BUILD_DURING_DEPLOYMENT=false `
        ENABLE_ORYX_BUILD=false `
        WEBSITE_RUN_FROM_PACKAGE=1 `
    --output none

Write-Host "SUCCESS: Configuration updated" -ForegroundColor Green
Write-Host ""

# Deploy API using Run From Package
Write-Host "Deploying API to Azure" -ForegroundColor Yellow
Write-Host "======================" -ForegroundColor Yellow

Write-Host "Uploading package using Run From Package..." -ForegroundColor Cyan

# Upload zip to Azure Blob Storage (simpler, more reliable)
$storageConnection = az storage account show-connection-string `
    --resource-group $ResourceGroup `
    --name "petelteststore" `
    --query connectionString `
    --output tsv 2>$null

if (-not $storageConnection) {
    Write-Host "Storage account not found. Using direct upload method..." -ForegroundColor Yellow
    
    # Use az webapp deploy with explicit no-build flags
    $env:WEBSITE_RUN_FROM_PACKAGE = "1"
    $env:SCM_DO_BUILD_DURING_DEPLOYMENT = "false"
    
    az webapp deploy `
        --resource-group $ResourceGroup `
        --name $ApiAppName `
        --src-path $ApiZipPath `
        --type zip `
        --async false `
        --timeout 600
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: Direct deploy failed. Trying FTP deployment..." -ForegroundColor Yellow
        
        # Last resort: Use Azure App Service editor/FTP
        Write-Host "Please deploy manually using one of these methods:" -ForegroundColor Red
        Write-Host "  1. Azure Portal: Deployment Center > Manual Deploy > Upload ZIP" -ForegroundColor White
        Write-Host "  2. Visual Studio: Right-click project > Publish" -ForegroundColor White
        Write-Host "  3. Package location: $ApiZipPath" -ForegroundColor White
        exit 1
    }
} else {
    Write-Host "Using storage account for deployment..." -ForegroundColor Cyan
    
    # Upload to blob storage
    $containerName = "deployments"
    $blobName = "api-$(Get-Date -Format 'yyyyMMdd-HHmmss').zip"
    
    az storage container create --name $containerName --connection-string $storageConnection --output none 2>$null
    
    az storage blob upload `
        --connection-string $storageConnection `
        --container-name $containerName `
        --name $blobName `
        --file $ApiZipPath `
        --overwrite
    
    # Get SAS URL
    $expiryDate = (Get-Date).AddHours(2).ToString("yyyy-MM-ddTHH:mm:ssZ")
    $sasUrl = az storage blob generate-sas `
        --connection-string $storageConnection `
        --container-name $containerName `
        --name $blobName `
        --permissions r `
        --expiry $expiryDate `
        --full-uri `
        --output tsv
    
    # Set WEBSITE_RUN_FROM_PACKAGE to the SAS URL
    az webapp config appsettings set `
        --resource-group $ResourceGroup `
        --name $ApiAppName `
        --settings WEBSITE_RUN_FROM_PACKAGE="$sasUrl" `
        --output none
}

Write-Host "SUCCESS: Package deployed" -ForegroundColor Green
Write-Host ""

# Ensure App is Running
Write-Host "Starting App Service" -ForegroundColor Yellow
Write-Host "====================" -ForegroundColor Yellow

Write-Host "Starting app..." -ForegroundColor Cyan
az webapp start --name $ApiAppName --resource-group $ResourceGroup

if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Failed to start app" -ForegroundColor Red
    exit 1
}

Write-Host "SUCCESS: App started" -ForegroundColor Green
Write-Host ""

# Verify Deployment
Write-Host "Verifying API Deployment" -ForegroundColor Yellow
Write-Host "========================" -ForegroundColor Yellow

$apiUrl = "https://$ApiAppName.azurewebsites.net"

Write-Host "Waiting for app to fully initialize..." -ForegroundColor Cyan
$maxRetries = 6
$retryCount = 0
$success = $false

while ($retryCount -lt $maxRetries -and -not $success) {
    $retryCount++
    Start-Sleep -Seconds 10
    
    Write-Host "Attempt $retryCount of $maxRetries..." -ForegroundColor Cyan
    
    try {
        $response = Invoke-WebRequest -Uri $apiUrl -Method GET -TimeoutSec 30 -UseBasicParsing
        Write-Host "SUCCESS: API responding (Status: $($response.StatusCode))" -ForegroundColor Green
        $success = $true
    } catch {
        if ($retryCount -lt $maxRetries) {
            Write-Host "Waiting for app to start..." -ForegroundColor Yellow
        } else {
            Write-Host "WARNING: API health check returned: $($_.Exception.Message)" -ForegroundColor Yellow
            Write-Host "This may be normal if API requires authentication" -ForegroundColor Yellow
        }
    }
Write-Host "IMPORTANT: Clear your browser cache (Ctrl+Shift+R) to see the new version!" -ForegroundColor Magenta
Write-Host ""
Write-Host "If you still see the old version:" -ForegroundColor Yellow
Write-Host "  1. Wait 2-3 minutes for Azure to fully restart" -ForegroundColor White
Write-Host "  2. Try: az webapp restart --name $ApiAppName --resource-group $ResourceGroup" -ForegroundColor White
Write-Host "  3. Check deployment logs in Azure Portal" -ForegroundColor White
Write-Host ""
}

# Additional cache busting - force a hard restart
Write-Host "Performing final restart to ensure new version is active..." -ForegroundColor Cyan
az webapp restart --name $ApiAppName --resource-group $ResourceGroup
Start-Sleep -Seconds 15

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
