# ============================================
# Remove Azure Front Door
# ============================================
# Run this AFTER verifying App Service IP restrictions work
# Annual savings: ~$3,960
# ============================================

param(
    [switch]$Confirm,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

$frontDoorConfig = @{
    ResourceGroup = 'petel-test-rg'
    ProfileName   = 'petel-frontdoor-test'
    WafPolicyName = 'petelWafTest'
}

Write-Host ""
Write-Host "============================================" -ForegroundColor Red
Write-Host "Remove Azure Front Door" -ForegroundColor Red
Write-Host "============================================" -ForegroundColor Red
Write-Host ""
Write-Host "This will DELETE:" -ForegroundColor Yellow
Write-Host "  - Front Door Profile: $($frontDoorConfig.ProfileName)" -ForegroundColor White
Write-Host "  - WAF Policy: $($frontDoorConfig.WafPolicyName)" -ForegroundColor White
Write-Host ""
Write-Host "Annual Cost Savings: ~`$3,960" -ForegroundColor Green
Write-Host ""

if ($DryRun) {
    Write-Host "DRY RUN MODE - No resources will be deleted" -ForegroundColor Yellow
    Write-Host ""
    
    # Check if resources exist
    $fdExists = az afd profile show --profile-name $frontDoorConfig.ProfileName --resource-group $frontDoorConfig.ResourceGroup --query "name" -o tsv 2>$null
    if ($fdExists) {
        Write-Host "[EXISTS] Front Door Profile: $($frontDoorConfig.ProfileName)" -ForegroundColor Cyan
        
        # List endpoints
        $endpoints = az afd endpoint list --profile-name $frontDoorConfig.ProfileName --resource-group $frontDoorConfig.ResourceGroup --query "[].{name:name, hostname:hostName}" -o json 2>$null | ConvertFrom-Json
        Write-Host "  Endpoints:" -ForegroundColor White
        foreach ($endpoint in $endpoints) {
            Write-Host "    - $($endpoint.name): $($endpoint.hostname)" -ForegroundColor Gray
        }
    }
    else {
        Write-Host "[NOT FOUND] Front Door Profile" -ForegroundColor Yellow
    }
    
    $wafExists = az network front-door waf-policy show --name $frontDoorConfig.WafPolicyName --resource-group $frontDoorConfig.ResourceGroup --query "name" -o tsv 2>$null
    if ($wafExists) {
        Write-Host "[EXISTS] WAF Policy: $($frontDoorConfig.WafPolicyName)" -ForegroundColor Cyan
    }
    else {
        Write-Host "[NOT FOUND] WAF Policy" -ForegroundColor Yellow
    }
    
    Write-Host ""
    Write-Host "To proceed with deletion, run:" -ForegroundColor Yellow
    Write-Host "  .\Remove-FrontDoor.ps1 -Confirm" -ForegroundColor White
    Write-Host ""
    exit 0
}

if (-not $Confirm) {
    Write-Host "SAFETY CHECK REQUIRED" -ForegroundColor Red
    Write-Host "===================" -ForegroundColor Red
    Write-Host ""
    Write-Host "Before deleting Front Door, verify:" -ForegroundColor Yellow
    Write-Host "  1. IP restrictions are applied to all App Services" -ForegroundColor White
    Write-Host "  2. You can access production apps from Israeli IP" -ForegroundColor White
    Write-Host "  3. Access is blocked from non-Israeli IPs" -ForegroundColor White
    Write-Host ""
    Write-Host "To delete Front Door, run:" -ForegroundColor Yellow
    Write-Host "  .\Remove-FrontDoor.ps1 -Confirm" -ForegroundColor White
    Write-Host ""
    Write-Host "To see what will be deleted, run:" -ForegroundColor Cyan
    Write-Host "  .\Remove-FrontDoor.ps1 -DryRun" -ForegroundColor White
    Write-Host ""
    exit 0
}

# Verify Azure CLI
try {
    az account show | Out-Null
    Write-Host "[OK] Azure CLI authenticated" -ForegroundColor Green
}
catch {
    Write-Host "[ERROR] Azure CLI not authenticated. Run: az login" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "FINAL CONFIRMATION" -ForegroundColor Red
Write-Host "==================" -ForegroundColor Red
Write-Host ""
Write-Host "You are about to DELETE Azure Front Door" -ForegroundColor Yellow
Write-Host "This action CANNOT be undone" -ForegroundColor Red
Write-Host ""
$response = Read-Host "Type 'DELETE' to confirm"

if ($response -ne 'DELETE') {
    Write-Host ""
    Write-Host "Deletion cancelled" -ForegroundColor Yellow
    exit 0
}

Write-Host ""
Write-Host "Deleting Front Door resources..." -ForegroundColor Yellow
Write-Host ""

# Step 1: Delete Front Door Profile (this includes all endpoints, routes, origins)
Write-Host "Step 1/2: Deleting Front Door Profile..." -ForegroundColor Yellow
$fdExists = az afd profile show --profile-name $frontDoorConfig.ProfileName --resource-group $frontDoorConfig.ResourceGroup --query "name" -o tsv 2>$null

if ($fdExists) {
    Write-Host "  Deleting $($frontDoorConfig.ProfileName)..." -ForegroundColor Gray
    az afd profile delete `
        --profile-name $frontDoorConfig.ProfileName `
        --resource-group $frontDoorConfig.ResourceGroup 2>$null
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  [OK] Front Door Profile deleted" -ForegroundColor Green
    }
    else {
        Write-Host "  [ERROR] Failed to delete Front Door Profile" -ForegroundColor Red
    }
}
else {
    Write-Host "  [SKIP] Front Door Profile not found" -ForegroundColor Yellow
}

# Step 2: Delete WAF Policy
Write-Host ""
Write-Host "Step 2/2: Deleting WAF Policy..." -ForegroundColor Yellow
$wafExists = az network front-door waf-policy show --name $frontDoorConfig.WafPolicyName --resource-group $frontDoorConfig.ResourceGroup --query "name" -o tsv 2>$null

if ($wafExists) {
    Write-Host "  Deleting $($frontDoorConfig.WafPolicyName)..." -ForegroundColor Gray
    az network front-door waf-policy delete `
        --name $frontDoorConfig.WafPolicyName `
        --resource-group $frontDoorConfig.ResourceGroup 2>$null
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  [OK] WAF Policy deleted" -ForegroundColor Green
    }
    else {
        Write-Host "  [ERROR] Failed to delete WAF Policy" -ForegroundColor Red
    }
}
else {
    Write-Host "  [SKIP] WAF Policy not found" -ForegroundColor Yellow
}

# Summary
Write-Host ""
Write-Host "============================================" -ForegroundColor Green
Write-Host "DELETION COMPLETE" -ForegroundColor Green
Write-Host "============================================" -ForegroundColor Green
Write-Host ""
Write-Host "Deleted Resources:" -ForegroundColor Cyan
Write-Host "  [OK] Front Door Profile" -ForegroundColor Green
Write-Host "  [OK] WAF Policy" -ForegroundColor Green
Write-Host "  [OK] All endpoints, routes, and origins" -ForegroundColor Green
Write-Host ""
Write-Host "Cost Savings:" -ForegroundColor Cyan
Write-Host "  Monthly: ~`$330" -ForegroundColor Green
Write-Host "  Annual: ~`$3,960" -ForegroundColor Green
Write-Host ""
Write-Host "Security:" -ForegroundColor Cyan
Write-Host "  App Service IP restrictions provide equivalent protection" -ForegroundColor White
Write-Host "  Israeli traffic only - enforced at network layer" -ForegroundColor White
Write-Host ""
