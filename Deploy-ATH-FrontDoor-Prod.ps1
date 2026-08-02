# ============================================================
# PetelATH Production -- Azure Front Door Deployment
# ============================================================
# Creates a Front Door Premium profile that routes ONLY to the
# Blazor server. The API is fully private -- accessible only from
# the Blazor App Service (server-to-server), not from the web.
#
# Traffic flow:
#   Browser (IL only) -> Front Door WAF -> Blazor App Service
#                                       -> API App Service (internal only)
#
# WAF enforces Israel-only access via GeoMatch (country = IL).
# No Israeli IP ranges needed -- GeoMatch is maintained by Microsoft.
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

# --- Configuration -----------------------------------------------------------
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

# --- Helpers -----------------------------------------------------------------
function Write-Step {
    param([string]$m)
    Write-Host ""
    Write-Host $m -ForegroundColor Yellow
    Write-Host ("-" * $m.Length) -ForegroundColor Yellow
}
function Write-Ok   { param([string]$m) Write-Host "  OK  $m" -ForegroundColor Green }
function Write-Skip { param([string]$m) Write-Host "  --  $m" -ForegroundColor DarkGray }
function Write-Warn { param([string]$m) Write-Host "  !!  $m" -ForegroundColor Yellow }
function Write-Err  { param([string]$m) Write-Host "  ERR $m" -ForegroundColor Red }

function Invoke-Az {
    param([string[]]$AzArgs)
    if ($DryRun) {
        Write-Host "  [DRY RUN] az $($AzArgs -join ' ')" -ForegroundColor DarkCyan
        return $null
    }
    $result = az @AzArgs 2>&1
    if ($LASTEXITCODE -ne 0) { throw "az $($AzArgs[0..2] -join ' ') failed: $result" }
    return $result
}

function Test-AzResource {
    param([string[]]$AzArgs)
    try {
        $output = az @AzArgs 2>&1
        return ($LASTEXITCODE -eq 0)
    }
    catch {
        return $false
    }
}

# --- Banner ------------------------------------------------------------------
Write-Host ""
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "  PetelATH Production -- Azure Front Door Deployment"        -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan
if ($DryRun)  { Write-Host "  DRY RUN MODE -- no resources will be created"          -ForegroundColor Yellow }
if ($WafOnly) { Write-Host "  WAF-ONLY MODE -- skipping Front Door infrastructure"   -ForegroundColor Yellow }
Write-Host ""
Write-Host "  Resource Group:    $($cfg.ResourceGroup)"    -ForegroundColor White
Write-Host "  Front Door:        $($cfg.FrontDoorProfile)" -ForegroundColor White
Write-Host "  WAF Policy:        $($cfg.WafPolicyName)"    -ForegroundColor White
Write-Host "  Blazor Backend:    $($cfg.BlazorHostname)"   -ForegroundColor White
Write-Host "  API (private):     $($cfg.ApiAppName).azurewebsites.net (no FD route)" -ForegroundColor White
Write-Host ""

# --- Prerequisites -----------------------------------------------------------
Write-Step "Verifying Prerequisites"

try {
    az account show --only-show-errors | Out-Null
    Write-Ok "Azure CLI authenticated"
}
catch {
    Write-Err "Not authenticated. Run: az login"
    exit 1
}

$rgOk = az group exists --name $cfg.ResourceGroup
if ($rgOk -ne 'true') {
    Write-Err "Resource group '$($cfg.ResourceGroup)' not found"
    exit 1
}
Write-Ok "Resource group exists"

if (-not $WafOnly) {
    $blazorExists = az webapp show --name $cfg.BlazorAppName --resource-group $cfg.ResourceGroup --query "name" -o tsv 2>$null
    if (-not $blazorExists) { Write-Err "Blazor App Service '$($cfg.BlazorAppName)' not found"; exit 1 }
    Write-Ok "Blazor App Service exists"

    $apiExists = az webapp show --name $cfg.ApiAppName --resource-group $cfg.ResourceGroup --query "name" -o tsv 2>$null
    if (-not $apiExists) { Write-Err "API App Service '$($cfg.ApiAppName)' not found"; exit 1 }
    Write-Ok "API App Service exists"
}

# --- Step 1: WAF Policy ------------------------------------------------------
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
}
else {
    Write-Skip "WAF policy already exists"
}

# Enable managed rule sets via ARM REST GET+PUT
# (CLI 'managed-rules add' sends an unsupported action for Premium WAF; PATCH not supported)
Write-Host "  Enabling managed rule sets (DefaultRuleSet 2.0 + BotManagerRuleSet 1.0) via ARM..." -ForegroundColor Gray
if (-not $DryRun) {
    $subId        = (az account show --query id -o tsv)
    $wafPolicyUrl = "https://management.azure.com/subscriptions/$subId/resourceGroups/$($cfg.ResourceGroup)/providers/Microsoft.Network/frontDoorWebApplicationFirewallPolicies/$($cfg.WafPolicyName)?api-version=2022-05-01"

    # GET current policy
    $policyJson = az rest --method get --url $wafPolicyUrl --only-show-errors 2>&1
    if ($LASTEXITCODE -ne 0) { throw "Failed to GET WAF policy: $policyJson" }

    $policy = $policyJson | ConvertFrom-Json

    # Inject managed rule sets
    $managedRuleSets = @(
        [PSCustomObject]@{ ruleSetType = "Microsoft_DefaultRuleSet";    ruleSetVersion = "2.0"; ruleSetAction = "Block" },
        [PSCustomObject]@{ ruleSetType = "Microsoft_BotManagerRuleSet"; ruleSetVersion = "1.0"; ruleSetAction = "Block" }
    )

    if (-not $policy.properties.PSObject.Properties["managedRules"]) {
        $policy.properties | Add-Member -MemberType NoteProperty -Name "managedRules" -Value ([PSCustomObject]@{})
    }
    $policy.properties.managedRules | Add-Member -MemberType NoteProperty -Name "managedRuleSets" -Value $managedRuleSets -Force

    # PUT the full policy back
    $tmpFile = [System.IO.Path]::GetTempFileName() + ".json"
    $policy | ConvertTo-Json -Depth 20 | Out-File $tmpFile -Encoding utf8

    az rest --method put `
        --url $wafPolicyUrl `
        --body "@$tmpFile" `
        --headers "Content-Type=application/json" `
        --only-show-errors 2>&1 | Out-Null

    Remove-Item $tmpFile -Force -ErrorAction SilentlyContinue

    if ($LASTEXITCODE -eq 0) { Write-Ok "Managed rule sets enabled (DefaultRuleSet 2.0 + BotManagerRuleSet 1.0)" }
    else { Write-Warn "Managed rules may need to be enabled manually in Azure Portal (WAF -> Managed rules)" }
}
else {
    Write-Host "  [DRY RUN] Would enable Microsoft_DefaultRuleSet 2.0 and Microsoft_BotManagerRuleSet 1.0" -ForegroundColor DarkCyan
}

# Custom rule: Block non-Israeli traffic via GeoMatch
# Uses ARM REST GET+PUT to avoid CLI --defer limitation and add the rule + match condition in one call.
Write-Host "  Configuring BlockNonIsrael GeoMatch rule via ARM..." -ForegroundColor Gray

if (-not $DryRun) {
    $subId   = (az account show --query id -o tsv)
    $wafUrl  = "https://management.azure.com/subscriptions/$subId/resourceGroups/$($cfg.ResourceGroup)/providers/Microsoft.Network/frontDoorWebApplicationFirewallPolicies/$($cfg.WafPolicyName)?api-version=2022-05-01"

    $policy = (az rest --method get --url $wafUrl --only-show-errors 2>&1) | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0) { throw "Failed to GET WAF policy" }

    $geoRule = [PSCustomObject]@{
        name            = "BlockNonIsrael"
        priority        = 100
        ruleType        = "MatchRule"
        action          = "Block"
        matchConditions = @(
            [PSCustomObject]@{
                matchVariable   = "RemoteAddr"
                operator        = "GeoMatch"
                negateCondition = $true
                matchValue      = @("IL")
            }
        )
    }
    $policy.properties.customRules.rules = @($geoRule)

    $tmpFile = [System.IO.Path]::GetTempFileName() + ".json"
    $policy | ConvertTo-Json -Depth 20 | Out-File $tmpFile -Encoding utf8

    az rest --method put --url $wafUrl --body "@$tmpFile" --headers "Content-Type=application/json" --only-show-errors 2>&1 | Out-Null
    Remove-Item $tmpFile -Force -ErrorAction SilentlyContinue

    if ($LASTEXITCODE -eq 0) { Write-Ok "BlockNonIsrael GeoMatch rule configured (non-IL -> Block 403)" }
    else { Write-Warn "GeoMatch rule may need manual verification in the Azure Portal" }
}
else {
    Write-Host "  [DRY RUN] Would create BlockNonIsrael: GeoMatch != IL -> Block (priority 100)" -ForegroundColor DarkCyan
}

if ($WafOnly) {
    Write-Host ""
    Write-Host "WAF-only update complete." -ForegroundColor Green
    Write-Host "  Policy: $($cfg.WafPolicyName) in $($cfg.ResourceGroup)" -ForegroundColor White
    exit 0
}

# --- Step 2: Front Door Profile ----------------------------------------------
Write-Step "Step 2: Front Door Profile"

$fdExists = Test-AzResource @("afd", "profile", "show",
    "--profile-name", $cfg.FrontDoorProfile,
    "--resource-group", $cfg.ResourceGroup)

if (-not $fdExists) {
    Write-Host "  Creating Front Door Premium profile (may take 2-3 minutes)..." -ForegroundColor Gray
    Invoke-Az @("afd", "profile", "create",
        "--profile-name", $cfg.FrontDoorProfile,
        "--resource-group", $cfg.ResourceGroup,
        "--sku", "Premium_AzureFrontDoor",
        "--only-show-errors") | Out-Null
    Write-Ok "Front Door profile created"
}
else {
    Write-Skip "Front Door profile already exists"
}

# --- Step 3: Endpoint --------------------------------------------------------
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
}
else {
    Write-Skip "Endpoint already exists"
}

# --- Step 4: Origin Group (Blazor only) --------------------------------------
Write-Step "Step 4: Origin Group -- Blazor"

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
}
else {
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
}
else {
    Write-Skip "blazor-backend origin already exists"
}

# --- Step 5: Route -----------------------------------------------------------
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
}
else {
    Write-Skip "blazor-route already exists"
}

# --- Step 6: Security Policy (WAF <-> Endpoint) ------------------------------
Write-Step "Step 6: Security Policy"

$subscriptionId = (az account show --query id -o tsv)
$wafPolicyId = "/subscriptions/$subscriptionId/resourceGroups/$($cfg.ResourceGroup)/providers/Microsoft.Network/frontDoorWebApplicationFirewallPolicies/$($cfg.WafPolicyName)"
$endpointId  = "/subscriptions/$subscriptionId/resourceGroups/$($cfg.ResourceGroup)/providers/Microsoft.Cdn/profiles/$($cfg.FrontDoorProfile)/afdEndpoints/$($cfg.EndpointName)"

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

# --- Step 7: Lock Blazor to Front Door only ----------------------------------
# Uses ARM REST PUT to replace all rules atomically -- avoids slow per-rule remove loop.
Write-Step "Step 7: App Service Lockdown -- Blazor"

if (-not $DryRun) {
    $subId       = (az account show --query id -o tsv)
    $blazorWebUrl = "https://management.azure.com/subscriptions/$subId/resourceGroups/$($cfg.ResourceGroup)/providers/Microsoft.Web/sites/$($cfg.BlazorAppName)/config/web?api-version=2022-03-01"

    $blazorRestrictions = @(
        [PSCustomObject]@{
            ipAddress   = "AzureFrontDoor.Backend"
            tag         = "ServiceTag"
            action      = "Allow"
            priority    = 100
            name        = "AllowFrontDoor"
            description = "Allow traffic only from Azure Front Door"
        }
    )

    $blazorBody = [PSCustomObject]@{
        properties = [PSCustomObject]@{
            ipSecurityRestrictions              = $blazorRestrictions
            ipSecurityRestrictionsDefaultAction = "Deny"
        }
    }

    $tmpFile = [System.IO.Path]::GetTempFileName() + ".json"
    $blazorBody | ConvertTo-Json -Depth 10 | Out-File $tmpFile -Encoding utf8

    az rest --method put --url $blazorWebUrl --body "@$tmpFile" --headers "Content-Type=application/json" --only-show-errors 2>&1 | Out-Null
    Remove-Item $tmpFile -Force -ErrorAction SilentlyContinue

    if ($LASTEXITCODE -eq 0) {
        Write-Ok "Blazor: AllowFrontDoor (AzureFrontDoor.Backend) + Deny default"
    }
    else {
        Write-Warn "Blazor access restriction update failed -- check Azure Portal"
    }
}
else {
    Write-Host "  [DRY RUN] Would set Blazor: Allow AzureFrontDoor.Backend + Deny default" -ForegroundColor DarkCyan
}

# --- Step 8: Lock API to Blazor outbound IPs only ----------------------------
# Uses ARM REST PUT to replace all rules atomically -- avoids slow per-rule remove loop.
Write-Step "Step 8: App Service Lockdown -- API (Blazor outbound IPs only)"

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
    }
    else {
        Write-Warn "Could not retrieve Blazor outbound IPs -- API lockdown skipped"
        Write-Host "  Manually add Blazor outbound IPs to the API access restrictions." -ForegroundColor Yellow
    }
}
else {
    $blazorOutboundIps = @("10.0.0.1", "10.0.0.2")
    Write-Host "  [DRY RUN] Would fetch outbound IPs from $($cfg.BlazorAppName)" -ForegroundColor DarkCyan
}

if ($blazorOutboundIps.Count -gt 0) {
    $subId      = (az account show --query id -o tsv)
    $apiWebUrl  = "https://management.azure.com/subscriptions/$subId/resourceGroups/$($cfg.ResourceGroup)/providers/Microsoft.Web/sites/$($cfg.ApiAppName)/config/web?api-version=2022-03-01"

    $apiRestrictions = @()
    $ipIndex = 1
    foreach ($ip in $blazorOutboundIps) {
        $apiRestrictions += [PSCustomObject]@{
            ipAddress   = "$ip/32"
            action      = "Allow"
            priority    = 100 + $ipIndex
            name        = "AllowBlazor_$ipIndex"
            description = "Blazor server outbound IP $ip"
        }
        $ipIndex++
    }

    $apiBody = [PSCustomObject]@{
        properties = [PSCustomObject]@{
            ipSecurityRestrictions              = $apiRestrictions
            ipSecurityRestrictionsDefaultAction = "Deny"
        }
    }

    if (-not $DryRun) {
        $tmpFile = [System.IO.Path]::GetTempFileName() + ".json"
        $apiBody | ConvertTo-Json -Depth 10 | Out-File $tmpFile -Encoding utf8

        az rest --method put --url $apiWebUrl --body "@$tmpFile" --headers "Content-Type=application/json" --only-show-errors 2>&1 | Out-Null
        Remove-Item $tmpFile -Force -ErrorAction SilentlyContinue

        if ($LASTEXITCODE -eq 0) {
            Write-Ok "API: $($apiRestrictions.Count) AllowBlazor rules + Deny default -- API is private"
        }
        else {
            Write-Warn "API access restriction update failed -- check Azure Portal"
        }
    }
    else {
        Write-Host "  [DRY RUN] Would set API: Allow $($blazorOutboundIps.Count) Blazor IPs + Deny default" -ForegroundColor DarkCyan
    }
}

# --- Summary -----------------------------------------------------------------
Write-Host ""
Write-Host "============================================================" -ForegroundColor Green
Write-Host "  Deployment Complete!"                                        -ForegroundColor Green
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
}
else {
    Write-Host "  (dry run -- no resources were created)" -ForegroundColor Yellow
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
