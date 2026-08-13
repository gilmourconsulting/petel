# Add missing Israel CIDRs to Petel ATH production Blazor
# Source: https://www.ipdeny.com/ipblocks/data/aggregated/il-aggregated.zone
# Only adds ranges not already covered by existing allow rules.
# Respects Azure App Service ~512 access-restriction limit.

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$AppName = 'petel-prod-blazor'
$ResourceGroup = 'petel-prod-rg'
$MaxAllowRules = 500  # headroom under Azure 512 hard limit
$IlAggregatedUrl = 'https://www.ipdeny.com/ipblocks/data/aggregated/il-aggregated.zone'

function ConvertTo-UInt32Ip([string]$ip) {
    $parts = $ip.Split('.') | ForEach-Object { [uint32]$_ }
    return ($parts[0] -shl 24) -bor ($parts[1] -shl 16) -bor ($parts[2] -shl 8) -bor $parts[3]
}

function Get-CidrBounds([string]$cidr) {
    $bits = $cidr.Split('/')
    $ip = ConvertTo-UInt32Ip $bits[0]
    $prefix = [int]$bits[1]
    $mask = if ($prefix -eq 0) { [uint32]0 } else { ([uint32]::MaxValue) -shl (32 - $prefix) }
    $network = $ip -band $mask
    $broadcast = $network -bor (-bnot $mask)
    return @{ Network = $network; Broadcast = $broadcast; Prefix = $prefix; Cidr = $cidr }
}

function Test-CidrFullyCovered($candidate, $allowers) {
    foreach ($a in $allowers) {
        if ($candidate.Network -ge $a.Network -and $candidate.Broadcast -le $a.Broadcast) {
            return $true
        }
    }
    return $false
}

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "Add missing Israel IPs - ATH production" -ForegroundColor Cyan
Write-Host "Target: $AppName / $ResourceGroup" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan

$existingRules = az webapp config access-restriction show `
    --name $AppName `
    --resource-group $ResourceGroup `
    --query "ipSecurityRestrictions[?action=='Allow' && ip_address!='Any']" -o json | ConvertFrom-Json

$existingIps = @($existingRules | ForEach-Object { $_.ip_address } | Where-Object { $_ })
$existingBounds = $existingIps | ForEach-Object { Get-CidrBounds $_ }
Write-Host "Current allow rules: $($existingIps.Count)"

Write-Host "Downloading IL aggregated zone..."
$agg = (Invoke-WebRequest -Uri $IlAggregatedUrl -UseBasicParsing).Content
$aggCidrs = $agg -split "`r?`n" | ForEach-Object { $_.Trim() } | Where-Object { $_ -match '^\d+\.\d+\.\d+\.\d+/\d+$' }
Write-Host "IL aggregated CIDRs: $($aggCidrs.Count)"

$missing = New-Object System.Collections.Generic.List[string]
foreach ($c in $aggCidrs) {
    $b = Get-CidrBounds $c
    if (-not (Test-CidrFullyCovered $b $existingBounds)) {
        [void]$missing.Add($c)
    }
}

$ordered = @($missing | Sort-Object { [int]($_.Split('/')[1]) }, { $_ })
$slots = $MaxAllowRules - $existingIps.Count
Write-Host "Missing uncovered IL CIDRs: $($missing.Count)"
Write-Host "Available slots (cap $MaxAllowRules): $slots"

if ($slots -le 0) {
    throw "No capacity to add rules (already at/over cap)."
}

$toAdd = @($ordered | Select-Object -First $slots)
Write-Host "Will add: $($toAdd.Count) rules"
Write-Host "Prefix breakdown:"
$toAdd | Group-Object { [int]($_.Split('/')[1]) } | Sort-Object { [int]$_.Name } | ForEach-Object {
    Write-Host ("  /{0}: {1}" -f $_.Name, $_.Count)
}

$maxPriority = ($existingRules | ForEach-Object { [int]$_.priority } | Measure-Object -Maximum).Maximum
if (-not $maxPriority) { $maxPriority = 99 }
$priority = [Math]::Max(100, $maxPriority + 1)
$ruleNumber = $existingIps.Count + 1

$added = 0
$failed = New-Object System.Collections.Generic.List[string]
$addedList = New-Object System.Collections.Generic.List[string]

foreach ($ipRange in $toAdd) {
    $ruleName = "Allow-IL-$ruleNumber"
    $prev = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    $null = az webapp config access-restriction add `
        --name $AppName `
        --resource-group $ResourceGroup `
        --rule-name $ruleName `
        --action Allow `
        --ip-address $ipRange `
        --priority $priority 2>&1
    $ok = ($LASTEXITCODE -eq 0)
    $ErrorActionPreference = $prev

    if ($ok) {
        Write-Host ("[{0}/{1}] Added {2} (prio {3})" -f ($added + 1), $toAdd.Count, $ipRange, $priority) -ForegroundColor Green
        $added++
        [void]$addedList.Add($ipRange)
    }
    else {
        Write-Host "[FAIL] $ipRange" -ForegroundColor Red
        [void]$failed.Add($ipRange)
    }

    $priority++
    $ruleNumber++
}

$final = az webapp config access-restriction show `
    --name $AppName `
    --resource-group $ResourceGroup `
    --query "ipSecurityRestrictions[?action=='Allow' && ip_address!='Any'].ip_address" -o tsv
$finalCount = @($final).Count

Write-Host ""
Write-Host "=== RESULT ===" -ForegroundColor Cyan
Write-Host "Added: $added"
Write-Host "Failed: $($failed.Count)"
Write-Host "Final allow rules: $finalCount"
Write-Host "Not added (capacity): $($missing.Count - $toAdd.Count)"

$outDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$addedList | Set-Content (Join-Path $outDir 'added-il-prod.txt')
$failed | Set-Content (Join-Path $outDir 'failed-il-prod.txt')
($existingIps + @($addedList)) | Select-Object -Unique | Set-Content (Join-Path $outDir 'prod-il-allowlist-after.txt')
Write-Host "Wrote artifacts to $outDir"
