# Complete Migration Cleanup Script
# Automates the process of cleaning up references and preparing for merge

param(
    [switch]$SkipArchive,
    [switch]$SkipGitOperations
)

$ErrorActionPreference = "Stop"

Write-Host "[CLEANUP] Frontend Migration Cleanup" -ForegroundColor Cyan
Write-Host "==============================" -ForegroundColor Cyan
Write-Host ""

# Step 1: Archive old frontend (unless skipped)
if (-not $SkipArchive) {
    Write-Host "Step 1: Archiving old frontend..." -ForegroundColor Yellow
    if (Test-Path ".\petelapp-frontend") {
        & .\Archive-Frontend.ps1
        if ($LASTEXITCODE -ne 0) {
            Write-Host "[ERROR] Archive failed. Aborting." -ForegroundColor Red
            exit 1
        }
    } else {
        Write-Host "   [INFO] petelapp-frontend folder not found (already removed?)" -ForegroundColor Gray
    }
    Write-Host ""
} else {
    Write-Host "[SKIP] Skipping archive step" -ForegroundColor Gray
    Write-Host ""
}

# Step 2: List files that were updated
Write-Host "Step 2: Files Updated" -ForegroundColor Yellow
Write-Host "   [OK] .gitignore - Removed petelapp-frontend exceptions" -ForegroundColor Green
Write-Host "   [OK] .github\copilot-instructions.md - Updated to Blazor" -ForegroundColor Green
Write-Host "   [OK] commands.txt - Updated to Blazor" -ForegroundColor Green
Write-Host ""

# Step 3: Git operations (unless skipped)
if (-not $SkipGitOperations) {
    Write-Host "Step 3: Git Operations" -ForegroundColor Yellow
    
    # Check git status
    Write-Host "   Current git status:" -ForegroundColor Gray
    git status --short
    Write-Host ""
    
    # Prompt for confirmation
    $confirm = Read-Host "   Do you want to remove petelapp-frontend folder from git and commit changes? (y/n)"
    if ($confirm -eq 'y' -or $confirm -eq 'Y') {
        
        # Stage updated files
        Write-Host "   [GIT] Staging updated files..." -ForegroundColor Cyan
        git add .gitignore
        git add .github\copilot-instructions.md
        git add commands.txt
        git add Archive-Frontend.ps1
        git add FRONTEND_CLEANUP_PLAN.md
        git add Complete-Cleanup.ps1
        git add MIGRATION_SUMMARY.md
        git add QUICK_REFERENCE.md
        git add Merge-To-Main.ps1
        
        # Remove old frontend folder
        if (Test-Path ".\petelapp-frontend") {
            Write-Host "   [GIT] Removing petelapp-frontend from git..." -ForegroundColor Cyan
            git rm -r petelapp-frontend
        }
        
        # Commit changes
        Write-Host "   [GIT] Committing changes..." -ForegroundColor Cyan
        git commit -m "Remove old vanilla JS frontend (migrated to Blazor)

- Archived petelapp-frontend folder for reference
- Updated .gitignore to remove frontend exceptions
- Updated copilot instructions to reference Blazor
- Updated development commands for Blazor
- All functionality now in PetelApp.BlazorServer"
        
        Write-Host "   [OK] Changes committed!" -ForegroundColor Green
        Write-Host ""
        
        # Prompt for push
        $pushConfirm = Read-Host "   Push changes to origin/move_to_blazor? (y/n)"
        if ($pushConfirm -eq 'y' -or $pushConfirm -eq 'Y') {
            Write-Host "   [GIT] Pushing to origin..." -ForegroundColor Cyan
            git push origin move_to_blazor
            Write-Host "   [OK] Pushed successfully!" -ForegroundColor Green
        } else {
            Write-Host "   [SKIP] Skipped push. Run manually: git push origin move_to_blazor" -ForegroundColor Gray
        }
        
    } else {
        Write-Host "   [SKIP] Skipped git operations" -ForegroundColor Gray
    }
    Write-Host ""
} else {
    Write-Host "[SKIP] Skipping git operations" -ForegroundColor Gray
    Write-Host ""
}

# Step 4: Next steps
Write-Host "[COMPLETE] Cleanup Complete!" -ForegroundColor Green
Write-Host ""
Write-Host "Next Steps:" -ForegroundColor Cyan
Write-Host "   1. Review deployment scripts that need updating:" -ForegroundColor White
Write-Host "      - Force-Redeploy.ps1" -ForegroundColor Gray
Write-Host "      - Deploy-ToAzure.ps1" -ForegroundColor Gray
Write-Host "      - Deploy-ToAzure-Fixed.ps1" -ForegroundColor Gray
Write-Host "      - PetelApp.Api\Generate deploy package.cmd" -ForegroundColor Gray
Write-Host ""
Write-Host "   2. Update documentation files:" -ForegroundColor White
Write-Host "      - DEPLOYMENT_GUIDE.md" -ForegroundColor Gray
Write-Host "      - QUICK_DEPLOY.md" -ForegroundColor Gray
Write-Host ""
Write-Host "   3. Test the Blazor application:" -ForegroundColor White
Write-Host "      cd PetelApp.BlazorServer && dotnet run" -ForegroundColor Gray
Write-Host ""
Write-Host "   4. When ready, merge to main:" -ForegroundColor White
Write-Host "      .\Merge-To-Main.ps1" -ForegroundColor Gray
Write-Host ""
