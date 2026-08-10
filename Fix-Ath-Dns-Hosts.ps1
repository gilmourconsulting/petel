# ============================================
# Point ath.petel.site at App Service (this PC)
# ============================================
# Permanent fix is at Namecheap DNS (see comments below).
# This script only overrides DNS on THIS computer via hosts.
# Run elevated: Right-click PowerShell -> Run as administrator
# ============================================

#Requires -RunAsAdministrator

$ErrorActionPreference = 'Stop'

# Permanent registrar fix (Namecheap Advanced DNS for petel.site):
#   Host: ath
#   Type: CNAME Record
#   Value: petel-prod-blazor.azurewebsites.net.
#   TTL: Automatic
# Remove any CNAME that targets *.azurefd.net

$hostTarget = 'petel-prod-blazor.azurewebsites.net'
$ip = $null
for ($i = 0; $i -lt 10; $i++) {
    $rec = Resolve-DnsName $hostTarget -ErrorAction SilentlyContinue
    $a = $rec | Where-Object { $_.IPAddress -and ($_.Type -eq 'A') } | Select-Object -First 1
    if ($a) { $ip = $a.IPAddress; break }
    $next = ($rec | Where-Object { $_.NameHost } | Select-Object -First 1).NameHost
    if (-not $next) { break }
    $hostTarget = $next
}

if (-not $ip) {
    throw "Could not resolve petel-prod-blazor.azurewebsites.net to an A record"
}

$hostsPath = "$env:SystemRoot\System32\drivers\etc\hosts"
$line = "$ip`tath.petel.site`t# petel-prod-blazor after Front Door removal"
$existing = Get-Content $hostsPath
$filtered = @($existing | Where-Object { $_ -notmatch '(^|\s)ath\.petel\.site(\s|$)' })
@($filtered + $line) | Set-Content -Path $hostsPath -Encoding ascii
ipconfig /flushdns | Out-Null

Write-Host "Updated hosts: ath.petel.site -> $ip" -ForegroundColor Green
Write-Host "Test: https://ath.petel.site/login" -ForegroundColor Cyan
