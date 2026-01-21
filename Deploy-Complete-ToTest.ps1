# ============================================
# Petel Blazor Server - Complete Test Deployment Script
# ============================================
# Deploys both Blazor Server and API to Azure test environment
# Last Updated: January 21, 2026
# ============================================

param(
    [switch]$SkipBuild,
    [switch]$ApiOnly,
    [switch]$BlazorOnly,
    [switch]$SkipIpRestrictions
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

# Configuration
$ResourceGroup = "petel-test-rg"
$AppServicePlan = "petel-test-plan"
$BlazorAppName = "petel-test-blazor"
$ApiAppName = "petel-test-api"
$ApiHostname = "petel-test-api-ahafcqfnh6drcdbd.israelcentral-01.azurewebsites.net"
$Runtime = "DOTNETCORE:8.0"

# Paths
$RootPath = "c:\dev\PetelFullApp"
$BlazorProjectPath = Join-Path $RootPath "PetelApp.BlazorServer"
$ApiProjectPath = Join-Path $RootPath "PetelApp.Api"

Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "Petel Blazor Server - Test Deployment" -ForegroundColor Cyan
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

function Write-Error {
    param([string]$Message)
    Write-Host "ERROR: $Message" -ForegroundColor Red
}

function Test-AzureCli {
    try {
        az account show | Out-Null
        return $true
    } catch {
        return $false
    }
}

# Verify Prerequisites
Write-Step "Verifying Prerequisites"

if (-not (Test-AzureCli)) {
    Write-Error "Azure CLI not authenticated. Run: az login"
    exit 1
}
Write-Success "Azure CLI authenticated"

if (-not (Test-Path $BlazorProjectPath)) {
    Write-Error "Blazor project not found: $BlazorProjectPath"
    exit 1
}

if (-not (Test-Path $ApiProjectPath)) {
    Write-Error "API project not found: $ApiProjectPath"
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
            Write-Error "Blazor build failed"
            exit 1
        }
        Write-Success "Blazor build completed"
    }
    
    Write-Host "Creating deployment package..." -ForegroundColor Gray
    Set-Location $RootPath
    Remove-Item "blazor-deploy.zip" -Force -ErrorAction SilentlyContinue
    Push-Location (Join-Path $BlazorProjectPath "publish")
    tar.exe -a -c -f (Join-Path $RootPath "blazor-deploy.zip") *
    Pop-Location
    
    $packageSize = (Get-Item "blazor-deploy.zip").Length / 1MB
    Write-Host "Package size: $([math]::Round($packageSize, 2)) MB" -ForegroundColor Gray
    
    Write-Host "Checking if Blazor app service exists..." -ForegroundColor Gray
    $blazorExists = az webapp show --resource-group $ResourceGroup --name $BlazorAppName 2>&1
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Creating Blazor app service..." -ForegroundColor Gray
        az webapp create --resource-group $ResourceGroup `
            --plan $AppServicePlan `
            --name $BlazorAppName `
            --runtime $Runtime | Out-Null
        Write-Success "Blazor app service created"
    } else {
        Write-Host "Blazor app service exists" -ForegroundColor Gray
    }
    
    Write-Host "Configuring Blazor app..." -ForegroundColor Gray
    az webapp config appsettings set `
        --resource-group $ResourceGroup `
        --name $BlazorAppName `
        --settings ASPNETCORE_ENVIRONMENT="Production" | Out-Null
    
    Write-Host "Deploying Blazor application..." -ForegroundColor Gray
    az webapp deploy `
        --resource-group $ResourceGroup `
        --name $BlazorAppName `
        --src-path "blazor-deploy.zip" `
        --type zip `
        --restart true `
        --timeout 300 2>&1 | Select-String -Pattern "Status:|successful" | Out-Null
    
    if ($LASTEXITCODE -eq 0) {
        Write-Success "Blazor deployment completed"
    } else {
        Write-Error "Blazor deployment failed"
        exit 1
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
            Write-Error "API build failed"
            exit 1
        }
        Write-Success "API build completed"
    }
    
    Write-Host "Creating deployment package..." -ForegroundColor Gray
    Set-Location $RootPath
    Remove-Item "api-deploy.zip" -Force -ErrorAction SilentlyContinue
    Push-Location (Join-Path $ApiProjectPath "publish")
    tar.exe -a -c -f (Join-Path $RootPath "api-deploy.zip") *
    Pop-Location
    
    $packageSize = (Get-Item "api-deploy.zip").Length / 1MB
    Write-Host "Package size: $([math]::Round($packageSize, 2)) MB" -ForegroundColor Gray
    
    Write-Host "Checking if API app service exists..." -ForegroundColor Gray
    $apiExists = az webapp show --resource-group $ResourceGroup --name $ApiAppName 2>&1
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Creating API app service..." -ForegroundColor Gray
        az webapp create --resource-group $ResourceGroup `
            --plan $AppServicePlan `
            --name $ApiAppName `
            --runtime $Runtime | Out-Null
        Write-Success "API app service created"
    } else {
        Write-Host "API app service exists" -ForegroundColor Gray
    }
    
    Write-Host "Configuring API app..." -ForegroundColor Gray
    az webapp config appsettings set `
        --resource-group $ResourceGroup `
        --name $ApiAppName `
        --settings ASPNETCORE_ENVIRONMENT="Production" | Out-Null
    
    Write-Host "Deploying API application..." -ForegroundColor Gray
    az webapp deploy `
        --resource-group $ResourceGroup `
        --name $ApiAppName `
        --src-path "api-deploy.zip" `
        --type zip `
        --restart true `
        --timeout 300 2>&1 | Select-String -Pattern "Status:|successful" | Out-Null
    
    if ($LASTEXITCODE -eq 0) {
        Write-Success "API deployment completed"
    } else {
        Write-Error "API deployment failed"
        exit 1
    }
}

# Configure IP Restrictions
if (-not $SkipIpRestrictions -and -not $BlazorOnly) {
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
    } else {
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
        } else {
            Write-Host "WARNING: Blazor app responded but content looks unexpected" -ForegroundColor Yellow
        }
    } catch {
        Write-Host "WARNING: Could not verify Blazor app: $($_.Exception.Message)" -ForegroundColor Yellow
    }
}

if (-not $BlazorOnly) {
    Write-Host "Testing API..." -ForegroundColor Gray
    try {
        $apiResponse = Invoke-WebRequest "https://$ApiHostname/api/entities/login" -UseBasicParsing -TimeoutSec 20
        Write-Success "API is responding (Status: $($apiResponse.StatusCode))"
    } catch {
        if ($_.Exception.Response.StatusCode -eq 'NotFound') {
            Write-Success "API is responding (404 expected for empty data)"
        } else {
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
    Write-Host "  https://$ApiHostname" -ForegroundColor White
    Write-Host "  Swagger: https://$ApiHostname/swagger" -ForegroundColor White
    Write-Host ""
}

Write-Host "Deployment artifacts:" -ForegroundColor Cyan
if (-not $ApiOnly) {
    Write-Host "  blazor-deploy.zip" -ForegroundColor Gray
}
if (-not $BlazorOnly) {
    Write-Host "  api-deploy.zip" -ForegroundColor Gray
}

Write-Host ""
Write-Host "Usage examples:" -ForegroundColor Cyan
Write-Host "  Deploy both:      .\Deploy-Complete-ToTest.ps1" -ForegroundColor Gray
Write-Host "  API only:         .\Deploy-Complete-ToTest.ps1 -ApiOnly" -ForegroundColor Gray
Write-Host "  Blazor only:      .\Deploy-Complete-ToTest.ps1 -BlazorOnly" -ForegroundColor Gray
Write-Host "  Skip build:       .\Deploy-Complete-ToTest.ps1 -SkipBuild" -ForegroundColor Gray
Write-Host "  Skip IP config:   .\Deploy-Complete-ToTest.ps1 -SkipIpRestrictions" -ForegroundColor Gray
Write-Host ""
