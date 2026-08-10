# ============================================
# Remove Azure Front Door
# ============================================
# Run this AFTER verifying App Service IP restrictions work
# Annual savings: ~$3,960
# ============================================

param(
    [switch]$Confirm,
    [switch]$DryRun,
    [switch]$Force
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

# All known Front Door profiles / WAF policies across environments
$frontDoorTargets = @(
    @{
        ResourceGroup = 'petel-test-rg'
        ProfileName   = 'petel-frontdoor-test'
        WafPolicyName = 'petelWafTest'
    },
    @{
        ResourceGroup = 'petel-staging-rg'
        ProfileName   = 'petel-frontdoor-staging'
        WafPolicyName = 'petelWafStaging'
    },
    @{
        ResourceGroup = 'petel-prod-rg'
        ProfileName   = 'petel-frontdoor-prod'
        WafPolicyName = 'petelWafProd'
    },
    @{
        # Alternate naming from Setup-Production-FrontDoor.ps1
        ResourceGroup = 'petel-prod-rg'
        ProfileName   = 'petel-prod-frontdoor'
        WafPolicyName = $null
    }
)

Write-Host ""
Write-Host "============================================" -ForegroundColor Red
Write-Host "Remove Azure Front Door" -ForegroundColor Red
Write-Host "============================================" -ForegroundColor Red
Write-Host ""
Write-Host "This will DELETE (if present):" -ForegroundColor Yellow
foreach ($t in $frontDoorTargets) {
    Write-Host "  - Profile: $($t.ProfileName) ($($t.ResourceGroup))" -ForegroundColor White
    if ($t.WafPolicyName) {
        Write-Host "  - WAF:     $($t.WafPolicyName) ($($t.ResourceGroup))" -ForegroundColor White
    }
}
Write-Host ""
Write-Host "Annual Cost Savings: ~`$3,960" -ForegroundColor Green
Write-Host ""

function Test-ResourceGroupExists {
    param([string]$ResourceGroup)
    $prevEap = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    $rg = az group show --name $ResourceGroup --query name -o tsv 2>$null
    $ErrorActionPreference = $prevEap
    return [bool]$rg
}

function Show-FrontDoorStatus {
    param([hashtable]$Target)

    if (-not (Test-ResourceGroupExists -ResourceGroup $Target.ResourceGroup)) {
        Write-Host "[SKIP RG] $($Target.ResourceGroup) not found" -ForegroundColor DarkGray
        return
    }

    $prevEap = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    $fdExists = az afd profile show --profile-name $Target.ProfileName --resource-group $Target.ResourceGroup --query "name" -o tsv 2>$null
    $ErrorActionPreference = $prevEap
    if ($fdExists) {
        Write-Host "[EXISTS] Front Door Profile: $($Target.ProfileName)" -ForegroundColor Cyan
        $prevEap = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        $endpoints = az afd endpoint list --profile-name $Target.ProfileName --resource-group $Target.ResourceGroup --query "[].{name:name, hostname:hostName}" -o json 2>$null | ConvertFrom-Json
        $ErrorActionPreference = $prevEap
        if ($endpoints) {
            Write-Host "  Endpoints:" -ForegroundColor White
            foreach ($endpoint in @($endpoints)) {
                Write-Host "    - $($endpoint.name): $($endpoint.hostname)" -ForegroundColor Gray
            }
        }
    }
    else {
        Write-Host "[NOT FOUND] Front Door Profile: $($Target.ProfileName)" -ForegroundColor Yellow
    }

    if ($Target.WafPolicyName) {
        $prevEap = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        $wafExists = az network front-door waf-policy show --name $Target.WafPolicyName --resource-group $Target.ResourceGroup --query "name" -o tsv 2>$null
        $ErrorActionPreference = $prevEap
        if ($wafExists) {
            Write-Host "[EXISTS] WAF Policy: $($Target.WafPolicyName)" -ForegroundColor Cyan
        }
        else {
            Write-Host "[NOT FOUND] WAF Policy: $($Target.WafPolicyName)" -ForegroundColor Yellow
        }
    }
}

function Remove-FrontDoorTarget {
    param([hashtable]$Target)

    if (-not (Test-ResourceGroupExists -ResourceGroup $Target.ResourceGroup)) {
        Write-Host "[SKIP RG] $($Target.ResourceGroup) not found" -ForegroundColor DarkGray
        return
    }

    Write-Host ""
    Write-Host "Processing $($Target.ProfileName) in $($Target.ResourceGroup)..." -ForegroundColor Yellow

    $prevEap = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    $fdExists = az afd profile show --profile-name $Target.ProfileName --resource-group $Target.ResourceGroup --query "name" -o tsv 2>$null
    $ErrorActionPreference = $prevEap
    if ($fdExists) {
        Write-Host "  Deleting Front Door Profile: $($Target.ProfileName)..." -ForegroundColor Gray
        $prevEap = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        az afd profile delete `
            --profile-name $Target.ProfileName `
            --resource-group $Target.ResourceGroup 2>$null
        $delOk = ($LASTEXITCODE -eq 0)
        $ErrorActionPreference = $prevEap

        if ($delOk) {
            Write-Host "  [OK] Front Door Profile deleted" -ForegroundColor Green
        }
        else {
            Write-Host "  [ERROR] Failed to delete Front Door Profile" -ForegroundColor Red
        }
    }
    else {
        Write-Host "  [SKIP] Front Door Profile not found" -ForegroundColor Yellow
    }

    if ($Target.WafPolicyName) {
        $prevEap = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        $wafExists = az network front-door waf-policy show --name $Target.WafPolicyName --resource-group $Target.ResourceGroup --query "name" -o tsv 2>$null
        $ErrorActionPreference = $prevEap
        if ($wafExists) {
            Write-Host "  Deleting WAF Policy: $($Target.WafPolicyName)..." -ForegroundColor Gray
            $prevEap = $ErrorActionPreference
            $ErrorActionPreference = 'Continue'
            az network front-door waf-policy delete `
                --name $Target.WafPolicyName `
                --resource-group $Target.ResourceGroup 2>$null
            $delOk = ($LASTEXITCODE -eq 0)
            $ErrorActionPreference = $prevEap

            if ($delOk) {
                Write-Host "  [OK] WAF Policy deleted" -ForegroundColor Green
            }
            else {
                Write-Host "  [ERROR] Failed to delete WAF Policy (may still be linked; retry after profile delete)" -ForegroundColor Red
            }
        }
        else {
            Write-Host "  [SKIP] WAF Policy not found" -ForegroundColor Yellow
        }
    }
}

if ($DryRun) {
    Write-Host "DRY RUN MODE - No resources will be deleted" -ForegroundColor Yellow
    Write-Host ""
    foreach ($t in $frontDoorTargets) {
        Show-FrontDoorStatus -Target $t
    }
    Write-Host ""
    Write-Host "To proceed with deletion, run:" -ForegroundColor Yellow
    Write-Host "  .\Remove-FrontDoor.ps1 -Confirm" -ForegroundColor White
    Write-Host "  .\Remove-FrontDoor.ps1 -Confirm -Force   # skip interactive DELETE prompt" -ForegroundColor White
    Write-Host ""
    exit 0
}

if (-not $Confirm) {
    Write-Host "SAFETY CHECK REQUIRED" -ForegroundColor Red
    Write-Host "===================" -ForegroundColor Red
    Write-Host ""
    Write-Host "Before deleting Front Door, verify:" -ForegroundColor Yellow
    Write-Host "  1. Israeli IP restrictions are on Blazor App Services" -ForegroundColor White
    Write-Host "  2. API is locked to Blazor outbound IPs only" -ForegroundColor White
    Write-Host "  3. You can access Blazor from an Israeli IP" -ForegroundColor White
    Write-Host "  4. Direct API access from browser is blocked (403)" -ForegroundColor White
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

if (-not $Force) {
    Write-Host ""
    Write-Host "FINAL CONFIRMATION" -ForegroundColor Red
    Write-Host "==================" -ForegroundColor Red
    Write-Host ""
    Write-Host "You are about to DELETE Azure Front Door resources" -ForegroundColor Yellow
    Write-Host "This action CANNOT be undone" -ForegroundColor Red
    Write-Host ""
    $response = Read-Host "Type 'DELETE' to confirm"

    if ($response -ne 'DELETE') {
        Write-Host ""
        Write-Host "Deletion cancelled" -ForegroundColor Yellow
        exit 0
    }
}
else {
    Write-Host "[Force] Skipping interactive confirmation" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Deleting Front Door resources..." -ForegroundColor Yellow

foreach ($t in $frontDoorTargets) {
    Remove-FrontDoorTarget -Target $t
}

# Summary
Write-Host ""
Write-Host "============================================" -ForegroundColor Green
Write-Host "DELETION COMPLETE" -ForegroundColor Green
Write-Host "============================================" -ForegroundColor Green
Write-Host ""
Write-Host "Deleted Resources (where present):" -ForegroundColor Cyan
Write-Host "  [OK] Front Door Profiles" -ForegroundColor Green
Write-Host "  [OK] WAF Policies" -ForegroundColor Green
Write-Host "  [OK] Endpoints, routes, and origins" -ForegroundColor Green
Write-Host ""
Write-Host "Cost Savings:" -ForegroundColor Cyan
Write-Host "  Monthly: ~`$330" -ForegroundColor Green
Write-Host "  Annual: ~`$3,960" -ForegroundColor Green
Write-Host ""
Write-Host "Security:" -ForegroundColor Cyan
Write-Host "  Blazor: Israeli IP restrictions" -ForegroundColor White
Write-Host "  API: Blazor outbound IPs only" -ForegroundColor White
Write-Host ""
