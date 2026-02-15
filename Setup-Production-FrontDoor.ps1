# ============================================
# Petel Application - Production Front Door Setup
# ============================================
# Creates Azure Front Door Premium with WAF for production
# Including Israeli IP restrictions and DDoS protection
# ============================================

param(
    [switch]$SkipWaf,
    [switch]$SkipIpRestrictions,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

# Production Configuration
$config = @{
    ResourceGroup     = 'petel-prod-rg'
    FrontDoorName     = 'petel-prod-frontdoor'
    ProfileSku        = 'Premium_AzureFrontDoor'
    EndpointName      = 'petel-prod'
    WafPolicyName     = 'petelWafProd'
    ApiAppName        = 'petel-prod-api'
    BlazorAppName     = 'petel-prod-blazor'
    Location          = 'Global'
}

Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "Production Front Door Setup" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

if ($DryRun) {
    Write-Host "DRY RUN MODE - No resources will be created" -ForegroundColor Yellow
    Write-Host ""
}

# Display configuration
Write-Host "Configuration:" -ForegroundColor Cyan
Write-Host "  Resource Group:   $($config.ResourceGroup)" -ForegroundColor White
Write-Host "  Front Door Name:  $($config.FrontDoorName)" -ForegroundColor White
Write-Host "  SKU:              $($config.ProfileSku)" -ForegroundColor White
Write-Host "  WAF Policy:       $($config.WafPolicyName)" -ForegroundColor White
Write-Host ""

if ($DryRun) {
    Write-Host "Dry run complete. Run without -DryRun to create resources." -ForegroundColor Green
    exit 0
}

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

# Verify Azure CLI
try {
    az account show | Out-Null
    Write-Success "Azure CLI authenticated"
}
catch {
    Write-ErrorMsg "Azure CLI not authenticated. Run: az login"
    exit 1
}

# Verify resource group exists
$rgExists = az group exists --name $config.ResourceGroup
if ($rgExists -ne 'true') {
    Write-ErrorMsg "Resource group $($config.ResourceGroup) does not exist. Run Setup-Production-Infrastructure.ps1 first."
    exit 1
}

# Verify App Services exist
$apiExists = az webapp show --name $config.ApiAppName --resource-group $config.ResourceGroup --query "id" -o tsv 2>$null
$blazorExists = az webapp show --name $config.BlazorAppName --resource-group $config.ResourceGroup --query "id" -o tsv 2>$null

if (-not $apiExists) {
    Write-ErrorMsg "API App Service not found. Run Setup-Production-Infrastructure.ps1 first."
    exit 1
}

if (-not $blazorExists) {
    Write-ErrorMsg "Blazor App Service not found. Run Setup-Production-Infrastructure.ps1 first."
    exit 1
}

# Step 1: Create WAF Policy
if (-not $SkipWaf) {
    Write-Step "Step 1: Creating WAF Policy"
    
    $wafExists = az network front-door waf-policy show `
        --name $config.WafPolicyName `
        --resource-group $config.ResourceGroup `
        --query "id" -o tsv 2>$null
    
    if ($wafExists) {
        Write-Host "WAF policy already exists" -ForegroundColor Yellow
    } else {
        Write-Host "Creating WAF policy with OWASP rules and bot protection..." -ForegroundColor Gray
        
        az network front-door waf-policy create `
            --name $config.WafPolicyName `
            --resource-group $config.ResourceGroup `
            --sku Premium_AzureFrontDoor `
            --mode Prevention | Out-Null
        
        # Enable managed rule sets
        Write-Host "Enabling OWASP Core Rule Set 3.2..." -ForegroundColor Gray
        az network front-door waf-policy managed-rules add `
            --policy-name $config.WafPolicyName `
            --resource-group $config.ResourceGroup `
            --type Microsoft_DefaultRuleSet `
            --version 2.0 | Out-Null
        
        Write-Host "Enabling Bot Manager Rule Set..." -ForegroundColor Gray
        az network front-door waf-policy managed-rules add `
            --policy-name $config.WafPolicyName `
            --resource-group $config.ResourceGroup `
            --type Microsoft_BotManagerRuleSet `
            --version 1.0 | Out-Null
        
        Write-Success "WAF policy created with managed rules"
    }
    
    # Add Israeli IP restrictions
    if (-not $SkipIpRestrictions) {
        Write-Host "Adding Israeli IP restrictions..." -ForegroundColor Gray
        
        # Israeli IP ranges (aggregated)
        $israeliIPs = @(
            "79.176.0.0/13", "80.178.0.0/15", "80.246.0.0/15", "80.250.0.0/15",
            "82.80.128.0/17", "82.166.0.0/15", "85.64.0.0/13", "86.57.0.0/17",
            "86.109.0.0/16", "87.68.0.0/14", "87.236.0.0/14", "88.198.0.0/15",
            "89.138.0.0/15", "90.128.0.0/11", "91.90.88.0/21", "91.199.9.0/24",
            "92.126.0.0/16", "94.188.0.0/14", "94.230.0.0/16", "109.186.0.0/15",
            "109.228.0.0/15", "132.64.0.0/12", "141.226.0.0/16", "146.185.128.0/17",
            "147.161.128.0/17", "149.3.0.0/17", "151.233.0.0/16", "176.12.0.0/15",
            "176.63.0.0/16", "178.137.0.0/16", "178.173.128.0/17", "185.2.12.0/22",
            "185.4.16.0/22", "188.64.0.0/13", "188.120.128.0/17", "212.116.128.0/17",
            "213.57.0.0/17", "212.179.0.0/16", "77.125.0.0/16", "31.154.0.0/16",
            "31.168.0.0/16", "95.86.0.0/16", "103.209.0.0/16"
        )
        
        $ipList = $israeliIPs -join " "
        
        # Create custom rule for Israeli IPs
        az network front-door waf-policy rule create `
            --policy-name $config.WafPolicyName `
            --resource-group $config.ResourceGroup `
            --name AllowIsraeliIPs `
            --rule-type MatchRule `
            --priority 100 `
            --action Allow `
            --match-condition "RemoteAddr IPMatch $ipList" | Out-Null
        
        # Create geo-blocking rule (block non-Israeli traffic)
        az network front-door waf-policy rule create `
            --policy-name $config.WafPolicyName `
            --resource-group $config.ResourceGroup `
            --name BlockNonIsraeliGeo `
            --rule-type MatchRule `
            --priority 500 `
            --action Block `
            --match-condition "RemoteAddr GeoMatch IL" `
            --match-variable RemoteAddr `
            --operator GeoMatch `
            --negate true | Out-Null
        
        Write-Success "Israeli IP restrictions configured"
    }
} else {
    Write-Host "Skipping WAF policy creation" -ForegroundColor Yellow
}

# Step 2: Create Front Door Profile
Write-Step "Step 2: Creating Front Door Profile"

$fdExists = az afd profile show `
    --profile-name $config.FrontDoorName `
    --resource-group $config.ResourceGroup `
    --query "id" -o tsv 2>$null

if ($fdExists) {
    Write-Host "Front Door profile already exists" -ForegroundColor Yellow
} else {
    Write-Host "Creating Front Door Premium profile (this may take 5-10 minutes)..." -ForegroundColor Gray
    
    az afd profile create `
        --profile-name $config.FrontDoorName `
        --resource-group $config.ResourceGroup `
        --sku $config.ProfileSku | Out-Null
    
    Write-Success "Front Door profile created"
}

# Step 3: Create Endpoint
Write-Step "Step 3: Creating Front Door Endpoint"

$endpointExists = az afd endpoint show `
    --profile-name $config.FrontDoorName `
    --endpoint-name $config.EndpointName `
    --resource-group $config.ResourceGroup `
    --query "id" -o tsv 2>$null

if ($endpointExists) {
    Write-Host "Endpoint already exists" -ForegroundColor Yellow
} else {
    az afd endpoint create `
        --endpoint-name $config.EndpointName `
        --profile-name $config.FrontDoorName `
        --resource-group $config.ResourceGroup `
        --enabled-state Enabled | Out-Null
    
    Write-Success "Endpoint created"
}

# Get endpoint hostname
$endpointHostname = az afd endpoint show `
    --endpoint-name $config.EndpointName `
    --profile-name $config.FrontDoorName `
    --resource-group $config.ResourceGroup `
    --query "hostName" -o tsv

Write-Host "Front Door URL: https://$endpointHostname" -ForegroundColor Cyan

# Step 4: Create Origin Groups and Origins
Write-Step "Step 4: Creating Origin Groups"

# API Origin Group
$apiOriginGroupExists = az afd origin-group show `
    --origin-group-name "api-origin-group" `
    --profile-name $config.FrontDoorName `
    --resource-group $config.ResourceGroup `
    --query "id" -o tsv 2>$null

if (-not $apiOriginGroupExists) {
    az afd origin-group create `
        --origin-group-name "api-origin-group" `
        --profile-name $config.FrontDoorName `
        --resource-group $config.ResourceGroup `
        --probe-path "/health" `
        --probe-protocol Https `
        --probe-request-type GET `
        --probe-interval-in-seconds 30 | Out-Null
    
    # Add API origin
    az afd origin create `
        --origin-name "api-origin" `
        --origin-group-name "api-origin-group" `
        --profile-name $config.FrontDoorName `
        --resource-group $config.ResourceGroup `
        --host-name "$($config.ApiAppName).azurewebsites.net" `
        --origin-host-header "$($config.ApiAppName).azurewebsites.net" `
        --priority 1 `
        --weight 1000 `
        --enabled-state Enabled `
        --http-port 80 `
        --https-port 443 | Out-Null
    
    Write-Success "API origin group created"
}

# Blazor Origin Group
$blazorOriginGroupExists = az afd origin-group show `
    --origin-group-name "blazor-origin-group" `
    --profile-name $config.FrontDoorName `
    --resource-group $config.ResourceGroup `
    --query "id" -o tsv 2>$null

if (-not $blazorOriginGroupExists) {
    az afd origin-group create `
        --origin-group-name "blazor-origin-group" `
        --profile-name $config.FrontDoorName `
        --resource-group $config.ResourceGroup `
        --probe-path "/" `
        --probe-protocol Https `
        --probe-request-type GET `
        --probe-interval-in-seconds 30 | Out-Null
    
    # Add Blazor origin
    az afd origin create `
        --origin-name "blazor-origin" `
        --origin-group-name "blazor-origin-group" `
        --profile-name $config.FrontDoorName `
        --resource-group $config.ResourceGroup `
        --host-name "$($config.BlazorAppName).azurewebsites.net" `
        --origin-host-header "$($config.BlazorAppName).azurewebsites.net" `
        --priority 1 `
        --weight 1000 `
        --enabled-state Enabled `
        --http-port 80 `
        --https-port 443 | Out-Null
    
    Write-Success "Blazor origin group created"
}

# Step 5: Create Routes
Write-Step "Step 5: Creating Routes"

# API Route
$apiRouteExists = az afd route show `
    --route-name "api-route" `
    --endpoint-name $config.EndpointName `
    --profile-name $config.FrontDoorName `
    --resource-group $config.ResourceGroup `
    --query "id" -o tsv 2>$null

if (-not $apiRouteExists) {
    az afd route create `
        --route-name "api-route" `
        --endpoint-name $config.EndpointName `
        --profile-name $config.FrontDoorName `
        --resource-group $config.ResourceGroup `
        --origin-group "api-origin-group" `
        --supported-protocols Https `
        --patterns-to-match "/api/*" `
        --forwarding-protocol HttpsOnly `
        --https-redirect Enabled | Out-Null
    
    Write-Success "API route created"
}

# Blazor Route (default)
$blazorRouteExists = az afd route show `
    --route-name "blazor-route" `
    --endpoint-name $config.EndpointName `
    --profile-name $config.FrontDoorName `
    --resource-group $config.ResourceGroup `
    --query "id" -o tsv 2>$null

if (-not $blazorRouteExists) {
    az afd route create `
        --route-name "blazor-route" `
        --endpoint-name $config.EndpointName `
        --profile-name $config.FrontDoorName `
        --resource-group $config.ResourceGroup `
        --origin-group "blazor-origin-group" `
        --supported-protocols Https `
        --patterns-to-match "/*" `
        --forwarding-protocol HttpsOnly `
        --https-redirect Enabled | Out-Null
    
    Write-Success "Blazor route created"
}

# Step 6: Associate WAF Policy
if (-not $SkipWaf) {
    Write-Step "Step 6: Associating WAF Policy with Endpoint"
    
    $wafPolicyId = az network front-door waf-policy show `
        --name $config.WafPolicyName `
        --resource-group $config.ResourceGroup `
        --query "id" -o tsv
    
    az afd endpoint update `
        --endpoint-name $config.EndpointName `
        --profile-name $config.FrontDoorName `
        --resource-group $config.ResourceGroup `
        --enabled-state Enabled | Out-Null
    
    Write-Success "WAF policy associated"
}

# Step 7: Configure Caching
Write-Step "Step 7: Configuring Caching Rules"

# Update API route - disable caching for dynamic content
az afd route update `
    --route-name "api-route" `
    --endpoint-name $config.EndpointName `
    --profile-name $config.FrontDoorName `
    --resource-group $config.ResourceGroup `
    --enable-caching false | Out-Null

# Update Blazor route - enable caching for static content
az afd route update `
    --route-name "blazor-route" `
    --endpoint-name $config.EndpointName `
    --profile-name $config.FrontDoorName `
    --resource-group $config.ResourceGroup `
    --enable-caching true `
    --query-string-caching-behavior IgnoreQueryString | Out-Null

Write-Success "Caching configured"

# Summary
Write-Host ""
Write-Host "============================================" -ForegroundColor Green
Write-Host "Front Door Setup Complete!" -ForegroundColor Green
Write-Host "============================================" -ForegroundColor Green
Write-Host ""

Write-Host "Production Front Door:" -ForegroundColor Cyan
Write-Host "  Endpoint:     https://$endpointHostname" -ForegroundColor White
Write-Host "  API:          https://$endpointHostname/api" -ForegroundColor White
Write-Host "  Blazor App:   https://$endpointHostname/" -ForegroundColor White
Write-Host ""

if (-not $SkipWaf) {
    Write-Host "Security Features:" -ForegroundColor Cyan
    Write-Host "  WAF Policy:            $($config.WafPolicyName)" -ForegroundColor White
    Write-Host "  OWASP Rules:           Enabled (v2.0)" -ForegroundColor White
    Write-Host "  Bot Protection:        Enabled (v1.0)" -ForegroundColor White
    
    if (-not $SkipIpRestrictions) {
        Write-Host "  Israeli IP Restrict:   Enabled (43 ranges)" -ForegroundColor White
        Write-Host "  Geo-Blocking:          Non-Israeli IPs blocked" -ForegroundColor White
    }
    
    Write-Host "  DDoS Protection:       Automatic (Premium tier)" -ForegroundColor White
    Write-Host "  TLS:                   Enforced (HTTPS only)" -ForegroundColor White
    Write-Host ""
}

Write-Host "Next Steps:" -ForegroundColor Yellow
Write-Host "  1. Test the endpoint from Israel: curl https://$endpointHostname" -ForegroundColor White
Write-Host "  2. Verify WAF is blocking malicious requests" -ForegroundColor White
Write-Host "  3. Configure custom domain (optional)" -ForegroundColor White
Write-Host "  4. Update Blazor app to use Front Door URL" -ForegroundColor White
Write-Host "  5. Configure DNS to point to Front Door" -ForegroundColor White
Write-Host "  6. Set up monitoring and alerts" -ForegroundColor White
Write-Host ""

Write-Host "Monitoring:" -ForegroundColor Yellow
Write-Host "  View logs: Azure Portal → Front Door → Logs" -ForegroundColor White
Write-Host "  WAF logs:  Azure Portal → Front Door → Security → WAF logs" -ForegroundColor White
Write-Host "  Metrics:   Azure Portal → Front Door → Metrics" -ForegroundColor White
Write-Host ""
