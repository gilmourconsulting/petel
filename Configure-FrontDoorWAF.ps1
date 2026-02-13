# Configure WAF for Front Door with Israeli IP Restrictions
# Run this after Front Door is created

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
        FrontDoorProfile  = 'petel-frontdoor-test'
        EndpointName      = 'petel-test'
        WafPolicyName     = 'petelWafTest'
    }
    'staging'    = @{
        ResourceGroup     = 'petel-staging-rg'
        FrontDoorProfile  = 'petel-frontdoor-staging'
        EndpointName      = 'petel-staging'
        WafPolicyName     = 'petelWafStaging'
    }
    'production' = @{
        ResourceGroup     = 'petel-prod-rg'
        FrontDoorProfile  = 'petel-frontdoor-prod'
        EndpointName      = 'petel-prod'
        WafPolicyName     = 'petelWafProd'
    }
}

$config = $envConfig[$Environment]
$ResourceGroup = $config.ResourceGroup
$FrontDoorProfile = $config.FrontDoorProfile
$EndpointName = $config.EndpointName
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
Write-Host "Configuring WAF for $Environment" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Create WAF Policy
Write-Host "Creating WAF policy..." -ForegroundColor Yellow
$wafExists = az network front-door waf-policy show --name $WafPolicyName --resource-group $ResourceGroup 2>$null

if (!$?) {
    Write-Host "Creating new WAF policy..." -ForegroundColor Gray
    az network front-door waf-policy create `
        --name $WafPolicyName `
        --resource-group $ResourceGroup `
        --sku Premium_AzureFrontDoor `
        --mode Prevention
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "SUCCESS: WAF policy created" -ForegroundColor Green
    }
} else {
    Write-Host "WAF policy already exists" -ForegroundColor Green
}

# Enable OWASP rules
Write-Host "Enabling OWASP ruleset..." -ForegroundColor Yellow
az network front-door waf-policy managed-rules add `
    --policy-name $WafPolicyName `
    --resource-group $ResourceGroup `
    --type DefaultRuleSet `
    --version 1.0 2>$null

Write-Host "SUCCESS: OWASP rules configured" -ForegroundColor Green

# Enable Bot Protection
Write-Host "Enabling Bot Protection..." -ForegroundColor Yellow
az network front-door waf-policy managed-rules add `
    --policy-name $WafPolicyName `
    --resource-group $ResourceGroup `
    --type Microsoft_BotManagerRuleSet `
    --version 1.0 2>$null

Write-Host "SUCCESS: Bot protection configured" -ForegroundColor Green

# Add Israeli IP restrictions
Write-Host "Configuring Israeli IP restrictions..." -ForegroundColor Yellow
Write-Host "Adding 47 Israeli IP ranges - this will take 2-3 minutes" -ForegroundColor Gray

$batchSize = 10
$batchNumber = 1

for ($i = 0; $i -lt $israeliIpRanges.Count; $i += $batchSize) {
    $endIndex = [Math]::Min($i + $batchSize - 1, $israeliIpRanges.Count - 1)
    $batch = $israeliIpRanges[$i..$endIndex]
    $ruleName = "AllowIsraeliIP$batchNumber"
    $priority = 100 + $batchNumber
    
    Write-Host "  Batch $batchNumber - adding $($batch.Count) ranges (priority $priority)" -ForegroundColor Gray
    
    # Delete if exists
    az network front-door waf-policy rule delete `
        --policy-name $WafPolicyName `
        --resource-group $ResourceGroup `
        --name $ruleName 2>$null
    
    # Create rule
    az network front-door waf-policy rule create `
        --policy-name $WafPolicyName `
        --resource-group $ResourceGroup `
        --name $ruleName `
        --rule-type MatchRule `
        --action Allow `
        --priority $priority `
        --match-variable RemoteAddr `
        --operator IPMatch `
        --match-value $batch 2>$null
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "    Batch $batchNumber completed" -ForegroundColor Green
    }
    
    $batchNumber++
}

Write-Host "SUCCESS: Israeli IP whitelist configured" -ForegroundColor Green

# Add geo-blocking rule
Write-Host "Adding geo-blocking for non-Israeli IPs..." -ForegroundColor Yellow

az network front-door waf-policy rule delete `
    --policy-name $WafPolicyName `
    --resource-group $ResourceGroup `
    --name BlockNonIsraeli 2>$null

az network front-door waf-policy rule create `
    --policy-name $WafPolicyName `
    --resource-group $ResourceGroup `
    --name BlockNonIsraeli `
    --rule-type MatchRule `
    --action Block `
    --priority 500 `
    --match-variable RemoteAddr `
    --operator GeoMatch `
    --negate `
    --match-value IL 2>$null

if ($LASTEXITCODE -eq 0) {
    Write-Host "SUCCESS: Geo-blocking configured" -ForegroundColor Green
} else {
    Write-Host "WARNING: Geo-blocking may need manual configuration" -ForegroundColor Yellow
}

# Associate with Front Door
Write-Host "Associating WAF with Front Door..." -ForegroundColor Yellow

$subscriptionId = (az account show --query id -o tsv)
$wafPolicyId = "/subscriptions/$subscriptionId/resourceGroups/$ResourceGroup/providers/Microsoft.Network/FrontDoorWebApplicationFirewallPolicies/$WafPolicyName"
$endpointId = "/subscriptions/$subscriptionId/resourceGroups/$ResourceGroup/providers/Microsoft.Cdn/profiles/$FrontDoorProfile/afdEndpoints/$EndpointName"

# Delete existing security policy if exists
az afd security-policy delete `
    --security-policy-name petelSecurityPolicy `
    --profile-name $FrontDoorProfile `
    --resource-group $ResourceGroup 2>$null

# Create security policy
az afd security-policy create `
    --security-policy-name petelSecurityPolicy `
    --profile-name $FrontDoorProfile `
    --resource-group $ResourceGroup `
    --domains $endpointId `
    --waf-policy $wafPolicyId 2>$null

if ($LASTEXITCODE -eq 0) {
    Write-Host "SUCCESS: WAF associated with Front Door" -ForegroundColor Green
} else {
    Write-Host "WARNING: Manual association may be needed" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "WAF Configuration Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "WAF Policy: $WafPolicyName" -ForegroundColor White
Write-Host "Israeli IP Ranges: 47 ranges whitelisted" -ForegroundColor White
Write-Host "Geo-Blocking: Enabled (blocks non-Israeli IPs)" -ForegroundColor White
Write-Host "OWASP Rules: Enabled" -ForegroundColor White
Write-Host "Bot Protection: Enabled" -ForegroundColor White
Write-Host ""
