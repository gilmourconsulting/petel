##############################################
# Deploy Blazor Server to Azure Test Environment
##############################################

param(
    [switch]$SkipBuild,
    [switch]$SkipIpRestrictions
)

# Configuration
$ResourceGroup = "petel-test-rg"
$BlazorAppName = "petel-test-blazor"
$Location = "israelcentral"
$RuntimeVersion = "DOTNETCORE:8.0"
$AppServicePlan = "petel-test-plan"

# Paths
$RootPath = "c:\dev\PetelFullApp"
$BlazorProjectPath = "$RootPath\PetelApp.BlazorServer"
$BlazorPublishPath = "$BlazorProjectPath\bin\Release\net8.0\publish"
$BlazorZipPath = "$RootPath\petel-blazor-package.zip"

# Blazor Outbound IPs (for API whitelist)
$BlazorOutboundIPs = @(
    "20.217.128.116", "20.217.128.117", "20.217.128.118", "20.217.128.119",
    "20.217.128.120", "20.217.128.121", "20.217.128.122", "20.217.128.123",
    "20.217.128.124", "20.217.128.125", "20.217.128.126", "20.217.128.127",
    "20.217.128.128", "20.217.128.129", "20.217.128.130", "20.217.128.131",
    "20.217.128.132", "20.217.128.133", "20.217.128.134", "20.217.128.135",
    "20.217.128.136", "20.217.128.137", "20.217.128.138", "20.217.128.139",
    "20.217.128.140", "20.217.128.141", "20.217.128.142", "20.217.128.143",
    "20.217.128.144", "20.217.128.25", "20.217.128.26", "20.217.128.27",
    "20.217.128.42", "20.217.128.50", "20.217.128.65", "20.217.128.67",
    "20.217.128.99", "20.217.52.0"
)

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "Petel Blazor Server - Test Environment Deployment" -ForegroundColor Cyan
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
if (-not (Test-Path $BlazorProjectPath)) {
    Write-Host "ERROR: Blazor project not found at $BlazorProjectPath" -ForegroundColor Red
    exit 1
}
Write-Host "SUCCESS: Project paths verified" -ForegroundColor Green
Write-Host ""

# Build Blazor
if (-not $SkipBuild) {
    Write-Host "Building Blazor Server Application" -ForegroundColor Yellow
    Write-Host "===================================" -ForegroundColor Yellow
    
    # Clean previous publish
    if (Test-Path $BlazorPublishPath) {
        Remove-Item -Path $BlazorPublishPath -Recurse -Force
    }
    
    Push-Location $BlazorProjectPath
    try {
        Write-Host "Building Blazor project..." -ForegroundColor Cyan
        dotnet publish -c Release -o "$BlazorPublishPath"
        
        if ($LASTEXITCODE -ne 0) {
            Write-Host "ERROR: Blazor build failed" -ForegroundColor Red
            Pop-Location
            exit 1
        }
        
        Write-Host "SUCCESS: Blazor built successfully" -ForegroundColor Green
    } finally {
        Pop-Location
    }
    Write-Host ""
} else {
    Write-Host "Skipping build - using existing publish folder" -ForegroundColor Yellow
    if (-not (Test-Path $BlazorPublishPath)) {
        Write-Host "ERROR: Publish folder not found at $BlazorPublishPath" -ForegroundColor Red
        exit 1
    }
    Write-Host ""
}

# Create Blazor Package
Write-Host "Creating Blazor Deployment Package" -ForegroundColor Yellow
Write-Host "===================================" -ForegroundColor Yellow

if (Test-Path $BlazorZipPath) {
    Remove-Item -Path $BlazorZipPath -Force
}

Push-Location $BlazorPublishPath
try {
    Write-Host "Creating zip package..." -ForegroundColor Cyan
    tar -czf $BlazorZipPath *
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: Failed to create Blazor package" -ForegroundColor Red
        Pop-Location
        exit 1
    }
    
    $zipSize = (Get-Item $BlazorZipPath).Length / 1MB
    Write-Host "SUCCESS: Blazor package created ($($zipSize.ToString('F2')) MB)" -ForegroundColor Green
} finally {
    Pop-Location
}
Write-Host ""

# Ensure App Service Exists
Write-Host "Verifying Azure App Service" -ForegroundColor Yellow
Write-Host "===========================" -ForegroundColor Yellow

$appExists = az webapp show --name $BlazorAppName --resource-group $ResourceGroup 2>$null
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
    Write-Host "Creating App Service for Blazor..." -ForegroundColor Cyan
    az webapp create `
        --name $BlazorAppName `
        --resource-group $ResourceGroup `
        --plan $AppServicePlan `
        --runtime $RuntimeVersion
    
    Write-Host "SUCCESS: App Service created" -ForegroundColor Green
} else {
    Write-Host "SUCCESS: App Service exists" -ForegroundColor Green
}
Write-Host ""

# Deploy Blazor
Write-Host "Deploying Blazor to Azure" -ForegroundColor Yellow
Write-Host "=========================" -ForegroundColor Yellow

Write-Host "Uploading package to Azure..." -ForegroundColor Cyan
az webapp deploy `
    --resource-group $ResourceGroup `
    --name $BlazorAppName `
    --src-path $BlazorZipPath `
    --type zip `
    --async false

if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Blazor deployment failed" -ForegroundColor Red
    exit 1
}

Write-Host "SUCCESS: Blazor deployed successfully" -ForegroundColor Green
Write-Host ""

# Configure IP Restrictions on API
if (-not $SkipIpRestrictions) {
    Write-Host "Configuring API IP Restrictions" -ForegroundColor Yellow
    Write-Host "================================" -ForegroundColor Yellow
    
    $ApiAppName = "petel-test-api"
    $apiExists = az webapp show --name $ApiAppName --resource-group $ResourceGroup 2>$null
    
    if ($apiExists) {
        Write-Host "Adding Blazor outbound IPs to API whitelist..." -ForegroundColor Cyan
        
        $priority = 300
        foreach ($ip in $BlazorOutboundIPs) {
            $ruleName = "Allow-Blazor-$priority"
            Write-Host "  Adding rule: $ruleName ($ip)" -ForegroundColor Gray
            
            az webapp config access-restriction add `
                --resource-group $ResourceGroup `
                --name $ApiAppName `
                --rule-name $ruleName `
                --action Allow `
                --ip-address "$ip/32" `
                --priority $priority 2>$null
            
            $priority++
        }
        
        Write-Host "SUCCESS: IP restrictions configured ($($BlazorOutboundIPs.Count) IPs)" -ForegroundColor Green
    } else {
        Write-Host "WARNING: API app not found - skipping IP restrictions" -ForegroundColor Yellow
    }
    Write-Host ""
} else {
    Write-Host "Skipping IP restrictions configuration" -ForegroundColor Yellow
    Write-Host ""
}

# Verify Deployment
Write-Host "Verifying Blazor Deployment" -ForegroundColor Yellow
Write-Host "===========================" -ForegroundColor Yellow

Write-Host "Waiting for app to start..." -ForegroundColor Cyan
Start-Sleep -Seconds 30

$blazorUrl = "https://$BlazorAppName.azurewebsites.net"
try {
    $response = Invoke-WebRequest -Uri $blazorUrl -Method GET -TimeoutSec 30 -UseBasicParsing
    Write-Host "SUCCESS: Blazor app responding (Status: $($response.StatusCode))" -ForegroundColor Green
} catch {
    Write-Host "WARNING: Blazor health check failed: $($_.Exception.Message)" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "============================================" -ForegroundColor Green
Write-Host "Blazor Deployment Complete!" -ForegroundColor Green
Write-Host "============================================" -ForegroundColor Green
Write-Host ""
Write-Host "Blazor URL: $blazorUrl" -ForegroundColor Cyan
Write-Host ""
Write-Host "To verify deployment manually:" -ForegroundColor Yellow
Write-Host "  az webapp browse --name $BlazorAppName --resource-group $ResourceGroup" -ForegroundColor White
Write-Host ""
Write-Host "To view logs:" -ForegroundColor Yellow
Write-Host "  az webapp log tail --name $BlazorAppName --resource-group $ResourceGroup" -ForegroundColor White
Write-Host ""
