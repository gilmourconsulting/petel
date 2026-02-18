# ============================================
# Remove Israeli IP Rules from API Server
# ============================================
# This script removes all Israeli public IP restrictions
# from the API server, leaving only Blazor server IPs.
#
# SECURITY: API should ONLY accept Blazor connections,
# NOT direct connections from Israeli public IPs.
# ============================================

param(
    [switch]$WhatIf
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "REMOVE ISRAELI IPS FROM API SERVER" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

$resourceGroup = "petel-prod-rg"
$apiApp = "petel-prod-api"

# Verify Azure CLI authentication
try {
    az account show | Out-Null
}
catch {
    Write-Host "[ERROR] Azure CLI not authenticated. Run: az login" -ForegroundColor Red
    exit 1
}

# Get current API restrictions
Write-Host "Analyzing current API configuration..." -ForegroundColor Yellow
$allRulesRaw = az webapp config access-restriction show --name $apiApp --resource-group $resourceGroup --query "ipSecurityRestrictions" -o json | ConvertFrom-Json
$allRules = $allRulesRaw | Where-Object { $_.name -ne 'Allow all' -and $_.name -ne 'Deny all' }

$israeliRules = $allRules | Where-Object { $_.name -like "Allow-Israeli-*" }
$blazorRules = $allRules | Where-Object { $_.name -like "Allow-Blazor-*" }
$otherRules = $allRules | Where-Object { $_.name -notlike "Allow-Israeli-*" -and $_.name -notlike "Allow-Blazor-*" }

Write-Host ""
Write-Host "Current Configuration:" -ForegroundColor White
Write-Host "  Total rules: $($allRules.Count)" -ForegroundColor Gray
Write-Host "  Israeli IP rules: $($israeliRules.Count)" -ForegroundColor Yellow
Write-Host "  Blazor IP rules: $($blazorRules.Count)" -ForegroundColor Green
Write-Host "  Other rules: $($otherRules.Count)" -ForegroundColor Gray

if ($israeliRules.Count -eq 0) {
    Write-Host ""
    Write-Host "[OK] No Israeli IP rules found - API is already secured!" -ForegroundColor Green
    exit 0
}

Write-Host ""
Write-Host "Rules to be removed:" -ForegroundColor Yellow
$israeliRules | Select-Object -First 10 name, ipAddress, priority | Format-Table -AutoSize
if ($israeliRules.Count -gt 10) {
    Write-Host "  ... and $($israeliRules.Count - 10) more rules" -ForegroundColor Gray
}

if ($WhatIf) {
    Write-Host ""
    Write-Host "WHATIF MODE - No changes will be made" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Would remove: $($israeliRules.Count) Israeli IP rules" -ForegroundColor Yellow
    Write-Host "Would keep: $($blazorRules.Count) Blazor IP rules" -ForegroundColor Green
    Write-Host "Would keep: $($otherRules.Count) other rules" -ForegroundColor Green
    Write-Host ""
    Write-Host "After removal, API will only accept connections from:" -ForegroundColor White
    Write-Host "  - Blazor server: $($blazorRules.Count) outbound IPs" -ForegroundColor Green
    if ($otherRules.Count -gt 0) {
        Write-Host "  - Other sources: $($otherRules.Count) custom rules" -ForegroundColor Green
    }
    exit 0
}

# Confirm action
Write-Host ""
Write-Host "WARNING: This will remove $($israeliRules.Count) Israeli IP rules from the API." -ForegroundColor Red
Write-Host "After removal, the API will ONLY accept connections from the Blazor server." -ForegroundColor Red
Write-Host ""
$confirmation = Read-Host "Are you sure you want to proceed? (Type 'YES' to confirm)"

if ($confirmation -ne "YES") {
    Write-Host ""
    Write-Host "Operation cancelled by user." -ForegroundColor Yellow
    exit 0
}

# Remove Israeli IP rules
Write-Host ""
Write-Host "Removing Israeli IP rules from API..." -ForegroundColor Yellow
Write-Host "(This may take several minutes...)" -ForegroundColor Gray
Write-Host ""

$removeCount = 0
$errorCount = 0
$totalRules = $israeliRules.Count

foreach ($rule in $israeliRules) {
    $removeCount++
    $percentComplete = [math]::Round(($removeCount / $totalRules) * 100)
    
    Write-Host "[$removeCount/$totalRules] Removing: $($rule.name) - $percentComplete% complete" -ForegroundColor Gray
    
    $retries = 3
    $success = $false
    
    for ($i = 1; $i -le $retries; $i++) {
        try {
            az webapp config access-restriction remove `
                --name $apiApp `
                --resource-group $resourceGroup `
                --rule-name $rule.name 2>&1 | Out-Null
            
            if ($LASTEXITCODE -eq 0) {
                $success = $true
                break
            }
            else {
                if ($i -lt $retries) {
                    Write-Host "    Retry $i/$retries..." -ForegroundColor Yellow
                    Start-Sleep -Seconds 2
                }
            }
        }
        catch {
            if ($i -lt $retries) {
                Write-Host "    Retry $i/$retries..." -ForegroundColor Yellow
                Start-Sleep -Seconds 2
            }
        }
    }
    
    if (-not $success) {
        Write-Host "    [FAILED] Could not remove after $retries attempts" -ForegroundColor Red
        $errorCount++
    }
    
    # Rate limiting - pause every 10 operations
    if ($removeCount % 10 -eq 0) {
        Start-Sleep -Milliseconds 500
    }
}

# Verify final configuration
Write-Host ""
Write-Host "Verifying final configuration..." -ForegroundColor Yellow

$allFinalRules = az webapp config access-restriction show --name $apiApp --resource-group $resourceGroup --query "ipSecurityRestrictions" -o json | ConvertFrom-Json
$finalRules = $allFinalRules | Where-Object { $_.name -ne 'Allow all' -and $_.name -ne 'Deny all' }

$finalIsraeliRules = $finalRules | Where-Object { $_.name -like "Allow-Israeli-*" }
$finalBlazorRules = $finalRules | Where-Object { $_.name -like "Allow-Blazor-*" }

Write-Host ""
Write-Host "============================================" -ForegroundColor Green
Write-Host "REMOVAL COMPLETE" -ForegroundColor Green
Write-Host "============================================" -ForegroundColor Green
Write-Host ""
Write-Host "Summary:" -ForegroundColor Cyan
Write-Host "  Rules removed: $($removeCount - $errorCount) / $totalRules" -ForegroundColor $(if ($errorCount -eq 0) { "Green" } else { "Yellow" })
if ($errorCount -gt 0) {
    Write-Host "  Failed removals: $errorCount" -ForegroundColor Red
}
Write-Host ""
Write-Host "Final API Configuration:" -ForegroundColor Cyan
Write-Host "  Total rules: $($finalRules.Count)" -ForegroundColor White
Write-Host "  Blazor Server IPs: $($finalBlazorRules.Count)" -ForegroundColor Green
Write-Host "  Israeli Public IPs: $($finalIsraeliRules.Count)" -ForegroundColor $(if ($finalIsraeliRules.Count -eq 0) { "Green" } else { "Red" })

if ($finalIsraeliRules.Count -eq 0) {
    Write-Host ""
    Write-Host "[OK] API is now properly secured!" -ForegroundColor Green
    Write-Host "  Only Blazor server can access the API." -ForegroundColor Green
    Write-Host ""
    Write-Host "Architecture: Users (Israeli IPs) → Blazor → API (Blazor IPs only)" -ForegroundColor White
} else {
    Write-Host ""
    Write-Host "[WARNING] $($finalIsraeliRules.Count) Israeli IP rules still exist!" -ForegroundColor Red
    Write-Host "  Run this script again to retry removal." -ForegroundColor Yellow
}

Write-Host ""
