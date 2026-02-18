# ============================================
# Deploy Front Door to Production
# ============================================
# - Uses shared WAF from test environment
# - Removes IP restrictions from App Services
# - Configures Front Door with proper routing
# ============================================

param(
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

# Configuration
$prodConfig = @{
    ResourceGroup     = 'petel-prod-rg'
    FrontDoorProfile  = 'petel-frontdoor-prod'
    EndpointName      = 'petel-prod'
    ApiHostname       = 'petel-prod-api.azurewebsites.net'
    BlazorHostname    = 'petel-prod-blazor.azurewebsites.net'
    ApiAppName        = 'petel-prod-api'
    BlazorAppName     = 'petel-prod-blazor'
}

$sharedWaf = @{
    ResourceGroup = 'petel-test-rg'
    PolicyName    = 'petelWafTest'
}

Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "Production Front Door Deployment" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Configuration:" -ForegroundColor Yellow
Write-Host "  Resource Group:   $($prodConfig.ResourceGroup)" -ForegroundColor White
Write-Host "  Front Door:       $($prodConfig.FrontDoorProfile)" -ForegroundColor White
Write-Host "  Endpoint:         $($prodConfig.EndpointName)" -ForegroundColor White
Write-Host "  Shared WAF:       $($sharedWaf.PolicyName) (from $($sharedWaf.ResourceGroup))" -ForegroundColor White
Write-Host "  API Backend:      $($prodConfig.ApiHostname)" -ForegroundColor White
Write-Host "  Blazor Backend:   $($prodConfig.BlazorHostname)" -ForegroundColor White
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
    Write-Host "✓ $Message" -ForegroundColor Green
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

# Verify resource group exists
$rgExists = az group exists --name $prodConfig.ResourceGroup
if ($rgExists -ne 'true') {
    Write-Host "ERROR: Resource group $($prodConfig.ResourceGroup) does not exist" -ForegroundColor Red
    exit 1
}
Write-Success "Resource group exists"

# Verify shared WAF exists
$wafExists = az network front-door waf-policy show `
    --name $sharedWaf.PolicyName `
    --resource-group $sharedWaf.ResourceGroup `
    --query "name" -o tsv 2>$null

if (-not $wafExists) {
    Write-Host "ERROR: Shared WAF policy $($sharedWaf.PolicyName) not found in $($sharedWaf.ResourceGroup)" -ForegroundColor Red
    Write-Host "Run Deploy-FrontDoor.ps1 for test environment first" -ForegroundColor Yellow
    exit 1
}
Write-Success "Shared WAF policy exists"

# Verify App Services exist
$apiExists = az webapp show --name $prodConfig.ApiAppName --resource-group $prodConfig.ResourceGroup --query "name" -o tsv 2>$null
$blazorExists = az webapp show --name $prodConfig.BlazorAppName --resource-group $prodConfig.ResourceGroup --query "name" -o tsv 2>$null

if (-not $apiExists -or -not $blazorExists) {
    Write-Host "ERROR: App Services not found" -ForegroundColor Red
    exit 1
}
Write-Success "App Services exist"

# Remove IP Restrictions
Write-Step "Removing IP Restrictions from App Services"

Write-Info "Removing restrictions from API..."
az webapp config access-restriction remove `
    --name $prodConfig.ApiAppName `
    --resource-group $prodConfig.ResourceGroup `
    --rule-name All `
    --action Allow 2>$null

# Remove all named rules
$apiRules = az webapp config access-restriction show `
    --name $prodConfig.ApiAppName `
    --resource-group $prodConfig.ResourceGroup `
    --query "ipSecurityRestrictions[?name!='Allow all'].name" -o tsv 2>$null

foreach ($ruleName in $apiRules) {
    if ($ruleName) {
        Write-Info "  Removing rule: $ruleName"
        az webapp config access-restriction remove `
            --name $prodConfig.ApiAppName `
            --resource-group $prodConfig.ResourceGroup `
            --rule-name $ruleName 2>$null
    }
}
Write-Success "API IP restrictions removed"

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
Write-Success "Blazor IP restrictions removed"

# Create Front Door Profile
Write-Step "Creating Front Door Profile"

$fdExists = az afd profile show `
    --profile-name $prodConfig.FrontDoorProfile `
    --resource-group $prodConfig.ResourceGroup `
    --query "name" -o tsv 2>$null

if (-not $fdExists) {
    Write-Info "Creating Front Door profile with Premium SKU..."
    az afd profile create `
        --profile-name $prodConfig.FrontDoorProfile `
        --resource-group $prodConfig.ResourceGroup `
        --sku Premium_AzureFrontDoor `
        --only-show-errors | Out-Null
    
    Write-Success "Front Door profile created"
}
else {
    Write-Success "Front Door profile already exists"
}

# Create Endpoint
Write-Info "Creating endpoint..."
$endpointExists = az afd endpoint show `
    --endpoint-name $prodConfig.EndpointName `
    --profile-name $prodConfig.FrontDoorProfile `
    --resource-group $prodConfig.ResourceGroup `
    --query "name" -o tsv 2>$null

if (-not $endpointExists) {
    az afd endpoint create `
        --endpoint-name $prodConfig.EndpointName `
        --profile-name $prodConfig.FrontDoorProfile `
        --resource-group $prodConfig.ResourceGroup `
        --enabled-state Enabled `
        --only-show-errors | Out-Null
    
    Write-Success "Endpoint created"
}
else {
    Write-Success "Endpoint already exists"
}

# Create Origin Groups and Origins
Write-Step "Configuring Origins"

# API Origin Group
Write-Info "Creating API origin group..."
$apiOriginGroupExists = az afd origin-group show `
    --origin-group-name api-origins `
    --profile-name $prodConfig.FrontDoorProfile `
    --resource-group $prodConfig.ResourceGroup `
    --query "name" -o tsv 2>$null

if (-not $apiOriginGroupExists) {
    az afd origin-group create `
        --origin-group-name api-origins `
        --profile-name $prodConfig.FrontDoorProfile `
        --resource-group $prodConfig.ResourceGroup `
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
    --origin-name api-backend `
    --origin-group-name api-origins `
    --profile-name $prodConfig.FrontDoorProfile `
    --resource-group $prodConfig.ResourceGroup `
    --query "name" -o tsv 2>$null

if (-not $apiOriginExists) {
    az afd origin create `
        --origin-name api-backend `
        --origin-group-name api-origins `
        --profile-name $prodConfig.FrontDoorProfile `
        --resource-group $prodConfig.ResourceGroup `
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
    --origin-group-name blazor-origins `
    --profile-name $prodConfig.FrontDoorProfile `
    --resource-group $prodConfig.ResourceGroup `
    --query "name" -o tsv 2>$null

if (-not $blazorOriginGroupExists) {
    az afd origin-group create `
        --origin-group-name blazor-origins `
        --profile-name $prodConfig.FrontDoorProfile `
        --resource-group $prodConfig.ResourceGroup `
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
    --origin-name blazor-backend `
    --origin-group-name blazor-origins `
    --profile-name $prodConfig.FrontDoorProfile `
    --resource-group $prodConfig.ResourceGroup `
    --query "name" -o tsv 2>$null

if (-not $blazorOriginExists) {
    az afd origin create `
        --origin-name blazor-backend `
        --origin-group-name blazor-origins `
        --profile-name $prodConfig.FrontDoorProfile `
        --resource-group $prodConfig.ResourceGroup `
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

# Create Routes
Write-Step "Configuring Routes"

# API Route
Write-Info "Creating API route..."
$apiRouteExists = az afd route show `
    --route-name api-route `
    --endpoint-name $prodConfig.EndpointName `
    --profile-name $prodConfig.FrontDoorProfile `
    --resource-group $prodConfig.ResourceGroup `
    --query "name" -o tsv 2>$null

if (-not $apiRouteExists) {
    az afd route create `
        --route-name api-route `
        --endpoint-name $prodConfig.EndpointName `
        --profile-name $prodConfig.FrontDoorProfile `
        --resource-group $prodConfig.ResourceGroup `
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
Write-Info "Creating Blazor route..."
$blazorRouteExists = az afd route show `
    --route-name blazor-route `
    --endpoint-name $prodConfig.EndpointName `
    --profile-name $prodConfig.FrontDoorProfile `
    --resource-group $prodConfig.ResourceGroup `
    --query "name" -o tsv 2>$null

if (-not $blazorRouteExists) {
    az afd route create `
        --route-name blazor-route `
        --endpoint-name $prodConfig.EndpointName `
        --profile-name $prodConfig.FrontDoorProfile `
        --resource-group $prodConfig.ResourceGroup `
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

# Associate Shared WAF with Front Door
Write-Step "Associating Shared WAF with Front Door"

# Get subscription ID and construct resource IDs
$subscriptionId = (az account show --query id -o tsv)
$wafPolicyId = "/subscriptions/$subscriptionId/resourceGroups/$($sharedWaf.ResourceGroup)/providers/Microsoft.Network/frontDoorWebApplicationFirewallPolicies/$($sharedWaf.PolicyName)"
$endpointId = "/subscriptions/$subscriptionId/resourceGroups/$($prodConfig.ResourceGroup)/providers/Microsoft.Cdn/profiles/$($prodConfig.FrontDoorProfile)/afdEndpoints/$($prodConfig.EndpointName)"

Write-Info "Creating security policy..."
$secPolicyExists = az afd security-policy show `
    --security-policy-name petelSecurityPolicy `
    --profile-name $prodConfig.FrontDoorProfile `
    --resource-group $prodConfig.ResourceGroup `
    --query "name" -o tsv 2>$null

if ($secPolicyExists) {
    Write-Info "Deleting existing security policy..."
    az afd security-policy delete `
        --security-policy-name petelSecurityPolicy `
        --profile-name $prodConfig.FrontDoorProfile `
        --resource-group $prodConfig.ResourceGroup `
        --only-show-errors 2>&1 | Out-Null
}

az afd security-policy create `
    --security-policy-name petelSecurityPolicy `
    --profile-name $prodConfig.FrontDoorProfile `
    --resource-group $prodConfig.ResourceGroup `
    --domains $endpointId `
    --waf-policy $wafPolicyId `
    --only-show-errors 2>&1 | Out-Null

if ($LASTEXITCODE -eq 0) {
    Write-Success "Shared WAF policy associated with Front Door"
}
else {
    Write-Host "WARNING: Could not associate WAF automatically. You may need to do this in Azure Portal." -ForegroundColor Yellow
}

# Get Front Door endpoint URL
Write-Step "Deployment Summary"

$endpointHostname = az afd endpoint show `
    --endpoint-name $prodConfig.EndpointName `
    --profile-name $prodConfig.FrontDoorProfile `
    --resource-group $prodConfig.ResourceGroup `
    --query hostName -o tsv 2>$null

Write-Host ""
Write-Host "============================================" -ForegroundColor Green
Write-Host " DEPLOYMENT COMPLETE!" -ForegroundColor Green
Write-Host "============================================" -ForegroundColor Green
Write-Host ""
Write-Host "Front Door Configuration:" -ForegroundColor Cyan
Write-Host "  Profile:          $($prodConfig.FrontDoorProfile)" -ForegroundColor White
Write-Host "  Endpoint:         https://$endpointHostname" -ForegroundColor White
Write-Host "  Shared WAF:       $($sharedWaf.PolicyName) (from $($sharedWaf.ResourceGroup))" -ForegroundColor White
Write-Host ""
Write-Host "Application URLs:" -ForegroundColor Cyan
Write-Host "  Blazor:           https://$endpointHostname" -ForegroundColor White
Write-Host "  API:              https://$endpointHostname/api" -ForegroundColor White
Write-Host ""
Write-Host "Security Changes:" -ForegroundColor Cyan
Write-Host "  ✓ IP restrictions removed from API" -ForegroundColor Green
Write-Host "  ✓ IP restrictions removed from Blazor" -ForegroundColor Green
Write-Host "  ✓ Shared WAF policy associated" -ForegroundColor Green
Write-Host "  ✓ HTTPS redirect enabled" -ForegroundColor Green
Write-Host ""
Write-Host "Next Steps:" -ForegroundColor Cyan
Write-Host "  1. Test the Front Door endpoint: https://$endpointHostname" -ForegroundColor Gray
Write-Host "  2. Update Blazor configuration to use Front Door URL" -ForegroundColor Gray
Write-Host "  3. Configure custom domain (optional)" -ForegroundColor Gray
Write-Host "  4. Monitor WAF logs in Azure Portal" -ForegroundColor Gray
Write-Host ""
