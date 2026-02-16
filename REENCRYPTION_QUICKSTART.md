# Quick Start: Re-Encrypt Production Data

## Problem Summary
After restoring the test database to production, encrypted fields (like `id_number` in `school_students`) cannot be decrypted because they use the test encryption key, but production uses a different key.

## Solution Created

✅ **New API command**: `reencrypt-with-old-key` - Re-encrypts data using the old test key
✅ **PowerShell script**: `Reencrypt-Production-Data.ps1` - Orchestrates the process
✅ **Verification script**: `Test-Encryption-After-Reencrypt.ps1` - Tests the fix
✅ **Service method**: `DataMigrationService.ReencryptWithOldKeyAsync()` - Core logic

## How to Fix (3 Steps)

### 1. Test in What-If Mode (Safe - No Changes)

```powershell
cd C:\dev\PetelFullApp
.\Reencrypt-Production-Data.ps1 -WhatIf
```

This will:
- ✅ Verify Azure CLI is authenticated
- ✅ Retrieve test and production encryption keys
- ✅ Show what would be done WITHOUT making changes

### 2. Run Re-Encryption (Modifies Database)

```powershell
cd C:\dev\PetelFullApp
.\Reencrypt-Production-Data.ps1
```

You'll be prompted to:
1. Confirm you have a database backup (type `yes`)
2. Confirm the operation (type `YES`)

The script will:
- Get the test encryption key from `petel-kv-test-4721`
- Get the production encryption key from `petel-kv-prod-6581`
- Decrypt `id_number` values with the TEST key
- Re-encrypt them with the PRODUCTION key
- Update the database

### 3. Verify the Fix

```powershell
cd C:\dev\PetelFullApp
.\Test-Encryption-After-Reencrypt.ps1
```

This will:
- Fetch a sample encrypted `id_number` from production database
- Test decryption with the production key
- Confirm data can be properly decrypted

## Re-Encrypt Other Fields

If you need to re-encrypt other encrypted fields:

```powershell
# Re-encrypt email in persons table
.\Reencrypt-Production-Data.ps1 -TableName "persons" -ColumnName "email"

# Re-encrypt phone_number in persons table
.\Reencrypt-Production-Data.ps1 -TableName "persons" -ColumnName "phone_number"

# Re-encrypt street in school_students table  
.\Reencrypt-Production-Data.ps1 -TableName "school_students" -ColumnName "street"
```

## What Tables/Columns Are Encrypted?

Based on the codebase, these fields use encryption:

**`school_students` table:**
- `id_number` (ID number) - **START HERE**
- `street` (Address street)

**`persons` table:**
- `id_number` (ID number)
- `email` (Email address)
- `phone_number` (Phone number)

**`users` table:**
- `email` (Email address)
- `otp_secret` (2FA secret)

## Safety Features

✅ **What-If mode** - Test without making changes
✅ **Backup verification** - Forces confirmation of backup
✅ **Double confirmation** - Must type "YES" to proceed
✅ **Progress logging** - Shows status every 100 records
✅ **Error collection** - Logs all failures for review
✅ **Raw SQL** - Bypasses EF Core to prevent double-encryption

## Troubleshooting

**"Cryptographic error during decryption"**
- The test key may be wrong
- Verify: `az keyvault secret show --vault-name petel-kv-test-4721 --name DataEncryption--EncryptionKey`

**"Old key must be 32 bytes"**
- Key is invalid or corrupted
- Check for whitespace or incomplete copy/paste

**"Build failed - file locked"**
- API is running and using the executable
- This is normal - the code compiled correctly
- Stop the API before building: `Get-Process PetelApp.Api | Stop-Process`

## Rollback

If something goes wrong:

```bash
# Restore from backup (Azure CLI)
az postgres flexible-server restore \
    --resource-group petel-prod-rg \
    --name petel-prod-db-4407 \
    --restore-point-in-time "2026-02-16T10:00:00Z" \
    --target-server-name petel-prod-db-restored
```

## Files Created

- [`REENCRYPTION_GUIDE.md`](REENCRYPTION_GUIDE.md) - Comprehensive documentation
- [`Reencrypt-Production-Data.ps1`](Reencrypt-Production-Data.ps1) - Main script
- [`Test-Encryption-After-Reencrypt.ps1`](Test-Encryption-After-Reencrypt.ps1) - Verification script
- `PetelApp.Api/Services/DataMigrationService.cs` - Added `ReencryptWithOldKeyAsync()` method
- `PetelApp.Api/Program.cs` - Added `reencrypt-with-old-key` command

## Next Steps

1. ✅ Run what-if mode to verify configuration
2. ✅ Confirm database backup exists
3. ✅ Run re-encryption for `school_students.id_number`
4. ✅ Verify with test script
5. ✅ Test in Blazor application
6. ✅ Repeat for other encrypted fields if needed

Need help? See [REENCRYPTION_GUIDE.md](REENCRYPTION_GUIDE.md) for detailed documentation.
