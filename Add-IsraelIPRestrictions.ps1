# Add-IsraelIPRestrictions.ps1
param(
    [Parameter(Mandatory=$true)]
    [string]$AppName,
    
    [Parameter(Mandatory=$true)]
    [string]$ResourceGroup
)

# Israeli IP ranges (major providers)
$israeliRanges = @(
    "212.179.0.0/16",  # Bezeq International
    "82.166.0.0/16",   # Bezeq
    "77.125.0.0/16",   # Hot/Cable
    "31.154.0.0/16",   # Cellcom
    "31.168.0.0/16",   # Partner
    "80.178.0.0/16",   # Israeli range
    "87.70.0.0/16",    # Israeli range
    "94.188.0.0/16",   # Israeli range
    "95.86.0.0/16",
    "103.209.0.0/16"   # Israeli range
)

Write-Host "[SECURITY] Configuring IP restrictions for $AppName..." -ForegroundColor Cyan

$priority = 100
foreach ($ipRange in $israeliRanges) {
    Write-Host "Adding rule for $ipRange..." -ForegroundColor Yellow
    
    # Generate a safe rule name by replacing / with _
    $ruleName = "Allow_" + $ipRange.Replace("/", "_")
    
    az webapp config access-restriction add `
        --resource-group $ResourceGroup `
        --name $AppName `
        --rule-name $ruleName `
        --action Allow `
        --ip-address $ipRange `
        --priority $priority
    
    $priority += 10
}

# Deny all other traffic
Write-Host "Adding deny all rule..." -ForegroundColor Yellow
az webapp config access-restriction add `
    --resource-group $ResourceGroup `
    --name $AppName `
    --rule-name "Deny_All" `
    --action Deny `
    --ip-address "0.0.0.0/0" `
    --priority 2147483647

Write-Host "[SUCCESS] IP restrictions configured successfully!" -ForegroundColor Green