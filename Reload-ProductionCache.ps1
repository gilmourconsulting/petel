# Script to reload system attributes cache in production
# Use this after manually updating system attributes in the database

$ProductionApiUrl = "https://petel-api.azurewebsites.net"

Write-Host "🔄 Reloading Production System Attributes Cache..." -ForegroundColor Cyan
Write-Host ""

try {
    # Step 1: Check current cached value
    Write-Host "1️⃣  Fetching current cached attributes..." -ForegroundColor Yellow
    $currentAttrs = Invoke-RestMethod -Uri "$ProductionApiUrl/api/systemattributes" -Method Get
    $otpIssuerAttr = $currentAttrs | Where-Object { $_.name -eq "Security_OtpIssuer" }
    
    if ($otpIssuerAttr) {
        Write-Host "   Current cached value: '$($otpIssuerAttr.value)'" -ForegroundColor Gray
    } else {
        Write-Host "   ⚠️  Security_OtpIssuer not found in cache!" -ForegroundColor Red
    }
    Write-Host ""
    
    # Step 2: Reload cache from database
    Write-Host "2️⃣  Reloading cache from database..." -ForegroundColor Yellow
    $reloadResponse = Invoke-RestMethod -Uri "$ProductionApiUrl/api/systemattributes/reload" -Method Post
    
    if ($reloadResponse.success) {
        Write-Host "   ✅ Cache reloaded successfully!" -ForegroundColor Green
        Write-Host "   📊 Loaded $($reloadResponse.count) attributes" -ForegroundColor Gray
        Write-Host "   🕐 Last loaded: $($reloadResponse.lastLoaded)" -ForegroundColor Gray
    } else {
        Write-Host "   ❌ Failed to reload cache: $($reloadResponse.message)" -ForegroundColor Red
        exit 1
    }
    Write-Host ""
    
    # Step 3: Verify new cached value
    Write-Host "3️⃣  Verifying updated cached attributes..." -ForegroundColor Yellow
    Start-Sleep -Seconds 1
    $updatedAttrs = Invoke-RestMethod -Uri "$ProductionApiUrl/api/systemattributes" -Method Get
    $newOtpIssuerAttr = $updatedAttrs | Where-Object { $_.name -eq "Security_OtpIssuer" }
    
    if ($newOtpIssuerAttr) {
        Write-Host "   New cached value: '$($newOtpIssuerAttr.value)'" -ForegroundColor Green
        
        if ($otpIssuerAttr -and $otpIssuerAttr.value -ne $newOtpIssuerAttr.value) {
            Write-Host ""
            Write-Host "   🎉 Value changed from '$($otpIssuerAttr.value)' to '$($newOtpIssuerAttr.value)'" -ForegroundColor Cyan
        } elseif ($otpIssuerAttr -and $otpIssuerAttr.value -eq $newOtpIssuerAttr.value) {
            Write-Host "   ℹ️  Value unchanged (may already match database)" -ForegroundColor Gray
        }
    }
    Write-Host ""
    
    Write-Host "✅ Cache reload complete!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Yellow
    Write-Host "  - Ask users to delete old OTP entries from their authenticator apps" -ForegroundColor Gray
    Write-Host "  - Have users scan new QR codes with the updated issuer name" -ForegroundColor Gray
}
catch {
    Write-Host ""
    Write-Host "❌ Error: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
    Write-Host "Troubleshooting:" -ForegroundColor Yellow
    Write-Host "  1. Check if production API is accessible: $ProductionApiUrl" -ForegroundColor Gray
    Write-Host "  2. Verify network connectivity" -ForegroundColor Gray
    Write-Host "  3. Check API logs for errors" -ForegroundColor Gray
    exit 1
}
