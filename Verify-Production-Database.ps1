# ============================================
# Production Database Verification Script
# ============================================
# Verifies database connectivity and data integrity
# ============================================

param(
    [switch]$Detailed
)

$ErrorActionPreference = "Stop"

$server = "petel-prod-db-4407.postgres.database.azure.com"
$database = "petelappdb"
$username = "peteldbadmin"

Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "Production Database Verification" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Server:   $server" -ForegroundColor White
Write-Host "Database: $database" -ForegroundColor White
Write-Host "Username: $username" -ForegroundColor White
Write-Host ""

# Check for psql
$psql = Get-Command psql -ErrorAction SilentlyContinue
if (-not $psql) {
    Write-Host "psql not in PATH, searching common locations..." -ForegroundColor Yellow
    
    # Search common PostgreSQL installation paths
    $pgPaths = @(
        "C:\Program Files\PostgreSQL\17\bin",
        "C:\Program Files\PostgreSQL\16\bin",
        "C:\Program Files\PostgreSQL\15\bin",
        "C:\Program Files\PostgreSQL\14\bin",
        "C:\Program Files (x86)\PostgreSQL\17\bin",
        "C:\PostgreSQL\17\bin"
    )
    
    $foundPath = $null
    foreach ($path in $pgPaths) {
        if (Test-Path (Join-Path $path "psql.exe")) {
            $foundPath = $path
            break
        }
    }
    
    if ($foundPath) {
        Write-Host "FOUND PostgreSQL at: $foundPath" -ForegroundColor Green
        $env:Path += ";$foundPath"
        $psql = Get-Command psql -ErrorAction SilentlyContinue
    } else {
        Write-Host "ERROR: psql command not found!" -ForegroundColor Red
        Write-Host "Install PostgreSQL client tools from: https://www.postgresql.org/download/windows/" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "Alternative: Use Azure Portal Data Studio or pgAdmin" -ForegroundColor Yellow
        exit 1
    }
}

Write-Host "Enter password for $username@$server" -ForegroundColor Yellow
Write-Host "(See production-db-credentials-*.txt file)" -ForegroundColor Gray
$passSecure = Read-Host -AsSecureString
$BSTR = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($passSecure)
$password = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto($BSTR)
[System.Runtime.InteropServices.Marshal]::ZeroFreeBSTR($BSTR)

$env:PGPASSWORD = $password

Write-Host ""
Write-Host "Running verification queries..." -ForegroundColor Gray
Write-Host ""

# Basic verification queries
$queries = @"
-- Database size
SELECT pg_size_pretty(pg_database_size('$database')) as database_size;

-- Schema verification
SELECT schema_name FROM information_schema.schemata WHERE schema_name = 'petel_schema';

-- Table count
SELECT COUNT(*) as table_count FROM information_schema.tables WHERE table_schema = 'petel_schema';

-- Key tables existence
SELECT 
    CASE WHEN EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'petel_schema' AND table_name = 'users') THEN 'YES' ELSE 'NO' END as users_table,
    CASE WHEN EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'petel_schema' AND table_name = 'schools') THEN 'YES' ELSE 'NO' END as schools_table,
    CASE WHEN EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'petel_schema' AND table_name = 'hebrew_years') THEN 'YES' ELSE 'NO' END as hebrew_years_table,
    CASE WHEN EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'petel_schema' AND table_name = 'school_students') THEN 'YES' ELSE 'NO' END as students_table;

-- Record counts
SELECT 
    (SELECT COUNT(*) FROM petel_schema.users) as user_count,
    (SELECT COUNT(*) FROM petel_schema.schools) as school_count,
    (SELECT COUNT(*) FROM petel_schema.hebrew_years) as year_count,
    (SELECT COUNT(*) FROM petel_schema.school_students) as student_count;
"@

$tempFile = [System.IO.Path]::GetTempFileName()
$queries | Out-File -FilePath $tempFile -Encoding utf8

try {
    psql -h $server -U $username -d $database -f $tempFile
    
    Write-Host ""
    Write-Host "SUCCESS: Database verification completed!" -ForegroundColor Green
    Write-Host ""
    
    if ($Detailed) {
        Write-Host "Running detailed analysis..." -ForegroundColor Yellow
        
        $detailedQueries = @"
-- All tables in petel_schema
SELECT table_name, 
       (xpath('/row/cnt/text()', xml_count))[1]::text::int as row_count
FROM (
    SELECT table_name, 
           query_to_xml(format('SELECT COUNT(*) as cnt FROM petel_schema.%I', table_name), false, true, '') as xml_count
    FROM information_schema.tables
    WHERE table_schema = 'petel_schema'
    ORDER BY table_name
) t;

-- Recent activity (if audit fields exist)
SELECT 'users' as table_name, MAX(created_at) as last_created
FROM petel_schema.users
UNION ALL
SELECT 'schools', MAX(created_at)
FROM petel_schema.schools
UNION ALL
SELECT 'school_students', MAX(created_at)
FROM petel_schema.school_students;
"@
        
        $detailedQueries | Out-File -FilePath $tempFile -Encoding utf8
        psql -h $server -U $username -d $database -f $tempFile
    }
    
    Write-Host ""
    Write-Host "Database Status: HEALTHY" -ForegroundColor Green
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Yellow
    Write-Host "  1. Continue with PRODUCTION_DEPLOYMENT_GUIDE.md Phase 2" -ForegroundColor White
    Write-Host "  2. Generate JWT secret key (64 characters)" -ForegroundColor White
    Write-Host "  3. Generate AES encryption key (32 bytes)" -ForegroundColor White
    Write-Host "  4. Add secrets to Key Vault: petel-kv-prod-6581" -ForegroundColor White
    Write-Host "  5. Deploy application code" -ForegroundColor White
    Write-Host ""
}
catch {
    Write-Host ""
    Write-Host "ERROR: Database verification failed!" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    Write-Host ""
    Write-Host "Possible issues:" -ForegroundColor Yellow
    Write-Host "  - Database not restored yet" -ForegroundColor White
    Write-Host "  - Wrong password" -ForegroundColor White
    Write-Host "  - Firewall blocking connection" -ForegroundColor White
    Write-Host "  - Schema not created" -ForegroundColor White
    Write-Host ""
    exit 1
}
finally {
    $env:PGPASSWORD = $null
    Remove-Item -Path $tempFile -ErrorAction SilentlyContinue
}

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "Verification Complete" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""
