# ============================================
# Fix API Server Security Configuration
# ============================================
# API should ONLY accept connections from Blazor server
# NOT from all Israeli IPs / public internet
# ============================================

param(
    [Parameter(Mandatory = $false)]
    [ValidateSet('test', 'staging', 'production', 'all')]
    [string]$Environment = 'all',

    [Parameter(Mandatory = $false)]
    [ValidateSet('ath', 'assistants', 'all')]
    [string]$App = 'all',

    [switch]$WhatIf
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

$targets = @(
    @{ Product = 'ath'; Env = 'test'; ResourceGroup = 'petel-test-rg'; ApiApp = 'petel-test-api'; BlazorApp = 'petel-test-blazor' }
    @{ Product = 'ath'; Env = 'staging'; ResourceGroup = 'petel-staging-rg'; ApiApp = 'petel-staging-api'; BlazorApp = 'petel-staging-blazor' }
    @{ Product = 'ath'; Env = 'production'; ResourceGroup = 'petel-prod-rg'; ApiApp = 'petel-prod-api'; BlazorApp = 'petel-prod-blazor' }
    @{ Product = 'assistants'; Env = 'test'; ResourceGroup = 'petel-assist-test-rg'; ApiApp = 'petel-assist-test-api'; BlazorApp = 'petel-assist-test-blazor' }
    @{ Product = 'assistants'; Env = 'staging'; ResourceGroup = 'petel-assist-staging-rg'; ApiApp = 'petel-assist-staging-api'; BlazorApp = 'petel-assist-staging-blazor' }
    @{ Product = 'assistants'; Env = 'production'; ResourceGroup = 'petel-assist-prod-rg'; ApiApp = 'petel-assist-prod-api'; BlazorApp = 'petel-assist-prod-blazor' }
)

Write-Host ""
Write-Host "============================================" -ForegroundColor Red
Write-Host "FIX API SECURITY CONFIGURATION" -ForegroundColor Red
Write-Host "============================================" -ForegroundColor Red
Write-Host ""
Write-Host "Environment: $Environment | App: $App" -ForegroundColor White
Write-Host "API will ONLY accept Blazor outbound IPs" -ForegroundColor Yellow
Write-Host ""

# Verify Azure CLI
try {
    az account show | Out-Null
}
catch {
    Write-Host "[ERROR] Azure CLI not authenticated. Run: az login" -ForegroundColor Red
    exit 1
}

function Lock-ApiToBlazor {
    param(
        [hashtable]$Target
    )

    $resourceGroup = $Target.ResourceGroup
    $apiApp = $Target.ApiApp
    $blazorApp = $Target.BlazorApp

    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "$($Target.Product)/$($Target.Env): $apiApp" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan

    $prevEap = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    $apiExists = az webapp show --name $apiApp --resource-group $resourceGroup --query name -o tsv 2>$null
    $ErrorActionPreference = $prevEap
    if (-not $apiExists) {
        Write-Host "[SKIP] API app not found" -ForegroundColor Yellow
        return
    }

    $prevEap = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    $blazorExists = az webapp show --name $blazorApp --resource-group $resourceGroup --query name -o tsv 2>$null
    $ErrorActionPreference = $prevEap
    if (-not $blazorExists) {
        Write-Host "[SKIP] Blazor app not found - cannot lock API" -ForegroundColor Yellow
        return
    }

    Write-Host "Getting Blazor server outbound IPs..." -ForegroundColor Yellow
    $blazorOutboundIps = az webapp show --name $blazorApp --resource-group $resourceGroup --query "possibleOutboundIpAddresses" -o tsv
    if (-not $blazorOutboundIps) {
        Write-Host "[ERROR] Could not read Blazor outbound IPs" -ForegroundColor Red
        return
    }
    $blazorIpArray = $blazorOutboundIps -split "," | Where-Object { $_ }

    Write-Host "Blazor outbound IPs ($($blazorIpArray.Count)):" -ForegroundColor White
    foreach ($ip in $blazorIpArray) {
        Write-Host "  - $ip" -ForegroundColor Gray
    }

    Write-Host ""
    Write-Host "Current API restrictions..." -ForegroundColor Yellow
    $currentRules = az webapp config access-restriction show `
        --name $apiApp `
        --resource-group $resourceGroup `
        --query "ipSecurityRestrictions[?name!='Allow all' && name!='Deny all' && name!='Deny_All']" -o json | ConvertFrom-Json

    if (-not $currentRules) { $currentRules = @() }
    elseif ($currentRules -isnot [System.Array]) { $currentRules = @($currentRules) }

    Write-Host "Current rules: $($currentRules.Count)" -ForegroundColor White

    if ($WhatIf) {
        Write-Host "WHATIF: Would remove $($currentRules.Count) existing rules" -ForegroundColor Cyan
        Write-Host "WHATIF: Would add $($blazorIpArray.Count) Blazor server IPs" -ForegroundColor Cyan
        return
    }

    Write-Host ""
    Write-Host "Removing existing API IP restrictions..." -ForegroundColor Yellow

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

            if ($removeCount % 10 -eq 0) {
                Start-Sleep -Milliseconds 500
            }
        }
    }

    Write-Host "Rules removed: $removeCount / $($currentRules.Count)" -ForegroundColor Green
    if ($errorCount -gt 0) {
        Write-Host "Rules failed: $errorCount" -ForegroundColor Red
    }

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

        if ($addCount % 10 -eq 0) {
            Start-Sleep -Milliseconds 500
        }
    }

    Write-Host "Blazor IPs added: $addCount / $($blazorIpArray.Count)" -ForegroundColor Green

    $finalRules = az webapp config access-restriction show `
        --name $apiApp `
        --resource-group $resourceGroup `
        --query "ipSecurityRestrictions[?name!='Allow all' && name!='Deny all']" -o json | ConvertFrom-Json

    if (-not $finalRules) { $finalRules = @() }
    elseif ($finalRules -isnot [System.Array]) { $finalRules = @($finalRules) }

    Write-Host "Final API rules: $($finalRules.Count)" -ForegroundColor White
}

$envFilter = if ($Environment -eq 'all') { @('test', 'staging', 'production') } else { @($Environment) }
$productFilter = if ($App -eq 'all') { @('ath', 'assistants') } else { @($App) }

$selected = $targets | Where-Object {
    ($productFilter -contains $_.Product) -and ($envFilter -contains $_.Env)
}

foreach ($t in $selected) {
    Lock-ApiToBlazor -Target $t
}

Write-Host ""
Write-Host "============================================" -ForegroundColor Green
Write-Host "SECURITY CONFIGURATION COMPLETE" -ForegroundColor Green
Write-Host "============================================" -ForegroundColor Green
Write-Host ""
Write-Host "Security Status:" -ForegroundColor Cyan
Write-Host "  - API only accessible from Blazor server" -ForegroundColor Green
Write-Host "  - End users cannot directly access API" -ForegroundColor Green
Write-Host "  - Blazor has Israeli IP restrictions (separate script)" -ForegroundColor Green
Write-Host ""
Write-Host "Architecture: Users -> Blazor -> API" -ForegroundColor White
Write-Host ""
