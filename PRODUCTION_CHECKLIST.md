# Production Environment Deployment Checklist

**Date:** February 15, 2026  
**Environment:** Production  
**Deployment Manager:** ___________________  
**Approval Authority:** ___________________

---

## Pre-Deployment Checklist

### Documentation Review
- [ ] Read [PRODUCTION_DEPLOYMENT_GUIDE.md](PRODUCTION_DEPLOYMENT_GUIDE.md) completely
- [ ] Review [SOC2_COMPLIANCE_ROADMAP.md](SOC2_COMPLIANCE_ROADMAP.md) for security requirements
- [ ] Understand rollback procedures
- [ ] Backup contact list prepared

### Access Verification
- [ ] Azure CLI installed and tested (`az --version`)
- [ ] Azure subscription access verified (`az account show`)
- [ ] Subscription has Owner or Contributor role
- [ ] PowerShell 5.1+ verified (`$PSVersionTable.PSVersion`)
- [ ] .NET 9.0 SDK installed (`dotnet --version`)
- [ ] .NET 8.0 SDK installed
- [ ] Git repository cloned and up to date

### Cost Approval
- [ ] Monthly cost estimate reviewed (~$555-755/month)
- [ ] Budget allocated and approved
- [ ] Billing alerts configured in Azure

### Backout Plan
- [ ] Rollback procedures documented
- [ ] Previous stable version identified
- [ ] Emergency contact list prepared
- [ ] Communication plan in place

---

## Phase 1: Infrastructure Setup (30-45 minutes)

### Step 1.1: Pre-Execution Verification
- [ ] Confirmed resource group `petel-prod-rg` does NOT exist
- [ ] Confirmed no naming conflicts with existing resources
- [ ] Saved current timestamp: ___________________

### Step 1.2: Run Infrastructure Setup
```powershell
cd c:\dev\PetelFullApp
.\Setup-Production-Infrastructure.ps1 -DryRun  # Review first
.\Setup-Production-Infrastructure.ps1
```

- [ ] Script completed without errors
- [ ] Resource group created: `petel-prod-rg`
- [ ] App Service Plan created: `petel-prod-plan` (P1V3)
- [ ] API App Service created: `petel-prod-api.azurewebsites.net`
- [ ] Blazor App Service created: `petel-prod-blazor.azurewebsites.net`
- [ ] PostgreSQL server created
- [ ] Key Vault created
- [ ] Managed identities assigned to App Services

### Step 1.3: Save Critical Information

**Database Credentials** (from `production-db-credentials-*.txt`):
```
Server:   _____________________________________________
Database: _____________________________________________
Username: _____________________________________________
Password: _____________________________________________
```
- [ ] Credentials copied to secure password manager
- [ ] Connection string tested
- [ ] Original credentials file **DELETED**

**Key Vault Name:**
```
Key Vault: _____________________________________________
```

**Resource URLs:**
```
API:          https://_______________.azurewebsites.net
Blazor:       https://_______________.azurewebsites.net
```

**Note**: No Front Door - using direct App Service URLs with IP restrictions.

### Step 1.4: Verify Resources
```powershell
az resource list --resource-group petel-prod-rg --output table
```

- [ ] All 5 resources present (Resource Group, Plan, API, Blazor, PostgreSQL, Key Vault)
- [ ] All resources in "Succeeded" state
- [ ] Screenshot saved for audit trail

---

## Phase 2: Security Configuration (30-45 minutes)

### Step 2.1: Generate Encryption Keys

```powershell
# JWT Secret
$jwtSecret = -join ((48..57) + (65..90) + (97..122) | Get-Random -Count 64 | ForEach-Object {[char]$_})
Write-Host "JWT: $jwtSecret"

# AES Key
$aesKey = New-Object byte[] 32
[System.Security.Cryptography.RNGCryptoServiceProvider]::Create().GetBytes($aesKey)
$aesKeyBase64 = [Convert]::ToBase64String($aesKey)
Write-Host "AES: $aesKeyBase64"
```

**Generated Keys** (save securely, then delete):
```
JWT Secret Key: ________________________________________________
AES Encryption: ________________________________________________
```

- [ ] JWT secret generated (64 characters)
- [ ] AES key generated (base64, 44 characters)
- [ ] Keys saved to password manager
- [ ] Keys NOT committed to Git

### Step 2.2: Add Secrets to Key Vault

Replace `<KEYVAULT_NAME>` with actual name from Step 1.3:

```powershell
$kv = "<KEYVAULT_NAME>"
$dbConn = "Host=<SERVER>;Database=<DB>;Username=<USER>;Password=<PASS>;SslMode=Require"

az keyvault secret set --vault-name $kv --name "ConnectionStrings--DefaultConnection" --value $dbConn
az keyvault secret set --vault-name $kv --name "ConnectionStrings--HangfireConnection" --value $dbConn
az keyvault secret set --vault-name $kv --name "Security--Jwt--SecretKey" --value "<JWT_SECRET>"
az keyvault secret set --vault-name $kv --name "Security--DataEncryption--EncryptionKey" --value "<AES_KEY>"
```

- [ ] `ConnectionStrings--DefaultConnection` secret created
- [ ] `ConnectionStrings--HangfireConnection` secret created
- [ ] `Security--Jwt--SecretKey` secret created
- [ ] `Security--DataEncryption--EncryptionKey` secret created

### Step 2.3: Configure App Service Key Vault References

```powershell
$api = "petel-prod-api"
$rg = "petel-prod-rg"
$kv = "<KEYVAULT_NAME>"

az webapp config appsettings set --name $api --resource-group $rg --settings `
    "ConnectionStrings__DefaultConnection=@Microsoft.KeyVault(SecretUri=https://$kv.vault.azure.net/secrets/ConnectionStrings--DefaultConnection/)" `
    "ConnectionStrings__HangfireConnection=@Microsoft.KeyVault(SecretUri=https://$kv.vault.azure.net/secrets/ConnectionStrings--HangfireConnection/)" `
    "Security__Jwt__SecretKey=@Microsoft.KeyVault(SecretUri=https://$kv.vault.azure.net/secrets/Security--Jwt--SecretKey/)" `
    "Security__DataEncryption__EncryptionKey=@Microsoft.KeyVault(SecretUri=https://$kv.vault.azure.net/secrets/Security--DataEncryption--EncryptionKey/)"
```

- [ ] App Service settings configured with Key Vault references
- [ ] Settings verified in Azure Portal → App Service → Configuration

### Step 2.4: Database Initialization

**Connect to Database:**
- Tool: pgAdmin, DBeaver, or psql
- Credentials: From Step 1.3

```sql
-- Verify connection
SELECT version();

-- Create schema (if not exists)
CREATE SCHEMA IF NOT EXISTS petel_schema;

-- Verify schema
\dn
```

- [ ] Connected to database successfully
- [ ] Schema `petel_schema` exists or created
- [ ] SSL/TLS connection verified

**Run Migration Scripts:**

- [ ] Execute schema creation scripts
- [ ] Execute table creation scripts  
- [ ] Execute initial data seeding scripts
- [ ] Verify all tables created: `\dt petel_schema.*`

**Create Initial Admin User:**

```sql
-- Generate BCrypt hash for password "Admin2025!" (CHANGE AFTER FIRST LOGIN!)
-- Use online BCrypt tool or backend API

INSERT INTO petel_schema.users (username, password_hash, email, first_name, last_name, is_active, created_at, updated_at)
VALUES (
    'admin',
    '$2a$11$<BCRYPT_HASH_HERE>',
    'admin@petel-system.co.il',
    'System',
    'Administrator',
    true,
    NOW(),
    NOW()
);

-- Assign SuperAdmin role
INSERT INTO petel_schema.user_roles (user_id, role_id, created_at)
VALUES (
    (SELECT id FROM petel_schema.users WHERE username = 'admin'),
    (SELECT id FROM petel_schema.roles WHERE role_name = 'SuperAdmin'),
    NOW()
);
```

- [ ] Admin user created
- [ ] SuperAdmin role assigned
- [ ] Password hash verified (NOT plaintext)
- [ ] Default password documented for initial login

**Admin Credentials:**
```
Username: admin
Password: _________________ (CHANGE IMMEDIATELY after first login)
```

---

## Phase 3: Application Deployment (15-20 minutes)

### Step 3.1: Update Configuration Files

**File: [PetelApp.BlazorServer/appsettings.Production.json](PetelApp.BlazorServer/appsettings.Production.json)**

Verify API URL points to production:
```json
{
  "ApiSettings": {
    "BaseUrl": "https://petel-prod-api.azurewebsites.net/api"
  }
}
```

- [ ] Blazor `appsettings.Production.json` updated
- [ ] API URL is `https://petel-prod-api.azurewebsites.net/api`
- [ ] File saved and committed to Git

### Step 3.2: Deploy Applications

```powershell
cd c:\dev\PetelFullApp

# Deploy both API and Blazor
.\Deploy-ToAzure.ps1 -Environment production
```

- [ ] Build succeeded for API project
- [ ] Build succeeded for Blazor project
- [ ] API deployment package created
- [ ] Blazor deployment package created
- [ ] API deployed to Azure App Service
- [ ] Blazor deployed to Azure App Service
- [ ] Both services restarted successfully

**Deployment Logs:**
- [ ] API deployment log reviewed - no errors
- [ ] Blazor deployment log reviewed - no errors

### Step 3.3: Initial Application Health Check

```powershell
# Test API health endpoint
curl https://petel-prod-api.azurewebsites.net/api/health

# Test Blazor app (should return HTML)
curl https://petel-prod-blazor.azurewebsites.net/
```

**Expected Results:**
- [ ] API returns: `{"status":"Healthy"}` or similar
- [ ] Blazor returns: HTML with "<!DOCTYPE html>"
- [ ] No 500 Internal Server Error
- [ ] No Key Vault access errors in logs

**If Errors Occur:**
```powershell
# Check API logs
az webapp log tail --name petel-prod-api --resource-group petel-prod-rg

# Check Blazor logs
az webapp log tail --name petel-prod-blazor --resource-group petel-prod-rg
```

- [ ] No critical errors in API logs
- [ ] No critical errors in Blazor logs
- [ ] Key Vault secrets loading successfully

**Common Issues and Fixes:**
- Key Vault access denied → Verify managed identity has Key Vault access policy
- Database connection failed → Verify connection string and firewall rules
- 404 errors → Re-deploy application

---

## Phase 4: IP Restrictions Configuration (10-15 minutes)

**Note**: Azure Front Door was removed to reduce costs. Using direct App Service IP restrictions instead.

### Step 4.1: Configure Israeli IP Restrictions

```powershell
cd c:\dev\PetelFullApp

# Apply Israeli IP restrictions to both App Services
.\Add-IsraelIPRestrictions.ps1 -Environment production
```

- [ ] IP restrictions configured on Blazor App Service
- [ ] IP restrictions configured on API App Service
- [ ] 47 Israeli IP ranges added (Bezeq, HOT, Cellcom, Partner, etc.)
- [ ] Blazor-to-API server communication whitelisted
- [ ] Script completed without errors

### Step 4.2: Verify IP Restrictions

```powershell
# Check Blazor IP restrictions
az webapp config access-restriction show `
    --name petel-prod-blazor `
    --resource-group petel-prod-rg

# Check API IP restrictions
az webapp config access-restriction show `
    --name petel-prod-api `
    --resource-group petel-prod-rg
```

- [ ] Blazor has IP restrictions configured
- [ ] API has IP restrictions configured
- [ ] Israeli IP ranges listed in output

### Step 4.3: Test Access from Israeli IP

```powershell
# Test Blazor (should succeed from Israeli IP)
curl https://petel-prod-blazor.azurewebsites.net

# Test API (should succeed from Israeli IP)
curl https://petel-prod-api.azurewebsites.net/api/health
```

**Expected Results:**
- [ ] Blazor responds (200 OK)
- [ ] API responds (200 OK)
- [ ] No certificate errors

### Step 4.4: Test Geographic Restriction (Optional)

**Test from Non-Israeli IP** (use VPN, cloud server, or ask colleague abroad):
```bash
curl https://petel-prod-blazor.azurewebsites.net
```

**Expected Results:**
- [ ] Access blocked (403 Forbidden)
- [ ] Geographic restriction working correctly

**Note**: See [ISRAELI_IP_RANGES_ANALYSIS.md](ISRAELI_IP_RANGES_ANALYSIS.md) for detailed IP range documentation.

# Restrict API to Front Door only
az webapp config access-restriction add --resource-group $rg --name $api --rule-name "AllowFrontDoor" --action Allow --service-tag AzureFrontDoor.Backend --priority 100

# Restrict Blazor to Front Door only
az webapp config access-restriction add --resource-group $rg --name $blazor --rule-name "AllowFrontDoor" --action Allow --service-tag AzureFrontDoor.Backend --priority 100
```

- [ ] API restricted to Front Door traffic only
- [ ] Blazor restricted to Front Door traffic only
- [ ] Direct access to App Services blocked (test returns 403)

---

## Phase 5: Production Validation (15-20 minutes)

### Step 5.1: Functional Testing

**Login and Authentication:**
- [ ] Navigate to: `https://<FRONT_DOOR_URL>.z01.azurefd.net`
- [ ] Login page loads with Petel branding
- [ ] Login with admin credentials (from Step 2.4)
- [ ] Login successful
- [ ] Redirected to dashboard

**Security Features:**
- [ ] OTP/2FA prompt appears (if configured)
- [ ] Session timeout works (wait 10 minutes, session expires)
- [ ] Logout works correctly

**Core Functionality:**
- [ ] Dashboard loads data
- [ ] Navigation menu works
- [ ] Create a test school/student
- [ ] Edit feature works
- [ ] Delete feature works (soft delete)
- [ ] Search/filter works
- [ ] Hebrew RTL display correct
- [ ] Reports generate
- [ ] File upload works

**Rate Limiting:**
```powershell
# Trigger rate limit (10 failed logins in 15 minutes)
for ($i=1; $i -le 15; $i++) {
    curl -X POST https://<FRONT_DOOR_URL>.z01.azurefd.net/api/auth/login `
        -H "Content-Type: application/json" `
        -d '{"username":"fake","password":"fake"}'
    Write-Host "Attempt $i"
}
```

- [ ] Rate limiting triggered after 10 attempts
- [ ] HTTP 429 (Too Many Requests) returned
- [ ] Error message in Hebrew displayed
- [ ] Access restored after 15 minutes

### Step 5.2: Security Headers Validation

```powershell
# Check security headers
curl -I https://<FRONT_DOOR_URL>.z01.azurefd.net/api/health
```

**Required Headers:**
- [ ] `X-Content-Type-Options: nosniff`
- [ ] `X-Frame-Options: DENY`
- [ ] `X-XSS-Protection: 1; mode=block`
- [ ] `Referrer-Policy: no-referrer`
- [ ] `Strict-Transport-Security: max-age=31536000`
- [ ] `Content-Security-Policy` present

### Step 5.3: Performance Testing

**Response Time Benchmarks:**
```powershell
# API health endpoint
Measure-Command { curl https://<FRONT_DOOR_URL>.z01.azurefd.net/api/health }

# Blazor page load
Measure-Command { curl https://<FRONT_DOOR_URL>.z01.azurefd.net/ }
```

**Expected Performance:**
- [ ] API health: < 500ms
- [ ] Blazor load: < 3 seconds
- [ ] No timeouts
- [ ] No 503 errors

### Step 5.4: Database Encryption Verification

**Test PII Encryption:**
```sql
-- Check that sensitive data is encrypted
SELECT id, first_name, last_name, id_number FROM petel_schema.persons LIMIT 1;
```

- [ ] `id_number` field is encrypted (looks like base64 string)
- [ ] Personal data NOT readable in database
- [ ] Application displays decrypted data correctly

### Step 5.5: Logging and Monitoring

**Azure Portal Verification:**
- [ ] Navigate to: Azure Portal → API App Service → Logs
- [ ] Application Insights connected (if enabled)
- [ ] No critical errors in last 24 hours
- [ ] Telemetry data flowing

**WAF Logs:**
- [ ] Front Door → Security → WAF logs
- [ ] Blocked requests logged
- [ ] Israeli IP allowed traffic logged

---

## Post-Deployment Tasks

### Immediate (Within 24 Hours)

- [ ] **Change default admin password**
- [ ] Create additional admin users
- [ ] Configure email/SMS for OTP (if not done)
- [ ] Set up Application Insights (if skipped)
- [ ] Configure alerts:
  - [ ] API response time > 2 seconds
  - [ ] Error rate > 5%
  - [ ] Database CPU > 80%
  - [ ] Failed login attempts > 50/hour
  - [ ] WAF blocks > 100/hour
- [ ] Document all production URLs in team wiki
- [ ] Update DNS to point to Front Door (if using custom domain)
- [ ] Notify stakeholders of production availability

### Within 1 Week

- [ ] Configure automated backups
- [ ] Set up backup monitoring
- [ ] Create disaster recovery runbook
- [ ] Conduct load testing
- [ ] Security audit
- [ ] User acceptance testing (UAT)
- [ ] Train support team on production environment
- [ ] Document incident response procedures

### Within 1 Month

- [ ] Schedule penetration testing
- [ ] Review and optimize costs
- [ ] Set up compliance reporting (SOC 2)
- [ ] Implement Application Insights custom metrics
- [ ] Configure availability tests
- [ ] Create operational dashboards
- [ ] Conduct disaster recovery drill

---

## Rollback Checklist (If Needed)

### Immediate Rollback

**If critical issues occur during or after deployment:**

1. **Stop Traffic:**
   ```powershell
   az afd endpoint update --profile-name petel-prod-frontdoor --endpoint-name petel-prod --resource-group petel-prod-rg --enabled-state Disabled
   ```
   - [ ] Front Door disabled
   - [ ] Traffic stopped

2. **Revert Application:**
   ```powershell
   # Deploy previous stable version
   git checkout <PREVIOUS_STABLE_COMMIT>
   .\Deploy-ToAzure.ps1 -Environment production
   ```
   - [ ] Previous version deployed
   - [ ] Application functional

3. **Restore Database** (if schema changes made):
   ```powershell
   az postgres flexible-server restore --resource-group petel-prod-rg --name petel-prod-db-restored --source-server petel-prod-db-XXXX --restore-time "<TIMESTAMP>"
   ```
   - [ ] Database restored to pre-deployment state
   - [ ] Connection strings updated

4. **Re-enable Traffic:**
   ```powershell
   az afd endpoint update --profile-name petel-prod-frontdoor --endpoint-name petel-prod --resource-group petel-prod-rg --enabled-state Enabled
   ```
   - [ ] Front Door re-enabled
   - [ ] Traffic flowing normally

5. **Verify Rollback:**
   - [ ] Application accessible
   - [ ] No errors in logs
   - [ ] Core functionality works
   - [ ] Notify stakeholders of rollback

---

## Sign-Off

### Deployment Team

**Infrastructure Deployed By:**
- Name: ___________________
- Date: ___________________
- Signature: ___________________

**Application Deployed By:**
- Name: ___________________
- Date: ___________________
- Signature: ___________________

**Validation Completed By:**
- Name: ___________________
- Date: ___________________
- Signature: ___________________

### Management Approval

**Technical Lead Approval:**
- Name: ___________________
- Date: ___________________
- Signature: ___________________

**Product Owner Approval:**
- Name: ___________________
- Date: ___________________
- Signature: ___________________

**Executive Approval:**
- Name: ___________________
- Date: ___________________
- Signature: ___________________

---

## Notes and Issues

### Deployment Issues Encountered:
```
Issue 1: _______________________________________________
Resolution: _______________________________________________

Issue 2: _______________________________________________
Resolution: _______________________________________________
```

### Deviations from Plan:
```
Deviation 1: _______________________________________________
Justification: _______________________________________________

Deviation 2: _______________________________________________
Justification: _______________________________________________
```

### Outstanding Items:
```
Item 1: _______________________________________________
Assigned to: _______________________________________________
Due date: _______________________________________________

Item 2: _______________________________________________
Assigned to: _______________________________________________
Due date: _______________________________________________
```

---

## Appendix

### Contact List

**Emergency Contacts:**
- Azure Support: https://portal.azure.com → Support
- On-Call Engineer: _______________________________________________
- Technical Lead: _______________________________________________
- Database Admin: _______________________________________________

### Resource Names
```
Resource Group:       petel-prod-rg
App Service Plan:     petel-prod-plan
API App Service:      petel-prod-api
Blazor App Service:   petel-prod-blazor
PostgreSQL Server:    petel-prod-db-XXXX
Key Vault:            petel-kv-prod-XXXX
Front Door:           petel-prod-frontdoor
WAF Policy:           petelWafProd
```

### URLs
```
API Direct:           https://petel-prod-api.azurewebsites.net
Blazor Direct:        https://petel-prod-blazor.azurewebsites.net
Front Door:           https://petel-prod-XXXXXXXXXXXX.z01.azurefd.net
Database:             petel-prod-db-XXXX.postgres.database.azure.com
Key Vault:            https://petel-kv-prod-XXXX.vault.azure.net
```

---

**End of Production Deployment Checklist**
