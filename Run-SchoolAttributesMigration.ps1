# =============================================
# Run School Attributes sort_order Migration
# =============================================

param(
    [string]$Environment = "test"
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "School Attributes sort_order Migration" -ForegroundColor Cyan
Write-Host "Environment: $Environment" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Get connection string from appsettings
$appsettingsPath = ".\PetelApp.Api\appsettings.$Environment.json"

if (-not (Test-Path $appsettingsPath)) {
    Write-Host "ERROR: appsettings file not found: $appsettingsPath" -ForegroundColor Red
    exit 1
}

Write-Host "Reading connection string from $appsettingsPath..." -ForegroundColor Gray

$appsettings = Get-Content $appsettingsPath | ConvertFrom-Json
$connectionString = $appsettings.ConnectionStrings.DefaultConnection

if ([string]::IsNullOrWhiteSpace($connectionString)) {
    Write-Host "ERROR: Connection string not found in appsettings" -ForegroundColor Red
    exit 1
}

Write-Host "SUCCESS: Connection string loaded" -ForegroundColor Green

# Parse connection string
$connParams = @{}
$connectionString -split ';' | ForEach-Object {
    if ($_ -match '(.+?)=(.+)') {
        $connParams[$matches[1].Trim()] = $matches[2].Trim()
    }
}

$host = $connParams['Host']
$port = if ($connParams['Port']) { $connParams['Port'] } else { '5432' }
$database = $connParams['Database']
$username = $connParams['Username']
$password = $connParams['Password']

Write-Host ""
Write-Host "Connection Details:" -ForegroundColor Cyan
Write-Host "  Host: $host" -ForegroundColor Gray
Write-Host "  Port: $port" -ForegroundColor Gray
Write-Host "  Database: $database" -ForegroundColor Gray
Write-Host "  Username: $username" -ForegroundColor Gray
Write-Host ""

# SQL script path
$sqlScript = ".\SQL\Migrations\Add_SortOrder_To_SchoolAttributeTypes.sql"

if (-not (Test-Path $sqlScript)) {
    Write-Host "ERROR: SQL script not found: $sqlScript" -ForegroundColor Red
    exit 1
}

Write-Host "SQL Script: $sqlScript" -ForegroundColor Gray
Write-Host ""

# Set PostgreSQL password environment variable
$env:PGPASSWORD = $password

Write-Host "Executing SQL script..." -ForegroundColor Yellow
Write-Host "----------------------------------------" -ForegroundColor Gray

# Run psql command
$psqlCommand = "psql -h $host -p $port -U $username -d $database -f `"$sqlScript`""

try {
    Invoke-Expression $psqlCommand
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host ""
        Write-Host "========================================" -ForegroundColor Green
        Write-Host "SUCCESS: Migration completed" -ForegroundColor Green
        Write-Host "========================================" -ForegroundColor Green
        Write-Host ""
        Write-Host "IMPORTANT: Restart the API to reload the SchoolAttributeCache" -ForegroundColor Yellow
    } else {
        Write-Host ""
        Write-Host "ERROR: Migration failed with exit code $LASTEXITCODE" -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host ""
    Write-Host "ERROR: Exception running migration" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    exit 1
} finally {
    # Clear password from environment
    Remove-Item Env:\PGPASSWORD -ErrorAction SilentlyContinue
}
