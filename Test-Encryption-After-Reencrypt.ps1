# Test-Encryption-After-Reencrypt.ps1
# Tests that encrypted data can be properly decrypted after re-encryption

param(
    [Parameter(Mandatory=$false)]
    [string]$EncryptedValue
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "TEST ENCRYPTION/DECRYPTION" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$ApiPath = "C:\dev\PetelFullApp\PetelApp.Api"
$ProdKeyVault = "petel-kv-prod-6581"

# Navigate to API project
if (-not (Test-Path $ApiPath)) {
    Write-Host "❌ API path not found: $ApiPath" -ForegroundColor Red
    exit 1
}
Push-Location $ApiPath

if ([string]::IsNullOrWhiteSpace($EncryptedValue)) {
    # Get a sample encrypted value from database
    Write-Host "No encrypted value provided. Fetching sample from database..." -ForegroundColor Yellow
    Write-Host ""
    
    # Get production connection string
    $connString = az keyvault secret show `
        --vault-name $ProdKeyVault `
        --name "ConnectionStrings--DefaultConnection" `
        --query "value" -o tsv
    
    if ([string]::IsNullOrWhiteSpace($connString)) {
        Write-Host "❌ Failed to get connection string" -ForegroundColor Red
        Pop-Location
        exit 1
    }
    
    # Parse connection string to get components
    $connParts = @{}
    $connString.Split(';') | ForEach-Object {
        if ($_ -match '(.+?)=(.+)') {
            $connParts[$matches[1]] = $matches[2]
        }
    }
    
    $dbHost = $connParts['Host']
    $database = $connParts['Database']
    $username = $connParts['Username']
    $password = $connParts['Password']
    
    Write-Host "Connecting to production database..." -ForegroundColor Yellow
    Write-Host "  Host: $dbHost" -ForegroundColor Gray
    Write-Host "  Database: $database" -ForegroundColor Gray
    Write-Host ""
    
    # Set PGPASSWORD for psql
    $env:PGPASSWORD = $password
    
    try {
        # Query for a sample encrypted id_number
        $query = "SELECT id, id_number FROM petel_schema.school_students WHERE id_number IS NOT NULL LIMIT 1;"
        $result = psql -h $dbHost -U $username -d $database -t -A -c $query 2>&1
        
        if ($LASTEXITCODE -ne 0) {
            Write-Host "❌ Database query failed: $result" -ForegroundColor Red
            Pop-Location
            exit 1
        }
        
        # Parse result
        $parts = $result.Split('|')
        if ($parts.Count -ge 2) {
            $studentId = $parts[0]
            $EncryptedValue = $parts[1]
            Write-Host "✅ Sample record retrieved:" -ForegroundColor Green
            Write-Host "   Student ID: $studentId" -ForegroundColor White
            Write-Host "   Encrypted value (first 50 chars): $($EncryptedValue.Substring(0, [Math]::Min(50, $EncryptedValue.Length)))..." -ForegroundColor White
            Write-Host ""
        } else {
            Write-Host "❌ No encrypted data found in database" -ForegroundColor Red
            Pop-Location
            exit 1
        }
    } finally {
        Remove-Item env:PGPASSWORD -ErrorAction SilentlyContinue
    }
}

# Test decryption
Write-Host "Testing decryption with production key..." -ForegroundColor Yellow
Write-Host ""

try {
    & dotnet run -- test-decrypt $EncryptedValue
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host ""
        Write-Host "========================================" -ForegroundColor Green
        Write-Host "✅ DECRYPTION TEST PASSED" -ForegroundColor Green
        Write-Host "========================================" -ForegroundColor Green
        Write-Host ""
        Write-Host "The encrypted data can be properly decrypted with the production key." -ForegroundColor Green
        Write-Host "Re-encryption was successful!" -ForegroundColor Green
    } else {
        Write-Host ""
        Write-Host "========================================" -ForegroundColor Red
        Write-Host "❌ DECRYPTION TEST FAILED" -ForegroundColor Red
        Write-Host "========================================" -ForegroundColor Red
        Write-Host ""
        Write-Host "The data could not be decrypted. Possible causes:" -ForegroundColor Yellow
        Write-Host "  • Re-encryption has not been run yet" -ForegroundColor White
        Write-Host "  • Wrong encryption key is configured" -ForegroundColor White
        Write-Host "  • Data is corrupted" -ForegroundColor White
    }
} catch {
    Write-Host ""
    Write-Host "❌ Error running decryption test: $_" -ForegroundColor Red
}

Pop-Location
