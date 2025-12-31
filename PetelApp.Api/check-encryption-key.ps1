# Navigate to project directory
cd C:\dev\PetelFullApp\PetelApp.Api

# Read appsettings.json and extract encryption key
$config = Get-Content appsettings.json | ConvertFrom-Json
$encryptionKey = $config.Security.DataEncryption.EncryptionKey

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "ENCRYPTION KEY CHECK" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "File: appsettings.json"
Write-Host "Key (first 20 chars): $($encryptionKey.Substring(0, [Math]::Min(20, $encryptionKey.Length)))..."
Write-Host "Key length: $($encryptionKey.Length) characters"
Write-Host ""

# Validate it's base64
try {
    $bytes = [Convert]::FromBase64String($encryptionKey)
    Write-Host "✅ Valid base64 format" -ForegroundColor Green
    Write-Host "✅ Decoded to $($bytes.Length) bytes" -ForegroundColor Green
    
    if ($bytes.Length -eq 32) {
        Write-Host "✅ Correct key size (256-bit)" -ForegroundColor Green
    } else {
        Write-Host "❌ WRONG key size! Expected 32 bytes, got $($bytes.Length)" -ForegroundColor Red
    }
} catch {
    Write-Host "❌ INVALID base64 format!" -ForegroundColor Red
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan