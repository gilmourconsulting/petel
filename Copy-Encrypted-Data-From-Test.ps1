# Copy-Encrypted-Data-From-Test.ps1
# Copies decrypted data from test, re-encrypts with production key, and updates production
#
# **IMPORTANT**: Uses environment variables to override appsettings.json, ensuring:
# - Export step connects to TEST database with TEST encryption key
# - Import step connects to PRODUCTION database with PRODUCTION encryption key

param(
    [string]$TableName = "school_students",
    [Parameter(Mandatory=$true)]
    [string[]]$Columns,  # e.g., @("id_number", "street")
    [switch]$WhatIf
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "COPY ENCRYPTED DATA FROM TEST TO PROD" -ForegroundColor Cyan
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
Write-Host "  Columns:          $($Columns -join ', ')" -ForegroundColor White
Write-Host ""

# Step 1: Verify Azure CLI is logged in
Write-Host "[1/8] Verifying Azure CLI authentication..." -ForegroundColor Yellow
try {
    $account = az account show 2>&1 | ConvertFrom-Json
    Write-Host "  ✅ Logged in as: $($account.user.name)" -ForegroundColor Green
} catch {
    Write-Host "  ❌ Not logged in to Azure CLI" -ForegroundColor Red
    Write-Host "  Run: az login" -ForegroundColor Yellow
    exit 1
}

# Step 2: Get test database connection
Write-Host ""
Write-Host "[2/8] Getting TEST database connection..." -ForegroundColor Yellow
try {
    $testConnString = az keyvault secret show `
        --vault-name $TestKeyVault `
        --name "ConnectionStrings--DefaultConnection" `
        --query "value" -o tsv
    
    $testConnParts = @{}
    $testConnString.Split(';') | ForEach-Object {
        if ($_ -match '(.+?)=(.+)') {
            $testConnParts[$matches[1]] = $matches[2]
        }
    }
    
    Write-Host "  ✅ Test DB: $($testConnParts['Host'])" -ForegroundColor Green
} catch {
    Write-Host "  ❌ Failed to get test connection: $_" -ForegroundColor Red
    exit 1
}

# Step 3: Get production database connection
Write-Host ""
Write-Host "[3/8] Getting PRODUCTION database connection..." -ForegroundColor Yellow
try {
    $prodConnString = az keyvault secret show `
        --vault-name $ProdKeyVault `
        --name "ConnectionStrings--DefaultConnection" `
        --query "value" -o tsv
    
    $prodConnParts = @{}
    $prodConnString.Split(';') | ForEach-Object {
        if ($_ -match '(.+?)=(.+)') {
            $prodConnParts[$matches[1]] = $matches[2]
        }
    }
    
    Write-Host "  ✅ Prod DB: $($prodConnParts['Host'])" -ForegroundColor Green
} catch {
    Write-Host "  ❌ Failed to get prod connection: $_" -ForegroundColor Red
    exit 1
}

# Step 4: Get TEST encryption key from Key Vault
Write-Host ""
Write-Host "[4/8] Retrieving TEST encryption key..." -ForegroundColor Yellow
try {
    $testEncryptionKey = az keyvault secret show `
        --vault-name $TestKeyVault `
        --name "DataEncryption--EncryptionKey" `
        --query "value" -o tsv
    
    if ([string]::IsNullOrWhiteSpace($testEncryptionKey)) {
        throw "Test encryption key is empty"
    }
    Write-Host "  ✅ Test encryption key retrieved" -ForegroundColor Green
} catch {
    Write-Host "  ❌ Failed to retrieve test encryption key: $_" -ForegroundColor Red
    exit 1
}

# Step 5: Export data from test database using API
Write-Host ""
Write-Host "[5/8] Exporting plaintext data from TEST database..." -ForegroundColor Yellow
$tempFile = Join-Path $env:TEMP "test_data_export_$(Get-Date -Format 'yyyyMMddHHmmss').csv"

try {
    # Navigate to API project with test config
    Push-Location $ApiPath
    
    $columnList = $Columns -join ','
    
    # Set environment variables to override appsettings.json
    $env:ConnectionStrings__DefaultConnection = $testConnString
    $env:Security__DataEncryption__EncryptionKey = $testEncryptionKey
    
    # Use dotnet run to export data (will use TEST database from environment)
    $exportCmd = "dotnet run -- export-encrypted-data $TableName `"$columnList`" `"$tempFile`""
    Write-Host "  Running: $exportCmd" -ForegroundColor Gray
    Write-Host "  Using database: $($testConnParts['Host'])" -ForegroundColor Gray
    
    Invoke-Expression $exportCmd
    
    if ($LASTEXITCODE -ne 0) {
        throw "Export command failed with exit code $LASTEXITCODE"
    }
    
    # Clear environment variables
    $env:ConnectionStrings__DefaultConnection = $null
    $env:Security__DataEncryption__EncryptionKey = $null
    
    Pop-Location
    
    if (-not (Test-Path $tempFile)) {
        throw "Export file was not created: $tempFile"
    }
    
    $recordCount = (Get-Content $tempFile | Measure-Object -Line).Lines - 1
    Write-Host "  ✅ Exported $recordCount records to: $tempFile" -ForegroundColor Green
    
} catch {
    Write-Host "  ❌ Failed to export from test: $_" -ForegroundColor Red
    $env:ConnectionStrings__DefaultConnection = $null
    $env:Security__DataEncryption__EncryptionKey = $null
    Pop-Location -ErrorAction SilentlyContinue
    exit 1
}

# Step 6: Check production backup
Write-Host ""
Write-Host "[6/8] Verifying production database backup..." -ForegroundColor Yellow
Write-Host "  ⚠️  CRITICAL: This will modify production data!" -ForegroundColor Yellow
Write-Host ""

if ($WhatIf) {
    Write-Host "  [WHAT-IF MODE] Would process $recordCount records" -ForegroundColor Cyan
    Write-Host "  Sample data from export file:" -ForegroundColor Cyan
    Get-Content $tempFile -First 5 | ForEach-Object { Write-Host "    $_" -ForegroundColor Gray }
    Write-Host ""
    Remove-Item $tempFile -Force
    exit 0
}

$backupConfirm = Read-Host "  Have you verified a production backup exists? (yes/no)"
if ($backupConfirm -ne "yes") {
    Write-Host "  ❌ Operation cancelled." -ForegroundColor Red
    Remove-Item $tempFile -Force
    exit 1
}

# Step 7: Get PRODUCTION encryption key from Key Vault
Write-Host ""
Write-Host "[7/8] Retrieving PRODUCTION encryption key..." -ForegroundColor Yellow
try {
    $prodEncryptionKey = az keyvault secret show `
        --vault-name $ProdKeyVault `
        --name "Security--DataEncryption--EncryptionKey" `
        --query "value" -o tsv
    
    if ([string]::IsNullOrWhiteSpace($prodEncryptionKey)) {
        throw "Production encryption key is empty"
    }
    Write-Host "  ✅ Production encryption key retrieved" -ForegroundColor Green
} catch {
    Write-Host "  ❌ Failed to retrieve production encryption key: $_" -ForegroundColor Red
    Remove-Item $tempFile -Force
    exit 1
}

# Step 8: Navigate to API project
Write-Host ""
Write-Host "[8/8] Re-encrypting and updating PRODUCTION database..." -ForegroundColor Yellow
if (-not (Test-Path $ApiPath)) {
    Write-Host "  ❌ API path not found: $ApiPath" -ForegroundColor Red
    Remove-Item $tempFile -Force
    exit 1
}
Push-Location $ApiPath
Write-Host ""

try {
    # Set environment variables to use PRODUCTION database and encryption key
    $env:ConnectionStrings__DefaultConnection = $prodConnString
    $env:Security__DataEncryption__EncryptionKey = $prodEncryptionKey
    
    Write-Host "  Using database: $($prodConnParts['Host'])" -ForegroundColor Gray
    Write-Host ""
    
    $columnsParam = $Columns -join ','
    echo "YES" | dotnet run -- import-and-reencrypt $tempFile $TableName $columnsParam
    
    if ($LASTEXITCODE -ne 0) {
        throw "Import command failed with exit code $LASTEXITCODE"
    }
    
    # Clear environment variables
    $env:ConnectionStrings__DefaultConnection = $null
    $env:Security__DataEncryption__EncryptionKey = $null
    
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "✅ DATA COPY COMPLETED SUCCESSFULLY" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
} catch {
    Write-Host ""
    Write-Host "❌ Error during import: $_" -ForegroundColor Red
    $env:ConnectionStrings__DefaultConnection = $null
    $env:Security__DataEncryption__EncryptionKey = $null
    Pop-Location
    Remove-Item $tempFile -Force -ErrorAction SilentlyContinue
    exit 1
}

# Cleanup
Write-Host ""
Write-Host "Cleaning up temp files..." -ForegroundColor Yellow
Remove-Item $tempFile -Force -ErrorAction SilentlyContinue
Write-Host "  ✅ Temp file removed" -ForegroundColor Green

Pop-Location

Write-Host ""
Write-Host "Script complete." -ForegroundColor Cyan
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. Test a few student records in Blazor app" -ForegroundColor White
Write-Host "  2. Verify ID numbers are displayed correctly" -ForegroundColor White
Write-Host "  3. Repeat for other encrypted fields if needed" -ForegroundColor White
