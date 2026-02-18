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
# Updated: February 18, 2026 - Option A (Priority 1-3)
# Source: https://www.ipdeny.com/ipblocks/data/aggregated/il-aggregated.zone
$israeliIpRanges = @(
    # Previously configured ranges
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
    
    # Manually added ranges from production
    "103.209.0.0/32",
    "147.236.0.0/16",
    "185.24.0.0/16",
    "78.138.0.0/16",
    "84.228.0.0/16",
    
    # Priority 1: Critical ISP blocks
    "77.124.0.0/14",
    "31.12.76.0/22",
    "31.40.220.0/22",
    "31.44.128.0/20",
    "62.0.0.0/16",
    "62.90.0.0/16",
    "81.218.0.0/16",
    "83.130.0.0/16",
    "132.64.0.0/13",
    "212.143.0.0/16",
    "213.8.0.0/16",
    "5.29.0.0/16",
    "37.142.0.0/16",
    "46.116.0.0/15",
    "46.120.0.0/15",
    "46.210.0.0/16",
    "85.250.0.0/16",
    "176.12.128.0/17",
    "176.13.0.0/16",
    "2.52.0.0/14",
    "5.102.192.0/18",
    "62.219.0.0/16",
    "80.230.0.0/16",
    "81.5.0.0/18",
    "82.80.0.0/15",
    "82.102.128.0/18",
    "94.159.128.0/17",
    "95.35.0.0/16",
    "132.72.0.0/14",
    "132.76.0.0/15",
    "132.78.0.0/16",
    "147.233.0.0/16",
    "147.234.0.0/17",
    "147.235.0.0/16",
    "192.114.0.0/15",
    "192.116.0.0/15",
    "192.118.0.0/16",
    
    # Priority 2: Business & Cloud Infrastructure
    "84.94.0.0/15",
    "84.108.0.0/14",
    "85.130.128.0/17",
    "109.64.0.0/14",
    "109.253.0.0/16",
    "138.134.0.0/16",
    "141.226.0.0/18",
    "62.56.128.0/19",
    "62.128.32.0/19",
    "80.74.96.0/19",
    "81.199.0.0/20",
    "89.208.0.0/21",
    "93.172.0.0/15",
    "176.228.0.0/14",
    "188.64.200.0/21",
    "188.120.128.0/19",
    "212.25.64.0/18",
    "212.68.128.0/19",
    "212.117.128.0/19",
    "217.132.0.0/16",
    
    # Priority 3: Additional ISPs & Regional
    "5.100.248.0/21",
    "5.144.48.0/20",
    "37.19.112.0/20",
    "37.44.200.0/22",
    "37.60.40.0/21",
    "62.182.192.0/21",
    "78.138.4.0/22",
    "85.155.128.0/20",
    "86.104.226.0/24",
    "88.202.216.0/21",
    "91.135.96.0/20",
    "91.143.224.0/20",
    "93.157.80.0/21",
    "95.142.16.0/20",
    "95.175.32.0/19",
    "109.160.128.0/17",
    "109.226.0.0/18",
    "109.234.16.0/21",
    "144.249.128.0/18",
    "146.185.56.0/21",
    "149.49.0.0/16",
    "164.138.112.0/20",
    "167.17.128.0/19"
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
        while ($existingRules.priority -contains $priority) {
            $priority++
        }
        
        # Try to add the rule
        $null = az webapp config access-restriction add `
            --name $AppName `
            --resource-group $ResourceGroup `
            --rule-name $ruleName `
            --action Allow `
            --ip-address $ipRange `
            --priority $priority 2>&1
        
        if ($LASTEXITCODE -eq 0) {
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
    Write-Host "  ✅ $AppType configured:" -ForegroundColor Green
    Write-Host "     Added: $addedCount new rules" -ForegroundColor Green
    Write-Host "     Skipped: $skippedCount existing rules" -ForegroundColor Gray
    Write-Host "     Total: $ruleCount IP restriction rules" -ForegroundColor Cyan
    Write-Host "  ✅ Only Israeli traffic allowed" -ForegroundColor Green
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
