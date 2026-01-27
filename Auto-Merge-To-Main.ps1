# Auto-Merge to Main
# Automatically merges move_to_blazor to main with yes to all prompts

$ErrorActionPreference = "Stop"

Write-Host "[AUTO-MERGE] Starting automatic merge to main..." -ForegroundColor Cyan
Write-Host ""

# Check current branch
$currentBranch = git branch --show-current
if ($currentBranch -ne "move_to_blazor") {
    Write-Host "[ERROR] Must be on move_to_blazor branch. Current: $currentBranch" -ForegroundColor Red
    exit 1
}

# Check for uncommitted changes
$status = git status --porcelain
if ($status) {
    Write-Host "[ERROR] You have uncommitted changes. Commit them first." -ForegroundColor Red
    git status --short
    exit 1
}

# Show commits
Write-Host "[INFO] Commits to be merged:" -ForegroundColor Yellow
git log main..move_to_blazor --oneline | Select-Object -First 10
Write-Host ""

# Switch to main
Write-Host "[GIT] Switching to main branch..." -ForegroundColor Cyan
git checkout main
if ($LASTEXITCODE -ne 0) {
    Write-Host "[ERROR] Failed to checkout main" -ForegroundColor Red
    exit 1
}

# Pull latest
Write-Host "[GIT] Pulling latest from origin/main..." -ForegroundColor Cyan
git pull origin main
if ($LASTEXITCODE -ne 0) {
    Write-Host "[ERROR] Failed to pull" -ForegroundColor Red
    git checkout move_to_blazor
    exit 1
}

# Merge
Write-Host "[GIT] Merging move_to_blazor..." -ForegroundColor Cyan
git merge move_to_blazor --no-ff -m "Merge move_to_blazor: Complete migration to Blazor Server"
if ($LASTEXITCODE -ne 0) {
    Write-Host "[ERROR] Merge failed - conflicts detected" -ForegroundColor Red
    Write-Host "Resolve conflicts manually then run: git commit && git push origin main" -ForegroundColor Yellow
    exit 1
}

Write-Host "[OK] Merge successful!" -ForegroundColor Green
Write-Host ""

# Build tests
Write-Host "[TEST] Testing builds..." -ForegroundColor Cyan
Push-Location PetelApp.Api
dotnet build --nologo --verbosity quiet
$apiOk = $LASTEXITCODE -eq 0
Pop-Location

Push-Location PetelApp.BlazorServer  
dotnet build --nologo --verbosity quiet
$blazorOk = $LASTEXITCODE -eq 0
Pop-Location

if ($apiOk) { Write-Host "   [OK] API build passed" -ForegroundColor Green } else { Write-Host "   [WARN] API build failed" -ForegroundColor Yellow }
if ($blazorOk) { Write-Host "   [OK] Blazor build passed" -ForegroundColor Green } else { Write-Host "   [WARN] Blazor build failed" -ForegroundColor Yellow }
Write-Host ""

# Push
Write-Host "[GIT] Pushing to origin/main..." -ForegroundColor Cyan
git push origin main
if ($LASTEXITCODE -ne 0) {
    Write-Host "[ERROR] Push failed" -ForegroundColor Red
    exit 1
}

Write-Host "[OK] Pushed successfully!" -ForegroundColor Green
Write-Host ""
Write-Host "[SUCCESS] Merge Complete!" -ForegroundColor Green
Write-Host ""
Write-Host "Summary:" -ForegroundColor Cyan
Write-Host "   - Merged move_to_blazor into main" -ForegroundColor White
Write-Host "   - Pushed to origin/main" -ForegroundColor White
Write-Host "   - Branch move_to_blazor kept (delete manually if desired)" -ForegroundColor White
Write-Host ""
Write-Host "To delete the feature branch:" -ForegroundColor Gray
Write-Host "   git branch -d move_to_blazor" -ForegroundColor Gray
Write-Host "   git push origin --delete move_to_blazor" -ForegroundColor Gray
Write-Host ""
