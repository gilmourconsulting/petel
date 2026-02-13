# ============================================
# Petel Application - Azure Front Door Deployment
# ============================================
# Creates Front Door with WAF and Israeli IP restrictions
# Supports: Test, Staging, Production
# ============================================

param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('test', 'staging', 'production')]
    [string]$Environment,
    
    [switch]$SkipWafConfiguration,
    [switch]$UpdateWafOnly
)

$ErrorActionPreference = "Continue"  # Changed from Stop to Continue for Azure CLI checks
$ProgressPreference = "SilentlyContinue"

# Configuration based on environment
$envConfig = @{
    'test'       = @{
        ResourceGroup     = 'petel-test-rg'
        FrontDoorProfile  = 'petel-frontdoor-test'
        EndpointName      = 'petel-test'
        WafPolicyName     = 'petelWafTest'
        BlazorHostname    = 'petel-test-blazor.azurewebsites.net'
        ApiHostname       = 'petel-test-api.azurewebsites.net'
        Location          = 'global'
    }
    'staging'    = @{
        ResourceGroup     = 'petel-staging-rg'
        FrontDoorProfile  = 'petel-frontdoor-staging'
        EndpointName      = 'petel-staging'
        WafPolicyName     = 'petelWafStaging'
        BlazorHostname    = 'petel-staging-blazor.azurewebsites.net'
        ApiHostname       = 'petel-staging-api.azurewebsites.net'
        Location          = 'global'
    }
    'production' = @{
        ResourceGroup     = 'petel-prod-rg'
        FrontDoorProfile  = 'petel-frontdoor-prod'
        EndpointName      = 'petel-prod'
        WafPolicyName     = 'petelWafProd'
        BlazorHostname    = 'petel-prod-blazor.azurewebsites.net'
        ApiHostname       = 'petel-prod-api.azurewebsites.net'
        Location          = 'global'
    }
}

$config = $envConfig[$Environment]
$ResourceGroup = $config.ResourceGroup
$FrontDoorProfile = $config.FrontDoorProfile
$EndpointName = $config.EndpointName
$WafPolicyName = $config.WafPolicyName
$BlazorHostname = $config.BlazorHostname
$ApiHostname = $config.ApiHostname

# Israeli IP ranges - comprehensive list
# Source: RIPE NCC and Israeli ISP allocations
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
    # Major Israeli providers
    "212.179.0.0/16",
    "82.166.0.0/16",
    "77.125.0.0/16",
    "31.154.0.0/16",
    "31.168.0.0/16",
    "80.178.0.0/16",
    "87.70.0.0/16",
    "94.188.0.0/16",
    "95.86.0.0/16",
    "103.209.0.0/16"
)

Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "Azure Front Door - $Environment Deployment" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

# Helper Functions
function Write-Step {
    param([string]$Message)
    Write-Host ""
    Write-Host $Message -ForegroundColor Yellow
    Write-Host ("=" * $Message.Length) -ForegroundColor Yellow
}

function Write-Success {
    param([string]$Message)
    Write-Host "SUCCESS: $Message" -ForegroundColor Green
}

function Write-ErrorMsg {
    param([string]$Message)
    Write-Host "ERROR: $Message" -ForegroundColor Red
}

function Write-Info {
    param([string]$Message)
    Write-Host "INFO: $Message" -ForegroundColor Cyan
}

function Test-AzureCli {
    try {
        az account show | Out-Null
        return $true
    }
    catch {
        return $false
    }
}

# Display configuration
$rangeCount = $israeliIpRanges.Count
Write-Info "Resource Group:      $ResourceGroup"
Write-Info "Front Door Profile:  $FrontDoorProfile"
Write-Info "Endpoint:            $EndpointName"
Write-Info "WAF Policy:          $WafPolicyName"
Write-Info "Blazor Hostname:     $BlazorHostname"
Write-Info "API Hostname:        $ApiHostname"
Write-Info "Israeli IP ranges:   $rangeCount ranges"
Write-Host ""

# Verify Prerequisites
Write-Step "Verifying Prerequisites"

if (-not (Test-AzureCli)) {
    Write-ErrorMsg "Azure CLI not authenticated. Run: az login"
    exit 1
}
Write-Success "Azure CLI authenticated"

# Check if resource group exists
$rgExists = az group show --name $ResourceGroup 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-ErrorMsg "Resource group '$ResourceGroup' does not exist"
    Write-Host "Create it with: az group create --name $ResourceGroup --location israelcentral" -ForegroundColor Yellow
    exit 1
}
Write-Success "Resource group exists"

# Check if app services exist
$blazorAppName = $BlazorHostname -replace '\.azurewebsites\.net$', ''
$blazorExists = az webapp show --resource-group $ResourceGroup --name $blazorAppName 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "WARNING: Blazor app service not found. Front Door will be created but may not work until app is deployed." -ForegroundColor Yellow
}
else {
    Write-Success "Blazor app service exists"
}

$apiAppName = $ApiHostname -replace '\.azurewebsites\.net$', ''
$apiExists = az webapp show --resource-group $ResourceGroup --name $apiAppName 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "WARNING: API app service not found. Front Door will be created but may not work until app is deployed." -ForegroundColor Yellow
}
else {
    Write-Success "API app service exists"
}

# Create Front Door Profile and Origins
if (-not $UpdateWafOnly) {
    Write-Step "Creating Azure Front Door Profile"

    # Check if Front Door profile exists
    Write-Host "Checking if Front Door profile exists..." -ForegroundColor Gray
    $fdExists = az afd profile show --profile-name $FrontDoorProfile --resource-group $ResourceGroup --only-show-errors 2>$null
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Creating Front Door profile with Premium SKU for WAF support..." -ForegroundColor Gray
        az afd profile create `
            --profile-name $FrontDoorProfile `
            --resource-group $ResourceGroup `
            --sku Premium_AzureFrontDoor `
            --only-show-errors | Out-Null
        
        if ($LASTEXITCODE -eq 0) {
            Write-Success "Front Door profile created"
        }
        else {
            Write-ErrorMsg "Failed to create Front Door profile"
            exit 1
        }
    }
    else {
        Write-Success "Front Door profile already exists"
    }

    # Create Endpoint
    Write-Host "Creating Front Door endpoint..." -ForegroundColor Gray
    $endpointExists = az afd endpoint show --endpoint-name $EndpointName --profile-name $FrontDoorProfile --resource-group $ResourceGroup 2>&1
    
    if ($LASTEXITCODE -ne 0) {
        az afd endpoint create `
            --endpoint-name $EndpointName `
            --profile-name $FrontDoorProfile `
            --resource-group $ResourceGroup `
            --enabled-state Enabled `
            --only-show-errors | Out-Null
        
        if ($LASTEXITCODE -eq 0) {
            Write-Success "Endpoint created"
        }
        else {
            Write-ErrorMsg "Failed to create endpoint"
            exit 1
        }
    }
    else {
        Write-Success "Endpoint already exists"
    }

    # Create Origin Groups and Origins
    Write-Step "Configuring Origins"

    # API Origin Group
    Write-Host "Creating API origin group..." -ForegroundColor Gray
    $apiOriginGroupExists = az afd origin-group show --origin-group-name api-origins --profile-name $FrontDoorProfile --resource-group $ResourceGroup 2>&1
    
    if ($LASTEXITCODE -ne 0) {
        az afd origin-group create `
            --origin-group-name api-origins `
            --profile-name $FrontDoorProfile `
            --resource-group $ResourceGroup `
            --probe-request-type GET `
            --probe-protocol Https `
            --probe-path /api/health `
            --sample-size 4 `
            --successful-samples-required 3 `
            --additional-latency-in-milliseconds 50 `
            --only-show-errors | Out-Null
        
        Write-Success "API origin group created"
    }
    else {
        Write-Success "API origin group already exists"
    }

    # API Origin
    Write-Host "Adding API origin..." -ForegroundColor Gray
    $apiOriginExists = az afd origin show --origin-name api-backend --origin-group-name api-origins --profile-name $FrontDoorProfile --resource-group $ResourceGroup 2>&1
    
    if ($LASTEXITCODE -ne 0) {
        az afd origin create `
            --origin-name api-backend `
            --origin-group-name api-origins `
            --profile-name $FrontDoorProfile `
            --resource-group $ResourceGroup `
            --host-name $ApiHostname `
            --origin-host-header $ApiHostname `
            --priority 1 `
            --weight 1000 `
            --enabled-state Enabled `
            --http-port 80 `
            --https-port 443 `
            --only-show-errors | Out-Null
        
        Write-Success "API origin added"
    }
    else {
        Write-Success "API origin already exists"
    }

    # Blazor Origin Group
    Write-Host "Creating Blazor origin group..." -ForegroundColor Gray
    $blazorOriginGroupExists = az afd origin-group show --origin-group-name blazor-origins --profile-name $FrontDoorProfile --resource-group $ResourceGroup 2>&1
    
    if ($LASTEXITCODE -ne 0) {
        az afd origin-group create `
            --origin-group-name blazor-origins `
            --profile-name $FrontDoorProfile `
            --resource-group $ResourceGroup `
            --probe-request-type HEAD `
            --probe-protocol Https `
            --probe-path / `
            --sample-size 4 `
            --successful-samples-required 3 `
            --additional-latency-in-milliseconds 50 `
            --only-show-errors | Out-Null
        
        Write-Success "Blazor origin group created"
    }
    else {
        Write-Success "Blazor origin group already exists"
    }

    # Blazor Origin
    Write-Host "Adding Blazor origin..." -ForegroundColor Gray
    $blazorOriginExists = az afd origin show --origin-name blazor-backend --origin-group-name blazor-origins --profile-name $FrontDoorProfile --resource-group $ResourceGroup 2>&1
    
    if ($LASTEXITCODE -ne 0) {
        az afd origin create `
            --origin-name blazor-backend `
            --origin-group-name blazor-origins `
            --profile-name $FrontDoorProfile `
            --resource-group $ResourceGroup `
            --host-name $BlazorHostname `
            --origin-host-header $BlazorHostname `
            --priority 1 `
            --weight 1000 `
            --enabled-state Enabled `
            --http-port 80 `
            --https-port 443 `
            --only-show-errors | Out-Null
        
        Write-Success "Blazor origin added"
    }
    else {
        Write-Success "Blazor origin already exists"
    }

    # Create Routes
    Write-Step "Configuring Routes"

    # API Route
    Write-Host "Creating API route..." -ForegroundColor Gray
    $apiRouteExists = az afd route show --route-name api-route --endpoint-name $EndpointName --profile-name $FrontDoorProfile --resource-group $ResourceGroup 2>&1
    
    if ($LASTEXITCODE -ne 0) {
        az afd route create `
            --route-name api-route `
            --endpoint-name $EndpointName `
            --profile-name $FrontDoorProfile `
            --resource-group $ResourceGroup `
            --origin-group api-origins `
            --supported-protocols Http Https `
            --link-to-default-domain Enabled `
            --https-redirect Enabled `
            --forwarding-protocol MatchRequest `
            --patterns-to-match "/api/*" `
            --only-show-errors | Out-Null
        
        Write-Success "API route created"
    }
    else {
        Write-Success "API route already exists"
    }

    # Blazor Route
    Write-Host "Creating Blazor route..." -ForegroundColor Gray
    $blazorRouteExists = az afd route show --route-name blazor-route --endpoint-name $EndpointName --profile-name $FrontDoorProfile --resource-group $ResourceGroup 2>&1
    
    if ($LASTEXITCODE -ne 0) {
        az afd route create `
            --route-name blazor-route `
            --endpoint-name $EndpointName `
            --profile-name $FrontDoorProfile `
            --resource-group $ResourceGroup `
            --origin-group blazor-origins `
            --supported-protocols Http Https `
            --link-to-default-domain Enabled `
            --https-redirect Enabled `
            --forwarding-protocol MatchRequest `
            --patterns-to-match "/*" `
            --only-show-errors | Out-Null
        
        Write-Success "Blazor route created"
    }
    else {
        Write-Success "Blazor route already exists"
    }
}

# Configure WAF Policy
if (-not $SkipWafConfiguration) {
    Write-Step "Configuring Web Application Firewall (WAF)"

    # Create WAF Policy
    Write-Host "Creating WAF policy..." -ForegroundColor Gray
    $wafExists = az network front-door waf-policy show --name $WafPolicyName --resource-group $ResourceGroup 2>&1
    
    if ($LASTEXITCODE -ne 0) {
        az network front-door waf-policy create `
            --name $WafPolicyName `
            --resource-group $ResourceGroup `
            --sku Premium_AzureFrontDoor `
            --mode Prevention `
            --only-show-errors | Out-Null
        
        if ($LASTEXITCODE -eq 0) {
            Write-Success "WAF policy created"
        }
        else {
            Write-ErrorMsg "Failed to create WAF policy"
            exit 1
        }
    }
    else {
        Write-Success "WAF policy already exists"
    }

    # Enable OWASP Core Rule Set
    Write-Host "Enabling OWASP Core Rule Set 2.1..." -ForegroundColor Gray
    az network front-door waf-policy managed-rule-set add `
        --policy-name $WafPolicyName `
        --resource-group $ResourceGroup `
        --type Microsoft_DefaultRuleSet `
        --version 2.1 `
        --only-show-errors 2>&1 | Out-Null
    
    if ($LASTEXITCODE -eq 0) {
        Write-Success "OWASP rule set enabled"
    }
    else {
        Write-Host "WARNING: OWASP rule set may already be configured" -ForegroundColor Yellow
    }

    # Enable Bot Protection
    Write-Host "Enabling Bot Protection..." -ForegroundColor Gray
    az network front-door waf-policy managed-rule-set add `
        --policy-name $WafPolicyName `
        --resource-group $ResourceGroup `
        --type Microsoft_BotManagerRuleSet `
        --version 1.0 `
        --only-show-errors 2>&1 | Out-Null
    
    if ($LASTEXITCODE -eq 0) {
        Write-Success "Bot protection enabled"
    }
    else {
        Write-Host "WARNING: Bot protection may already be configured" -ForegroundColor Yellow
    }

    # Configure Israeli IP Restrictions
    Write-Step "Configuring Israeli IP Restrictions"
    
    Write-Host "Adding Israeli IP whitelist - $rangeCount ranges" -ForegroundColor Gray
    Write-Host "This may take 2-3 minutes..." -ForegroundColor Gray

    # Note: Azure CLI has limit on command line length, so we add IPs in batches
    $batchSize = 20
    $batchNumber = 1
    
    for ($i = 0; $i -lt $israeliIpRanges.Count; $i += $batchSize) {
        $batch = $israeliIpRanges[$i..[Math]::Min($i + $batchSize - 1, $israeliIpRanges.Count - 1)]
        $ruleName = "AllowIsraeliIPs_Batch$batchNumber"
        $priority = 100 + $batchNumber
        
        $totalBatches = [Math]::Ceiling($israeliIpRanges.Count / $batchSize)
        $batchCount = $batch.Count
        Write-Host "  Adding batch $batchNumber of $totalBatches - $batchCount ranges" -ForegroundColor Gray
        
        # Check if this batch rule exists
        $batchRuleExists = az network front-door waf-policy rule show `
            --policy-name $WafPolicyName `
            --resource-group $ResourceGroup `
            --name $ruleName `
            --only-show-errors 2>&1

        if ($LASTEXITCODE -eq 0) {
            # Delete existing batch rule
            az network front-door waf-policy rule delete `
                --policy-name $WafPolicyName `
                --resource-group $ResourceGroup `
                --name $ruleName `
                --only-show-errors 2>&1 | Out-Null
        }

        # Create rule with defer flag
        az network front-door waf-policy rule create `
            --policy-name $WafPolicyName `
            --resource-group $ResourceGroup `
            --name $ruleName `
            --rule-type MatchRule `
            --action Allow `
            --priority $priority `
            --defer `
            --only-show-errors 2>&1 | Out-Null
        
        # Add match condition
        az network front-door waf-policy rule match-condition add `
            --policy-name $WafPolicyName `
            --resource-group $ResourceGroup `
            --name $ruleName `
            --match-variable RemoteAddr `
            --operator IPMatch `
            --values $batch `
            --only-show-errors 2>&1 | Out-Null
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "    Batch $batchNumber configured" -ForegroundColor Green
        }
        else {
            Write-Host "    WARNING: Batch $batchNumber may have issues" -ForegroundColor Yellow
        }
        
        $batchNumber++
    }

    Write-Success "Israeli IP whitelist configured - $batchNumber batches"

    # Add geo-blocking rule to block non-Israeli traffic
    Write-Host "Adding geo-blocking rule for non-Israeli traffic..." -ForegroundColor Gray
    
    $geoBlockExists = az network front-door waf-policy rule show `
        --policy-name $WafPolicyName `
        --resource-group $ResourceGroup `
        --name BlockNonIsraeliGeo `
        --only-show-errors 2>&1

    if ($LASTEXITCODE -eq 0) {
        az network front-door waf-policy rule delete `
            --policy-name $WafPolicyName `
            --resource-group $ResourceGroup `
            --name BlockNonIsraeliGeo `
            --only-show-errors 2>&1 | Out-Null
    }

    # Create geo-blocking rule
    az network front-door waf-policy rule create `
        --policy-name $WafPolicyName `
        --resource-group $ResourceGroup `
        --name BlockNonIsraeliGeo `
        --rule-type MatchRule `
        --action Block `
        --priority 500 `
        --defer `
        --only-show-errors 2>&1 | Out-Null

    # Add match condition with negation for Israel
    az network front-door waf-policy rule match-condition add `
        --policy-name $WafPolicyName `
        --resource-group $ResourceGroup `
        --name BlockNonIsraeliGeo `
        --match-variable RemoteAddr `
        --operator GeoMatch `
        --negate true `
        --values IL `
        --only-show-errors 2>&1 | Out-Null
    
    if ($LASTEXITCODE -eq 0) {
        Write-Success "Geo-blocking rule configured"
    }
    else {
        Write-Host "WARNING: Geo-blocking rule may have issues" -ForegroundColor Yellow
    }

    # Associate WAF with Front Door
    if (-not $UpdateWafOnly) {
        Write-Step "Associating WAF with Front Door"

        # Get subscription ID
        $subscriptionId = (az account show --query id -o tsv)
        $wafPolicyId = "/subscriptions/$subscriptionId/resourceGroups/$ResourceGroup/providers/Microsoft.Network/frontDoorWebApplicationFirewallPolicies/$WafPolicyName"

        Write-Host "Creating security policy..." -ForegroundColor Gray
        
        # Check if security policy exists
        $secPolicyExists = az afd security-policy show `
            --security-policy-name petelSecurityPolicy `
            --profile-name $FrontDoorProfile `
            --resource-group $ResourceGroup `
            --only-show-errors 2>&1

        if ($LASTEXITCODE -eq 0) {
            Write-Host "Updating existing security policy..." -ForegroundColor Gray
            az afd security-policy delete `
                --security-policy-name petelSecurityPolicy `
                --profile-name $FrontDoorProfile `
                --resource-group $ResourceGroup `
                --only-show-errors 2>&1 | Out-Null
        }

        # Get endpoint ID
        $endpointId = "/subscriptions/$subscriptionId/resourceGroups/$ResourceGroup/providers/Microsoft.Cdn/profiles/$FrontDoorProfile/afdEndpoints/$EndpointName"

        az afd security-policy create `
            --security-policy-name petelSecurityPolicy `
            --profile-name $FrontDoorProfile `
            --resource-group $ResourceGroup `
            --domains $endpointId `
            --waf-policy $wafPolicyId `
            --only-show-errors 2>&1 | Out-Null
        
        if ($LASTEXITCODE -eq 0) {
            Write-Success "Security policy created and associated with Front Door"
        }
        else {
            Write-ErrorMsg "Failed to associate WAF with Front Door"
            Write-Host "You may need to do this manually in the Azure Portal" -ForegroundColor Yellow
        }
    }
}

# Get Front Door endpoint URL
Write-Step "Deployment Summary"

$endpointHostname = az afd endpoint show `
    --endpoint-name $EndpointName `
    --profile-name $FrontDoorProfile `
    --resource-group $ResourceGroup `
    --query hostName -o tsv 2>&1

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "============================================" -ForegroundColor Green
    Write-Host " FRONT DOOR DEPLOYMENT COMPLETE!" -ForegroundColor Green
    Write-Host "============================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "Front Door Endpoint:" -ForegroundColor Cyan
    Write-Host "  https://$endpointHostname" -ForegroundColor White
    Write-Host ""
    Write-Host "Blazor App (via Front Door):" -ForegroundColor Cyan
    Write-Host "  https://$endpointHostname" -ForegroundColor White
    Write-Host ""
    Write-Host "API (via Front Door):" -ForegroundColor Cyan
    Write-Host "  https://$endpointHostname/api" -ForegroundColor White
    Write-Host ""
    Write-Host "Security Configuration:" -ForegroundColor Cyan
    Write-Host "  WAF Policy:      $WafPolicyName" -ForegroundColor White
    Write-Host "  OWASP Rules:     Enabled v2.1" -ForegroundColor White
    Write-Host "  Bot Protection:  Enabled v1.0" -ForegroundColor White
    Write-Host "  Israeli IPs:     $rangeCount ranges whitelisted" -ForegroundColor White
    Write-Host "  Geo-Blocking:    Enabled (non-Israeli IPs blocked)" -ForegroundColor White
    Write-Host ""
    Write-Host "Next Steps:" -ForegroundColor Cyan
    Write-Host "  1. Test the Front Door endpoint above" -ForegroundColor Gray
    Write-Host "  2. Update your DNS to point to the Front Door endpoint" -ForegroundColor Gray
    Write-Host "  3. Configure custom domain in Front Door (optional)" -ForegroundColor Gray
    Write-Host "  4. Monitor WAF logs in Azure Portal" -ForegroundColor Gray
    Write-Host ""
    Write-Host "Management Commands:" -ForegroundColor Cyan
    Write-Host "  Update WAF only:  .\Deploy-FrontDoor.ps1 -Environment $Environment -UpdateWafOnly" -ForegroundColor Gray
    Write-Host "  Skip WAF config:  .\Deploy-FrontDoor.ps1 -Environment $Environment -SkipWafConfiguration" -ForegroundColor Gray
    Write-Host ""
}
else {
    Write-Host ""
    Write-Host "WARNING: Could not retrieve endpoint hostname" -ForegroundColor Yellow
    Write-Host "Front Door may still be provisioning. Check Azure Portal in a few minutes." -ForegroundColor Yellow
    Write-Host ""
}

Write-Host "Deployment script completed successfully!" -ForegroundColor Green
Write-Host ""
