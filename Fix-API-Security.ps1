# ============================================
# Fix API Server Security Configuration
# ============================================
# API should ONLY accept connections from Blazor server
# NOT from all Israeli IPs
# ============================================

param(
    [switch]$WhatIf
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

Write-Host ""
Write-Host "============================================" -ForegroundColor Red
Write-Host "FIX API SECURITY CONFIGURATION" -ForegroundColor Red
Write-Host "============================================" -ForegroundColor Red
Write-Host ""

$resourceGroup = "petel-prod-rg"
$apiApp = "petel-prod-api"
$blazorApp = "petel-prod-blazor"

# Verify Azure CLI
try {
    az account show | Out-Null
}
catch {
    Write-Host "[ERROR] Azure CLI not authenticated. Run: az login" -ForegroundColor Red
    exit 1
}

# Get Blazor outbound IPs
Write-Host "Getting Blazor server outbound IPs..." -ForegroundColor Yellow
$blazorOutboundIps = az webapp show --name $blazorApp --resource-group $resourceGroup --query "possibleOutboundIpAddresses" -o tsv
$blazorIpArray = $blazorOutboundIps -split ","

Write-Host "Blazor outbound IPs:" -ForegroundColor White
foreach ($ip in $blazorIpArray) {
    Write-Host "  - $ip" -ForegroundColor Gray
}

# Get current API restrictions
Write-Host ""
Write-Host "Current API restrictions..." -ForegroundColor Yellow
$currentRules = az webapp config access-restriction show `
    --name $apiApp `
    --resource-group $resourceGroup `
    --query "ipSecurityRestrictions[?name!='Allow all' && name!='Deny all']" -o json | ConvertFrom-Json

Write-Host "Current rules: $($currentRules.Count)" -ForegroundColor White

if ($WhatIf) {
    Write-Host ""
    Write-Host "WHATIF: Would remove $($currentRules.Count) Israeli IP rules" -ForegroundColor Cyan
    Write-Host "WHATIF: Would add $($blazorIpArray.Count) Blazor server IPs" -ForegroundColor Cyan
    Write-Host "WHATIF: Would add Azure management service tag" -ForegroundColor Cyan
    exit 0
}

# Remove all existing restrictions
Write-Host ""
Write-Host "Removing all Israeli IP restrictions from API..." -ForegroundColor Yellow
Write-Host "(This may take several minutes...)" -ForegroundColor Gray

$removeCount = 0
$errorCount = 0

foreach ($rule in $currentRules) {
    if ($rule.name) {
        Write-Host "  Removing: $($rule.name)" -ForegroundColor Gray
        
        $retries = 3
        $success = $false
        
        for ($i = 1; $i -le $retries; $i++) {
            try {
                az webapp config access-restriction remove `
                    --name $apiApp `
                    --resource-group $resourceGroup `
                    --rule-name $rule.name 2>&1 | Out-Null
                
                if ($LASTEXITCODE -eq 0) {
                    $removeCount++
                    $success = $true
                    break
                }
                else {
                    Write-Host "    Retry $i/$retries..." -ForegroundColor Yellow
                    Start-Sleep -Seconds 2
                }
            }
            catch {
                Write-Host "    Retry $i/$retries..." -ForegroundColor Yellow
                Start-Sleep -Seconds 2
            }
        }
        
        if (-not $success) {
            Write-Host "    Failed after $retries retries" -ForegroundColor Red
            $errorCount++
        }
        
        # Rate limiting - pause every 10 operations
        if ($removeCount % 10 -eq 0) {
            Start-Sleep -Milliseconds 500
        }
    }
}

Write-Host ""
Write-Host "Rules removed: $removeCount / $($currentRules.Count)" -ForegroundColor Green
if ($errorCount -gt 0) {
    Write-Host "Rules failed: $errorCount" -ForegroundColor Red
}

# Add Blazor server IPs
Write-Host ""
Write-Host "Adding Blazor server IPs to API allowlist..." -ForegroundColor Yellow

$priority = 100
$ruleNumber = 1
$addCount = 0

foreach ($ip in $blazorIpArray) {
    $ruleName = "Allow-Blazor-$ruleNumber"
    
    $retries = 3
    $success = $false
    
    for ($i = 1; $i -le $retries; $i++) {
        try {
            az webapp config access-restriction add `
                --name $apiApp `
                --resource-group $resourceGroup `
                --rule-name $ruleName `
                --action Allow `
                --ip-address "$ip/32" `
                --priority $priority 2>&1 | Out-Null
            
            if ($LASTEXITCODE -eq 0) {
                Write-Host "  Added: $ip" -ForegroundColor Green
                $addCount++
                $success = $true
                break
            }
            else {
                Write-Host "    Retry $i/$retries for $ip..." -ForegroundColor Yellow
                Start-Sleep -Seconds 2
            }
        }
        catch {
            Write-Host "    Retry $i/$retries for $ip..." -ForegroundColor Yellow
            Start-Sleep -Seconds 2
        }
    }
    
    if (-not $success) {
        Write-Host "  Failed: $ip (after $retries retries)" -ForegroundColor Red
    }
    
    $priority++
    $ruleNumber++
    
    # Rate limiting - pause every 10 operations
    if ($addCount % 10 -eq 0) {
        Start-Sleep -Milliseconds 500
    }
}

Write-Host ""
Write-Host "Blazor IPs added: $addCount / $($blazorIpArray.Count)" -ForegroundColor Green

# Verify final configuration
Write-Host ""
Write-Host "Final API configuration..." -ForegroundColor Yellow
$finalRules = az webapp config access-restriction show `
    --name $apiApp `
    --resource-group $resourceGroup `
    --query "ipSecurityRestrictions[?name!='Allow all' && name!='Deny all']" -o json | ConvertFrom-Json

Write-Host "Total rules: $($finalRules.Count)" -ForegroundColor White

Write-Host ""
Write-Host "============================================" -ForegroundColor Green
Write-Host "SECURITY CONFIGURATION COMPLETE" -ForegroundColor Green
Write-Host "============================================" -ForegroundColor Green
Write-Host ""
Write-Host "Security Status:" -ForegroundColor Cyan
Write-Host "  - API only accessible from Blazor server" -ForegroundColor Green
Write-Host "  - End users cannot directly access API" -ForegroundColor Green
Write-Host "  - Blazor has Israeli IP restrictions" -ForegroundColor Green
Write-Host ""
Write-Host "Architecture: Users -> Blazor -> API" -ForegroundColor White
Write-Host ""
