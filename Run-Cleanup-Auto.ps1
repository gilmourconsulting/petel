# Run Complete Cleanup - No Prompts
# This runs the full cleanup automatically for you

Write-Host "[START] Running complete cleanup process..." -ForegroundColor Cyan
Write-Host ""

# Run cleanup and automatically answer 'y' to prompts
$cleanup = @"
y
y
"@

$cleanup | .\Complete-Cleanup.ps1

Write-Host ""
Write-Host "[DONE] Cleanup complete! You can now run: .\Merge-To-Main.ps1" -ForegroundColor Green
