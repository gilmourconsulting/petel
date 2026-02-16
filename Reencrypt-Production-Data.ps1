# Reencrypt-Production-Data.ps1
# Re-encrypts data that was encrypted with the test key after database restoration

param(
    [string]$TableName = "school_students",
    [string]$ColumnName = "id_number",
    [switch]$WhatIf
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "RE-ENCRYPT PRODUCTION DATA" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Configuration
$TestKeyVault = "petel-kv-test-4721"
$ProdKeyVault = "petel-kv-prod-6581"
$ApiPath = "C:\dev\PetelFullApp\PetelApp.Api"

Write-Host "Configuration:" -ForegroundColor Yellow
Write-Host "  Test Key Vault:   $TestKeyVault" -ForegroundColor White
Write-Host "  Prod Key Vault:   $ProdKeyVault" -ForegroundColor White
Write-Host "  Table:            petel_schema.$TableName" -ForegroundColor White
Write-Host "  Column:           $ColumnName" -ForegroundColor White
Write-Host ""

# Step 1: Verify Azure CLI is logged in
Write-Host "[1/6] Verifying Azure CLI authentication..." -ForegroundColor Yellow
try {
    $account = az account show 2>&1 | ConvertFrom-Json
    Write-Host "  ✅ Logged in as: $($account.user.name)" -ForegroundColor Green
} catch {
    Write-Host "  ❌ Not logged in to Azure CLI" -ForegroundColor Red
    Write-Host "  Run: az login" -ForegroundColor Yellow
    exit 1
}

# Step 2: Retrieve test encryption key
Write-Host ""
Write-Host "[2/6] Retrieving TEST encryption key from Key Vault..." -ForegroundColor Yellow
try {
    $testKey = az keyvault secret show `
        --vault-name $TestKeyVault `
        --name "DataEncryption--EncryptionKey" `
        --query "value" -o tsv
    
    if ([string]::IsNullOrWhiteSpace($testKey)) {
        throw "Test key is empty"
    }
    
    # Validate key format
    $testKeyBytes = [Convert]::FromBase64String($testKey)
    if ($testKeyBytes.Length -ne 32) {
        throw "Test key is invalid size: $($testKeyBytes.Length) bytes (expected 32)"
    }
    
    Write-Host "  ✅ Test key retrieved: $($testKey.Substring(0, 20))... ($($testKeyBytes.Length) bytes)" -ForegroundColor Green
} catch {
    Write-Host "  ❌ Failed to retrieve test key: $_" -ForegroundColor Red
    exit 1
}

# Step 3: Retrieve production encryption key (for verification)
Write-Host ""
Write-Host "[3/6] Retrieving PRODUCTION encryption key from Key Vault..." -ForegroundColor Yellow
try {
    $prodKey = az keyvault secret show `
        --vault-name $ProdKeyVault `
        --name "Security--DataEncryption--EncryptionKey" `
        --query "value" -o tsv
    
    if ([string]::IsNullOrWhiteSpace($prodKey)) {
        throw "Production key is empty"
    }
    
    # Validate key format
    $prodKeyBytes = [Convert]::FromBase64String($prodKey)
    if ($prodKeyBytes.Length -ne 32) {
        throw "Production key is invalid size: $($prodKeyBytes.Length) bytes (expected 32)"
    }
    
    Write-Host "  ✅ Production key retrieved: $($prodKey.Substring(0, 20))... ($($prodKeyBytes.Length) bytes)" -ForegroundColor Green
    
    # Verify they are different
    if ($testKey -eq $prodKey) {
        Write-Host "  ⚠️  WARNING: Test and Production keys are identical!" -ForegroundColor Yellow
        Write-Host "  This script may not be necessary." -ForegroundColor Yellow
    }
} catch {
    Write-Host "  ❌ Failed to retrieve production key: $_" -ForegroundColor Red
    exit 1
}

# Step 4: Check database backup
Write-Host ""
Write-Host "[4/6] Verifying database backup exists..." -ForegroundColor Yellow
Write-Host "  ⚠️  CRITICAL: Ensure you have a recent database backup!" -ForegroundColor Yellow
Write-Host "  This operation will modify production data." -ForegroundColor Yellow
Write-Host ""
Write-Host "  Backup verification commands:" -ForegroundColor White
Write-Host "  • Check Azure Portal > Azure Database for PostgreSQL > Backups" -ForegroundColor Gray
Write-Host "  • Or run: az postgres flexible-server backup list --resource-group petel-prod-rg --name petel-prod-db-4407" -ForegroundColor Gray
Write-Host ""

if (-not $WhatIf) {
    $backupConfirm = Read-Host "  Have you verified a backup exists? (yes/no)"
    if ($backupConfirm -ne "yes") {
        Write-Host "  ❌ Operation cancelled. Please verify backup first." -ForegroundColor Red
        exit 1
    }
}

# Step 5: Navigate to API project
Write-Host ""
Write-Host "[5/6] Navigating to API project..." -ForegroundColor Yellow
if (-not (Test-Path $ApiPath)) {
    Write-Host "  ❌ API path not found: $ApiPath" -ForegroundColor Red
    exit 1
}
Push-Location $ApiPath
Write-Host "  ✅ Current directory: $ApiPath" -ForegroundColor Green

# Step 6: Execute re-encryption command
Write-Host ""
Write-Host "[6/6] Executing re-encryption command..." -ForegroundColor Yellow
Write-Host ""

if ($WhatIf) {
    Write-Host "  [WHAT-IF MODE] Command that would be executed:" -ForegroundColor Cyan
    Write-Host "  dotnet run -- reencrypt-with-old-key `"$testKey`" $TableName $ColumnName" -ForegroundColor White
    Write-Host ""
    Write-Host "  Key comparison:" -ForegroundColor Cyan
    Write-Host "    Test key (first 30):       $($testKey.Substring(0, 30))..." -ForegroundColor White
    Write-Host "    Production key (first 30): $($prodKey.Substring(0, 30))..." -ForegroundColor White
    Write-Host ""
    Pop-Location
    exit 0
}

try {
    # Run the re-encryption command
    # Note: The command will prompt for confirmation internally
    & dotnet run -- reencrypt-with-old-key $testKey $TableName $ColumnName
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host ""
        Write-Host "========================================" -ForegroundColor Green
        Write-Host "✅ RE-ENCRYPTION COMPLETED SUCCESSFULLY" -ForegroundColor Green
        Write-Host "========================================" -ForegroundColor Green
        Write-Host ""
        Write-Host "Next steps:" -ForegroundColor Yellow
        Write-Host "  1. Test decryption of a sample record" -ForegroundColor White
        Write-Host "  2. Verify application can read encrypted data" -ForegroundColor White
        Write-Host "  3. Monitor application logs for decryption errors" -ForegroundColor White
    } else {
        Write-Host ""
        Write-Host "❌ Re-encryption command failed with exit code: $LASTEXITCODE" -ForegroundColor Red
        Pop-Location
        exit $LASTEXITCODE
    }
} catch {
    Write-Host ""
    Write-Host "❌ Error executing re-encryption: $_" -ForegroundColor Red
    Pop-Location
    exit 1
}

Pop-Location

Write-Host ""
Write-Host "Script complete." -ForegroundColor Cyan
