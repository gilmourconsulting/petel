# Merge move_to_blazor to main
# This script helps you safely merge the move_to_blazor branch to main

param(
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"

Write-Host "[MERGE] Merge move_to_blazor to main" -ForegroundColor Cyan
Write-Host "===============================" -ForegroundColor Cyan
Write-Host ""

# Check current branch
$currentBranch = git branch --show-current
Write-Host "Current branch: $currentBranch" -ForegroundColor Gray

if ($currentBranch -ne "move_to_blazor") {
    Write-Host "[WARNING] You're not on move_to_blazor branch" -ForegroundColor Yellow
    $continue = Read-Host "Continue anyway? (y/n)"
    if ($continue -ne 'y' -and $continue -ne 'Y') {
        Write-Host "Aborting." -ForegroundColor Red
        exit 1
    }
}
Write-Host ""

# Check for uncommitted changes
$status = git status --porcelain
if ($status) {
    Write-Host "[ERROR] You have uncommitted changes:" -ForegroundColor Red
    git status --short
    Write-Host ""
    Write-Host "Please commit or stash your changes before merging." -ForegroundColor Yellow
    Write-Host "Run: git add . && git commit -m 'Your message'" -ForegroundColor Gray
    exit 1
}

# Show commits that will be merged
Write-Host "[INFO] Commits to be merged:" -ForegroundColor Yellow
git log main..move_to_blazor --oneline | Select-Object -First 10
Write-Host ""

# Confirm merge
$confirm = Read-Host "Ready to merge move_to_blazor into main? (y/n)"
if ($confirm -ne 'y' -and $confirm -ne 'Y') {
    Write-Host "Merge cancelled." -ForegroundColor Gray
    exit 0
}
Write-Host ""

# Switch to main
Write-Host "[GIT] Switching to main branch..." -ForegroundColor Cyan
git checkout main
if ($LASTEXITCODE -ne 0) {
    Write-Host "[ERROR] Failed to checkout main branch" -ForegroundColor Red
    exit 1
}
Write-Host ""

# Pull latest changes
Write-Host "[GIT] Pulling latest changes from origin/main..." -ForegroundColor Cyan
git pull origin main
if ($LASTEXITCODE -ne 0) {
    Write-Host "[ERROR] Failed to pull from origin/main" -ForegroundColor Red
    Write-Host "Switching back to move_to_blazor..." -ForegroundColor Yellow
    git checkout move_to_blazor
    exit 1
}
Write-Host ""

# Perform merge
Write-Host "[GIT] Merging move_to_blazor into main..." -ForegroundColor Cyan
git merge move_to_blazor --no-ff
$mergeResult = $LASTEXITCODE

if ($mergeResult -ne 0) {
    Write-Host ""
    Write-Host "[ERROR] Merge conflicts detected!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Conflicting files:" -ForegroundColor Yellow
    git status --short | Where-Object { $_ -match "^(UU|AA|DD)" }
    Write-Host ""
    Write-Host "Please resolve conflicts manually:" -ForegroundColor Yellow
    Write-Host "1. Open conflicting files and resolve conflicts" -ForegroundColor White
    Write-Host "2. Stage resolved files: git add <file>" -ForegroundColor White
    Write-Host "3. Complete merge: git commit" -ForegroundColor White
    Write-Host "4. Push: git push origin main" -ForegroundColor White
    Write-Host ""
    Write-Host "Or abort merge: git merge --abort" -ForegroundColor Gray
    exit 1
}

Write-Host "[OK] Merge successful!" -ForegroundColor Green
Write-Host ""

# Run tests (optional)
if (-not $SkipTests) {
    Write-Host "[TEST] Running tests..." -ForegroundColor Cyan
    
    # Test API
    Write-Host "   Testing API build..." -ForegroundColor Gray
    Push-Location PetelApp.Api
    dotnet build --nologo --verbosity quiet
    $apiBuildResult = $LASTEXITCODE
    Pop-Location
    
    if ($apiBuildResult -ne 0) {
        Write-Host "   [WARNING] API build failed" -ForegroundColor Yellow
    } else {
        Write-Host "   [OK] API build successful" -ForegroundColor Green
    }
    
    # Test Blazor
    Write-Host "   Testing Blazor build..." -ForegroundColor Gray
    Push-Location PetelApp.BlazorServer
    dotnet build --nologo --verbosity quiet
    $blazorBuildResult = $LASTEXITCODE
    Pop-Location
    
    if ($blazorBuildResult -ne 0) {
        Write-Host "   [WARNING] Blazor build failed" -ForegroundColor Yellow
    } else {
        Write-Host "   [OK] Blazor build successful" -ForegroundColor Green
    }
    
    Write-Host ""
    
    if ($apiBuildResult -ne 0 -or $blazorBuildResult -ne 0) {
        Write-Host "[WARNING] Some builds failed. Please review before pushing." -ForegroundColor Yellow
        $pushAnyway = Read-Host "Push to origin/main anyway? (y/n)"
        if ($pushAnyway -ne 'y' -and $pushAnyway -ne 'Y') {
            Write-Host "Not pushing. Fix builds and run: git push origin main" -ForegroundColor Gray
            exit 0
        }
    }
}

# Push to origin
Write-Host "[GIT] Pushing to origin/main..." -ForegroundColor Cyan
$pushConfirm = Read-Host "Push changes to origin/main? (y/n)"
if ($pushConfirm -eq 'y' -or $pushConfirm -eq 'Y') {
    git push origin main
    if ($LASTEXITCODE -ne 0) {
        Write-Host "[ERROR] Push failed" -ForegroundColor Red
        exit 1
    }
    Write-Host "[OK] Pushed to origin/main!" -ForegroundColor Green
} else {
    Write-Host "[SKIP] Skipped push. Run manually: git push origin main" -ForegroundColor Gray
}
Write-Host ""

# Optional: Delete feature branch
Write-Host "[CLEANUP] Branch Cleanup" -ForegroundColor Cyan
$deleteBranch = Read-Host "Delete move_to_blazor branch? (local and remote) (y/n)"
if ($deleteBranch -eq 'y' -or $deleteBranch -eq 'Y') {
    Write-Host "   Deleting local branch..." -ForegroundColor Gray
    git branch -d move_to_blazor
    
    Write-Host "   Deleting remote branch..." -ForegroundColor Gray
    git push origin --delete move_to_blazor
    
    Write-Host "   [OK] Branch deleted" -ForegroundColor Green
} else {
    Write-Host "   Branch kept. Delete later with:" -ForegroundColor Gray
    Write-Host "   git branch -d move_to_blazor" -ForegroundColor Gray
    Write-Host "   git push origin --delete move_to_blazor" -ForegroundColor Gray
}
Write-Host ""

# Success summary
Write-Host "[SUCCESS] Merge Complete!" -ForegroundColor Green
Write-Host ""
Write-Host "Summary:" -ForegroundColor Cyan
Write-Host "   [OK] Merged move_to_blazor into main" -ForegroundColor Green
Write-Host "   [OK] Pushed to origin/main" -ForegroundColor Green
if ($deleteBranch -eq 'y' -or $deleteBranch -eq 'Y') {
    Write-Host "   [OK] Deleted move_to_blazor branch" -ForegroundColor Green
}
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "   1. Update deployment scripts for Blazor" -ForegroundColor White
Write-Host "   2. Update DEPLOYMENT_GUIDE.md" -ForegroundColor White
Write-Host "   3. Deploy Blazor application to test environment" -ForegroundColor White
Write-Host ""
