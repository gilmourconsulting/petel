# Archived Deployment Scripts

These scripts were archived on January 27, 2026 after the Blazor Server migration.

## Archived Files

- **Force-Redeploy.ps1** - Old API deployment with vanilla JS frontend
- **Deploy-ToAzure.ps1** - Old combined deployment script
- **Deploy-ToAzure-Fixed.ps1** - Fixed version of old deployment

## Why Archived

These scripts reference the old `petelapp-frontend` folder which was removed during the Blazor Server migration. They are no longer functional.

## Current Deployment Scripts

Use these instead:

- **Deploy-Blazor-ToAzure.ps1** - Deploy Blazor Server to Azure
- **Deploy-Blazor-ToTest.ps1** - Deploy Blazor to test environment
- **Deploy-Complete-ToTest.ps1** - Deploy both Blazor + API to test
- **Deploy-Api-ToTest.ps1** - Deploy API only

## Migration Date

Main branch merged with Blazor Server migration on January 27, 2026.
