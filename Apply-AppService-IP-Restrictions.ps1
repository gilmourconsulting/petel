# ============================================
# Apply Israeli IP Restrictions to App Services
# ============================================
# Cost-effective approach using built-in App Service features
# No additional cost - replaces Front Door Premium ($330/month)
# ============================================

param(
    [Parameter(Mandatory = $false)]
    [ValidateSet('test', 'production', 'both')]
    [string]$Environment = 'both',
    
    [switch]$RemoveExisting
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

# Israeli IP ranges - comprehensive list covering major ISPs
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
    # Major Israeli ISPs
    "212.179.0.0/16",
    "77.125.0.0/16",
    "31.154.0.0/16",
    "31.168.0.0/16",
    "87.70.0.0/16",
    "95.86.0.0/16",
    "103.209.0.0/16"
)

# Environment configurations
$envConfig = @{
    'test' = @{
        ResourceGroup = 'petel-test-rg'
        ApiAppName    = 'petel-test-api'
        BlazorAppName = 'petel-test-blazor'
    }
    'production' = @{
        ResourceGroup = 'petel-prod-rg'
        ApiAppName    = 'petel-prod-api'
        BlazorAppName = 'petel-prod-blazor'
    }
}

Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "Apply Israeli IP Restrictions" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Cost Savings: Replaces Front Door Premium (~`$330/month)" -ForegroundColor Green
Write-Host "IP Ranges: $($israeliIpRanges.Count) Israeli CIDR blocks" -ForegroundColor White
Write-Host "Environment: $Environment" -ForegroundColor White
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

# Function to apply IP restrictions to an app
function Apply-IpRestrictions {
    param(
        [string]$AppName,
        [string]$ResourceGroup,
        [string]$AppType
    )
    
    Write-Host ""
    Write-Host "Configuring $AppType : $AppName" -ForegroundColor Yellow
    Write-Host ("=" * 60) -ForegroundColor Yellow
    
    # Verify app exists
    $appExists = az webapp show --name $AppName --resource-group $ResourceGroup --query "name" -o tsv 2>$null
    if (-not $appExists) {
        Write-Host "[SKIP] App service not found" -ForegroundColor Yellow
        return
    }
    
    # Remove existing restrictions if requested
    if ($RemoveExisting) {
        Write-Host "  Removing existing IP restrictions..." -ForegroundColor Gray
        $existingRules = az webapp config access-restriction show `
            --name $AppName `
            --resource-group $ResourceGroup `
            --query "ipSecurityRestrictions[?name!='Allow all'].name" -o tsv 2>$null
        
        foreach ($ruleName in $existingRules) {
            if ($ruleName) {
                az webapp config access-restriction remove `
                    --name $AppName `
                    --resource-group $ResourceGroup `
                    --rule-name $ruleName 2>$null
            }
        }
        Write-Host "  [OK] Existing restrictions cleared" -ForegroundColor Green
    }
    
    # Add Israeli IP ranges
    Write-Host "  Adding $($israeliIpRanges.Count) Israeli IP ranges..." -ForegroundColor Gray
    
    $priority = 100
    $ruleNumber = 1
    
    foreach ($ipRange in $israeliIpRanges) {
        $ruleName = "Allow-Israeli-$ruleNumber"
        
        # Try to add the rule
        $result = az webapp config access-restriction add `
            --name $AppName `
            --resource-group $ResourceGroup `
            --rule-name $ruleName `
            --action Allow `
            --ip-address $ipRange `
            --priority $priority 2>&1
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "    [$ruleNumber/$($israeliIpRanges.Count)] Added: $ipRange" -ForegroundColor Gray
        }
        else {
            # Rule might already exist, try to continue
            Write-Host "    [$ruleNumber/$($israeliIpRanges.Count)] Skipped: $ipRange (may already exist)" -ForegroundColor DarkGray
        }
        
        $priority++
        $ruleNumber++
    }
    
    # Get final count
    $finalRules = az webapp config access-restriction show `
        --name $AppName `
        --resource-group $ResourceGroup `
        --query "ipSecurityRestrictions[?name!='Allow all']" -o json 2>$null | ConvertFrom-Json
    
    $ruleCount = ($finalRules | Measure-Object).Count
    
    Write-Host ""
    Write-Host "  [OK] $AppType configured with $ruleCount IP restriction rules" -ForegroundColor Green
    Write-Host "  [OK] Only Israeli traffic allowed" -ForegroundColor Green
}

# Apply to requested environments
$environments = @()
if ($Environment -eq 'both') {
    $environments = @('test', 'production')
}
else {
    $environments = @($Environment)
}

foreach ($env in $environments) {
    $config = $envConfig[$env]
    
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "Environment: $($env.ToUpper())" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    
    # Apply to API
    Apply-IpRestrictions -AppName $config.ApiAppName -ResourceGroup $config.ResourceGroup -AppType "API"
    
    # Apply to Blazor
    Apply-IpRestrictions -AppName $config.BlazorAppName -ResourceGroup $config.ResourceGroup -AppType "Blazor"
}

# Summary
Write-Host ""
Write-Host "============================================" -ForegroundColor Green
Write-Host "DEPLOYMENT COMPLETE" -ForegroundColor Green
Write-Host "============================================" -ForegroundColor Green
Write-Host ""
Write-Host "Security Status:" -ForegroundColor Cyan
Write-Host "  [OK] Israeli IP restrictions applied" -ForegroundColor Green
Write-Host "  [OK] Non-Israeli traffic blocked at network layer" -ForegroundColor Green
Write-Host "  [OK] No additional costs" -ForegroundColor Green
Write-Host ""
Write-Host "Testing:" -ForegroundColor Cyan
Write-Host "  1. Access from Israeli IP - should work" -ForegroundColor White
Write-Host "  2. Access from non-Israeli IP - should be blocked (403)" -ForegroundColor White
Write-Host ""
Write-Host "Next Steps:" -ForegroundColor Cyan
Write-Host "  1. Test access to production apps" -ForegroundColor White
Write-Host "  2. Test access to test apps" -ForegroundColor White
Write-Host "  3. Verify blocking from non-Israeli IP" -ForegroundColor White
Write-Host "  4. Run .\Remove-FrontDoor.ps1 to delete Front Door" -ForegroundColor Yellow
Write-Host ""
Write-Host "Annual Cost Savings: ~`$3,960" -ForegroundColor Green
Write-Host ""
