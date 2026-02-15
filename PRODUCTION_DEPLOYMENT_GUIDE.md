# Production Environment Deployment Guide

**Document Date:** February 15, 2026  
**Target Environment:** Azure Production  
**Application:** Petel Educational Management System  
**Version:** .NET 9.0 API + Blazor Server (.NET 8.0)

---

## Table of Contents

1. [Overview](#overview)
2. [Prerequisites](#prerequisites)
3. [Phase 1: Infrastructure Setup](#phase-1-infrastructure-setup)
4. [Phase 2: Security Configuration](#phase-2-security-configuration)
5. [Phase 3: Application Deployment](#phase-3-application-deployment)
6. [Phase 4: Front Door and WAF](#phase-4-front-door-and-waf)
7. [Phase 5: Production Validation](#phase-5-production-validation)
8. [Rollback Procedures](#rollback-procedures)
9. [Monitoring and Maintenance](#monitoring-and-maintenance)

---

## Overview

This guide provides a complete step-by-step process for deploying the Petel Educational Management System to a production environment on Azure.

### Architecture Components

- **Backend API**: ASP.NET Core 9.0 Web API
- **Frontend**: Blazor Server (.NET 8.0)
- **Database**: Azure PostgreSQL Flexible Server
- **Secrets Management**: Azure Key Vault
- **CDN/WAF**: Azure Front Door Premium with WAF
- **Security**: Israeli IP restrictions, rate limiting, JWT auth, 2FA/OTP
- **Monitoring**: Application Insights (optional)

### Estimated Deployment Time

- **Infrastructure Setup**: 30-45 minutes
- **Security Configuration**: 30-45 minutes
- **Application Deployment**: 15-20 minutes
- **Front Door Setup**: 20-30 minutes
- **Validation**: 15-20 minutes
- **Total**: 2-3 hours

---

## Prerequisites

### Required Access and Tools

- [ ] Azure subscription with Owner or Contributor role
- [ ] Azure CLI installed and authenticated (`az login`)
- [ ] PowerShell 5.1 or higher
- [ ] .NET 9.0 SDK installed
- [ ] .NET 8.0 SDK installed
- [ ] Git repository access
- [ ] Database migration scripts ready

### Required Information

- [ ] Production database credentials (will be generated or provide existing)
- [ ] Custom domain name (if applicable)
- [ ] SSL certificate (if using custom domain)
- [ ] Email/SMS provider credentials for OTP
- [ ] Admin user credentials for initial login

### Cost Estimation (Monthly)

- **App Service Plan P1V3**: ~$150-200
- **PostgreSQL 2 vCores**: ~$100-150
- **Azure Front Door Premium**: ~$300-400
- **Key Vault**: ~$5
- **Total**: ~$555-755/month

---

## Phase 1: Infrastructure Setup

### Step 1.1: Run Infrastructure Setup Script

The automated script creates all necessary Azure resources.

```powershell
cd c:\dev\PetelFullApp

# Dry run to see what will be created
.\Setup-Production-Infrastructure.ps1 -DryRun

# Create all resources (recommended)
.\Setup-Production-Infrastructure.ps1

# Or skip specific components if already exist
.\Setup-Production-Infrastructure.ps1 -SkipDatabase  # Use existing DB
.\Setup-Production-Infrastructure.ps1 -SkipKeyVault  # Use existing KV
```

**What Gets Created:**

- Resource Group: `petel-prod-rg`
- App Service Plan: `petel-prod-plan` (P1V3 tier)
- API App Service: `petel-prod-api.azurewebsites.net`
- Blazor App Service: `petel-prod-blazor.azurewebsites.net`
- PostgreSQL Server: `petel-prod-db-XXXX.postgres.database.azure.com`
- Key Vault: `petel-kv-prod-XXXX.vault.azure.net`

**Duration:** 30-45 minutes (PostgreSQL creation takes longest)

### Step 1.2: Save Database Credentials

The script generates a credentials file: `production-db-credentials-YYYYMMDD-HHMMSS.txt`

**CRITICAL:**
1. Immediately copy credentials to a secure password manager
2. Add credentials to Key Vault (next step)
3. **DELETE the credentials file** - do not commit to Git!

### Step 1.3: Verify Resource Creation

```powershell
# Verify all resources exist
az resource list --resource-group petel-prod-rg --output table
```

Expected resources:
- Microsoft.Web/serverfarms
- Microsoft.Web/sites (API)
- Microsoft.Web/sites (Blazor)
- Microsoft.DBforPostgreSQL/flexibleServers
- Microsoft.KeyVault/vaults

---

## Phase 2: Security Configuration

### Step 2.1: Generate Encryption Keys

```powershell
# Generate JWT Secret Key (256-bit)
$jwtSecret = -join ((48..57) + (65..90) + (97..122) | Get-Random -Count 64 | ForEach-Object {[char]$_})
Write-Host "JWT Secret Key: $jwtSecret"

# Generate AES Encryption Key (256-bit base64)
$aesKey = New-Object byte[] 32
[System.Security.Cryptography.RNGCryptoServiceProvider]::Create().GetBytes($aesKey)
$aesKeyBase64 = [Convert]::ToBase64String($aesKey)
Write-Host "AES Encryption Key: $aesKeyBase64"
```

**Save these keys securely!**

### Step 2.2: Add Secrets to Key Vault

Replace `KEY_VAULT_NAME` with your actual Key Vault name from Step 1.

```powershell
$keyVaultName = "petel-kv-prod-XXXX"  # Replace with actual name

# Database connection string
$dbConnectionString = "Host=petel-prod-db-XXXX.postgres.database.azure.com;Database=petelappdb;Username=peteldbadmin;Password=YOUR_DB_PASSWORD;SslMode=Require"

az keyvault secret set `
    --vault-name $keyVaultName `
    --name "ConnectionStrings--DefaultConnection" `
    --value $dbConnectionString

# Hangfire connection (same as default for now)
az keyvault secret set `
    --vault-name $keyVaultName `
    --name "ConnectionStrings--HangfireConnection" `
    --value $dbConnectionString

# JWT Secret
az keyvault secret set `
    --vault-name $keyVaultName `
    --name "Security--Jwt--SecretKey" `
    --value $jwtSecret

# Encryption Key
az keyvault secret set `
    --vault-name $keyVaultName `
    --name "Security--DataEncryption--EncryptionKey" `
    --value $aesKeyBase64
```

### Step 2.3: Configure App Service Key Vault References

```powershell
$apiAppName = "petel-prod-api"
$resourceGroup = "petel-prod-rg"

# Configure API to use Key Vault secrets
az webapp config appsettings set `
    --name $apiAppName `
    --resource-group $resourceGroup `
    --settings `
        "ConnectionStrings__DefaultConnection=@Microsoft.KeyVault(SecretUri=https://$keyVaultName.vault.azure.net/secrets/ConnectionStrings--DefaultConnection/)" `
        "ConnectionStrings__HangfireConnection=@Microsoft.KeyVault(SecretUri=https://$keyVaultName.vault.azure.net/secrets/ConnectionStrings--HangfireConnection/)" `
        "Security__Jwt__SecretKey=@Microsoft.KeyVault(SecretUri=https://$keyVaultName.vault.azure.net/secrets/Security--Jwt--SecretKey/)" `
        "Security__DataEncryption__EncryptionKey=@Microsoft.KeyVault(SecretUri=https://$keyVaultName.vault.azure.net/secrets/Security--DataEncryption--EncryptionKey/)"
```

### Step 2.4: Database Initialization

**Run migration scripts to create schema and seed data:**

```powershell
# Connect to database using credentials from Step 1.2
# Use pgAdmin, DBeaver, or psql

# Run these scripts in order:
# 1. Create schema
# 2. Create tables
# 3. Insert initial data (system attributes, roles, etc.)
# 4. Create initial admin user
```

**Example initial admin user creation:**

```sql
-- Insert admin user (password: Admin2025! - CHANGE IMMEDIATELY after first login)
INSERT INTO petel_schema.users (username, password_hash, email, first_name, last_name, is_active)
VALUES (
    'admin',
    '$2a$11$YourBCryptHashHere',  -- Generate using BCrypt
    'admin@petel-system.co.il',
    'Admin',
    'User',
    true
);

-- Assign SuperAdmin role
INSERT INTO petel_schema.user_roles (user_id, role_id)
VALUES (
    (SELECT id FROM petel_schema.users WHERE username = 'admin'),
    (SELECT id FROM petel_schema.roles WHERE role_name = 'SuperAdmin')
);
```

---

## Phase 3: Application Deployment

### Step 3.1: Update Azure API URL in Blazor Configuration

Edit [PetelApp.BlazorServer/appsettings.Production.json](c:\dev\PetelFullApp\PetelApp.BlazorServer\appsettings.Production.json):

```json
{
  "ApiSettings": {
    "BaseUrl": "https://petel-prod-api.azurewebsites.net/api"
  }
}
```

### Step 3.2: Deploy Both Applications

**Option 1: Deploy Both (Recommended)**

```powershell
cd c:\dev\PetelFullApp

.\Deploy-ToAzure.ps1 -Environment production
```

**Option 2: Deploy Individually**

```powershell
# Deploy API only
.\Deploy-ToAzure.ps1 -Environment production -ApiOnly

# Deploy Blazor only
.\Deploy-ToAzure.ps1 -Environment production -BlazorOnly
```

**What Happens:**
1. ✅ Clean previous build artifacts
2. ✅ Build and publish .NET projects
3. ✅ Copy environment-specific configurations
4. ✅ Create deployment ZIP packages
5. ✅ Deploy to Azure App Services
6. ✅ Restart applications
7. ✅ Verify deployments

**Duration:** 15-20 minutes

### Step 3.3: Verify Application Health

```powershell
# Test API health
curl https://petel-prod-api.azurewebsites.net/health

# Test Blazor app
curl https://petel-prod-blazor.azurewebsites.net
```

Expected responses:
- API: `{"status":"Healthy"}`
- Blazor: HTML content with "Petel" in title

---

## Phase 4: Front Door and WAF

### Step 4.1: Create Front Door with WAF

```powershell
cd c:\dev\PetelFullApp

# Creates Front Door Premium with WAF
.\Deploy-FrontDoor.ps1 -Environment production
```

**What Gets Created:**
- Azure Front Door Premium profile
- WAF policy with Israeli IP restrictions
- OWASP Core Rule Set 3.2
- Bot protection
- DDoS protection (automatic)

**Duration:** 20-30 minutes

### Step 4.2: Get Front Door Endpoints

```powershell
$frontDoorName = "petel-prod-frontdoor"
$resourceGroup = "petel-prod-rg"

az afd endpoint list `
    --profile-name $frontDoorName `
    --resource-group $resourceGroup `
    --query "[].{Name:name, Hostname:hostName}" -o table
```

Expected output:
```
Name              Hostname
----------------  --------------------------------------------
petel-prod        petel-prod-XXXXXXXXXXXX.z01.azurefd.net
```

### Step 4.3: Configure Israeli IP Restrictions

**Automatic (Recommended):**

```powershell
.\Add-IsraeliIPRestrictions-FrontDoor.ps1 -Environment production
```

**Manual (if needed):**

See [FRONT_DOOR_DEPLOYMENT_SUMMARY.md](c:\dev\PetelFullApp\FRONT_DOOR_DEPLOYMENT_SUMMARY.md) for manual steps.

### Step 4.4: Test WAF Rules

```powershell
# Test from Israeli IP (should succeed)
curl https://petel-prod-XXXXXXXXXXXX.z01.azurefd.net

# Test SQL injection (should be blocked)
curl "https://petel-prod-XXXXXXXXXXXX.z01.azurefd.net?test=1' OR '1'='1"
```

Expected: 403 Forbidden for malicious requests

---

## Phase 5: Production Validation

### Step 5.1: Functional Testing

**Test Core Functionality:**

- [ ] Login with admin credentials
- [ ] OTP/2FA flow works
- [ ] Session timeout works (10 minutes)
- [ ] Rate limiting triggers correctly
- [ ] Data encryption works (persons, students)
- [ ] CRUD operations work
- [ ] File uploads work
- [ ] Reports generate correctly
- [ ] Hebrew RTL display correct
- [ ] All menu items load
- [ ] Security headers present

### Step 5.2: Security Validation

**Check Security Headers:**

```powershell
curl -I https://petel-prod-api.azurewebsites.net/api/health
```

Expected headers:
- `X-Content-Type-Options: nosniff`
- `X-Frame-Options: DENY`
- `X-XSS-Protection: 1; mode=block`
- `Referrer-Policy: no-referrer`
- `Strict-Transport-Security: max-age=31536000`

**Test Rate Limiting:**

```powershell
# Trigger rate limit (should block after 10 attempts)
for ($i=1; $i -le 15; $i++) {
    curl -X POST https://petel-prod-api.azurewebsites.net/api/auth/login `
        -H "Content-Type: application/json" `
        -d '{"username":"test","password":"test"}'
    Write-Host "Attempt $i"
    Start-Sleep -Seconds 1
}
```

Expected: HTTP 429 (Too Many Requests) after 10 attempts

**Verify Key Vault Access:**

```powershell
# Check API logs for Key Vault access
az webapp log tail --name petel-prod-api --resource-group petel-prod-rg
```

Should see: "Successfully loaded secrets from Key Vault"

### Step 5.3: Performance Testing

**Baseline Metrics:**

```powershell
# API response time
Measure-Command { curl https://petel-prod-api.azurewebsites.net/api/health }

# Blazor load time
Measure-Command { curl https://petel-prod-blazor.azurewebsites.net }
```

Expected:
- API health: < 200ms
- Blazor load: < 2 seconds

### Step 5.4: Load Testing (Optional)

Use Azure Load Testing or Apache JMeter:
- 100 concurrent users
- 5-minute duration
- Should maintain < 1 second response time

---

## Rollback Procedures

### Emergency Rollback

If critical issues occur in production:

**1. Immediate Rollback to Previous Version:**

```powershell
# Get previous deployment
az webapp deployment slot list --name petel-prod-api --resource-group petel-prod-rg

# Swap to previous version (if using slots)
az webapp deployment slot swap --name petel-prod-api --resource-group petel-prod-rg --slot staging --target-slot production
```

**2. Disable Front Door:**

```powershell
# Temporarily disable Front Door to stop traffic
az afd endpoint update --profile-name petel-prod-frontdoor --endpoint-name petel-prod --resource-group petel-prod-rg --enabled-state Disabled
```

**3. Restore Database Backup:**

```powershell
# List available backups
az postgres flexible-server backup list --resource-group petel-prod-rg --server-name petel-prod-db-XXXX

# Restore to specific point in time
az postgres flexible-server restore --resource-group petel-prod-rg --name petel-prod-db-restored --source-server petel-prod-db-XXXX --restore-time "2026-02-15T10:00:00Z"
```

### Partial Rollback

**Rollback API only:**

```powershell
.\Deploy-ToAzure.ps1 -Environment production -ApiOnly -SkipBuild
# (Deploy previous version from Git)
```

**Rollback Blazor only:**

```powershell
.\Deploy-ToAzure.ps1 -Environment production -BlazorOnly -SkipBuild
# (Deploy previous version from Git)
```

---

## Monitoring and Maintenance

### Step 6.1: Configure Application Insights (Recommended)

```powershell
# Create Application Insights
az monitor app-insights component create `
    --app petel-prod-insights `
    --location israelcentral `
    --resource-group petel-prod-rg `
    --application-type web

# Get instrumentation key
$instrumentationKey = az monitor app-insights component show `
    --app petel-prod-insights `
    --resource-group petel-prod-rg `
    --query instrumentationKey -o tsv

# Add to App Services
az webapp config appsettings set `
    --name petel-prod-api `
    --resource-group petel-prod-rg `
    --settings "APPINSIGHTS_INSTRUMENTATIONKEY=$instrumentationKey"
```

### Step 6.2: Set Up Alerts

**Critical Alerts:**

- API response time > 2 seconds
- Error rate > 5%
- Database CPU > 80%
- Failed login attempts > 50/hour
- WAF blocks > 100/hour

**Configure in Azure Portal:**
Monitoring → Alerts → Create alert rule

### Step 6.3: Backup Strategy

**Automated Backups:**

- PostgreSQL: Automatic daily backups (7-day retention)
- Key Vault: Soft-delete enabled (90-day retention)
- App Service: Slot-based deployments for instant rollback

**Manual Backups:**

```powershell
# Backup database manually
az postgres flexible-server backup create --resource-group petel-prod-rg --server-name petel-prod-db-XXXX
```

### Step 6.4: Regular Maintenance

**Weekly:**
- Review WAF logs for attack patterns
- Check error logs
- Verify backup success

**Monthly:**
- Update dependencies (NuGet packages)
- Review and rotate secrets
- Performance optimization review

**Quarterly:**
- Security audit
- Penetration testing
- Disaster recovery drill

---

## Troubleshooting

### Common Issues

**1. Application Won't Start**

Check logs:
```powershell
az webapp log tail --name petel-prod-api --resource-group petel-prod-rg
```

Common causes:
- Key Vault access denied → Check managed identity permissions
- Database connection failed → Verify connection string and firewall rules
- Missing configuration → Verify all appsettings are set

**2. 403 Forbidden from WAF**

Causes:
- IP not in allowed list → Add your IP to WAF policy
- Malicious request detected → Review WAF logs
- Bot detection triggered → Add user-agent to allowed list

**3. 429 Too Many Requests**

Cause: Rate limiting triggered
Solution: Wait 15 minutes or adjust rate limits in appsettings.Production.json

**4. Key Vault Access Denied**

```powershell
# Grant managed identity access
$apiPrincipalId = az webapp identity show --name petel-prod-api --resource-group petel-prod-rg --query principalId -o tsv
az keyvault set-policy --name petel-kv-prod-XXXX --object-id $apiPrincipalId --secret-permissions get list
```

---

## Post-Deployment Checklist

- [ ] All Azure resources created successfully
- [ ] Database initialized with schema and initial data
- [ ] Key Vault secrets configured
- [ ] Application deployed and running
- [ ] Front Door and WAF configured
- [ ] Israeli IP restrictions active
- [ ] Rate limiting functional
- [ ] Security headers present
- [ ] SSL/TLS working
- [ ] Monitoring configured
- [ ] Alerts set up
- [ ] Backup strategy verified
- [ ] Admin credentials changed from defaults
- [ ] Documentation updated with production URLs
- [ ] Team trained on monitoring and maintenance

---

## Support Contacts

**Technical Issues:**
- Azure Support: https://portal.azure.com (Support tickets)
- Development Team: [Contact Information]

**Security Incidents:**
- Immediate: Disable Front Door to stop traffic
- Contact: Security team lead
- Document: All actions taken

---

## Appendix

### A. Production URLs

- **API**: https://petel-prod-api.azurewebsites.net
- **Blazor**: https://petel-prod-blazor.azurewebsites.net
- **Front Door**: https://petel-prod-XXXXXXXXXXXX.z01.azurefd.net
- **Database**: petel-prod-db-XXXX.postgres.database.azure.com
- **Key Vault**: https://petel-kv-prod-XXXX.vault.azure.net

### B. Rate Limiting Configuration

Production limits (per 15 minutes):
- Login: 10 attempts
- OTP validation: 5 attempts
- OTP setup: 3 attempts

See [appsettings.Production.json](c:\dev\PetelFullApp\PetelApp.Api\appsettings.Production.json) for full configuration.

### C. Security Configuration

See:
- [BLAZOR_SECURITY_USAGE_GUIDE.md](c:\dev\PetelFullApp\BLAZOR_SECURITY_USAGE_GUIDE.md)
- [PRODUCTION_RATE_LIMITING_GUIDE.md](c:\dev\PetelFullApp\PRODUCTION_RATE_LIMITING_GUIDE.md)
- [SOC2_COMPLIANCE_ROADMAP.md](c:\dev\PetelFullApp\SOC2_COMPLIANCE_ROADMAP.md)

---

**End of Production Deployment Guide**
