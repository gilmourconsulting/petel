# Production Environment - Quick Start Guide

**Last Updated:** February 15, 2026  
**Estimated Time:** 2-3 hours  
**Complexity:** Moderate  

---

## Overview

This guide provides the fastest path to deploying a production environment for the Petel Educational Management System.

## Prerequisites

✅ Azure subscription with Contributor/Owner access  
✅ Azure CLI authenticated (`az login`)  
✅ PowerShell 5.1+  
✅ .NET 9.0 and .NET 8.0 SDKs installed  

## 5-Step Deployment Process

### Step 1: Create Infrastructure (30-45 min)

```powershell
cd c:\dev\PetelFullApp

# Preview what will be created
.\Setup-Production-Infrastructure.ps1 -DryRun

# Create all resources
.\Setup-Production-Infrastructure.ps1
```

**What happens:**
- Creates resource group, App Service Plan, API, Blazor, PostgreSQL, Key Vault
- Generates database credentials (save them!)
- Configures managed identities

**Save This Info:**
- Database credentials from `production-db-credentials-*.txt`
- Key Vault name from output
- **DELETE credentials file after saving!**

---

### Step 2: Configure Security (30-45 min)

**A. Generate keys:**

```powershell
# JWT Secret (copy output)
-join ((48..57) + (65..90) + (97..122) | Get-Random -Count 64 | ForEach-Object {[char]$_})

# AES Key (copy output)
$aes = New-Object byte[] 32; [System.Security.Cryptography.RNGCryptoServiceProvider]::Create().GetBytes($aes); [Convert]::ToBase64String($aes)
```

**B. Add secrets to Key Vault:**

```powershell
$kv = "petel-kv-prod-XXXX"  # Replace with your Key Vault name
$dbConn = "Host=petel-prod-db-XXXX.postgres.database.azure.com;Database=petelappdb;Username=peteldbadmin;Password=YOUR_PASSWORD;SslMode=Require"

az keyvault secret set --vault-name $kv --name "ConnectionStrings--DefaultConnection" --value $dbConn
az keyvault secret set --vault-name $kv --name "ConnectionStrings--HangfireConnection" --value $dbConn
az keyvault secret set --vault-name $kv --name "Security--Jwt--SecretKey" --value "YOUR_JWT_SECRET"
az keyvault secret set --vault-name $kv --name "Security--DataEncryption--EncryptionKey" --value "YOUR_AES_KEY"
```

**C. Configure App Service:**

```powershell
$api = "petel-prod-api"
$rg = "petel-prod-rg"
$kv = "petel-kv-prod-XXXX"

az webapp config appsettings set --name $api --resource-group $rg --settings `
    "ConnectionStrings__DefaultConnection=@Microsoft.KeyVault(SecretUri=https://$kv.vault.azure.net/secrets/ConnectionStrings--DefaultConnection/)" `
    "ConnectionStrings__HangfireConnection=@Microsoft.KeyVault(SecretUri=https://$kv.vault.azure.net/secrets/ConnectionStrings--HangfireConnection/)" `
    "Security__Jwt__SecretKey=@Microsoft.KeyVault(SecretUri=https://$kv.vault.azure.net/secrets/Security--Jwt--SecretKey/)" `
    "Security__DataEncryption__EncryptionKey=@Microsoft.KeyVault(SecretUri=https://$kv.vault.azure.net/secrets/Security--DataEncryption--EncryptionKey/)"
```

**D. Initialize database:**

- Connect with pgAdmin/DBeaver using credentials from Step 1
- Run schema creation scripts
- Run table creation scripts
- Create admin user

```sql
-- Create admin (password: Admin2025! - CHANGE AFTER LOGIN!)
INSERT INTO petel_schema.users (username, password_hash, email, first_name, last_name, is_active)
VALUES ('admin', '$2a$11$<BCRYPT_HASH>', 'admin@petel.co.il', 'Admin', 'User', true);
```

---

### Step 3: Deploy Application (15-20 min)

**Verify configuration:**

Check [PetelApp.BlazorServer/appsettings.Production.json](PetelApp.BlazorServer/appsettings.Production.json):

```json
{
  "ApiSettings": {
    "BaseUrl": "https://petel-prod-api.azurewebsites.net/api"
  }
}
```

**Deploy:**

```powershell
cd c:\dev\PetelFullApp
.\Deploy-ToAzure.ps1 -Environment production
```

**Verify:**

```powershell
# Should return {"status":"Healthy"}
curl https://petel-prod-api.azurewebsites.net/api/health

# Should return HTML
curl https://petel-prod-blazor.azurewebsites.net/
```

---

### Step 4: Setup Front Door & WAF (20-30 min)

```powershell
cd c:\dev\PetelFullApp
.\Setup-Production-FrontDoor.ps1
```

**What happens:**
- Creates Azure Front Door Premium
- Configures WAF with OWASP rules
- Enables bot protection
- Adds Israeli IP restrictions (43 ranges)
- Blocks non-Israeli geo-locations
- Configures routes for API and Blazor

**Save Front Door URL from output!**

**Test WAF:**

```powershell
# Normal request (should work)
curl https://petel-prod-XXXX.z01.azurefd.net

# SQL injection (should be blocked)
curl "https://petel-prod-XXXX.z01.azurefd.net/?test=1' OR '1'='1"
```

**Restrict App Services to Front Door only:**

```powershell
$rg = "petel-prod-rg"

az webapp config access-restriction add --resource-group $rg --name petel-prod-api --rule-name "AllowFrontDoor" --action Allow --service-tag AzureFrontDoor.Backend --priority 100

az webapp config access-restriction add --resource-group $rg --name petel-prod-blazor --rule-name "AllowFrontDoor" --action Allow --service-tag AzureFrontDoor.Backend --priority 100
```

---

### Step 5: Validate & Go Live (15-20 min)

**Security Headers Check:**

```powershell
curl -I https://petel-prod-XXXX.z01.azurefd.net/api/health
```

Verify presence:
- `X-Content-Type-Options: nosniff`
- `X-Frame-Options: DENY`
- `Strict-Transport-Security`

**Functional Test:**

1. Navigate to Front Door URL
2. Login with admin credentials
3. Create test data
4. Verify Hebrew RTL works
5. Test logout

**Rate Limiting Test:**

```powershell
# Should block after 10 attempts
for ($i=1; $i -le 15; $i++) {
    curl -X POST https://petel-prod-XXXX.z01.azurefd.net/api/auth/login `
        -H "Content-Type: application/json" `
        -d '{"username":"test","password":"test"}'
}
```

**Performance Check:**

```powershell
# Should be < 500ms
Measure-Command { curl https://petel-prod-XXXX.z01.azurefd.net/api/health }
```

---

## Immediate Post-Deployment Tasks

1. **Change admin password** from default
2. Configure email/SMS for OTP
3. Set up monitoring alerts
4. Notify stakeholders
5. Update DNS (if using custom domain)

---

## Monitoring

**API Logs:**
```powershell
az webapp log tail --name petel-prod-api --resource-group petel-prod-rg
```

**Blazor Logs:**
```powershell
az webapp log tail --name petel-prod-blazor --resource-group petel-prod-rg
```

**WAF Logs:**
Azure Portal → Front Door → Security → WAF logs

---

## Rollback

**If issues occur:**

```powershell
# Disable Front Door (stops traffic)
az afd endpoint update --profile-name petel-prod-frontdoor --endpoint-name petel-prod --resource-group petel-prod-rg --enabled-state Disabled

# Revert to previous version
git checkout <PREVIOUS_TAG>
.\Deploy-ToAzure.ps1 -Environment production

# Re-enable
az afd endpoint update --profile-name petel-prod-frontdoor --endpoint-name petel-prod --resource-group petel-prod-rg --enabled-state Enabled
```

---

## Troubleshooting

### Application won't start

```powershell
az webapp log tail --name petel-prod-api --resource-group petel-prod-rg
```

Common causes:
- Key Vault access denied → Check managed identity permissions
- Database connection failed → Verify connection string
- Missing config → Check App Service settings

### 403 Forbidden from WAF

- IP not in Israeli ranges → Add to WAF custom rule
- Malicious request → Check WAF logs for details

### 429 Too Many Requests

- Rate limit hit → Wait 15 minutes
- Adjust limits in appsettings.Production.json if needed

### Key Vault Access Denied

```powershell
$principalId = az webapp identity show --name petel-prod-api --resource-group petel-prod-rg --query principalId -o tsv
az keyvault set-policy --name $kv --object-id $principalId --secret-permissions get list
```

---

## Cost Estimate

**Monthly costs (approximate):**
- App Service Plan (P1V3): $150-200
- PostgreSQL (2 vCores): $100-150
- Front Door Premium: $300-400
- Key Vault: $5
- **Total: ~$555-755/month**

---

## Complete Documentation

For detailed information:
- [PRODUCTION_DEPLOYMENT_GUIDE.md](PRODUCTION_DEPLOYMENT_GUIDE.md) - Comprehensive guide
- [PRODUCTION_CHECKLIST.md](PRODUCTION_CHECKLIST.md) - Step-by-step checklist
- [SOC2_COMPLIANCE_ROADMAP.md](SOC2_COMPLIANCE_ROADMAP.md) - Security compliance
- [BLAZOR_DEPLOYMENT_GUIDE.md](BLAZOR_DEPLOYMENT_GUIDE.md) - Blazor specifics

---

## Support

**Azure Support:** https://portal.azure.com → Support  
**Documentation:** See guides above  
**Emergency:** Disable Front Door to stop traffic immediately  

---

**Ready to deploy? Start with Step 1!**
