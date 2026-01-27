# Archive Old Frontend Folder
# This script creates a backup of the petelapp-frontend folder before deletion

$ErrorActionPreference = "Stop"

Write-Host "[ARCHIVE] Archiving Old Frontend Folder" -ForegroundColor Cyan
Write-Host "=================================" -ForegroundColor Cyan

# Configuration
$sourceFolder = ".\petelapp-frontend"
$archiveFolder = ".\archived-frontend"
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$archiveName = "petelapp-frontend_backup_$timestamp.zip"
$archivePath = Join-Path $archiveFolder $archiveName

# Check if source folder exists
if (-not (Test-Path $sourceFolder)) {
    Write-Host "[ERROR] petelapp-frontend folder not found" -ForegroundColor Red
    exit 1
}

# Create archive directory
if (-not (Test-Path $archiveFolder)) {
    Write-Host "[INFO] Creating archive directory..." -ForegroundColor Yellow
    New-Item -ItemType Directory -Path $archiveFolder | Out-Null
}

# Create zip archive
Write-Host "[ARCHIVE] Creating backup archive: $archiveName" -ForegroundColor Yellow
try {
    Compress-Archive -Path $sourceFolder -DestinationPath $archivePath -CompressionLevel Optimal
    Write-Host "[OK] Backup created successfully!" -ForegroundColor Green
    Write-Host "   Location: $archivePath" -ForegroundColor Gray
    
    # Show archive size
    $archiveSize = (Get-Item $archivePath).Length / 1MB
    Write-Host "   Size: $([math]::Round($archiveSize, 2)) MB" -ForegroundColor Gray
    
} catch {
    Write-Host "[ERROR] Error creating archive: $_" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "[COMPLETE] Archive Complete!" -ForegroundColor Green
Write-Host "   The old frontend is backed up at: $archivePath" -ForegroundColor Gray
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "1. Review the archive to ensure everything is backed up" -ForegroundColor White
Write-Host "2. Run: git rm -r petelapp-frontend" -ForegroundColor White
Write-Host "3. Run: git commit -m 'Remove old vanilla JS frontend (migrated to Blazor)'" -ForegroundColor White
Write-Host "4. Update deployment scripts and documentation" -ForegroundColor White
