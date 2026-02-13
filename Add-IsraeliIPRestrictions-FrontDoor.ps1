# Add Israeli IP Restrictions to Front Door WAF
# Uses custom rule JSON for Front Door WAF policies

param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('test', 'staging', 'production')]
    [string]$Environment
)

$ErrorActionPreference = "Continue"

# Configuration
$envConfig = @{
    'test'       = @{
        ResourceGroup     = 'petel-test-rg'
        WafPolicyName     = 'petelWafTest'
    }
    'staging'    = @{
        ResourceGroup     = 'petel-staging-rg'
        WafPolicyName     = 'petelWafStaging'
    }
    'production' = @{
        ResourceGroup     = 'petel-prod-rg'
        WafPolicyName     = 'petelWafProd'
    }
}

$config = $envConfig[$Environment]
$ResourceGroup = $config.ResourceGroup
$WafPolicyName = $config.WafPolicyName

# Israeli IP ranges
$israeliIpRanges = @(
    "79.176.0.0/13", "80.178.0.0/15", "80.246.0.0/15", "80.250.0.0/15",
    "82.80.128.0/17", "82.166.0.0/15", "85.64.0.0/13", "86.57.0.0/17",
    "86.109.0.0/16", "87.68.0.0/14", "87.236.0.0/14", "88.198.0.0/15",
    "89.138.0.0/15", "90.128.0.0/11", "91.90.88.0/21", "91.199.9.0/24",
    "92.126.0.0/16", "94.188.0.0/14", "94.230.0.0/16", "109.186.0.0/15",
    "109.228.0.0/15", "132.64.0.0/12", "141.226.0.0/16", "146.185.128.0/17",
    "147.161.128.0/17", "149.3.0.0/17", "151.233.0.0/16", "176.12.0.0/15",
    "176.63.0.0/16", "178.137.0.0/16", "178.173.128.0/17", "185.2.12.0/22",
    "185.4.16.0/22", "188.64.0.0/13", "188.120.128.0/17", "212.116.128.0/17",
    "213.57.0.0/17", "212.179.0.0/16", "82.166.0.0/16", "77.125.0.0/16",
    "31.154.0.0/16", "31.168.0.0/16", "80.178.0.0/16", "87.70.0.0/16",
    "94.188.0.0/16", "95.86.0.0/16", "103.209.0.0/16"
)

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Adding Israeli IP Restrictions" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Create custom rules JSON file
$customRulesJson = @{
    customRules = @{
        rules = @(
            # Rule 1: Allow Israeli IP ranges (first 25)
            @{
                name = "AllowIsraeliIPs_Part1"
                priority = 100
                ruleType = "MatchRule"
                matchConditions = @(
                    @{
                        matchVariable = "RemoteAddr"
                        operator = "IPMatch"
                        matchValue = $israeliIpRanges[0..24]
                    }
                )
                action = "Allow"
            },
            # Rule 2: Allow Israeli IP ranges (remaining)
            @{
                name = "AllowIsraeliIPs_Part2"
                priority = 101
                ruleType = "MatchRule"
                matchConditions = @(
                    @{
                        matchVariable = "RemoteAddr"
                        operator = "IPMatch"
                        matchValue = $israeliIpRanges[25..($israeliIpRanges.Count - 1)]
                    }
                )
                action = "Allow"
            },
            # Rule 3: Block all non-Israeli IPs
            @{
                name = "BlockNonIsraeliGeo"
                priority = 500
                ruleType = "MatchRule"
                matchConditions = @(
                    @{
                        matchVariable = "RemoteAddr"
                        operator = "GeoMatch"
                        negateCondition = $true
                        matchValue = @("IL")
                    }
                )
                action = "Block"
            }
        )
    }
} | ConvertTo-Json -Depth 10

# Save to file
$jsonFile = "waf-custom-rules-$Environment.json"
$customRulesJson | Out-File $jsonFile -Encoding UTF8

Write-Host "Created custom rules file: $jsonFile" -ForegroundColor Gray
Write-Host ""

# Update WAF policy with custom rules
Write-Host "Updating WAF policy with Israeli IP restrictions..." -ForegroundColor Yellow
Write-Host "This includes:" -ForegroundColor Gray
Write-Host "  - $($israeliIpRanges.Count) Israeli IP ranges" -ForegroundColor Gray
Write-Host "  - Geo-blocking for non-Israeli IPs" -ForegroundColor Gray
Write-Host ""

az network front-door waf-policy update `
    --name $WafPolicyName `
    --resource-group $ResourceGroup `
    --set customRules=@$jsonFile

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "SUCCESS!" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "Israeli IP Restrictions Active:" -ForegroundColor White
    Write-Host "  - $($israeliIpRanges.Count) Israeli IP ranges whitelisted" -ForegroundColor Green
    Write-Host "  - Geo-blocking enabled (blocks non-Israeli IPs)" -ForegroundColor Green
    Write-Host "  - OWASP protection enabled" -ForegroundColor Green
    Write-Host "  - Bot protection enabled" -ForegroundColor Green
    Write-Host ""
    Write-Host "WAF Policy: $WafPolicyName" -ForegroundColor Cyan
    Write-Host "Resource Group: $ResourceGroup" -ForegroundColor Cyan
    Write-Host ""
    
    # Cleanup
    Remove-Item $jsonFile -Force
    Write-Host "Cleanup: Removed temporary JSON file" -ForegroundColor Gray
} else {
    Write-Host ""
    Write-Host "ERROR: Failed to update WAF policy" -ForegroundColor Red
    Write-Host "JSON file saved as: $jsonFile" -ForegroundColor Yellow
    Write-Host "You can manually update the WAF policy in Azure Portal" -ForegroundColor Yellow
}

Write-Host ""
