# ============================================
# Petel Application - Complete Azure Deployment Script
# ============================================
# Flexible deployment for API and/or Blazor Server
# Supports: Test, Staging, Production
# ============================================

param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('test', 'staging', 'production')]
    [string]$Environment,
    
    [switch]$ApiOnly,
    [switch]$BlazorOnly,
    [switch]$SkipBuild,
    [switch]$SkipIpRestrictions
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

# Configuration based on environment
$envConfig = @{
    'test'       = @{
        ResourceGroup  = 'petel-test-rg'
        AppServicePlan = 'petel-test-plan'
        BlazorAppName  = 'petel-test-blazor'
        ApiAppName     = 'petel-test-api'
        ApiUrl         = 'https://petel-test-api.azurewebsites.net'
        Location       = 'israelcentral'
        BlazorRuntime  = 'DOTNETCORE:8.0'
        ApiRuntime     = 'DOTNETCORE:9.0'
    }
    'staging'    = @{
        ResourceGroup  = 'petel-staging-rg'
        AppServicePlan = 'petel-staging-plan'
        BlazorAppName  = 'petel-staging-blazor'
        ApiAppName     = 'petel-staging-api'
        ApiUrl         = 'https://petel-staging-api.azurewebsites.net'
        Location       = 'israelcentral'
        BlazorRuntime  = 'DOTNETCORE:8.0'
        ApiRuntime     = 'DOTNETCORE:9.0'
    }
    'production' = @{
        ResourceGroup  = 'petel-prod-rg'
        AppServicePlan = 'petel-prod-plan'
        BlazorAppName  = 'petel-prod-blazor'
        ApiAppName     = 'petel-prod-api'
        ApiUrl         = 'https://petel-prod-api.azurewebsites.net'
        Location       = 'israelcentral'
        BlazorRuntime  = 'DOTNETCORE:8.0'
        ApiRuntime     = 'DOTNETCORE:9.0'
    }
}

$config = $envConfig[$Environment]
$ResourceGroup = $config.ResourceGroup
$AppServicePlan = $config.AppServicePlan
$BlazorAppName = $config.BlazorAppName
$ApiAppName = $config.ApiAppName
$ApiUrl = $config.ApiUrl
$Location = $config.Location
$BlazorRuntime = $config.BlazorRuntime
$ApiRuntime = $config.ApiRuntime

# Paths
$RootPath = "c:\dev\PetelFullApp"
$BlazorProjectPath = Join-Path $RootPath "PetelApp.BlazorServer"
$ApiProjectPath = Join-Path $RootPath "PetelApp.Api"

Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "Petel Application - $Environment Deployment" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

# Helper Functions
function Write-Step {
    param([string]$Message)
    Write-Host ""
    Write-Host $Message -ForegroundColor Yellow
    Write-Host ("=" * $Message.Length) -ForegroundColor Yellow
}

function Write-Success {
    param([string]$Message)
    Write-Host "SUCCESS: $Message" -ForegroundColor Green
}

function Write-ErrorMsg {
    param([string]$Message)
    Write-Host "ERROR: $Message" -ForegroundColor Red
}

function Test-AzureCli {
    try {
        az account show | Out-Null
        return $true
    }
    catch {
        return $false
    }
}

# Display deployment plan
Write-Host "Resource Group:  $ResourceGroup" -ForegroundColor Cyan
if (-not $BlazorOnly) {
    Write-Host "API App:         $ApiAppName" -ForegroundColor Cyan
}
if (-not $ApiOnly) {
    Write-Host "Blazor App:      $BlazorAppName" -ForegroundColor Cyan
}
Write-Host ""

# Verify Prerequisites
Write-Step "Verifying Prerequisites"

if (-not (Test-AzureCli)) {
    Write-ErrorMsg "Azure CLI not authenticated. Run: az login"
    exit 1
}
Write-Success "Azure CLI authenticated"

if (-not (Test-Path $BlazorProjectPath)) {
    Write-ErrorMsg "Blazor project not found: $BlazorProjectPath"
    exit 1
}

if (-not (Test-Path $ApiProjectPath)) {
    Write-ErrorMsg "API project not found: $ApiProjectPath"
    exit 1
}
Write-Success "Project paths verified"

# Deploy Blazor Server
if (-not $ApiOnly) {
    Write-Step "Deploying Blazor Server Application"
    
    if (-not $SkipBuild) {
        Write-Host "Building Blazor project..." -ForegroundColor Gray
        Set-Location $BlazorProjectPath
        Remove-Item "publish" -Recurse -Force -ErrorAction SilentlyContinue
        
        dotnet publish -c Release -o "publish" --nologo -v quiet
        
        if ($LASTEXITCODE -ne 0) {
            Write-ErrorMsg "Blazor build failed"
            exit 1
        }
        Write-Success "Blazor build completed"
    }
    
    Write-Host "Creating deployment package..." -ForegroundColor Gray
    Set-Location $RootPath
    Remove-Item "blazor-deploy-$Environment.zip" -Force -ErrorAction SilentlyContinue
    Push-Location (Join-Path $BlazorProjectPath "publish")
    tar.exe -a -c -f (Join-Path $RootPath "blazor-deploy-$Environment.zip") *
    Pop-Location
    
    $packageSize = (Get-Item "blazor-deploy-$Environment.zip").Length / 1MB
    Write-Host "Package size: $([math]::Round($packageSize, 2)) MB" -ForegroundColor Gray
    
    Write-Host "Checking if Blazor app service exists..." -ForegroundColor Gray
    $blazorExists = az webapp show --resource-group $ResourceGroup --name $BlazorAppName 2>&1
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Creating Blazor app service..." -ForegroundColor Gray
        
        # Check if App Service Plan exists
        $planExists = az appservice plan show --name $AppServicePlan --resource-group $ResourceGroup 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-Host "Creating App Service Plan..." -ForegroundColor Gray
            az appservice plan create `
                --name $AppServicePlan `
                --resource-group $ResourceGroup `
                --location $Location `
                --sku B1 | Out-Null
        }
        
        az webapp create --resource-group $ResourceGroup `
            --plan $AppServicePlan `
            --name $BlazorAppName `
            --runtime $BlazorRuntime | Out-Null
        Write-Success "Blazor app service created"
    }
    else {
        Write-Host "Blazor app service exists" -ForegroundColor Gray
    }
    
    Write-Host "Configuring Blazor app..." -ForegroundColor Gray
    $aspnetEnv = if ($Environment -eq 'test') { 'Staging' } elseif ($Environment -eq 'staging') { 'Staging' } else { 'Production' }
    az webapp config appsettings set `
        --resource-group $ResourceGroup `
        --name $BlazorAppName `
        --settings ASPNETCORE_ENVIRONMENT="$aspnetEnv" | Out-Null
    
    Write-Host "Deploying Blazor application..." -ForegroundColor Gray
    $deployResult = az webapp deploy `
        --resource-group $ResourceGroup `
        --name $BlazorAppName `
        --src-path "blazor-deploy-$Environment.zip" `
        --type zip `
        --restart true `
        --timeout 300 `
        --only-show-errors 2>&1
    
    if ($deployResult -like "*Deployment successful*" -or $deployResult -like "*status*") {
        Write-Success "Blazor deployment completed"
    }
    else {
        Write-Host "Deployment output:" -ForegroundColor Yellow
        Write-Host $deployResult
        if ($LASTEXITCODE -ne 0) {
            Write-ErrorMsg "Blazor deployment failed"
            exit 1
        }
        else {
            Write-Success "Blazor deployment completed (check output above for details)"
        }
    }
}

# Deploy API
if (-not $BlazorOnly) {
    Write-Step "Deploying API Application"
    
    if (-not $SkipBuild) {
        Write-Host "Building API project..." -ForegroundColor Gray
        Set-Location $ApiProjectPath
        Remove-Item "publish" -Recurse -Force -ErrorAction SilentlyContinue
        
        dotnet publish -c Release -o "publish" --nologo -v quiet
        
        if ($LASTEXITCODE -ne 0) {
            Write-ErrorMsg "API build failed"
            exit 1
        }
        Write-Success "API build completed"
    }
    
    Write-Host "Creating deployment package..." -ForegroundColor Gray
    Set-Location $RootPath
    Remove-Item "api-deploy-$Environment.zip" -Force -ErrorAction SilentlyContinue
    Push-Location (Join-Path $ApiProjectPath "publish")
    tar.exe -a -c -f (Join-Path $RootPath "api-deploy-$Environment.zip") *
    Pop-Location
    
    $packageSize = (Get-Item "api-deploy-$Environment.zip").Length / 1MB
    Write-Host "Package size: $([math]::Round($packageSize, 2)) MB" -ForegroundColor Gray
    
    Write-Host "Checking if API app service exists..." -ForegroundColor Gray
    $apiExists = az webapp show --resource-group $ResourceGroup --name $ApiAppName 2>&1
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Creating API app service..." -ForegroundColor Gray
        
        # Check if App Service Plan exists
        $planExists = az appservice plan show --name $AppServicePlan --resource-group $ResourceGroup 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-Host "Creating App Service Plan..." -ForegroundColor Gray
            az appservice plan create `
                --name $AppServicePlan `
                --resource-group $ResourceGroup `
                --location $Location `
                --sku B1 | Out-Null
        }
        
        az webapp create --resource-group $ResourceGroup `
            --plan $AppServicePlan `
            --name $ApiAppName `
            --runtime $ApiRuntime | Out-Null
        Write-Success "API app service created"
    }
    else {
        Write-Host "API app service exists" -ForegroundColor Gray
    }
    
    Write-Host "Configuring API app..." -ForegroundColor Gray
    $aspnetEnv = if ($Environment -eq 'test') { 'Staging' } elseif ($Environment -eq 'staging') { 'Staging' } else { 'Production' }
    az webapp config appsettings set `
        --resource-group $ResourceGroup `
        --name $ApiAppName `
        --settings ASPNETCORE_ENVIRONMENT="$aspnetEnv" | Out-Null
    
    Write-Host "Deploying API application..." -ForegroundColor Gray
    $deployResult = az webapp deploy `
        --resource-group $ResourceGroup `
        --name $ApiAppName `
        --src-path "api-deploy-$Environment.zip" `
        --type zip `
        --restart true `
        --timeout 300 `
        --only-show-errors 2>&1

    
    
    $deployOutput = $deployResult | Out-String

    if ($LASTEXITCODE -eq 0) {
        Write-Success "API deployment completed"
    }
    elseif ($LASTEXITCODE -ne 0) {
        Write-Host "Deployment output:" -ForegroundColor Yellow
        Write-Host $deployOutput
        Write-ErrorMsg "API deployment failed with exit code $LASTEXITCODE"
        exit 1
    }
}

# Configure IP Restrictions
if (-not $SkipIpRestrictions -and -not $c -and -not $ApiOnly) {
    Write-Step "Configuring IP Restrictions"
    
    Write-Host "Getting Blazor outbound IPs..." -ForegroundColor Gray
    $blazorIps = az webapp show `
        --resource-group $ResourceGroup `
        --name $BlazorAppName `
        --query possibleOutboundIpAddresses -o tsv
    
    Write-Host "Found $($blazorIps.Split(',').Count) outbound IPs" -ForegroundColor Gray
    
    Write-Host "Checking current API IP restrictions..." -ForegroundColor Gray
    $currentRules = az webapp config access-restriction show `
        --resource-group $ResourceGroup `
        --name $ApiAppName `
        --query 'ipSecurityRestrictions[].name' -o tsv
    
    $ipArray = $blazorIps -split ','
    $priority = 300
    $addedCount = 0
    
    foreach ($ip in $ipArray) {
        $ruleName = "Allow-Blazor-$priority"
        
        if ($currentRules -notcontains $ruleName) {
            Write-Host "  Adding $ip..." -ForegroundColor Gray
            az webapp config access-restriction add `
                --resource-group $ResourceGroup `
                --name $ApiAppName `
                --rule-name $ruleName `
                --action Allow `
                --ip-address "$ip/32" `
                --priority $priority 2>&1 | Out-Null
            $addedCount++
        }
        $priority++
    }
    
    if ($addedCount -gt 0) {
        Write-Success "Added $addedCount IP restriction rule(s)"
    }
    else {
        Write-Host "All IP restrictions already configured" -ForegroundColor Gray
    }
}

# Verification
Write-Step "Verifying Deployment"

Write-Host "Waiting for applications to start (30 seconds)..." -ForegroundColor Gray
Start-Sleep -Seconds 30

if (-not $ApiOnly) {
    Write-Host "Testing Blazor app..." -ForegroundColor Gray
    try {
        $blazorResponse = Invoke-WebRequest "https://$BlazorAppName.azurewebsites.net" -UseBasicParsing -TimeoutSec 20
        if ($blazorResponse.Content -like "*Blazor*" -or $blazorResponse.Content -like "*_framework*") {
            Write-Success "Blazor app is responding (Status: $($blazorResponse.StatusCode))"
        }
        else {
            Write-Host "WARNING: Blazor app responded but content looks unexpected" -ForegroundColor Yellow
        }
    }
    catch {
        Write-Host "WARNING: Could not verify Blazor app: $($_.Exception.Message)" -ForegroundColor Yellow
    }
}

if (-not $BlazorOnly) {
    Write-Host "Testing API..." -ForegroundColor Gray
    try {
        $apiResponse = Invoke-WebRequest "$ApiUrl" -UseBasicParsing -TimeoutSec 20
        Write-Success "API is responding (Status: $($apiResponse.StatusCode))"
    }
    catch {
        if ($_.Exception.Response.StatusCode -eq 'NotFound') {
            Write-Success "API is responding (404 expected for root)"
        }
        else {
            Write-Host "WARNING: Could not verify API: $($_.Exception.Message)" -ForegroundColor Yellow
        }
    }
}

# Summary
Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "DEPLOYMENT COMPLETE!" -ForegroundColor Green
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

if (-not $ApiOnly) {
    Write-Host "Blazor App:" -ForegroundColor Cyan
    Write-Host "  https://$BlazorAppName.azurewebsites.net" -ForegroundColor White
    Write-Host ""
}

if (-not $BlazorOnly) {
    Write-Host "API:" -ForegroundColor Cyan
    Write-Host "  $ApiUrl" -ForegroundColor White
    Write-Host "  Swagger: $ApiUrl/swagger" -ForegroundColor White
    Write-Host ""
}

Write-Host "Deployment artifacts:" -ForegroundColor Cyan
if (-not $ApiOnly) {
    Write-Host "  blazor-deploy-$Environment.zip" -ForegroundColor Gray
}
if (-not $BlazorOnly) {
    Write-Host "  api-deploy-$Environment.zip" -ForegroundColor Gray
}

Write-Host ""
Write-Host "Usage examples:" -ForegroundColor Cyan
Write-Host "  Deploy both:      .\Deploy-ToAzure.ps1 -Environment $Environment" -ForegroundColor Gray
Write-Host "  API only:         .\Deploy-ToAzure.ps1 -Environment $Environment -ApiOnly" -ForegroundColor Gray
Write-Host "  Blazor only:      .\Deploy-ToAzure.ps1 -Environment $Environment -BlazorOnly" -ForegroundColor Gray
Write-Host "  Skip build:       .\Deploy-ToAzure.ps1 -Environment $Environment -SkipBuild" -ForegroundColor Gray
Write-Host "  Skip IP config:   .\Deploy-ToAzure.ps1 -Environment $Environment -SkipIpRestrictions" -ForegroundColor Gray
Write-Host ""
