# ============================================================
# PetelATH Production — Azure Front Door Deployment
# ============================================================
# Creates a Front Door Premium profile that routes ONLY to the
# Blazor server. The API is fully private — accessible only from
# the Blazor App Service (server-to-server), not from the web.
#
# Traffic flow:
#   Browser (IL only) -> Front Door WAF -> Blazor App Service
#                                       -> API App Service (internal only)
#
# WAF enforces Israel-only access via GeoMatch (country = IL).
# No Israeli IP ranges needed — GeoMatch is maintained by Microsoft.
#
# Usage:
#   .\Deploy-ATH-FrontDoor-Prod.ps1           # full deploy
#   .\Deploy-ATH-FrontDoor-Prod.ps1 -WafOnly  # update WAF rules only
#   .\Deploy-ATH-FrontDoor-Prod.ps1 -DryRun   # print config, no changes
# ============================================================

param(
    [switch]$WafOnly,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"
$ProgressPreference    = "SilentlyContinue"

# ── Configuration ────────────────────────────────────────────
$cfg = @{
    ResourceGroup    = 'petel-prod-rg'
    FrontDoorProfile = 'petel-frontdoor-prod'
    EndpointName     = 'petel-prod'
    WafPolicyName    = 'petelWafProd'
    SecurityPolicy   = 'petelSecurityPolicy'
    BlazorAppName    = 'petel-prod-blazor'
    ApiAppName       = 'petel-prod-api'
    BlazorHostname   = 'petel-prod-blazor.azurewebsites.net'
}

# ── Helpers ──────────────────────────────────────────────────
function Write-Step   { param([string]$m) Write-Host "`n$m" -ForegroundColor Yellow; Write-Host ("─" * $m.Length) -ForegroundColor Yellow }
function Write-Ok     { param([string]$m) Write-Host "  OK  $m" -ForegroundColor Green }
function Write-Skip   { param([string]$m) Write-Host "  --  $m" -ForegroundColor DarkGray }
function Write-Warn   { param([string]$m) Write-Host "  !!  $m" -ForegroundColor Yellow }
function Write-Err    { param([string]$m) Write-Host "  ERR $m" -ForegroundColor Red }

function Invoke-Az {
    param([string[]]$Args)
    if ($DryRun) { Write-Host "  [DRY RUN] az $($Args -join ' ')" -ForegroundColor DarkCyan; return $null }
    $result = az @Args 2>&1
    if ($LASTEXITCODE -ne 0) { throw "az $($Args[0..2] -join ' ') failed: $result" }
    return $result
}

function Test-AzResource {
    param([string[]]$Args)
    az @Args --only-show-errors 2>$null | Out-Null
    return $LASTEXITCODE -eq 0
}

# ── Banner ────────────────────────────────────────────────────
Write-Host ""
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "  PetelATH Production — Azure Front Door Deployment" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan
if ($DryRun)  { Write-Host "  DRY RUN MODE — no resources will be created" -ForegroundColor Yellow }
if ($WafOnly) { Write-Host "  WAF-ONLY MODE — skipping Front Door infrastructure" -ForegroundColor Yellow }
Write-Host ""
Write-Host "  Resource Group:    $($cfg.ResourceGroup)"    -ForegroundColor White
Write-Host "  Front Door:        $($cfg.FrontDoorProfile)" -ForegroundColor White
Write-Host "  WAF Policy:        $($cfg.WafPolicyName)"    -ForegroundColor White
Write-Host "  Blazor Backend:    $($cfg.BlazorHostname)"   -ForegroundColor White
Write-Host "  API (private):     $($cfg.ApiAppName).azurewebsites.net (no FD route)" -ForegroundColor White
Write-Host ""

# ── Prerequisites ─────────────────────────────────────────────
Write-Step "Verifying Prerequisites"

try { az account show --only-show-errors | Out-Null; Write-Ok "Azure CLI authenticated" }
catch { Write-Err "Not authenticated. Run: az login"; exit 1 }

$rgOk = az group exists --name $cfg.ResourceGroup
if ($rgOk -ne 'true') { Write-Err "Resource group '$($cfg.ResourceGroup)' not found"; exit 1 }
Write-Ok "Resource group exists"

if (-not $WafOnly) {
    $blazorExists = az webapp show --name $cfg.BlazorAppName --resource-group $cfg.ResourceGroup --query "name" -o tsv 2>$null
    if (-not $blazorExists) { Write-Err "Blazor App Service '$($cfg.BlazorAppName)' not found"; exit 1 }
    Write-Ok "Blazor App Service exists"

    $apiExists = az webapp show --name $cfg.ApiAppName --resource-group $cfg.ResourceGroup --query "name" -o tsv 2>$null
    if (-not $apiExists) { Write-Err "API App Service '$($cfg.ApiAppName)' not found"; exit 1 }
    Write-Ok "API App Service exists"
}

# ── Step 1: WAF Policy ────────────────────────────────────────
Write-Step "Step 1: WAF Policy"

$wafExists = Test-AzResource @("network", "front-door", "waf-policy", "show",
    "--name", $cfg.WafPolicyName, "--resource-group", $cfg.ResourceGroup)

if (-not $wafExists) {
    Write-Host "  Creating WAF policy (Premium_AzureFrontDoor, Prevention mode)..." -ForegroundColor Gray
    Invoke-Az @("network", "front-door", "waf-policy", "create",
        "--name", $cfg.WafPolicyName,
        "--resource-group", $cfg.ResourceGroup,
        "--sku", "Premium_AzureFrontDoor",
        "--mode", "Prevention",
        "--only-show-errors") | Out-Null
    Write-Ok "WAF policy created"
} else {
    Write-Skip "WAF policy already exists"
}

# Managed rule sets
Write-Host "  Configuring managed rule sets..." -ForegroundColor Gray

# OWASP / Default Rule Set
$owaspResult = az network front-door waf-policy managed-rule-set add `
    --policy-name $cfg.WafPolicyName `
    --resource-group $cfg.ResourceGroup `
    --type "Microsoft_DefaultRuleSet" `
    --version "2.1" `
    --only-show-errors 2>&1

if ($LASTEXITCODE -eq 0) { Write-Ok "OWASP DefaultRuleSet 2.1 enabled" }
else { Write-Warn "OWASP rules may already be configured (skipping)" }

# Bot Protection
$botResult = az network front-door waf-policy managed-rule-set add `
    --policy-name $cfg.WafPolicyName `
    --resource-group $cfg.ResourceGroup `
    --type "Microsoft_BotManagerRuleSet" `
    --version "1.0" `
    --only-show-errors 2>&1

if ($LASTEXITCODE -eq 0) { Write-Ok "BotManagerRuleSet 1.0 enabled" }
else { Write-Warn "Bot rules may already be configured (skipping)" }

# Custom rule: Block non-Israeli traffic via GeoMatch
Write-Host "  Configuring Israel-only GeoMatch block rule..." -ForegroundColor Gray

$geoRuleExists = Test-AzResource @("network", "front-door", "waf-policy", "rule", "show",
    "--policy-name", $cfg.WafPolicyName, "--resource-group", $cfg.ResourceGroup, "--name", "BlockNonIsrael")

if ($geoRuleExists) {
    Write-Host "  Removing existing GeoMatch rule for update..." -ForegroundColor Gray
    if (-not $DryRun) {
        az network front-door waf-policy rule delete `
            --policy-name $cfg.WafPolicyName `
            --resource-group $cfg.ResourceGroup `
            --name "BlockNonIsrael" `
            --only-show-errors | Out-Null
    }
}

# Create the block rule (deferred — match condition added next)
if (-not $DryRun) {
    az network front-door waf-policy rule create `
        --policy-name $cfg.WafPolicyName `
        --resource-group $cfg.ResourceGroup `
        --name "BlockNonIsrael" `
        --rule-type "MatchRule" `
        --action "Block" `
        --priority 100 `
        --defer `
        --only-show-errors | Out-Null

    # Add match condition: block when country is NOT Israel
    az network front-door waf-policy rule match-condition add `
        --policy-name $cfg.WafPolicyName `
        --resource-group $cfg.ResourceGroup `
        --name "BlockNonIsrael" `
        --match-variable "RemoteAddr" `
        --operator "GeoMatch" `
        --negate true `
        --values "IL" `
        --only-show-errors | Out-Null

    if ($LASTEXITCODE -eq 0) { Write-Ok "GeoMatch block rule (non-IL -> 403) configured" }
    else { Write-Warn "GeoMatch rule may need manual verification in Azure Portal" }
} else {
    Write-Host "  [DRY RUN] Would create GeoMatch block rule: non-IL -> Block (priority 100)" -ForegroundColor DarkCyan
}

if ($WafOnly) {
    Write-Host ""
    Write-Host "WAF-only update complete." -ForegroundColor Green
    Write-Host "  Policy: $($cfg.WafPolicyName) in $($cfg.ResourceGroup)" -ForegroundColor White
    exit 0
}

# ── Step 2: Front Door Profile ────────────────────────────────
Write-Step "Step 2: Front Door Profile"

$fdExists = Test-AzResource @("afd", "profile", "show",
    "--profile-name", $cfg.FrontDoorProfile, "--resource-group", $cfg.ResourceGroup)

if (-not $fdExists) {
    Write-Host "  Creating Front Door Premium profile (may take 2-3 minutes)..." -ForegroundColor Gray
    Invoke-Az @("afd", "profile", "create",
        "--profile-name", $cfg.FrontDoorProfile,
        "--resource-group", $cfg.ResourceGroup,
        "--sku", "Premium_AzureFrontDoor",
        "--only-show-errors") | Out-Null
    Write-Ok "Front Door profile created"
} else {
    Write-Skip "Front Door profile already exists"
}

# ── Step 3: Endpoint ──────────────────────────────────────────
Write-Step "Step 3: Endpoint"

$epExists = Test-AzResource @("afd", "endpoint", "show",
    "--endpoint-name", $cfg.EndpointName,
    "--profile-name", $cfg.FrontDoorProfile,
    "--resource-group", $cfg.ResourceGroup)

if (-not $epExists) {
    Invoke-Az @("afd", "endpoint", "create",
        "--endpoint-name", $cfg.EndpointName,
        "--profile-name", $cfg.FrontDoorProfile,
        "--resource-group", $cfg.ResourceGroup,
        "--enabled-state", "Enabled",
        "--only-show-errors") | Out-Null
    Write-Ok "Endpoint created"
} else {
    Write-Skip "Endpoint already exists"
}

# ── Step 4: Origin Group (Blazor only) ───────────────────────
Write-Step "Step 4: Origin Group — Blazor"

$ogExists = Test-AzResource @("afd", "origin-group", "show",
    "--origin-group-name", "blazor-origins",
    "--profile-name", $cfg.FrontDoorProfile,
    "--resource-group", $cfg.ResourceGroup)

if (-not $ogExists) {
    Invoke-Az @("afd", "origin-group", "create",
        "--origin-group-name", "blazor-origins",
        "--profile-name", $cfg.FrontDoorProfile,
        "--resource-group", $cfg.ResourceGroup,
        "--probe-request-type", "HEAD",
        "--probe-protocol", "Https",
        "--probe-path", "/",
        "--probe-interval-in-seconds", "30",
        "--sample-size", "4",
        "--successful-samples-required", "3",
        "--additional-latency-in-milliseconds", "50",
        "--only-show-errors") | Out-Null
    Write-Ok "blazor-origins origin group created"
} else {
    Write-Skip "blazor-origins already exists"
}

$originExists = Test-AzResource @("afd", "origin", "show",
    "--origin-name", "blazor-backend",
    "--origin-group-name", "blazor-origins",
    "--profile-name", $cfg.FrontDoorProfile,
    "--resource-group", $cfg.ResourceGroup)

if (-not $originExists) {
    Invoke-Az @("afd", "origin", "create",
        "--origin-name", "blazor-backend",
        "--origin-group-name", "blazor-origins",
        "--profile-name", $cfg.FrontDoorProfile,
        "--resource-group", $cfg.ResourceGroup,
        "--host-name", $cfg.BlazorHostname,
        "--origin-host-header", $cfg.BlazorHostname,
        "--priority", "1",
        "--weight", "1000",
        "--enabled-state", "Enabled",
        "--http-port", "80",
        "--https-port", "443",
        "--only-show-errors") | Out-Null
    Write-Ok "blazor-backend origin added"
} else {
    Write-Skip "blazor-backend origin already exists"
}

# ── Step 5: Route ─────────────────────────────────────────────
Write-Step "Step 5: Route (/* -> Blazor)"

$routeExists = Test-AzResource @("afd", "route", "show",
    "--route-name", "blazor-route",
    "--endpoint-name", $cfg.EndpointName,
    "--profile-name", $cfg.FrontDoorProfile,
    "--resource-group", $cfg.ResourceGroup)

if (-not $routeExists) {
    Invoke-Az @("afd", "route", "create",
        "--route-name", "blazor-route",
        "--endpoint-name", $cfg.EndpointName,
        "--profile-name", $cfg.FrontDoorProfile,
        "--resource-group", $cfg.ResourceGroup,
        "--origin-group", "blazor-origins",
        "--supported-protocols", "Https",
        "--patterns-to-match", "/*",
        "--forwarding-protocol", "HttpsOnly",
        "--https-redirect", "Enabled",
        "--link-to-default-domain", "Enabled",
        "--only-show-errors") | Out-Null
    Write-Ok "blazor-route created (/* -> blazor-origins, HTTPS only)"
} else {
    Write-Skip "blazor-route already exists"
}

# ── Step 6: Security Policy (WAF <-> Endpoint) ────────────────
Write-Step "Step 6: Security Policy"

$subscriptionId = (az account show --query id -o tsv)
$wafPolicyId  = "/subscriptions/$subscriptionId/resourceGroups/$($cfg.ResourceGroup)/providers/Microsoft.Network/frontDoorWebApplicationFirewallPolicies/$($cfg.WafPolicyName)"
$endpointId   = "/subscriptions/$subscriptionId/resourceGroups/$($cfg.ResourceGroup)/providers/Microsoft.Cdn/profiles/$($cfg.FrontDoorProfile)/afdEndpoints/$($cfg.EndpointName)"

$spExists = Test-AzResource @("afd", "security-policy", "show",
    "--security-policy-name", $cfg.SecurityPolicy,
    "--profile-name", $cfg.FrontDoorProfile,
    "--resource-group", $cfg.ResourceGroup)

if ($spExists) {
    Write-Host "  Removing existing security policy for update..." -ForegroundColor Gray
    if (-not $DryRun) {
        az afd security-policy delete `
            --security-policy-name $cfg.SecurityPolicy `
            --profile-name $cfg.FrontDoorProfile `
            --resource-group $cfg.ResourceGroup `
            --only-show-errors | Out-Null
    }
}

Invoke-Az @("afd", "security-policy", "create",
    "--security-policy-name", $cfg.SecurityPolicy,
    "--profile-name", $cfg.FrontDoorProfile,
    "--resource-group", $cfg.ResourceGroup,
    "--domains", $endpointId,
    "--waf-policy", $wafPolicyId,
    "--only-show-errors") | Out-Null
Write-Ok "WAF policy associated with Front Door endpoint"

# ── Step 7: Lock Blazor to Front Door only ────────────────────
Write-Step "Step 7: App Service Lockdown — Blazor (Allow AzureFrontDoor.Backend only)"

# Remove any existing named rules first so we can set a clean state
Write-Host "  Clearing existing access restriction rules on Blazor..." -ForegroundColor Gray
if (-not $DryRun) {
    $existingRules = az webapp config access-restriction show `
        --name $cfg.BlazorAppName `
        --resource-group $cfg.ResourceGroup `
        --query "ipSecurityRestrictions[?name!='Allow all' && name!='Deny all'].name" -o tsv 2>$null

    foreach ($r in $existingRules) {
        if ($r) {
            az webapp config access-restriction remove `
                --name $cfg.BlazorAppName `
                --resource-group $cfg.ResourceGroup `
                --rule-name $r 2>$null | Out-Null
        }
    }
}

# Allow Azure Front Door Backend service tag
Invoke-Az @("webapp", "config", "access-restriction", "add",
    "--name", $cfg.BlazorAppName,
    "--resource-group", $cfg.ResourceGroup,
    "--rule-name", "AllowFrontDoor",
    "--action", "Allow",
    "--service-tag", "AzureFrontDoor.Backend",
    "--priority", "100") | Out-Null
Write-Ok "AllowFrontDoor rule added (AzureFrontDoor.Backend, priority 100)"

# Deny everything else
Invoke-Az @("webapp", "config", "access-restriction", "add",
    "--name", $cfg.BlazorAppName,
    "--resource-group", $cfg.ResourceGroup,
    "--rule-name", "DenyAll",
    "--action", "Deny",
    "--ip-address", "0.0.0.0/0",
    "--priority", "200") | Out-Null
Write-Ok "DenyAll rule added (0.0.0.0/0, priority 200) — direct access blocked"

# ── Step 8: Lock API to Blazor outbound IPs only ─────────────
Write-Step "Step 8: App Service Lockdown — API (Allow Blazor outbound IPs only)"

Write-Host "  Fetching Blazor outbound IP addresses..." -ForegroundColor Gray
$blazorOutboundIps = @()

if (-not $DryRun) {
    $ipString = az webapp show `
        --name $cfg.BlazorAppName `
        --resource-group $cfg.ResourceGroup `
        --query "outboundIpAddresses" -o tsv 2>$null

    if ($ipString) {
        $blazorOutboundIps = $ipString -split "," | ForEach-Object { $_.Trim() } | Where-Object { $_ }
        Write-Ok "$($blazorOutboundIps.Count) outbound IPs retrieved from Blazor App Service"
    } else {
        Write-Warn "Could not retrieve Blazor outbound IPs — API lockdown skipped"
        Write-Host "  Manually add Blazor outbound IPs to API access restrictions." -ForegroundColor Yellow
    }
} else {
    $blazorOutboundIps = @("10.0.0.1", "10.0.0.2")  # placeholder for dry run output
    Write-Host "  [DRY RUN] Would fetch outbound IPs from $($cfg.BlazorAppName)" -ForegroundColor DarkCyan
}

if ($blazorOutboundIps.Count -gt 0) {
    # Clear existing named rules on API
    Write-Host "  Clearing existing access restriction rules on API..." -ForegroundColor Gray
    if (-not $DryRun) {
        $existingApiRules = az webapp config access-restriction show `
            --name $cfg.ApiAppName `
            --resource-group $cfg.ResourceGroup `
            --query "ipSecurityRestrictions[?name!='Allow all' && name!='Deny all'].name" -o tsv 2>$null

        foreach ($r in $existingApiRules) {
            if ($r) {
                az webapp config access-restriction remove `
                    --name $cfg.ApiAppName `
                    --resource-group $cfg.ResourceGroup `
                    --rule-name $r 2>$null | Out-Null
            }
        }
    }

    # Add one Allow rule per outbound IP
    $ipIndex = 1
    foreach ($ip in $blazorOutboundIps) {
        $ruleName = "AllowBlazor_$ipIndex"
        $priority = 100 + $ipIndex

        Invoke-Az @("webapp", "config", "access-restriction", "add",
            "--name", $cfg.ApiAppName,
            "--resource-group", $cfg.ResourceGroup,
            "--rule-name", $ruleName,
            "--action", "Allow",
            "--ip-address", "$ip/32",
            "--priority", "$priority") | Out-Null

        Write-Ok "$ruleName: Allow $ip/32 (priority $priority)"
        $ipIndex++
    }

    # Deny all other traffic
    Invoke-Az @("webapp", "config", "access-restriction", "add",
        "--name", $cfg.ApiAppName,
        "--resource-group", $cfg.ResourceGroup,
        "--rule-name", "DenyAll",
        "--action", "Deny",
        "--ip-address", "0.0.0.0/0",
        "--priority", "200") | Out-Null
    Write-Ok "DenyAll rule added on API — API is now private (Blazor server-to-server only)"
}

# ── Summary ───────────────────────────────────────────────────
Write-Host ""
Write-Host "============================================================" -ForegroundColor Green
Write-Host "  Deployment Complete!" -ForegroundColor Green
Write-Host "============================================================" -ForegroundColor Green
Write-Host ""

if (-not $DryRun) {
    $endpointHostname = az afd endpoint show `
        --endpoint-name $cfg.EndpointName `
        --profile-name $cfg.FrontDoorProfile `
        --resource-group $cfg.ResourceGroup `
        --query "hostName" -o tsv 2>$null

    Write-Host "  Front Door URL:    https://$endpointHostname" -ForegroundColor Cyan
    Write-Host ""
} else {
    Write-Host "  (dry run — no resources were created)" -ForegroundColor Yellow
    Write-Host ""
}

Write-Host "  Security Summary:" -ForegroundColor White
Write-Host "    WAF Policy:         $($cfg.WafPolicyName)" -ForegroundColor Gray
Write-Host "    Geo restriction:    Israel only (GeoMatch IL, non-IL -> Block 403)" -ForegroundColor Gray
Write-Host "    OWASP protection:   Microsoft_DefaultRuleSet 2.1" -ForegroundColor Gray
Write-Host "    Bot protection:     Microsoft_BotManagerRuleSet 1.0" -ForegroundColor Gray
Write-Host "    Blazor access:      AzureFrontDoor.Backend only" -ForegroundColor Gray
Write-Host "    API access:         Blazor outbound IPs only (not internet-reachable)" -ForegroundColor Gray
Write-Host ""
Write-Host "  Next steps:" -ForegroundColor White
Write-Host "    1. Test from an Israeli IP: https://$endpointHostname" -ForegroundColor Gray
Write-Host "    2. Confirm non-Israeli IPs receive 403" -ForegroundColor Gray
Write-Host "    3. Confirm API is unreachable from the internet directly" -ForegroundColor Gray
Write-Host "    4. (Optional) Configure a custom domain in Front Door" -ForegroundColor Gray
Write-Host "    5. Monitor WAF logs: Azure Portal -> Front Door -> Security -> WAF logs" -ForegroundColor Gray
Write-Host ""
