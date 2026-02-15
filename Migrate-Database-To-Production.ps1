# ============================================
# Database Backup and Restore Script
# Test Database → Production Database
# ============================================
# Prerequisites: PostgreSQL tools installed
# Download: https://www.postgresql.org/download/windows/
# ============================================

param(
    [switch]$BackupOnly,
    [switch]$RestoreOnly,
    [string]$BackupFile = "petel-test-backup-$(Get-Date -Format 'yyyyMMdd-HHmmss').sql"
)

$ErrorActionPreference = "Stop"

# Configuration
$testServer = "petel-test-db.postgres.database.azure.com"
$testDb = "petelappdb"
$testUser = "PetelAdmin"

$prodServer = "petel-prod-db-4407.postgres.database.azure.com"
$prodDb = "petelappdb"
$prodUser = "peteldbadmin"

Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "Database Migration - Test to Production" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

# Check if PostgreSQL tools are installed
Write-Host "Checking for PostgreSQL tools..." -ForegroundColor Yellow
$pgDump = Get-Command pg_dump -ErrorAction SilentlyContinue
$psql = Get-Command psql -ErrorAction SilentlyContinue

if (-not $pgDump -or -not $psql) {
    Write-Host "PostgreSQL not in PATH, searching common locations..." -ForegroundColor Yellow
    
    # Search common PostgreSQL installation paths
    $pgPaths = @(
        "C:\Program Files\PostgreSQL\17\bin",
        "C:\Program Files\PostgreSQL\16\bin",
        "C:\Program Files\PostgreSQL\15\bin",
        "C:\Program Files\PostgreSQL\14\bin",
        "C:\Program Files (x86)\PostgreSQL\17\bin",
        "C:\Program Files (x86)\PostgreSQL\16\bin",
        "C:\PostgreSQL\17\bin",
        "C:\PostgreSQL\16\bin"
    )
    
    $foundPath = $null
    foreach ($path in $pgPaths) {
        if (Test-Path (Join-Path $path "pg_dump.exe")) {
            $foundPath = $path
            break
        }
    }
    
    if ($foundPath) {
        Write-Host "FOUND PostgreSQL at: $foundPath" -ForegroundColor Green
        Write-Host "Adding to PATH for this session..." -ForegroundColor Gray
        $env:Path += ";$foundPath"
        
        # Verify tools are now available
        $pgDump = Get-Command pg_dump -ErrorAction SilentlyContinue
        $psql = Get-Command psql -ErrorAction SilentlyContinue
        
        if ($pgDump -and $psql) {
            Write-Host "SUCCESS: PostgreSQL tools are now available" -ForegroundColor Green
        }
    } else {
        Write-Host ""
        Write-Host "ERROR: PostgreSQL tools not found!" -ForegroundColor Red
        Write-Host ""
        Write-Host "Please install PostgreSQL client tools:" -ForegroundColor Yellow
        Write-Host "  1. Download from: https://www.postgresql.org/download/windows/" -ForegroundColor White
        Write-Host "  2. Or install with Chocolatey: choco install postgresql" -ForegroundColor White
        Write-Host "  3. Add to PATH: C:\Program Files\PostgreSQL\17\bin" -ForegroundColor White
        Write-Host ""
        Write-Host "Alternative: Use DATABASE_MIGRATION_GUIDE.md for GUI tools (pgAdmin, DBeaver)" -ForegroundColor Yellow
        Write-Host ""
        exit 1
    }
}

Write-Host "SUCCESS: PostgreSQL tools found" -ForegroundColor Green
Write-Host "  pg_dump: $($pgDump.Source)" -ForegroundColor Gray
Write-Host "  psql: $($psql.Source)" -ForegroundColor Gray
Write-Host ""

# Backup Phase
if (-not $RestoreOnly) {
    Write-Host "============================================" -ForegroundColor Yellow
    Write-Host "Phase 1: Backup Test Database" -ForegroundColor Yellow
    Write-Host "============================================" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Source Database:" -ForegroundColor Cyan
    Write-Host "  Server:   $testServer" -ForegroundColor White
    Write-Host "  Database: $testDb" -ForegroundColor White
    Write-Host "  Username: $testUser" -ForegroundColor White
    Write-Host ""
    
    # Prompt for test database password
    Write-Host "Enter password for $testUser@$testServer" -ForegroundColor Yellow
    $testPassSecure = Read-Host -AsSecureString
    $BSTR = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($testPassSecure)
    $testPassword = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto($BSTR)
    [System.Runtime.InteropServices.Marshal]::ZeroFreeBSTR($BSTR)
    
    Write-Host ""
    Write-Host "Creating backup... (this may take several minutes)" -ForegroundColor Gray
    Write-Host "Output file: $BackupFile" -ForegroundColor White
    Write-Host ""
    
    $env:PGPASSWORD = $testPassword
    $env:PGSSLMODE = "require"
    
    try {
        # Run pg_dump with verbose output and SSL
        pg_dump `
            -h $testServer `
            -U $testUser `
            -d $testDb `
            -F p `
            --no-owner `
            --no-privileges `
            --verbose `
            -f $BackupFile 2>&1 | Out-String | Write-Host
        
        $env:PGPASSWORD = $null
        
        if (Test-Path $BackupFile) {
            $fileSize = (Get-Item $BackupFile).Length / 1MB
            Write-Host ""
            Write-Host "SUCCESS: Backup completed!" -ForegroundColor Green
            Write-Host "  File: $BackupFile" -ForegroundColor White
            Write-Host "  Size: $([math]::Round($fileSize, 2)) MB" -ForegroundColor White
            Write-Host ""
        } else {
            Write-Host "ERROR: Backup file not created!" -ForegroundColor Red
            exit 1
        }
    }
    catch {
        $env:PGPASSWORD = $null
        $env:PGSSLMODE = $null
        Write-Host ""
        Write-Host "ERROR during backup: $($_.Exception.Message)" -ForegroundColor Red
        Write-Host ""
        Write-Host "Common issues:" -ForegroundColor Yellow
        Write-Host "  - Wrong password" -ForegroundColor White
        Write-Host "  - Windows Firewall blocking PostgreSQL (port 5432)" -ForegroundColor White
        Write-Host "  - Azure firewall rules not propagated yet (wait 1-2 minutes)" -ForegroundColor White
        Write-Host "  - Network/VPN issues" -ForegroundColor White
        Write-Host ""
        Write-Host "Quick fixes:" -ForegroundColor Cyan
        Write-Host "  1. Check Windows Firewall: Allow outbound port 5432" -ForegroundColor White
        Write-Host "  2. Test connection: psql -h $testServer -U $testUser -d $testDb" -ForegroundColor White
        Write-Host "  3. Wait a minute and try again" -ForegroundColor White
        Write-Host ""
        exit 1
    }
    
    if ($BackupOnly) {
        Write-Host "Backup complete! Run again without -BackupOnly to restore to production." -ForegroundColor Yellow
        exit 0
    }
    
    Write-Host "Press any key to continue with restore to production..."
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
}

# Restore Phase
if (-not $BackupOnly) {
    Write-Host ""
    Write-Host "============================================" -ForegroundColor Yellow
    Write-Host "Phase 2: Restore to Production Database" -ForegroundColor Yellow
    Write-Host "============================================" -ForegroundColor Yellow
    Write-Host ""
    
    if ($RestoreOnly) {
        # Prompt for backup file if not specified
        if (-not (Test-Path $BackupFile)) {
            Write-Host "Available backup files:" -ForegroundColor Cyan
            Get-ChildItem -Filter "petel-test-backup-*.sql" | ForEach-Object {
                Write-Host "  $($_.Name) ($([math]::Round($_.Length / 1MB, 2)) MB)" -ForegroundColor White
            }
            Write-Host ""
            $BackupFile = Read-Host "Enter backup file name"
            
            if (-not (Test-Path $BackupFile)) {
                Write-Host "ERROR: Backup file not found: $BackupFile" -ForegroundColor Red
                exit 1
            }
        }
    }
    
    Write-Host "Target Database:" -ForegroundColor Cyan
    Write-Host "  Server:   $prodServer" -ForegroundColor White
    Write-Host "  Database: $prodDb" -ForegroundColor White
    Write-Host "  Username: $prodUser" -ForegroundColor White
    Write-Host ""
    Write-Host "Backup file: $BackupFile" -ForegroundColor White
    Write-Host ""
    
    Write-Host "WARNING: This will overwrite the production database!" -ForegroundColor Red
    $confirm = Read-Host "Type 'RESTORE' to continue"
    
    if ($confirm -ne 'RESTORE') {
        Write-Host "Cancelled by user" -ForegroundColor Yellow
        exit 0
    }
    
    Write-Host ""
    Write-Host "Enter password for $prodUser@$prodServer" -ForegroundColor Yellow
    Write-Host "(See production-db-credentials-*.txt file)" -ForegroundColor Gray
    $prodPassSecure = Read-Host -AsSecureString
    $BSTR = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($prodPassSecure)
    $prodPassword = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto($BSTR)
    [System.Runtime.InteropServices.Marshal]::ZeroFreeBSTR($BSTR)
    
    Write-Host ""
    Write-Host "Restoring database... (this may take several minutes)" -ForegroundColor Gray
    Write-Host ""
    
    $env:PGPASSWORD = $prodPassword
    $env:PGSSLMODE = "require"
    
    try {
        # Run psql restore
        psql `
            -h $prodServer `
            -U $prodUser `
            -d $prodDb `
            -f $BackupFile 2>&1 | Out-String | Write-Host
        
        $env:PGPASSWORD = $null
        
        Write-Host ""
        Write-Host "SUCCESS: Database restored!" -ForegroundColor Green
        Write-Host ""
        Write-Host "Next Steps:" -ForegroundColor Yellow
        Write-Host "  1. Verify data with: psql -h $prodServer -U $prodUser -d $prodDb" -ForegroundColor White
        Write-Host "  2. Continue with PRODUCTION_DEPLOYMENT_GUIDE.md Phase 2" -ForegroundColor White
        Write-Host "     - Generate JWT and AES keys" -ForegroundColor White
        Write-Host "     - Add secrets to Key Vault" -ForegroundColor White
        Write-Host "     - Deploy application" -ForegroundColor White
        Write-Host ""
    }
    catch {
        $env:PGPASSWORD = $null
        Write-Host ""
        Write-Host "ERROR during restore: $($_.Exception.Message)" -ForegroundColor Red
        Write-Host ""
        Write-Host "Check output above for specific errors." -ForegroundColor Yellow
        Write-Host "You may need to manually fix schema conflicts." -ForegroundColor Yellow
        Write-Host ""
        exit 1
    }
}

Write-Host "============================================" -ForegroundColor Green
Write-Host "Database Migration Complete!" -ForegroundColor Green
Write-Host "============================================" -ForegroundColor Green
Write-Host ""
