# Re-Encryption Guide: Fixing Data After Database Restore

## Problem

When you restored the test database to production, the encrypted fields (like `id_number` in `school_students` table) were encrypted using the **test environment's encryption key**. Now in production, the application cannot decrypt this data because it's using a **different production encryption key**.

## Solution Overview

We've created tools to:
1. Decrypt data using the **old test key**
2. Re-encrypt data using the **new production key**  
3. Update the database with properly encrypted data

## Files Created

1. **`Reencrypt-Production-Data.ps1`** - Main orchestration script
2. **`Test-Encryption-After-Reencrypt.ps1`** - Verification script
3. **API Command**: `reencrypt-with-old-key` - Core re-encryption logic
4. **`DataMigrationService.ReencryptWithOldKeyAsync()`** - Re-encryption method

## Prerequisites

✅ **CRITICAL: Database Backup**
   - Verify you have a recent backup of production database
   - Check: Azure Portal → Azure Database for PostgreSQL → Backups
   - Or: `az postgres flexible-server backup list --resource-group petel-prod-rg --name petel-prod-db-4407`

✅ **Azure CLI** authenticated
   - Run: `az login`
   - Verify: `az account show`

✅ **PostgreSQL Client** (psql) installed (for verification script only)
   - Windows: Download from [PostgreSQL Downloads](https://www.postgresql.org/download/windows/)

✅ **.NET 9.0 SDK** installed
   - Verify: `dotnet --version`

## Step-by-Step Process

### Step 1: Test in What-If Mode

First, run the script in what-if mode to verify configuration without making changes:

```powershell
cd C:\dev\PetelFullApp
.\Reencrypt-Production-Data.ps1 -WhatIf
```

**Expected Output:**
```
========================================
RE-ENCRYPT PRODUCTION DATA
========================================

Configuration:
  Test Key Vault:   petel-kv-test-4721
  Prod Key Vault:   petel-kv-prod-6581
  Table:            petel_schema.school_students
  Column:           id_number

[1/6] Verifying Azure CLI authentication...
  ✅ Logged in as: your@email.com

[2/6] Retrieving TEST encryption key from Key Vault...
  ✅ Test key retrieved: ABC123XYZ... (32 bytes)

[3/6] Retrieving PRODUCTION encryption key from Key Vault...
  ✅ Production key retrieved: DEF456UVW... (32 bytes)

[6/6] Executing re-encryption command...

  [WHAT-IF MODE] Command that would be executed:
  dotnet run -- reencrypt-with-old-key "ABC123XYZ..." school_students id_number

  Key comparison:
    Test key (first 30):       ABC123XYZ...
    Production key (first 30): DEF456UVW...
```

### Step 2: Run Re-Encryption

Once verified, run the actual re-encryption:

```powershell
cd C:\dev\PetelFullApp
.\Reencrypt-Production-Data.ps1
```

**You will be prompted:**
1. **Backup confirmation** - Type `yes` to confirm you have a backup
2. **Final confirmation** - Type `YES` (all caps) to proceed with data modification

**Expected Output:**
```
[6/6] Executing re-encryption command...

========================================
RE-ENCRYPTING WITH OLD KEY
========================================
Table: petel_schema.school_students
Column: id_number
Old key (first 20 chars): ABC123XYZ...

⚠️  WARNING: This will modify production data!
⚠️  Ensure you have a database backup before proceeding.

Type 'YES' to continue: YES

🔄 Starting re-encryption...

Progress: 100/543 records re-encrypted
Progress: 200/543 records re-encrypted
Progress: 300/543 records re-encrypted
Progress: 400/543 records re-encrypted
Progress: 500/543 records re-encrypted

========================================
✅ RE-ENCRYPTION COMPLETE
   Re-encrypted: 543 records
   Errors: 0
========================================
```

### Step 3: Verify Re-Encryption

Test that the data can now be decrypted properly:

```powershell
cd C:\dev\PetelFullApp
.\Test-Encryption-After-Reencrypt.ps1
```

**Expected Output:**
```
========================================
TEST ENCRYPTION/DECRYPTION
========================================

No encrypted value provided. Fetching sample from database...

Connecting to production database...
  Host: petel-prod-db-4407.postgres.database.azure.com
  Database: postgres

✅ Sample record retrieved:
   Student ID: 123
   Encrypted value (first 50 chars): ABC123DEF456...

Testing decryption with production key...

========================================
DECRYPTION TEST
========================================
Encrypted value: ABC123DEF456...
Length: 128 characters

✅ DECRYPTION SUCCESSFUL
Decrypted value: 123456789
========================================

========================================
✅ DECRYPTION TEST PASSED
========================================

The encrypted data can be properly decrypted with the production key.
Re-encryption was successful!
```

### Step 4: Verify in Application

1. Open the Blazor application: https://petel-prod-blazor.azurewebsites.net
2. Navigate to a student details page
3. Verify that ID numbers are displayed correctly
4. Check application logs for any decryption errors

## Re-Encrypting Other Fields

The script can re-encrypt any encrypted field in any table:

```powershell
# Re-encrypt email in persons table
.\Reencrypt-Production-Data.ps1 -TableName "persons" -ColumnName "email"

# Re-encrypt phone_number in persons table
.\Reencrypt-Production-Data.ps1 -TableName "persons" -ColumnName "phone_number"

# Re-encrypt street in school_students table
.\Reencrypt-Production-Data.ps1 -TableName "school_students" -ColumnName "street"
```

## Manual Command Execution

If you prefer to run the command directly without the PowerShell script:

### 1. Get Keys from Azure Key Vault

```powershell
# Get test key
$testKey = az keyvault secret show `
    --vault-name petel-kv-test-4721 `
    --name "DataEncryption--EncryptionKey" `
    --query "value" -o tsv

# Get production key (for verification)
$prodKey = az keyvault secret show `
    --vault-name petel-kv-prod-6581 `
    --name "DataEncryption--EncryptionKey" `
    --query "value" -o tsv

Write-Host "Test key: $($testKey.Substring(0, 30))..."
Write-Host "Prod key: $($prodKey.Substring(0, 30))..."
```

### 2. Run Re-Encryption Command

```powershell
cd C:\dev\PetelFullApp\PetelApp.Api
dotnet run -- reencrypt-with-old-key "$testKey" school_students id_number
```

### 3. Test Specific Encrypted Value

```powershell
cd C:\dev\PetelFullApp\PetelApp.Api
dotnet run -- test-decrypt "YOUR_ENCRYPTED_VALUE_HERE"
```

## Troubleshooting

### Error: "Cryptographic error during decryption"

**Cause:** The test key is incorrect or the data was encrypted with a different key.

**Solution:**
1. Verify the test key: `az keyvault secret show --vault-name petel-kv-test-4721 --name DataEncryption--EncryptionKey`
2. Check when the key was last modified in Key Vault
3. If the database was restored from a snapshot, ensure you're using the key from that time period

### Error: "Old key must be 32 bytes"

**Cause:** The encryption key is not valid base64 or is the wrong length.

**Solution:**
1. Verify key format: It should be a base64-encoded 32-byte string
2. Check for extra whitespace or line breaks
3. Regenerate key if needed: `dotnet run -- generate-encryption-key`

### Error: "No encrypted data found in database"

**Cause:** The `id_number` column is NULL for all records, or the table is empty.

**Solution:**
1. Check database: `SELECT COUNT(*) FROM petel_schema.school_students WHERE id_number IS NOT NULL;`
2. Verify you're connected to the correct database
3. If data truly doesn't exist, no re-encryption is needed

### Re-encryption Completed with Errors

**Cause:** Some records failed to decrypt/re-encrypt.

**Solution:**
1. Check application logs for specific errors: `C:\dev\PetelFullApp\PetelApp.Api\logs\`
2. Identify failed record IDs
3. Investigate those specific records in the database
4. May need to manually fix or delete corrupted records

## Rollback Procedure

If re-encryption fails or produces incorrect results:

### Option 1: Restore from Backup

```bash
# List available backups
az postgres flexible-server backup list \
    --resource-group petel-prod-rg \
    --name petel-prod-db-4407

# Restore from specific backup
az postgres flexible-server restore \
    --resource-group petel-prod-rg \
    --name petel-prod-db-4407 \
    --restore-point-in-time "2026-02-16T10:00:00Z" \
    --target-server-name petel-prod-db-restored
```

### Option 2: Re-run with Different Key

If you used the wrong old key, re-run with the correct one:

```powershell
.\Reencrypt-Production-Data.ps1
# Provide the correct test encryption key when prompted
```

## Technical Details

### How It Works

1. **Bypass EF Core Value Converters**: Uses raw SQL to read encrypted data directly from database
2. **Decrypt with Old Key**: Uses AES-256-CBC with the test environment's key
3. **Re-encrypt with New Key**: Uses the production environment's key
4. **Direct SQL Update**: Updates database without triggering EF Core's automatic encryption

### Security Considerations

✅ **Keys are retrieved from Azure Key Vault** - Not hardcoded
✅ **Confirmation prompts** - Prevents accidental execution
✅ **Audit trail** - All operations are logged
✅ **Backup verification** - Forces user to confirm backup exists
⚠️ **Keys visible in process memory** - Run on secure machine only
⚠️ **Database modification** - Always test in non-production first

### Performance

- **Processing speed**: ~100-500 records per second (depending on record size)
- **Database load**: Low - single record updates with no joins
- **Memory usage**: Minimal - processes one record at a time
- **Estimated time**: 
  - 100 records: ~10 seconds
  - 1,000 records: ~1 minute
  - 10,000 records: ~10 minutes

## Support

If you encounter issues:

1. **Check logs**: `C:\dev\PetelFullApp\PetelApp.Api\logs\`
2. **Verify keys**: Ensure correct keys are in Key Vault
3. **Test with sample**: Use test-decrypt command with known encrypted value
4. **Database backup**: Always restore from backup if data is corrupted

## Related Documentation

- [Database Configuration Guide](DATABASE_CONFIGURATION_COMPLETE.md)
- [Data Encryption Implementation](JWT_DATABASE_CONFIG_IMPLEMENTATION.md)
- [Deployment Guide](DEPLOYMENT_GUIDE.md)
