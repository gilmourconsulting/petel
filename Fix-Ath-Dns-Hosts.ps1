# ============================================
# Point ath.petel.site at the AWS prod EC2 (this PC)
# ============================================
# Permanent fix is at Namecheap DNS (see comments below).
# This script only overrides DNS on THIS computer via hosts.
# Run elevated: Right-click PowerShell -> Run as administrator
# ============================================

#Requires -RunAsAdministrator

$ErrorActionPreference = 'Stop'

# Permanent registrar record (Namecheap Advanced DNS for petel.site):
#   Host: ath
#   Type: A Record
#   Value: 16.164.176.34
#   TTL: Automatic
# Remove the CNAME that targets petel-prod-blazor.azurewebsites.net.

$ip = '16.164.176.34'

$hostsPath = "$env:SystemRoot\System32\drivers\etc\hosts"
$line = "$ip`tath.petel.site`t# AWS petel-prod-app"
$existing = Get-Content $hostsPath
$filtered = @($existing | Where-Object { $_ -notmatch '(^|\s)ath\.petel\.site(\s|$)' })
@($filtered + $line) | Set-Content -Path $hostsPath -Encoding ascii
ipconfig /flushdns | Out-Null

Write-Host "Updated hosts: ath.petel.site -> $ip" -ForegroundColor Green
Write-Host "Test: https://ath.petel.site/login" -ForegroundColor Cyan
