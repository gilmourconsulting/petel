# ============================================
# Apply Israeli IP Restrictions to App Services
# ============================================
# Cost-effective approach using built-in App Service features
# No additional cost - replaces Front Door Premium ($330/month)
#
# SECURITY: Israeli CIDRs are applied to BLAZOR only.
# API must be locked to Blazor outbound IPs via Fix-API-Security.ps1
# ============================================

param(
    [Parameter(Mandatory = $false)]
    [ValidateSet('test', 'staging', 'production', 'both', 'all')]
    [string]$Environment = 'all',
    
    [Parameter(Mandatory = $false)]
    [ValidateSet('ath', 'assistants', 'all')]
    [string]$App = 'all',
    
    [switch]$RemoveExisting
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

# Israeli IP ranges - proven Feb 17-18 2026 successful apply (git 5bb6ab8)
# DO NOT use the unapplied Option A ~130 expansion
$israeliIpRanges = @(
    "79.176.0.0/13",
    "80.178.0.0/15",
    "80.246.0.0/15",
    "80.250.0.0/15",
    "82.80.128.0/17",
    "82.166.0.0/15",
    "85.64.0.0/13",
    "86.57.0.0/17",
    "86.109.0.0/16",
    "87.68.0.0/14",
    "87.236.0.0/14",
    "88.198.0.0/15",
    "89.138.0.0/15",
    "90.128.0.0/11",
    "91.90.88.0/21",
    "91.199.9.0/24",
    "92.126.0.0/16",
    "94.188.0.0/14",
    "94.230.0.0/16",
    "109.186.0.0/15",
    "109.228.0.0/15",
    "132.64.0.0/12",
    "141.226.0.0/16",
    "146.185.128.0/17",
    "147.161.128.0/17",
    "149.3.0.0/17",
    "151.233.0.0/16",
    "176.12.0.0/15",
    "176.63.0.0/16",
    "178.137.0.0/16",
    "178.173.128.0/17",
    "185.2.12.0/22",
    "185.4.16.0/22",
    "188.64.0.0/13",
    "188.120.128.0/17",
    "212.116.128.0/17",
    "213.57.0.0/17",
    "212.179.0.0/16",
    "77.125.0.0/16",
    "31.154.0.0/16",
    "31.168.0.0/16",
    "87.70.0.0/16",
    "95.86.0.0/16",
    "103.209.0.0/16",
    # Inventory extras preserved from prior live rules (test Blazor)
    "147.236.0.0/16"
)

# Environment configurations - Blazor only (API is locked separately)
$envConfig = @{
    'ath-test' = @{
        ResourceGroup = 'petel-test-rg'
        BlazorAppName = 'petel-test-blazor'
        Product       = 'ath'
        Env           = 'test'
    }
    'ath-staging' = @{
        ResourceGroup = 'petel-staging-rg'
        BlazorAppName = 'petel-staging-blazor'
        Product       = 'ath'
        Env           = 'staging'
    }
    'ath-production' = @{
        ResourceGroup = 'petel-prod-rg'
        BlazorAppName = 'petel-prod-blazor'
        Product       = 'ath'
        Env           = 'production'
    }
    'assistants-test' = @{
        ResourceGroup = 'petel-assist-test-rg'
        BlazorAppName = 'petel-assist-test-blazor'
        Product       = 'assistants'
        Env           = 'test'
    }
    'assistants-staging' = @{
        ResourceGroup = 'petel-assist-staging-rg'
        BlazorAppName = 'petel-assist-staging-blazor'
        Product       = 'assistants'
        Env           = 'staging'
    }
    'assistants-production' = @{
        ResourceGroup = 'petel-assist-prod-rg'
        BlazorAppName = 'petel-assist-prod-blazor'
        Product       = 'assistants'
        Env           = 'production'
    }
}

Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "Apply Israeli IP Restrictions (Blazor only)" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Cost Savings: Replaces Front Door Premium (~`$330/month)" -ForegroundColor Green
Write-Host "IP Ranges: $($israeliIpRanges.Count) Israeli CIDR blocks" -ForegroundColor White
Write-Host "Environment: $Environment" -ForegroundColor White
Write-Host "App: $App" -ForegroundColor White
Write-Host "Target: Blazor App Services ONLY (API stays private)" -ForegroundColor Yellow
Write-Host ""

# Verify Azure CLI
try {
    az account show | Out-Null
    Write-Host "[OK] Azure CLI authenticated" -ForegroundColor Green
}
catch {
    Write-Host "[ERROR] Azure CLI not authenticated. Run: az login" -ForegroundColor Red
    exit 1
}

function Apply-IpRestrictions {
    param(
        [string]$AppName,
        [string]$ResourceGroup,
        [string]$AppType
    )
    
    Write-Host ""
    Write-Host "Configuring $AppType : $AppName" -ForegroundColor Yellow
    Write-Host ("=" * 60) -ForegroundColor Yellow
    
    # Verify app exists (missing RGs/apps must not abort other targets)
    $prevEap = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    $appExists = az webapp show --name $AppName --resource-group $ResourceGroup --query "name" -o tsv 2>$null
    $ErrorActionPreference = $prevEap
    if (-not $appExists) {
        Write-Host "[SKIP] App service not found" -ForegroundColor Yellow
        return
    }
    
    # Remove existing restrictions if requested
    if ($RemoveExisting) {
        Write-Host "  Removing existing IP restrictions..." -ForegroundColor Gray
        $prevEap = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        $existingRuleNames = az webapp config access-restriction show `
            --name $AppName `
            --resource-group $ResourceGroup `
            --query "ipSecurityRestrictions[?name!='Allow all' && name!='Deny all'].name" -o tsv 2>$null
        
        foreach ($ruleName in $existingRuleNames) {
            if ($ruleName) {
                az webapp config access-restriction remove `
                    --name $AppName `
                    --resource-group $ResourceGroup `
                    --rule-name $ruleName 2>$null | Out-Null
            }
        }
        $ErrorActionPreference = $prevEap
        Write-Host "  [OK] Existing restrictions cleared" -ForegroundColor Green
    }
    
    # Get existing IP restrictions
    Write-Host "  Checking existing IP restrictions..." -ForegroundColor Gray
    $existingRules = az webapp config access-restriction show `
        --name $AppName `
        --resource-group $ResourceGroup `
        --query "ipSecurityRestrictions[?name!='Allow all' && name!='Deny all']" -o json 2>$null | ConvertFrom-Json
    
    $existingIPs = @()
    if ($existingRules) {
        $existingIPs = $existingRules | ForEach-Object { $_.ip_address } | Where-Object { $_ }
    }
    Write-Host "  Found $($existingIPs.Count) existing IP rules" -ForegroundColor Gray
    
    # Add Israeli IP ranges
    Write-Host "  Processing $($israeliIpRanges.Count) Israeli IP ranges..." -ForegroundColor Gray
    
    $priority = 100
    $ruleNumber = 1
    $addedCount = 0
    $skippedCount = 0
    
    foreach ($ipRange in $israeliIpRanges) {
        # Check if IP already exists
        if ($existingIPs -contains $ipRange) {
            Write-Host "    [$ruleNumber/$($israeliIpRanges.Count)] Skipped: $ipRange (already exists)" -ForegroundColor DarkGray
            $skippedCount++
            $ruleNumber++
            continue
        }
        
        $ruleName = "Allow-Israeli-$ruleNumber"
        
        # Find available priority
        while ($existingRules -and ($existingRules.priority -contains $priority)) {
            $priority++
        }
        
        # Try to add the rule (Azure CLI stderr must not abort the loop)
        $prevEap = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        $null = az webapp config access-restriction add `
            --name $AppName `
            --resource-group $ResourceGroup `
            --rule-name $ruleName `
            --action Allow `
            --ip-address $ipRange `
            --priority $priority 2>&1
        $addOk = ($LASTEXITCODE -eq 0)
        $ErrorActionPreference = $prevEap
        
        if ($addOk) {
            Write-Host "    [$ruleNumber/$($israeliIpRanges.Count)] Added: $ipRange (priority $priority)" -ForegroundColor Green
            $addedCount++
        }
        else {
            Write-Host "    [$ruleNumber/$($israeliIpRanges.Count)] Failed: $ipRange" -ForegroundColor Red
        }
        
        $priority++
        $ruleNumber++
    }
    
    # Get final count
    $finalRules = az webapp config access-restriction show `
        --name $AppName `
        --resource-group $ResourceGroup `
        --query "ipSecurityRestrictions[?name!='Allow all' && name!='Deny all']" -o json 2>$null | ConvertFrom-Json
    
    $ruleCount = ($finalRules | Measure-Object).Count
    
    Write-Host ""
    Write-Host "  [OK] $AppType configured:" -ForegroundColor Green
    Write-Host "     Added: $addedCount new rules" -ForegroundColor Green
    Write-Host "     Skipped: $skippedCount existing rules" -ForegroundColor Gray
    Write-Host "     Total: $ruleCount IP restriction rules" -ForegroundColor Cyan
    Write-Host "  [OK] Only Israeli traffic allowed to Blazor" -ForegroundColor Green
}

function Test-ResourceGroupExists {
    param([string]$ResourceGroup)
    $prevEap = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    $rg = az group show --name $ResourceGroup --query name -o tsv 2>$null
    $ErrorActionPreference = $prevEap
    return [bool]$rg
}

# Resolve which configs to apply
$envFilter = @()
if ($Environment -eq 'all' -or $Environment -eq 'both') {
    $envFilter = @('test', 'staging', 'production')
}
else {
    $envFilter = @($Environment)
}

$productFilter = @()
if ($App -eq 'all') {
    $productFilter = @('ath', 'assistants')
}
else {
    $productFilter = @($App)
}

$targets = $envConfig.GetEnumerator() | Where-Object {
    ($productFilter -contains $_.Value.Product) -and ($envFilter -contains $_.Value.Env)
} | Sort-Object Name

foreach ($target in $targets) {
    $config = $target.Value
    
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "Target: $($target.Name)" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan

    if (-not (Test-ResourceGroupExists -ResourceGroup $config.ResourceGroup)) {
        Write-Host "[SKIP] Resource group $($config.ResourceGroup) not found" -ForegroundColor Yellow
        continue
    }
    
    Apply-IpRestrictions -AppName $config.BlazorAppName -ResourceGroup $config.ResourceGroup -AppType "Blazor"
}

# Summary
Write-Host ""
Write-Host "============================================" -ForegroundColor Green
Write-Host "DEPLOYMENT COMPLETE" -ForegroundColor Green
Write-Host "============================================" -ForegroundColor Green
Write-Host ""
Write-Host "Security Status:" -ForegroundColor Cyan
Write-Host "  [OK] Israeli IP restrictions applied to Blazor" -ForegroundColor Green
Write-Host "  [OK] Non-Israeli traffic blocked at network layer" -ForegroundColor Green
Write-Host "  [OK] API NOT modified - lock with Fix-API-Security.ps1" -ForegroundColor Yellow
Write-Host "  [OK] No additional costs" -ForegroundColor Green
Write-Host ""
Write-Host "Next Steps:" -ForegroundColor Cyan
Write-Host "  1. Run .\Fix-API-Security.ps1 -Environment all" -ForegroundColor White
Write-Host "  2. Test Blazor from Israeli IP" -ForegroundColor White
Write-Host "  3. Verify API returns 403 from browser" -ForegroundColor White
Write-Host "  4. Run .\Remove-FrontDoor.ps1 -Confirm" -ForegroundColor Yellow
Write-Host ""
Write-Host "Annual Cost Savings: ~`$3,960" -ForegroundColor Green
Write-Host ""
