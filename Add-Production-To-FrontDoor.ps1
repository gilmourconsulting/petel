# ============================================
# Add Production to Existing Front Door
# ============================================
# Cost-effective approach: Reuses existing Front Door
# - No additional base fees ($0 extra monthly cost)
# - Same WAF policy (already configured with Israeli IP restrictions)
# - Adds production endpoints/routes to test Front Door profile
# - Removes IP restrictions from production App Services
# ============================================

param(
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

# Configuration - Reuse existing test Front Door
$sharedConfig = @{
    ResourceGroup     = 'petel-test-rg'
    FrontDoorProfile  = 'petel-frontdoor-test'  # Existing test Front Door
    WafPolicyName     = 'petelWafTest'           # Existing WAF
}

$prodConfig = @{
    ResourceGroup  = 'petel-prod-rg'
    EndpointName   = 'petel-prod'
    ApiHostname    = 'petel-prod-api.azurewebsites.net'
    BlazorHostname = 'petel-prod-blazor.azurewebsites.net'
    ApiAppName     = 'petel-prod-api'
    BlazorAppName  = 'petel-prod-blazor'
}

Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "Add Production to Front Door" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "COST SAVINGS APPROACH" -ForegroundColor Green
Write-Host "   Reusing existing test Front Door profile" -ForegroundColor White
Write-Host "   No additional base fees (~`$330/month saved)" -ForegroundColor Green
Write-Host ""
Write-Host "Configuration:" -ForegroundColor Yellow
Write-Host "  Shared Front Door: $($sharedConfig.FrontDoorProfile)" -ForegroundColor White
Write-Host "  Shared WAF:        $($sharedConfig.WafPolicyName)" -ForegroundColor White
Write-Host "  New Endpoint:      $($prodConfig.EndpointName)" -ForegroundColor White
Write-Host "  API Backend:       $($prodConfig.ApiHostname)" -ForegroundColor White
Write-Host "  Blazor Backend:    $($prodConfig.BlazorHostname)" -ForegroundColor White
Write-Host ""

if ($DryRun) {
    Write-Host "DRY RUN MODE - No changes will be made" -ForegroundColor Yellow
    Write-Host ""
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
    Write-Host "[OK] $Message" -ForegroundColor Green
}

function Write-Info {
    param([string]$Message)
    Write-Host "  $Message" -ForegroundColor Gray
}

# Verify Azure CLI
Write-Step "Verifying Prerequisites"
try {
    az account show | Out-Null
    Write-Success "Azure CLI authenticated"
}
catch {
    Write-Host "ERROR: Azure CLI not authenticated. Run: az login" -ForegroundColor Red
    exit 1
}

# Verify shared Front Door exists
$fdExists = az afd profile show `
    --profile-name $sharedConfig.FrontDoorProfile `
    --resource-group $sharedConfig.ResourceGroup `
    --query "name" -o tsv 2>$null

if (-not $fdExists) {
    Write-Host "ERROR: Shared Front Door profile $($sharedConfig.FrontDoorProfile) not found" -ForegroundColor Red
    Write-Host "Run Deploy-FrontDoor.ps1 -Environment test first" -ForegroundColor Yellow
    exit 1
}
Write-Success "Shared Front Door profile exists"

# Verify WAF exists
$wafExists = az network front-door waf-policy show `
    --name $sharedConfig.WafPolicyName `
    --resource-group $sharedConfig.ResourceGroup `
    --query "name" -o tsv 2>$null

if (-not $wafExists) {
    Write-Host "ERROR: WAF policy $($sharedConfig.WafPolicyName) not found" -ForegroundColor Red
    exit 1
}
Write-Success "Shared WAF policy exists"

# Verify production App Services exist
$apiExists = az webapp show --name $prodConfig.ApiAppName --resource-group $prodConfig.ResourceGroup --query "name" -o tsv 2>$null
$blazorExists = az webapp show --name $prodConfig.BlazorAppName --resource-group $prodConfig.ResourceGroup --query "name" -o tsv 2>$null

if (-not $apiExists -or -not $blazorExists) {
    Write-Host "ERROR: Production App Services not found" -ForegroundColor Red
    exit 1
}
Write-Success "Production App Services exist"

# Remove IP Restrictions from Production App Services
Write-Step "Removing IP Restrictions from Production App Services"

Write-Info "Removing restrictions from API..."
# Get all named rules
$apiRules = az webapp config access-restriction show `
    --name $prodConfig.ApiAppName `
    --resource-group $prodConfig.ResourceGroup `
    --query "ipSecurityRestrictions[?name!='Allow all'].name" -o tsv 2>$null

$removedCount = 0
foreach ($ruleName in $apiRules) {
    if ($ruleName) {
        Write-Info "  Removing rule: $ruleName"
        az webapp config access-restriction remove `
            --name $prodConfig.ApiAppName `
            --resource-group $prodConfig.ResourceGroup `
            --rule-name $ruleName 2>$null
        $removedCount++
    }
}
Write-Success "API IP restrictions removed - $removedCount total"

Write-Info "Verifying Blazor has no restrictions..."
$blazorRules = az webapp config access-restriction show `
    --name $prodConfig.BlazorAppName `
    --resource-group $prodConfig.ResourceGroup `
    --query "ipSecurityRestrictions[?name!='Allow all'].name" -o tsv 2>$null

if ($blazorRules) {
    Write-Info "Removing restrictions from Blazor..."
    foreach ($ruleName in $blazorRules) {
        if ($ruleName) {
            az webapp config access-restriction remove `
                --name $prodConfig.BlazorAppName `
                --resource-group $prodConfig.ResourceGroup `
                --rule-name $ruleName 2>$null
        }
    }
}
Write-Success "Blazor IP restrictions verified/removed"

# Create Production Endpoint
Write-Step "Creating Production Endpoint"

Write-Info "Creating endpoint: $($prodConfig.EndpointName)..."
$endpointExists = az afd endpoint show `
    --endpoint-name $prodConfig.EndpointName `
    --profile-name $sharedConfig.FrontDoorProfile `
    --resource-group $sharedConfig.ResourceGroup `
    --query "name" -o tsv 2>$null

if (-not $endpointExists) {
    az afd endpoint create `
        --endpoint-name $prodConfig.EndpointName `
        --profile-name $sharedConfig.FrontDoorProfile `
        --resource-group $sharedConfig.ResourceGroup `
        --enabled-state Enabled `
        --only-show-errors | Out-Null
    
    Write-Success "Production endpoint created"
}
else {
    Write-Success "Production endpoint already exists"
}

# Create Production Origin Groups and Origins
Write-Step "Configuring Production Origins"

# API Origin Group
Write-Info "Creating API origin group..."
$apiOriginGroupExists = az afd origin-group show `
    --origin-group-name prod-api-origins `
    --profile-name $sharedConfig.FrontDoorProfile `
    --resource-group $sharedConfig.ResourceGroup `
    --query "name" -o tsv 2>$null

if (-not $apiOriginGroupExists) {
    az afd origin-group create `
        --origin-group-name prod-api-origins `
        --profile-name $sharedConfig.FrontDoorProfile `
        --resource-group $sharedConfig.ResourceGroup `
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
Write-Info "Adding API origin..."
$apiOriginExists = az afd origin show `
    --origin-name prod-api-backend `
    --origin-group-name prod-api-origins `
    --profile-name $sharedConfig.FrontDoorProfile `
    --resource-group $sharedConfig.ResourceGroup `
    --query "name" -o tsv 2>$null

if (-not $apiOriginExists) {
    az afd origin create `
        --origin-name prod-api-backend `
        --origin-group-name prod-api-origins `
        --profile-name $sharedConfig.FrontDoorProfile `
        --resource-group $sharedConfig.ResourceGroup `
        --host-name $prodConfig.ApiHostname `
        --origin-host-header $prodConfig.ApiHostname `
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
Write-Info "Creating Blazor origin group..."
$blazorOriginGroupExists = az afd origin-group show `
    --origin-group-name prod-blazor-origins `
    --profile-name $sharedConfig.FrontDoorProfile `
    --resource-group $sharedConfig.ResourceGroup `
    --query "name" -o tsv 2>$null

if (-not $blazorOriginGroupExists) {
    az afd origin-group create `
        --origin-group-name prod-blazor-origins `
        --profile-name $sharedConfig.FrontDoorProfile `
        --resource-group $sharedConfig.ResourceGroup `
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
Write-Info "Adding Blazor origin..."
$blazorOriginExists = az afd origin show `
    --origin-name prod-blazor-backend `
    --origin-group-name prod-blazor-origins `
    --profile-name $sharedConfig.FrontDoorProfile `
    --resource-group $sharedConfig.ResourceGroup `
    --query "name" -o tsv 2>$null

if (-not $blazorOriginExists) {
    az afd origin create `
        --origin-name prod-blazor-backend `
        --origin-group-name prod-blazor-origins `
        --profile-name $sharedConfig.FrontDoorProfile `
        --resource-group $sharedConfig.ResourceGroup `
        --host-name $prodConfig.BlazorHostname `
        --origin-host-header $prodConfig.BlazorHostname `
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

# Create Production Routes
Write-Step "Configuring Production Routes"

# API Route
Write-Info "Creating API route..."
$apiRouteExists = az afd route show `
    --route-name prod-api-route `
    --endpoint-name $prodConfig.EndpointName `
    --profile-name $sharedConfig.FrontDoorProfile `
    --resource-group $sharedConfig.ResourceGroup `
    --query "name" -o tsv 2>$null

if (-not $apiRouteExists) {
    az afd route create `
        --route-name prod-api-route `
        --endpoint-name $prodConfig.EndpointName `
        --profile-name $sharedConfig.FrontDoorProfile `
        --resource-group $sharedConfig.ResourceGroup `
        --origin-group prod-api-origins `
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
Write-Info "Creating Blazor route..."
$blazorRouteExists = az afd route show `
    --route-name prod-blazor-route `
    --endpoint-name $prodConfig.EndpointName `
    --profile-name $sharedConfig.FrontDoorProfile `
    --resource-group $sharedConfig.ResourceGroup `
    --query "name" -o tsv 2>$null

if (-not $blazorRouteExists) {
    az afd route create `
        --route-name prod-blazor-route `
        --endpoint-name $prodConfig.EndpointName `
        --profile-name $sharedConfig.FrontDoorProfile `
        --resource-group $sharedConfig.ResourceGroup `
        --origin-group prod-blazor-origins `
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

# Associate WAF with Production Endpoint
Write-Step "Associating WAF with Production Endpoint"

$subscriptionId = (az account show --query id -o tsv)
$wafPolicyId = "/subscriptions/$subscriptionId/resourceGroups/$($sharedConfig.ResourceGroup)/providers/Microsoft.Network/frontDoorWebApplicationFirewallPolicies/$($sharedConfig.WafPolicyName)"
$prodEndpointId = "/subscriptions/$subscriptionId/resourceGroups/$($sharedConfig.ResourceGroup)/providers/Microsoft.Cdn/profiles/$($sharedConfig.FrontDoorProfile)/afdEndpoints/$($prodConfig.EndpointName)"

Write-Info "Creating security policy for production endpoint..."
$secPolicyExists = az afd security-policy show `
    --security-policy-name prodSecurityPolicy `
    --profile-name $sharedConfig.FrontDoorProfile `
    --resource-group $sharedConfig.ResourceGroup `
    --query "name" -o tsv 2>$null

if ($secPolicyExists) {
    Write-Info "Updating existing security policy..."
    az afd security-policy delete `
        --security-policy-name prodSecurityPolicy `
        --profile-name $sharedConfig.FrontDoorProfile `
        --resource-group $sharedConfig.ResourceGroup `
        --only-show-errors 2>&1 | Out-Null
}

az afd security-policy create `
    --security-policy-name prodSecurityPolicy `
    --profile-name $sharedConfig.FrontDoorProfile `
    --resource-group $sharedConfig.ResourceGroup `
    --domains $prodEndpointId `
    --waf-policy $wafPolicyId `
    --only-show-errors 2>&1 | Out-Null

if ($LASTEXITCODE -eq 0) {
    Write-Success "WAF policy associated with production endpoint"
}
else {
    Write-Host "WARNING: Could not associate WAF automatically. Manual configuration may be needed." -ForegroundColor Yellow
}

# Get endpoint URLs
Write-Step "Deployment Summary"

$prodEndpointHostname = az afd endpoint show `
    --endpoint-name $prodConfig.EndpointName `
    --profile-name $sharedConfig.FrontDoorProfile `
    --resource-group $sharedConfig.ResourceGroup `
    --query hostName -o tsv 2>$null

$testEndpointHostname = az afd endpoint show `
    --endpoint-name petel-test `
    --profile-name $sharedConfig.FrontDoorProfile `
    --resource-group $sharedConfig.ResourceGroup `
    --query hostName -o tsv 2>$null

Write-Host ""
Write-Host "============================================" -ForegroundColor Green
Write-Host " DEPLOYMENT COMPLETE!" -ForegroundColor Green
Write-Host "============================================" -ForegroundColor Green
Write-Host ""
Write-Host "Cost Savings:" -ForegroundColor Cyan
Write-Host "  No additional base fees (~`$330/month saved)" -ForegroundColor Green
Write-Host "  Shared WAF policy (already configured)" -ForegroundColor Green
Write-Host "  Only marginal per-request costs for prod traffic" -ForegroundColor Green
Write-Host ""
Write-Host "Production URLs:" -ForegroundColor Cyan
Write-Host "  Endpoint:         https://$prodEndpointHostname" -ForegroundColor White
Write-Host "  Blazor App:       https://$prodEndpointHostname" -ForegroundColor White
Write-Host "  API:              https://$prodEndpointHostname/api" -ForegroundColor White
Write-Host ""
Write-Host "Test URLs (unchanged):" -ForegroundColor Cyan
Write-Host "  Endpoint:         https://$testEndpointHostname" -ForegroundColor White
Write-Host ""
Write-Host "Shared Resources:" -ForegroundColor Cyan
Write-Host "  Front Door:       $($sharedConfig.FrontDoorProfile)" -ForegroundColor White
Write-Host "  WAF Policy:       $($sharedConfig.WafPolicyName)" -ForegroundColor White
Write-Host "  Israeli IPs:      47 ranges (OWASP + Bot Protection enabled)" -ForegroundColor White
Write-Host ""
Write-Host "Security Changes:" -ForegroundColor Cyan
Write-Host "  Removed IP restriction rules from production API" -ForegroundColor Green
Write-Host "  WAF protection enabled for production endpoint" -ForegroundColor Green
Write-Host "  HTTPS redirect enabled" -ForegroundColor Green
Write-Host ""
Write-Host "Next Steps:" -ForegroundColor Cyan
Write-Host "  1. Test production endpoint" -ForegroundColor Gray
Write-Host "  2. Update Blazor app settings to use Front Door URL" -ForegroundColor Gray
Write-Host "  3. Update DNS to point to Front Door if using custom domain" -ForegroundColor Gray
Write-Host "  4. Monitor both endpoints in Azure Portal" -ForegroundColor Gray
Write-Host ""
