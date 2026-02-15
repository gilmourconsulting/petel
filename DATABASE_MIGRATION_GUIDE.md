# Database Migration Guide - Test to Production

**Date:** February 15, 2026  
**Source:** petel-test-db.postgres.database.azure.com (petel-test-rg)  
**Target:** petel-prod-db-4407.postgres.database.azure.com (petel-prod-rg)

---

## Option 1: Point-in-Time Restore (Fastest - Recommended)

Azure PostgreSQL automatically backs up your test database. You can restore it to production.

### Step 1: Create Restore from Test Database

```powershell
# Get latest backup time
az postgres flexible-server show `
    --name petel-test-db `
    --resource-group petel-test-rg `
    --query "backup.earliestRestoreDate" -o tsv

# Restore test database to production server
# This creates a NEW server with test data
az postgres flexible-server restore `
    --resource-group petel-prod-rg `
    --name petel-prod-db-restored `
    --source-server /subscriptions/cab259e3-0053-427d-a93a-9330eff7dcd3/resourceGroups/petel-test-rg/providers/Microsoft.DBforPostgreSQL/flexibleServers/petel-test-db `
    --restore-time "$(Get-Date -Format 'yyyy-MM-ddTHH:mm:ss')"
```

**Issue:** This creates a NEW server, not restores to existing.

---

## Option 2: Install PostgreSQL Tools and Dump/Restore (Recommended)

### Step 1: Install PostgreSQL Client Tools

**Download:** https://www.postgresql.org/download/windows/

Or use Chocolatey:
```powershell
choco install postgresql
```

### Step 2: Create Dump from Test Database

```powershell
$testServer = "petel-test-db.postgres.database.azure.com"
$testDb = "petelappdb"
$testUser = "PetelAdmin"
$dumpFile = "petel-test-backup-$(Get-Date -Format 'yyyyMMdd').sql"

# Run pg_dump (will prompt for password)
pg_dump `
    -h $testServer `
    -U $testUser `
    -d $testDb `
    -F p `
    --no-owner `
    --no-privileges `
    -f $dumpFile

# Verify dump
if (Test-Path $dumpFile) {
    $sizeMB = (Get-Item $dumpFile).Length / 1MB
    Write-Host "Dump created: $dumpFile ($([math]::Round($sizeMB, 2)) MB)"
}
```

**Password:** Use the test database password (from test environment setup)

### Step 3: Restore to Production Database

```powershell
$prodServer = "petel-prod-db-4407.postgres.database.azure.com"
$prodDb = "petelappdb"
$prodUser = "peteldbadmin"
$dumpFile = "petel-test-backup-YYYYMMDD.sql"  # Use actual filename

# Run psql restore (will prompt for password)
psql `
    -h $prodServer `
    -U $prodUser `
    -d $prodDb `
    -f $dumpFile

# Check for errors
Write-Host "Restore complete! Check output for any errors."
```

**Password:** See production-db-credentials-20260215-184336.txt

### Step 4: Verify Production Database

```powershell
# Connect to production database
psql -h $prodServer -U $prodUser -d $prodDb

# Run verification queries
SELECT COUNT(*) FROM petel_schema.users;
SELECT COUNT(*) FROM petel_schema.roles;
SELECT COUNT(*) FROM petel_schema.schools;

\q
```

---

## Option 3: Use pgAdmin (GUI Tool)

### Step 1: Install pgAdmin

**Download:** https://www.pgadmin.org/download/

### Step 2: Connect to Test Database

1. Open pgAdmin
2. Add Server → Test Database
   - Host: petel-test-db.postgres.database.azure.com
   - Port: 5432
   - Database: petelappdb
   - Username: PetelAdmin
   - SSL Mode: Require

### Step 3: Backup Test Database

1. Right-click on `petelappdb` database
2. Select "Backup..."
3. Format: Plain (SQL)
4. File: Choose location (e.g., `C:\backups\petel-test-backup.sql`)
5. Options:
   - Include DROP statements: No
   - Include CREATE statements: No
   - Use INSERT commands: Yes
   - Include blobs: Yes
6. Click "Backup"

### Step 4: Connect to Production Database

1. Add Server → Production Database
   - Host: petel-prod-db-4407.postgres.database.azure.com
   - Port: 5432
   - Database: petelappdb
   - Username: peteldbadmin
   - Password: (from production-db-credentials-*.txt)
   - SSL Mode: Require

### Step 5: Restore to Production

1. Right-click on `petelappdb` database
2. Select "Restore..."
3. Format: Plain (SQL)
4. File: Select backup file
5. Options:
   - Restore: Data only
6. Click "Restore"

---

## Option 4: Use DBeaver (Alternative GUI)

### Step 1: Install DBeaver

**Download:** https://dbeaver.io/download/

### Step 2: Export from Test

1. Connect to test database
2. Tools → Database → Export Data
3. Select all tables in `petel_schema`
4. Choose SQL INSERT format
5. Export to file

### Step 3: Import to Production

1. Connect to production database
2. Tools → Database → Execute SQL Script
3. Select exported file
4. Run

---

## Option 5: Azure Data Studio

### Step 1: Install Azure Data Studio with PostgreSQL Extension

**Download:** https://docs.microsoft.com/azure-data-studio/download

### Step 2: Connect and Export

1. Install PostgreSQL extension
2. Connect to test database
3. Right-click schema → Generate Script → CREATE and INSERT
4. Save script

### Step 3: Connect and Import

1. Connect to production database
2. File → Open (exported script)
3. Run script

---

## Quick Command Reference

### Test Database Connection Info
```
Server:   petel-test-db.postgres.database.azure.com
Database: petelappdb
Username: PetelAdmin
Password: (from test environment setup)
Port:     5432
SSL:      Required
```

### Production Database Connection Info
```
Server:   petel-prod-db-4407.postgres.database.azure.com
Database: petelappdb
Username: peteldbadmin
Password: (see production-db-credentials-20260215-184336.txt)
Port:     5432
SSL:      Required
```

### PostgreSQL Connection String Format
```
Host=SERVER;Database=DATABASE;Username=USERNAME;Password=PASSWORD;SslMode=Require
```

---

## Troubleshooting

### Error: Connection Timeout

**Cause:** Azure firewall blocking connection  
**Solution:** Add your IP to firewall rules

```powershell
# Get your public IP
$myIp = (Invoke-WebRequest -Uri "https://api.ipify.org").Content

# Add to test database
az postgres flexible-server firewall-rule create `
    --resource-group petel-test-rg `
    --name petel-test-db `
    --rule-name "MyComputer" `
    --start-ip-address $myIp `
    --end-ip-address $myIp

# Add to production database
az postgres flexible-server firewall-rule create `
    --resource-group petel-prod-rg `
    --name petel-prod-db-4407 `
    --rule-name "MyComputer" `
    --start-ip-address $myIp `
    --end-ip-address $myIp
```

### Error: Password Authentication Failed

**Solution:** Verify you're using the correct username/password format

For Azure PostgreSQL, username format is: `username@servername`

### Error: SSL Required

**Solution:** Always use `sslmode=require` in connection strings

---

## Recommended Approach

**For fastest migration:**

1. Install pgAdmin (easiest GUI) or PostgreSQL tools (command-line)
2. Backup test database to local file
3. Restore to production database
4. Verify data integrity
5. Continue with PRODUCTION_DEPLOYMENT_GUIDE.md Phase 2

**Installation Time:** 10-15 minutes  
**Backup Time:** 5-10 minutes (depends on database size)  
**Restore Time:** 5-10 minutes  
**Total:** ~20-35 minutes

---

## Next Steps After Database Restore

See [PRODUCTION_DEPLOYMENT_GUIDE.md](PRODUCTION_DEPLOYMENT_GUIDE.md) Phase 2:

1. Generate JWT and AES encryption keys
2. Add secrets to Key Vault
3. Configure App Service Key Vault references
4. Deploy application code
5. Setup Front Door and WAF
6. Validate deployment

---

## Database Statistics Check

After restore, verify data was copied:

```sql
-- Connect to production database
\c petelappdb

-- Check schema
\dn

-- Check all tables
SELECT schemaname, tablename, 
       pg_size_pretty(pg_total_relation_size(schemaname||'.'||tablename)) AS size
FROM pg_tables 
WHERE schemaname = 'petel_schema'
ORDER BY pg_total_relation_size(schemaname||'.'||tablename) DESC;

-- Count records in key tables
SELECT 
    'users' as table_name, COUNT(*) as records FROM petel_schema.users
UNION ALL
SELECT 'schools', COUNT(*) FROM petel_schema.schools
UNION ALL
SELECT 'students', COUNT(*) FROM petel_schema.school_students
UNION ALL
SELECT 'roles', COUNT(*) FROM petel_schema.roles;
```

---

**Need Help?** See PRODUCTION_DEPLOYMENT_GUIDE.md for full deployment guide.
