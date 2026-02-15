# Production Environment - README

**Environment:** Production  
**Status:** Ready for Deployment  
**Last Updated:** February 15, 2026  

---

## Overview

This folder contains all scripts, documentation, and configuration files needed to deploy the Petel Educational Management System to a production Azure environment.

## Quick Links

### 🚀 Getting Started
- **[PRODUCTION_QUICK_START.md](PRODUCTION_QUICK_START.md)** - Fast-track deployment guide (2-3 hours)
- **[PRODUCTION_DEPLOYMENT_GUIDE.md](PRODUCTION_DEPLOYMENT_GUIDE.md)** - Comprehensive deployment guide
- **[PRODUCTION_CHECKLIST.md](PRODUCTION_CHECKLIST.md)** - Step-by-step validation checklist

### 🔧 Scripts
- **[Setup-Production-Infrastructure.ps1](Setup-Production-Infrastructure.ps1)** - Creates all Azure resources
- **[Setup-Production-FrontDoor.ps1](Setup-Production-FrontDoor.ps1)** - Configures Front Door and WAF
- **[Deploy-ToAzure.ps1](Deploy-ToAzure.ps1)** - Deploys application code

### 📚 Additional Documentation
- [SOC2_COMPLIANCE_ROADMAP.md](SOC2_COMPLIANCE_ROADMAP.md) - Security compliance status
- [BLAZOR_DEPLOYMENT_GUIDE.md](BLAZOR_DEPLOYMENT_GUIDE.md) - Blazor-specific deployment info
- [PRODUCTION_RATE_LIMITING_GUIDE.md](PRODUCTION_RATE_LIMITING_GUIDE.md) - Rate limiting configuration
- [BLAZOR_SECURITY_USAGE_GUIDE.md](BLAZOR_SECURITY_USAGE_GUIDE.md) - Security implementation details

---

## Architecture

```
Internet (Israeli IPs Only)
    ↓
Azure Front Door Premium + WAF
    ├── OWASP Rules (v3.2)
    ├── Bot Protection
    ├── Israeli IP Whitelist (43 ranges)
    └── DDoS Protection
    ↓
┌─────────────────────┬─────────────────────┐
│   Blazor Server     │      API            │
│   (.NET 8.0)        │   (.NET 9.0)        │
│   petel-prod-blazor │   petel-prod-api    │
└─────────────────────┴─────────────────────┘
            ↓                    ↓
    ┌───────────────────────────────┐
    │   Azure Key Vault             │
    │   - DB Connection String      │
    │   - JWT Secret                │
    │   - Encryption Keys           │
    └───────────────────────────────┘
                   ↓
    ┌───────────────────────────────┐
    │   PostgreSQL Flexible Server  │
    │   - 2 vCores, 8GB RAM         │
    │   - Encrypted at rest         │
    │   - Automated backups (7 days)│
    └───────────────────────────────┘
```

---

## Deployment Process

### Phase 1: Infrastructure Setup (30-45 min)

```powershell
# Run infrastructure setup script
.\Setup-Production-Infrastructure.ps1
```

**Creates:**
- Resource Group: `petel-prod-rg`
- App Service Plan: `petel-prod-plan` (P1V3)
- API App Service: `petel-prod-api`
- Blazor App Service: `petel-prod-blazor`
- PostgreSQL Server: `petel-prod-db-XXXX`
- Key Vault: `petel-kv-prod-XXXX`

### Phase 2: Security Configuration (30-45 min)

1. Generate encryption keys
2. Add secrets to Key Vault
3. Configure App Service Key Vault references
4. Initialize database schema
5. Create admin user

### Phase 3: Application Deployment (15-20 min)

```powershell
# Deploy both API and Blazor
.\Deploy-ToAzure.ps1 -Environment production
```

### Phase 4: Front Door & WAF (20-30 min)

```powershell
# Setup Front Door with WAF
.\Setup-Production-FrontDoor.ps1
```

### Phase 5: Validation (15-20 min)

- Functional testing
- Security validation
- Performance testing
- Rate limiting verification

---

## Resources Created

| Resource Type | Name | Purpose | SKU/Tier |
|--------------|------|---------|----------|
| Resource Group | `petel-prod-rg` | Container for all resources | N/A |
| App Service Plan | `petel-prod-plan` | Hosting plan | P1V3 (Premium) |
| App Service | `petel-prod-api` | Backend API | .NET 9.0 Linux |
| App Service | `petel-prod-blazor` | Frontend | .NET 8.0 Linux |
| PostgreSQL | `petel-prod-db-XXXX` | Database | 2 vCores, 8GB |
| Key Vault | `petel-kv-prod-XXXX` | Secrets management | Standard |
| Front Door | `petel-prod-frontdoor` | CDN + WAF | Premium |
| WAF Policy | `petelWafProd` | Web Application Firewall | Premium |

---

## Security Features

### ✅ Implemented

- **Authentication:** JWT with signed tokens, 8-hour expiration
- **2FA/OTP:** SMS/Email-based one-time passwords
- **Encryption:** 
  - AES-256 for PII data (persons, students)
  - TLS 1.2+ for data in transit
  - Database encryption at rest
- **Rate Limiting:** 
  - Login: 10 attempts per 15 min
  - OTP: 5 attempts per 15 min
  - API: Varies by HTTP method
- **WAF Protection:**
  - OWASP Core Rule Set 3.2
  - Bot protection
  - Israeli IP restrictions (43 ranges)
  - Geo-blocking (non-Israeli traffic)
- **Security Headers:**
  - X-Content-Type-Options
  - X-Frame-Options
  - Strict-Transport-Security
  - CSP (Content Security Policy)
- **Audit Logging:** All CRUD operations tracked
- **RBAC:** 3-level security (page/action/menu)
- **Session Management:** 10-minute timeout
- **Password Policy:** BCrypt hashing, 3-month expiration

---

## Configuration Files

### Backend API

- [PetelApp.Api/appsettings.Production.json](PetelApp.Api/appsettings.Production.json)
  - Connection strings → Key Vault references
  - JWT configuration
  - Rate limiting rules
  - Security settings

### Frontend Blazor

- [PetelApp.BlazorServer/appsettings.Production.json](PetelApp.BlazorServer/appsettings.Production.json)
  - API base URL
  - Security CSP settings

---

## URLs

### Direct Access (Internal Only - POST-Front Door restriction)
- **API:** https://petel-prod-api.azurewebsites.net
- **Blazor:** https://petel-prod-blazor.azurewebsites.net

### Front Door (Public Access)
- **Main:** https://petel-prod-XXXXXXXXXXXX.z01.azurefd.net
- **API:** https://petel-prod-XXXXXXXXXXXX.z01.azurefd.net/api
- **Blazor:** https://petel-prod-XXXXXXXXXXXX.z01.azurefd.net/

*(Exact URL provided after Front Door deployment)*

---

## Cost Breakdown

| Service | Tier | Monthly Cost (USD) |
|---------|------|-------------------|
| App Service Plan | P1V3 | $150-200 |
| PostgreSQL | 2 vCores, 8GB | $100-150 |
| Front Door | Premium | $300-400 |
| Key Vault | Standard | $5 |
| **Total** | | **$555-755** |

*Costs are approximate and may vary based on usage*

---

## Monitoring & Alerts

### Recommended Alerts

- API response time > 2 seconds
- Error rate > 5%
- Database CPU > 80%
- Failed login attempts > 50/hour
- WAF blocks > 100/hour
- Database storage > 90%

### Logs Access

```powershell
# API logs
az webapp log tail --name petel-prod-api --resource-group petel-prod-rg

# Blazor logs
az webapp log tail --name petel-prod-blazor --resource-group petel-prod-rg

# Front Door WAF logs
# Azure Portal → Front Door → Security → WAF logs
```

---

## Backup & Recovery

### Automated Backups

- **PostgreSQL:** Daily automated backups, 7-day retention
- **Key Vault:** Soft-delete enabled, 90-day retention
- **App Services:** Deployment slot-based rollback

### Manual Backup

```powershell
# Create database backup
az postgres flexible-server backup create --resource-group petel-prod-rg --server-name petel-prod-db-XXXX

# Export Key Vault secrets (backup)
az keyvault secret list --vault-name petel-kv-prod-XXXX --query "[].id" -o tsv | ForEach-Object { az keyvault secret show --id $_ }
```

### Disaster Recovery

**Recovery Time Objective (RTO):** 1 hour  
**Recovery Point Objective (RPO):** 24 hours (daily backups)

**Recovery Steps:**
1. Restore database from backup (15 min)
2. Deploy previous application version (10 min)
3. Verify functionality (15 min)
4. Update DNS/Front Door if needed (5 min)

---

## Maintenance Windows

**Recommended Schedule:**
- **Patching:** 2nd Sunday of each month, 2:00-4:00 AM IST
- **Backups:** Daily at 3:00 AM IST (automated)
- **Security Updates:** As needed (emergency)

**Planned Downtime:**
- Application updates: 5-10 minutes
- Database updates: 15-30 minutes
- Infrastructure changes: 30-60 minutes

---

## Support & Escalation

### Level 1: Application Issues
- Check application logs
- Verify service health in Azure Portal
- Review recent deployments

### Level 2: Infrastructure Issues
- Azure support ticket
- Review monitoring dashboards
- Check service status: https://status.azure.com

### Level 3: Security Incidents
- **IMMEDIATE:** Disable Front Door to stop traffic
- Contact Azure security team
- Preserve logs for forensics
- Follow incident response runbook

---

## Compliance

### SOC 2 Readiness: 75%

**Completed:**
- [x] Data encryption (at rest and in transit)
- [x] Access control and RBAC
- [x] Audit logging
- [x] Key management (Azure Key Vault)
- [x] Security headers and WAF
- [x] Rate limiting and DDoS protection

**Remaining:**
- [ ] Application Insights integration
- [ ] Formal security documentation
- [ ] External penetration testing
- [ ] SOC 2 audit engagement

See [SOC2_COMPLIANCE_ROADMAP.md](SOC2_COMPLIANCE_ROADMAP.md) for details.

---

## Common Issues & Solutions

### Issue: Application Won't Start

**Symptoms:** 503 Service Unavailable  
**Causes:**
- Key Vault access denied
- Database connection failed
- Missing configuration

**Solution:**
```powershell
# Check logs
az webapp log tail --name petel-prod-api --resource-group petel-prod-rg

# Verify Key Vault access
$principalId = az webapp identity show --name petel-prod-api --resource-group petel-prod-rg --query principalId -o tsv
az keyvault set-policy --name petel-kv-prod-XXXX --object-id $principalId --secret-permissions get list
```

### Issue: 403 Forbidden (WAF)

**Symptoms:** Cannot access application  
**Causes:**
- IP not in Israeli range
- Malicious request pattern detected

**Solution:**
- Verify client IP is in Israel
- Check WAF logs for block reason
- Add IP to custom allow list if legitimate

### Issue: 429 Too Many Requests

**Symptoms:** Rate limiting triggered  
**Cause:** Exceeded request limits

**Solution:**
- Wait 15 minutes for limit reset
- Review rate limits in appsettings.Production.json
- Adjust limits if legitimate traffic

---

## Deployment Checklist

Before deploying to production:

- [ ] All test environment validations passed
- [ ] Security audit completed
- [ ] Load testing completed
- [ ] Backup strategy verified
- [ ] Monitoring and alerts configured
- [ ] Team trained on production procedures
- [ ] Incident response plan documented
- [ ] Stakeholder approval obtained
- [ ] Communication plan in place

---

## Change Management

### Making Changes to Production

1. **Propose Change** - Document change request
2. **Test in Lower Environment** - Validate in test/staging
3. **Schedule Change Window** - Coordinate with stakeholders
4. **Create Rollback Plan** - Document revert procedure
5. **Execute Change** - Follow deployment procedure
6. **Validate** - Verify functionality
7. **Monitor** - Watch for issues post-change
8. **Document** - Update change log

### Emergency Changes

Skip steps 3-4 only for critical security issues or outages.

---

## Documentation Updates

This documentation should be updated when:
- Infrastructure changes are made
- New features are deployed
- Security configurations change
- Incidents occur (lessons learned)
- Monthly review cycle

**Last Review:** February 15, 2026  
**Next Review:** March 15, 2026

---

## Getting Help

**Documentation:**
- Start with [PRODUCTION_QUICK_START.md](PRODUCTION_QUICK_START.md)
- Detailed guide: [PRODUCTION_DEPLOYMENT_GUIDE.md](PRODUCTION_DEPLOYMENT_GUIDE.md)
- Validation: [PRODUCTION_CHECKLIST.md](PRODUCTION_CHECKLIST.md)

**Azure Support:**
- https://portal.azure.com → Support + Troubleshooting
- Phone: Available in Azure Portal

**Emergency Contacts:**
- Technical Lead: _________________
- Azure Support: _________________
- On-Call Engineer: _________________

---

## Version History

| Date | Version | Changes | Author |
|------|---------|---------|--------|
| 2026-02-15 | 1.0 | Initial production setup documentation | System |
| | | | |

---

**Ready to deploy? Start with [PRODUCTION_QUICK_START.md](PRODUCTION_QUICK_START.md)!**
