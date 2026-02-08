# SOC 2 Compliance Roadmap - Petel Educational Management System

**Document Date:** February 2, 2026  
**System Version:** .NET 8.0 with Blazor Server  
**Current Status:** 75% Compliant - Production Ready with Remaining Items  
**Target Completion:** August 2026 (6 months)

---

## Executive Summary

The Petel system has achieved **significant SOC 2 readiness** following the Blazor migration and Azure Key Vault implementation. We are approximately **75% compliant** with SOC 2 Trust Services Criteria, with clear action items remaining before production deployment and full audit readiness.

### Key Achievements ✅
- ✅ Azure Key Vault secrets management (test environment)
- ✅ AES-256 encryption for PII data
- ✅ JWT authentication with signed tokens
- ✅ Blazor Server with ProtectedSessionStorage
- ✅ Comprehensive audit logging
- ✅ Role-based access control (RBAC)
- ✅ 2FA/OTP authentication support

### Remaining Work ❌
- ✅ Security headers (1 day - High Priority) - **COMPLETED 2026-02-02**
- ❌ Rate limiting (3 days - High Priority)
- ❌ Azure Front Door + WAF configuration (Infrastructure team)
- ❌ Application Insights integration (1 day)
- ❌ Formal security documentation (2-3 weeks)
- ❌ External penetration testing (3-4 weeks)
- ❌ SOC 2 audit engagement (3-4 months)

---

## Current Implementation Status by Environment

### Development Environment (Local)

#### ✅ Complete & Functional
- Local PostgreSQL database
- All security features operational
- JWT authentication and encryption working
- Serilog file logging configured
- Development secrets in appsettings.Development.json

#### ✅ Not Required for Dev
- Azure Key Vault (uses local config)
- Rate limiting (testing only)
- Security headers (optional)
- IP restrictions
- Application Insights

**Status: 100% Complete for Development** ✅

---

### Test Environment (Azure)

#### ✅ Already Deployed & Working
1. **Infrastructure**
   - Azure App Service (petel-test-api, petel-test-blazor)
   - Azure PostgreSQL Flexible Server
   - Azure Key Vault (`petel-kv-test-4721`)
   - HTTPS/SSL encryption enabled

2. **Security Features**
   - Secrets loaded from Key Vault:
     - Database connection strings
     - JWT secret keys  
     - Data encryption keys
   - AES-256 encryption for PII (persons, students)
   - JWT tokens with ProtectedSessionStorage
   - BCrypt password hashing
   - RBAC with 3-level security (page/action/menu)
   - 2FA/OTP support
   - Audit logging to database
   - Session timeout (10 minutes configurable)

#### ❌ Quick Wins Needed (1-2 Weeks)

**1. Security Headers (Priority: HIGH)** ✅ **COMPLETED 2026-02-02**
- **Effort:** 1 day
- **Owner:** Development team
- **Status:** Implemented in both PetelApp.Api/Program.cs and PetelApp.BlazorServer/Program.cs
- **Details:**
  - Added security headers middleware after UseRouting() in API
  - Added security headers middleware after UseAntiforgery() in Blazor Server
  - Headers only applied in non-development environments
  - Blazor CSP adapted for framework requirements (unsafe-inline for scripts)

**2. Rate Limiting (Priority: HIGH)**
- **Effort:** 3 days
- **Owner:** Development team
- **Package:** AspNetCoreRateLimit
- **Commands:**
  ```bash
  cd PetelApp.Api
  dotnet add package AspNetCoreRateLimit
  ```
- **Configuration Required:**
  - Login endpoint: 5 attempts per 15 minutes per IP
  - OTP verify: 3 attempts per 15 minutes per user
  - General API: 100 requests per minute per user
  - Files to modify: `Program.cs`, new `RateLimitConfiguration.cs`

**3. Application Insights (Priority: MEDIUM)**
- **Effort:** 1 day
- **Owner:** Development team
- **Package:** Microsoft.ApplicationInsights.AspNetCore
- **Commands:**
  ```bash
  cd PetelApp.Api
  dotnet add package Microsoft.ApplicationInsights.AspNetCore
  cd ../PetelApp.BlazorServer
  dotnet add package Microsoft.ApplicationInsights.AspNetCore
  ```
- **Features:**
  - Performance monitoring
  - Exception tracking
  - Custom security events (failed logins)
  - Dependency tracking (database calls)

#### ❌ Azure Front Door Configuration (Priority: HIGH)
- **Effort:** 1-2 days
- **Owner:** Infrastructure team
- **Requirements:**

**A. Create Azure Front Door**
```bash
# Create Front Door profile
az afd profile create \
  --profile-name petel-frontdoor-test \
  --resource-group petel-test-rg \
  --sku Premium_AzureFrontDoor

# Create endpoint
az afd endpoint create \
  --endpoint-name petel-test \
  --profile-name petel-frontdoor-test \
  --resource-group petel-test-rg

# Create origin group for API
az afd origin-group create \
  --origin-group-name api-origins \
  --profile-name petel-frontdoor-test \
  --resource-group petel-test-rg \
  --probe-path /api/health \
  --probe-protocol Https

# Add API origin
az afd origin create \
  --origin-name api-backend \
  --origin-group-name api-origins \
  --profile-name petel-frontdoor-test \
  --resource-group petel-test-rg \
  --host-name petel-test-api-ahafcqfnh6drcdbd.israelcentral-01.azurewebsites.net \
  --origin-host-header petel-test-api-ahafcqfnh6drcdbd.israelcentral-01.azurewebsites.net \
  --priority 1 \
  --weight 1000 \
  --enabled-state Enabled \
  --http-port 80 \
  --https-port 443

# Create origin group for Blazor
az afd origin-group create \
  --origin-group-name blazor-origins \
  --profile-name petel-frontdoor-test \
  --resource-group petel-test-rg \
  --probe-path / \
  --probe-protocol Https

# Add Blazor origin
az afd origin create \
  --origin-name blazor-backend \
  --origin-group-name blazor-origins \
  --profile-name petel-frontdoor-test \
  --resource-group petel-test-rg \
  --host-name petel-test-blazor.azurewebsites.net \
  --origin-host-header petel-test-blazor.azurewebsites.net \
  --priority 1 \
  --weight 1000 \
  --enabled-state Enabled
```

**B. Configure WAF (Web Application Firewall)**
```bash
# Create WAF policy
az network front-door waf-policy create \
  --name petelWafTest \
  --resource-group petel-test-rg \
  --sku Premium_AzureFrontDoor \
  --mode Prevention

# Enable OWASP Core Rule Set
az network front-door waf-policy managed-rule-set add \
  --policy-name petelWafTest \
  --resource-group petel-test-rg \
  --type Microsoft_DefaultRuleSet \
  --version 2.1

# Enable Bot Protection
az network front-door waf-policy managed-rule-set add \
  --policy-name petelWafTest \
  --resource-group petel-test-rg \
  --type Microsoft_BotManagerRuleSet \
  --version 1.0
```

**C. Israel IP Whitelist Configuration**
```bash
# Create custom rule for Israeli IP ranges
az network front-door waf-policy rule create \
  --policy-name petelWafTest \
  --resource-group petel-test-rg \
  --name AllowIsraeliIPs \
  --priority 100 \
  --rule-type MatchRule \
  --action Allow \
  --match-variable RemoteAddr \
  --operator IPMatch \
  --match-values \
    79.176.0.0/13 \
    80.178.0.0/15 \
    80.246.0.0/15 \
    80.250.0.0/15 \
    82.80.128.0/17 \
    82.166.0.0/15 \
    85.64.0.0/13 \
    86.57.0.0/17 \
    86.109.0.0/16 \
    87.68.0.0/14 \
    87.236.0.0/14 \
    88.198.0.0/15 \
    89.138.0.0/15 \
    90.128.0.0/11 \
    91.90.88.0/21 \
    91.199.9.0/24 \
    92.126.0.0/16 \
    94.188.0.0/14 \
    94.230.0.0/16 \
    109.186.0.0/15 \
    109.228.0.0/15 \
    132.64.0.0/12 \
    141.226.0.0/16 \
    146.185.128.0/17 \
    147.161.128.0/17 \
    149.3.0.0/17 \
    151.233.0.0/16 \
    176.12.0.0/15 \
    176.63.0.0/16 \
    178.137.0.0/16 \
    178.173.128.0/17 \
    185.2.12.0/22 \
    185.4.16.0/22 \
    188.64.0.0/13 \
    188.120.128.0/17 \
    212.116.128.0/17 \
    213.57.0.0/17

# Block all other traffic
az network front-door waf-policy rule create \
  --policy-name petelWafTest \
  --resource-group petel-test-rg \
  --name BlockNonIsraeliIPs \
  --priority 200 \
  --rule-type MatchRule \
  --action Block \
  --match-variable RemoteAddr \
  --operator GeoMatch \
  --negate \
  --match-values IL
```

**D. Associate WAF with Front Door**
```bash
az afd security-policy create \
  --profile-name petel-frontdoor-test \
  --resource-group petel-test-rg \
  --security-policy-name petelSecurityPolicy \
  --domains petel-test.azurefd.net \
  --waf-policy /subscriptions/{subscription-id}/resourceGroups/petel-test-rg/providers/Microsoft.Network/frontDoorWebApplicationFirewallPolicies/petelWafTest
```

**Status: 75% Complete for Test** ⚠️

---

### Production Environment (Azure)

#### ✅ Designed (Same as Test + Enhancements)
- All test environment features
- Separate Azure Key Vault for production secrets
- Separate resource groups and databases
- Production domain (petel.site)
- Enhanced monitoring and alerting

#### ❌ Additional Requirements for Production

**1. Production Azure Key Vault**
```bash
# Create production Key Vault
az keyvault create \
  --name petel-kv-prod \
  --resource-group petel-prod-rg \
  --location israelcentral \
  --enable-rbac-authorization true

# Set secrets (after creating resources)
az keyvault secret set --vault-name petel-kv-prod \
  --name ConnectionStrings--DefaultConnection \
  --value "Host=petel-prod-db.postgres.database.azure.com;..."

az keyvault secret set --vault-name petel-kv-prod \
  --name Security--Jwt--SecretKey \
  --value "{GENERATED_SECRET}"

az keyvault secret set --vault-name petel-kv-prod \
  --name Security--DataEncryption--EncryptionKey \
  --value "{GENERATED_KEY}"
```

**2. Backup & Restore Configuration**
```bash
# Enable geo-redundant backups on production database
az postgres flexible-server update \
  --resource-group petel-prod-rg \
  --name petel-prod-db \
  --backup-retention 14 \
  --geo-redundant-backup Enabled

# Verify backup configuration
az postgres flexible-server backup list \
  --resource-group petel-prod-rg \
  --name petel-prod-db

# Test restore procedure (to be documented)
az postgres flexible-server restore \
  --resource-group petel-prod-rg \
  --name petel-prod-db-restore-test \
  --source-server petel-prod-db \
  --restore-time "2026-02-01T12:00:00Z"
```

**3. Enhanced Monitoring & Alerts**
```bash
# Create Log Analytics workspace
az monitor log-analytics workspace create \
  --resource-group petel-prod-rg \
  --workspace-name petel-prod-logs \
  --retention-time 90

# Configure Application Insights
az monitor app-insights component create \
  --app petel-prod-insights \
  --location israelcentral \
  --resource-group petel-prod-rg \
  --workspace petel-prod-logs

# Create alert rules
# Failed login attempts
az monitor metrics alert create \
  --name FailedLoginAlert \
  --resource-group petel-prod-rg \
  --scopes /subscriptions/{sub-id}/resourceGroups/petel-prod-rg/providers/Microsoft.Web/sites/petel-prod-api \
  --condition "count requests where resultCode >= 401 > 10" \
  --window-size 5m \
  --evaluation-frequency 1m \
  --action email-admin

# High error rate
az monitor metrics alert create \
  --name HighErrorRate \
  --resource-group petel-prod-rg \
  --scopes /subscriptions/{sub-id}/resourceGroups/petel-prod-rg/providers/Microsoft.Web/sites/petel-prod-api \
  --condition "percentage exceptions > 5" \
  --window-size 5m \
  --action email-devops

# Database connection failures
az monitor metrics alert create \
  --name DatabaseConnectionFailure \
  --resource-group petel-prod-rg \
  --scopes /subscriptions/{sub-id}/resourceGroups/petel-prod-rg/providers/Microsoft.DBforPostgreSQL/flexibleServers/petel-prod-db \
  --condition "count connections_failed > 5" \
  --window-size 5m \
  --action sms-oncall

# Certificate expiration
az monitor metrics alert create \
  --name CertificateExpiring \
  --resource-group petel-prod-rg \
  --scopes /subscriptions/{sub-id}/resourceGroups/petel-prod-rg/providers/Microsoft.Web/sites/petel-prod-api \
  --condition "days_until_certificate_expiration < 30" \
  --action email-admin
```

**4. Log Retention & Automated Purging**
- **Implementation:** Hangfire job or Azure Function
- **Schedule:** Daily at 2 AM
- **SQL Script:**
  ```sql
  -- Delete audit logs older than 2 years
  DELETE FROM petel_schema.action_audit_logs 
  WHERE timestamp < NOW() - INTERVAL '2 years';
  
  -- Archive old Serilog files (via Azure Function)
  -- Move to cold storage after 90 days
  ```

**5. Disaster Recovery Testing**
- **Schedule:** Quarterly
- **Procedure:**
  1. Restore database to point-in-time (last backup)
  2. Verify data integrity
  3. Test application connectivity
  4. Document recovery time actual (RTA)
  5. Update RTO/RPO if needed

**Status: 50% Complete for Production** ⚠️

---

## Required Documentation for SOC 2 Audit

### 1. Information Security Policy
**Priority:** HIGH  
**Effort:** 1 week  
**Owner:** Management + Compliance Officer

**Required Sections:**
1. **Purpose and Scope**
   - Policy objectives
   - Applicable systems and personnel
   - Regulatory requirements (Israeli privacy laws, GDPR if applicable)

2. **Roles and Responsibilities**
   - Information Security Officer
   - System administrators
   - Developers
   - End users

3. **Access Control Standards**
   - User provisioning/deprovisioning
   - Password requirements (BCrypt hashing, complexity)
   - 2FA/OTP requirements
   - Privileged access management
   - Principle of least privilege

4. **Data Classification**
   - Public data
   - Internal data
   - Confidential data (student PII)
   - Restricted data (financial records)

5. **Encryption Standards**
   - Data in transit (TLS 1.2+)
   - Data at rest (AES-256)
   - Key management (Azure Key Vault)

6. **Audit Logging Requirements**
   - What must be logged
   - Retention periods (2 years for audit logs)
   - Review procedures

7. **Incident Response**
   - Incident classification
   - Escalation procedures
   - Communication requirements

8. **Third-Party Security**
   - Vendor assessment process
   - Security requirements for contractors

### 2. Incident Response Plan
**Priority:** HIGH  
**Effort:** 4 days  
**Owner:** IT Manager + Security Team

**Required Elements:**

**A. Incident Classification**
| Severity | Examples | Response Time |
|----------|----------|---------------|
| P1 - Critical | Data breach, system compromise, complete outage | 15 minutes |
| P2 - High | Failed security controls, partial outage | 1 hour |
| P3 - Medium | Suspicious activity, minor vulnerabilities | 4 hours |
| P4 - Low | Policy violations, informational alerts | 24 hours |

**B. Escalation Matrix**
```
P1/P2 Incidents:
  1. Security Alert → On-Call Engineer (immediate)
  2. Engineer → IT Manager (15 min)
  3. Manager → CTO (30 min)
  4. CTO → CEO (1 hour if data breach)

P3/P4 Incidents:
  1. Security Alert → Security Team email
  2. Team Lead reviews and assigns
  3. Escalate if necessary
```

**C. Response Procedures**
1. **Detection** - How incidents are identified (alerts, logs, reports)
2. **Containment** - Immediate actions to prevent spread
3. **Eradication** - Remove threat from environment
4. **Recovery** - Restore normal operations
5. **Post-Incident Review** - Document lessons learned

**D. Evidence Preservation**
- Log collection procedures
- Chain of custody for forensics
- Legal considerations

**E. Communication Plan**
- Internal notifications (staff, management)
- External notifications (customers, authorities)
- Public relations (if needed)
- Regulatory reporting (48-72 hours for data breaches)

### 3. Change Management Procedures
**Priority:** MEDIUM  
**Effort:** 2 days  
**Owner:** Development Manager

**Document:**

**A. Change Request Process**
```
1. Developer creates change request in Azure DevOps
   - Description of change
   - Business justification
   - Risk assessment
   - Rollback plan

2. Peer review (code review required)
   - Security implications
   - Performance impact
   - Testing requirements

3. Approval workflow
   - Small changes: Tech Lead approval
   - Medium changes: Manager approval
   - Large changes: CTO + Stakeholder approval

4. Testing requirements
   - Unit tests pass
   - Integration tests pass
   - Security testing (if applicable)
   - User acceptance testing (UAT)

5. Deployment
   - Deploy to test environment first
   - Verify functionality
   - Schedule production deployment
   - Execute deployment during maintenance window

6. Post-deployment verification
   - Smoke tests
   - Monitor for errors (24 hours)
   - Stakeholder sign-off
```

**B. Emergency Change Process**
- Security patches: Expedited approval
- Production outages: On-call manager approval
- Post-implementation review required

**C. Documentation Requirements**
- All changes logged in Azure DevOps
- Deployment notes maintained
- Rollback procedures tested

### 4. Data Retention Policy
**Priority:** MEDIUM  
**Effort:** 1 day  
**Owner:** Data Protection Officer + Legal

**Define Retention Periods:**

| Data Type | Retention Period | Rationale | Disposal Method |
|-----------|------------------|-----------|-----------------|
| Student Records | 7 years after graduation | Israeli education law | Secure deletion or archive |
| Financial Transactions | 7 years | Tax law requirements | Archive to cold storage |
| Audit Logs | 2 years | SOC 2 requirement | Automated purge via SQL job |
| Application Logs | 90 days | Operational needs | Log rotation (Serilog) |
| Backup Data | 30 days | Recovery needs | Automatic expiration |
| User Accounts (deleted) | 30 days soft delete | Recovery grace period | Hard delete after 30 days |
| Session Data | 24 hours | Security best practice | Automatic expiration |
| Password Reset Tokens | 1 hour | Security best practice | Automatic invalidation |

**Implementation:**
```sql
-- Hangfire job - daily at 2 AM
[AutomaticRetry(Attempts = 3)]
public async Task PurgeOldData()
{
    // Audit logs older than 2 years
    await _context.Database.ExecuteSqlRawAsync(
        "DELETE FROM petel_schema.action_audit_logs WHERE timestamp < NOW() - INTERVAL '2 years'");
    
    // Soft-deleted users older than 30 days
    await _context.Database.ExecuteSqlRawAsync(
        "DELETE FROM petel_schema.users WHERE deleted_at < NOW() - INTERVAL '30 days'");
    
    // Expired sessions
    await _context.Database.ExecuteSqlRawAsync(
        "DELETE FROM petel_schema.user_sessions WHERE expires_at < NOW()");
}
```

### 5. Business Continuity Plan
**Priority:** MEDIUM  
**Effort:** 3 days  
**Owner:** IT Manager + Management

**Include:**

**A. Recovery Objectives**
- **RTO (Recovery Time Objective):** 4 hours
- **RPO (Recovery Point Objective):** 15 minutes

**B. Disaster Scenarios**
1. **Database Failure**
   - Azure automatic failover (if geo-replication enabled)
   - Manual restore from backup
   - Estimated recovery: 1-2 hours

2. **Application Outage**
   - Restart App Service
   - Rollback to previous deployment
   - Estimated recovery: 30 minutes

3. **Data Center Failure (Israel region)**
   - Failover to secondary region (if configured)
   - Restore from geo-redundant backup
   - Estimated recovery: 4-8 hours

4. **Security Breach**
   - Follow incident response plan
   - May require full system restoration
   - Estimated recovery: 8-24 hours

**C. Recovery Procedures**
Detailed step-by-step procedures for each scenario (see Appendix A)

**D. Contact Lists**
- On-call engineers
- Azure support
- Database administrators
- Management escalation

**E. Testing Schedule**
- Backup restore testing: Quarterly
- Disaster recovery drill: Annually
- Tabletop exercise: Semi-annually

---

## External Validation Requirements

### 1. Penetration Testing
**Priority:** HIGH  
**Timeline:** Month 3-4  
**Cost:** $5,000 - $15,000  
**Frequency:** Annual + after major changes

**Scope:**
- **Web Application Testing**
  - Authentication bypass attempts
  - Authorization flaws (RBAC testing)
  - Session management vulnerabilities
  - Input validation (SQL injection, XSS)
  - CSRF protection verification

- **API Security Testing**
  - JWT token manipulation
  - Rate limiting verification
  - Endpoint authorization
  - Data exposure risks

- **Infrastructure Security**
  - Network configuration review
  - SSL/TLS configuration
  - Server hardening assessment
  - Azure configuration review

**Deliverables:**
- Executive summary
- Detailed findings report
- Risk ratings (Critical/High/Medium/Low)
- Remediation recommendations
- Retest verification

**Vendors to Consider:**
- Israeli cybersecurity firms (local language support)
- International firms with Azure expertise
- Minimum qualifications: CREST certified or equivalent

### 2. Vulnerability Scanning
**Priority:** MEDIUM  
**Timeline:** Ongoing (start Month 2)  
**Cost:** $1,000 - $3,000/year  
**Frequency:** Weekly automated scans

**Tools:**

**A. Azure Security Center (Free)**
```bash
# Enable Security Center
az security pricing create \
  --name VirtualMachines \
  --tier Standard

az security auto-provisioning-setting update \
  --name default \
  --auto-provision On
```

**B. Dependency Scanning**
- **GitHub Dependabot** (free) - Already enabled if using GitHub
- **OWASP Dependency-Check** (open source)
  ```bash
  # Add to CI/CD pipeline
  dotnet tool install --global dependency-check
  dependency-check --project Petel --scan ./PetelApp.Api
  ```

**C. Code Analysis**
- **SonarQube** or **SonarCloud**
  ```bash
  # Add to Azure DevOps pipeline
  dotnet tool install --global dotnet-sonarscanner
  dotnet sonarscanner begin /k:"PetelApp"
  dotnet build
  dotnet sonarscanner end
  ```

### 3. SOC 2 Audit Engagement
**Priority:** CRITICAL  
**Timeline:** Month 4-17  
**Cost:** $15,000 - $50,000 (Type I + Type II)  
**Frequency:** Annual

**Process:**

**Phase 1: Pre-Audit Gap Assessment (4-6 weeks)**
- Auditor reviews documentation
- Identifies control gaps
- Provides remediation guidance
- Cost: $3,000 - $5,000

**Phase 2: SOC 2 Type I Audit (6-8 weeks)**
- Point-in-time evaluation
- Control design assessment
- Testing at specific date
- Report issuance
- Cost: $8,000 - $20,000

**Phase 3: SOC 2 Type II Audit (12 months)**
- Continuous evidence collection
- Quarterly auditor visits
- Annual control testing
- Operating effectiveness over time
- Report issuance
- Cost: $15,000 - $40,000

**Trust Services Criteria:**
- **Security (Common Criteria)**
  - CC6.1: Logical and Physical Access
  - CC6.2: Authorization
  - CC6.6: Encryption
  - CC6.7: Data Sanitization
  - CC7.2: System Monitoring
  - CC7.3: Response to Incidents
  - CC8.1: Change Management

- **Availability (if needed)**
  - A1.2: Recovery and Backup

**Selecting an Auditor:**
- Must be licensed CPA firm
- SOC 2 experience required
- Azure/cloud experience preferred
- Israeli presence helpful (language/legal)
- References from similar-sized companies

---

## Implementation Timeline

### Month 1: Quick Technical Wins
**Weeks 1-2: Development Team**
- ✅ Day 1: Implement security headers (API + Blazor)
- ✅ Day 2-4: Implement rate limiting
  - AspNetCoreRateLimit package
  - Configure rules (login, OTP, general API)
  - Test rate limit behavior
- ✅ Day 5: Integrate Application Insights SDK
  - Add package to both projects
  - Configure instrumentation key from Azure
  - Test custom event logging

**Weeks 3-4: Infrastructure Team**
- ✅ Week 3: Configure Azure Front Door for test
  - Create Front Door resource
  - Configure origins (API + Blazor)
  - Setup WAF with OWASP rules
  - Configure Israel IP whitelist
  - Test access from Israel and other countries
- ✅ Week 4: Setup monitoring and alerts
  - Configure Application Insights in Azure
  - Create alert rules (failed logins, errors, performance)
  - Test alert delivery (email/SMS)
  - Configure Log Analytics workspace

**Deliverable:** Test environment 90% SOC 2 ready

### Month 2: Documentation Phase 1
**Week 1: Information Security Policy**
- Draft policy document
- Review with legal/compliance
- Management approval
- Publish to internal portal

**Week 2: Incident Response Plan**
- Define incident classification
- Create escalation matrix
- Document response procedures
- Conduct tabletop exercise

**Week 3: Change Management & Data Retention**
- Document change management process
- Define data retention periods
- Create automated purge jobs
- Update deployment procedures

**Week 4: Business Continuity Plan**
- Define RTO/RPO
- Document disaster scenarios
- Create recovery procedures
- Schedule DR testing

**Deliverable:** Core security documentation complete

### Month 3: Production Preparation
**Week 1-2: Infrastructure Setup**
- Create production Azure resources
- Setup production Key Vault
- Configure geo-redundant backups
- Setup Azure Front Door for production
- Configure production monitoring

**Week 3-4: Deployment & Testing**
- Deploy to production
- Verify all security controls
- Test backup/restore procedures
- Conduct security review
- User acceptance testing

**Deliverable:** Production environment live

### Month 4: External Validation Begins
**Week 1-2: Penetration Testing Preparation**
- Select penetration testing vendor
- Define scope and rules of engagement
- Prepare test environment
- Coordinate timing

**Week 3-4: Vulnerability Assessment**
- Run automated vulnerability scans
- Review and remediate findings
- Setup continuous scanning
- Document scan results

**Ongoing: SOC 2 Pre-Audit**
- Engage SOC 2 auditor
- Gap assessment review
- Remediate identified gaps
- Begin evidence collection

**Deliverable:** External validation in progress

### Month 5: Penetration Test & Remediation
**Week 1-2: Active Penetration Testing**
- Testing firm conducts assessment
- Daily briefings on findings
- Emergency fixes if critical issues found

**Week 3-4: Remediation**
- Review detailed findings report
- Prioritize remediation work
- Implement fixes
- Request retest verification

**Deliverable:** Penetration test complete, issues remediated

### Month 6: SOC 2 Type I Audit
**Week 1-2: Audit Preparation**
- Final documentation review
- Evidence collection
- System walkthrough with auditor
- Control demonstration

**Week 3-4: Type I Audit Execution**
- Auditor performs control testing
- Respond to information requests
- Address any findings
- Review draft report

**Deliverable:** SOC 2 Type I report issued

### Months 7-18: Type II Evidence Collection
**Ongoing Activities:**
- Monthly control testing
- Quarterly auditor visits
- Continuous evidence collection
- Annual penetration testing
- Quarterly DR testing
- Policy reviews and updates

**Month 18: SOC 2 Type II Audit Complete**
- Final control testing
- Report issuance
- Certificate received

**Deliverable:** Full SOC 2 Type II compliance

---

## Roles & Responsibilities

### Infrastructure Team
**Primary Owner:** DevOps/Cloud Engineer

**Immediate Tasks (Month 1):**
- Configure Azure Front Door for test environment
- Setup WAF with Israel IP whitelist
- Configure Application Insights
- Setup Azure Monitor alerts
- Verify PostgreSQL backups

**Ongoing Tasks:**
- Monitor infrastructure health
- Manage Azure resources
- Implement security updates
- Conduct DR testing

### Development Team
**Primary Owner:** Lead Developer

**Immediate Tasks (Month 1):**
- Implement security headers
- Implement rate limiting
- Integrate Application Insights SDK
- Create automated log purging job

**Ongoing Tasks:**
- Security code reviews
- Dependency updates
- Feature development with security in mind
- Support auditor information requests

### Management/Compliance
**Primary Owner:** CTO or Compliance Officer

**Immediate Tasks (Month 2):**
- Write Information Security Policy
- Write Incident Response Plan
- Document Change Management
- Define Data Retention Policy
- Create Business Continuity Plan

**Ongoing Tasks:**
- Policy reviews and updates
- Staff security training
- Vendor management
- Auditor coordination
- Executive reporting

### QA/Testing Team
**Primary Owner:** QA Lead

**Tasks:**
- Security testing procedures
- Penetration test coordination
- Backup restore testing
- DR drill participation
- User acceptance testing

---

## Success Criteria & Milestones

### Milestone 1: Test Environment Ready (End of Month 1)
✅ **Criteria:**
- Security headers implemented
- Rate limiting functional
- Application Insights collecting data
- Azure Front Door configured with WAF
- Israel IP whitelist active
- All alerts configured and tested

**Sign-off:** CTO + Infrastructure Lead

### Milestone 2: Documentation Complete (End of Month 2)
✅ **Criteria:**
- Information Security Policy approved
- Incident Response Plan documented
- Change Management procedures formalized
- Data Retention Policy defined
- Business Continuity Plan created
- All documents reviewed by legal/compliance

**Sign-off:** CTO + Compliance Officer

### Milestone 3: Production Deployed (End of Month 3)
✅ **Criteria:**
- Production environment configured
- All security controls operational
- Backup/restore tested successfully
- Monitoring and alerting active
- DR procedures documented
- User training completed

**Sign-off:** CTO + Management Team

### Milestone 4: External Validation Complete (End of Month 5)
✅ **Criteria:**
- Penetration test passed (no critical findings)
- Vulnerability scanning operational
- All identified issues remediated
- Retest verification passed
- SOC 2 pre-audit gaps addressed

**Sign-off:** CTO + Security Team

### Milestone 5: SOC 2 Type I Complete (End of Month 6)
✅ **Criteria:**
- Type I audit report received
- All audit findings addressed
- Report shows no significant deficiencies
- Certificate suitable for customer sharing

**Sign-off:** CEO + Board of Directors

### Milestone 6: SOC 2 Type II Complete (Month 18)
✅ **Criteria:**
- Type II audit report received
- 12 months of evidence collection complete
- Operating effectiveness demonstrated
- Full SOC 2 certification achieved

**Sign-off:** CEO + Board of Directors

---

## Budget Estimates

### One-Time Costs (Year 1)

| Item | Cost (USD) | Notes |
|------|------------|-------|
| **Azure Front Door Premium** | $330/month = $3,960/year | Includes WAF |
| **Application Insights** | $100-300/month = $1,200-3,600/year | Based on data volume |
| **Penetration Testing** | $8,000 - $15,000 | Annual |
| **Vulnerability Scanning Tools** | $1,000 - $3,000/year | Various tools |
| **SOC 2 Type I Audit** | $8,000 - $20,000 | One-time |
| **SOC 2 Type II Audit** | $15,000 - $40,000 | Annual |
| **Security Consultant** | $5,000 - $10,000 | Optional, gap assessment |
| **Training & Certification** | $2,000 - $5,000 | Staff security training |
| **TOTAL (First Year)** | **$43,160 - $99,560** | |

### Recurring Costs (Year 2+)

| Item | Cost (USD) | Frequency |
|------|------------|-----------|
| Azure Front Door | $3,960 | Annual |
| Application Insights | $1,200 - $3,600 | Annual |
| Penetration Testing | $8,000 - $15,000 | Annual |
| Vulnerability Scanning | $1,000 - $3,000 | Annual |
| SOC 2 Type II Audit | $15,000 - $40,000 | Annual |
| **TOTAL (Ongoing)** | **$29,160 - $65,560** | Annual |

### Internal Resource Costs (FTE Time)

| Role | Month 1 | Month 2 | Month 3-6 | Ongoing |
|------|---------|---------|-----------|---------|
| Infrastructure Engineer | 80% | 40% | 20% | 10% |
| Lead Developer | 60% | 20% | 20% | 10% |
| Developer | 40% | 10% | 10% | 5% |
| Compliance Officer | 20% | 80% | 40% | 20% |
| QA Engineer | 20% | 20% | 40% | 10% |
| Management | 10% | 20% | 20% | 10% |

---

## Risk Assessment & Mitigation

### High Risks

**1. Penetration Test Reveals Critical Vulnerabilities**
- **Impact:** Project delay, security remediation required
- **Probability:** Medium (20-30%)
- **Mitigation:**
  - Early security code review
  - Implement all security controls before test
  - Budget extra time for remediation
  - Engage pen test firm early for guidance

**2. SOC 2 Audit Identifies Control Deficiencies**
- **Impact:** Failed audit, reputational risk
- **Probability:** Low-Medium (15-25%)
- **Mitigation:**
  - Pre-audit gap assessment
  - Continuous evidence collection
  - Regular internal audits
  - Engage auditor early for guidance

**3. Azure Front Door Configuration Issues**
- **Impact:** Service disruption, blocked legitimate traffic
- **Probability:** Medium (25-35%)
- **Mitigation:**
  - Test thoroughly in test environment
  - Implement gradually (monitoring mode first)
  - Maintain backdoor access for emergency
  - Document rollback procedures

**4. Resource Constraints (Time/People)**
- **Impact:** Timeline delays
- **Probability:** High (40-50%)
- **Mitigation:**
  - Clear prioritization
  - External consultants if needed
  - Management commitment
  - Regular progress reviews

### Medium Risks

**5. Documentation Quality Issues**
- **Impact:** Audit delays, need for rewrites
- **Probability:** Medium (30%)
- **Mitigation:**
  - Use templates from consulting firms
  - Legal/compliance review
  - Peer review process

**6. Backup/Restore Failures**
- **Impact:** Data loss risk, compliance issues
- **Probability:** Low (10-15%)
- **Mitigation:**
  - Regular testing (quarterly)
  - Azure managed backups (reliable)
  - Multiple backup retention points
  - Documented procedures

---

## Appendices

### Appendix A: Disaster Recovery Procedures

**Scenario 1: Database Failure**

**Symptoms:**
- Application cannot connect to database
- PostgreSQL service unresponsive
- Error messages in Application Insights

**Procedure:**
1. **Verify Outage** (5 minutes)
   ```bash
   # Check database status
   az postgres flexible-server show \
     --resource-group petel-prod-rg \
     --name petel-prod-db
   
   # Check Azure Service Health
   az rest --method get --url https://management.azure.com/subscriptions/{sub}/providers/Microsoft.ResourceHealth/availabilityStatuses
   ```

2. **Attempt Service Restart** (10 minutes)
   ```bash
   az postgres flexible-server restart \
     --resource-group petel-prod-rg \
     --name petel-prod-db
   ```

3. **If Restart Fails: Restore from Backup** (1-2 hours)
   ```bash
   # List available backups
   az postgres flexible-server backup list \
     --resource-group petel-prod-rg \
     --name petel-prod-db
   
   # Restore to new server (cannot overwrite existing)
   az postgres flexible-server restore \
     --resource-group petel-prod-rg \
     --name petel-prod-db-restore \
     --source-server petel-prod-db \
     --restore-time "2026-02-02T10:00:00Z"
   
   # Update connection strings in Key Vault
   az keyvault secret set \
     --vault-name petel-kv-prod \
     --name ConnectionStrings--DefaultConnection \
     --value "Host=petel-prod-db-restore.postgres..."
   
   # Restart App Services to pick up new connection
   az webapp restart --name petel-prod-api --resource-group petel-prod-rg
   az webapp restart --name petel-prod-blazor --resource-group petel-prod-rg
   ```

4. **Verify Recovery** (15 minutes)
   - Test login functionality
   - Verify data integrity (spot checks)
   - Monitor Application Insights for errors
   - Notify stakeholders of resolution

**Total RTO: 2-3 hours**

---

**Scenario 2: Application Outage**

**Symptoms:**
- Users cannot access system
- HTTP 503 or 500 errors
- High error rate in Application Insights

**Procedure:**
1. **Verify Outage** (5 minutes)
   ```bash
   # Check app service status
   az webapp show --name petel-prod-api --resource-group petel-prod-rg
   az webapp show --name petel-prod-blazor --resource-group petel-prod-rg
   
   # Check application logs
   az webapp log tail --name petel-prod-api --resource-group petel-prod-rg
   ```

2. **Attempt Quick Fixes** (15 minutes)
   ```bash
   # Option A: Restart app service
   az webapp restart --name petel-prod-api --resource-group petel-prod-rg
   
   # Option B: Scale up if resource exhaustion
   az appservice plan update \
     --resource-group petel-prod-rg \
     --name petel-prod-plan \
     --sku P1V2
   ```

3. **If Issue Persists: Rollback Deployment** (30 minutes)
   ```bash
   # List recent deployments
   az webapp deployment list \
     --name petel-prod-api \
     --resource-group petel-prod-rg
   
   # Rollback to previous slot or deployment
   az webapp deployment source config-zip \
     --resource-group petel-prod-rg \
     --name petel-prod-api \
     --src /path/to/previous-version.zip
   ```

4. **Verify Recovery** (10 minutes)
   - Test core functionality
   - Monitor error rates
   - Notify users of resolution

**Total RTO: 1 hour**

---

### Appendix B: Security Headers Reference

**Production-Ready Configuration:**

```csharp
// PetelApp.Api/Program.cs and PetelApp.BlazorServer/Program.cs
// Add after app.UseRouting() and before app.MapControllers()

if (!app.Environment.IsDevelopment())
{
    app.Use(async (context, next) =>
    {
        // Prevent clickjacking
        context.Response.Headers.Add("X-Frame-Options", "DENY");
        
        // Prevent MIME sniffing
        context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
        
        // Enable XSS filter in older browsers
        context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
        
        // Control referrer information
        context.Response.Headers.Add("Referrer-Policy", "strict-origin-when-cross-origin");
        
        // Disable dangerous browser features
        context.Response.Headers.Add("Permissions-Policy", 
            "geolocation=(), microphone=(), camera=(), payment=()");
        
        // Content Security Policy (strict)
        context.Response.Headers.Add("Content-Security-Policy",
            "default-src 'self'; " +
            "script-src 'self'; " +
            "style-src 'self' 'unsafe-inline'; " +  // Blazor requires inline styles
            "img-src 'self' data:; " +
            "font-src 'self'; " +
            "connect-src 'self'; " +
            "frame-ancestors 'none'; " +
            "base-uri 'self'; " +
            "form-action 'self'");
        
        await next();
    });
}
```

---

### Appendix C: Rate Limiting Configuration

**PetelApp.Api/Configuration/RateLimitConfiguration.cs:**

```csharp
using AspNetCoreRateLimit;

namespace PetelApp.Api.Configuration
{
    public static class RateLimitConfiguration
    {
        public static void ConfigureRateLimiting(this IServiceCollection services, IConfiguration configuration)
        {
            // Needed to store rate limit counters
            services.AddMemoryCache();
            
            // Load configuration
            services.Configure<IpRateLimitOptions>(configuration.GetSection("IpRateLimiting"));
            services.Configure<IpRateLimitPolicies>(configuration.GetSection("IpRateLimitPolicies"));
            
            // Inject counter and rules stores
            services.AddInMemoryRateLimiting();
            
            // Configuration
            services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
        }
    }
}
```

**appsettings.Production.json:**

```json
{
  "IpRateLimiting": {
    "EnableEndpointRateLimiting": true,
    "StackBlockedRequests": false,
    "RealIpHeader": "X-Forwarded-For",
    "ClientIdHeader": "X-ClientId",
    "HttpStatusCode": 429,
    "GeneralRules": [
      {
        "Endpoint": "POST:/api/auth/login",
        "Period": "15m",
        "Limit": 5
      },
      {
        "Endpoint": "POST:/api/auth/verify-otp",
        "Period": "15m",
        "Limit": 3
      },
      {
        "Endpoint": "*",
        "Period": "1m",
        "Limit": 100
      },
      {
        "Endpoint": "*",
        "Period": "1h",
        "Limit": 1000
      }
    ]
  }
}
```

**Program.cs:**

```csharp
// Add before builder.Build()
builder.Services.ConfigureRateLimiting(builder.Configuration);

// Add in middleware pipeline (after UseRouting, before UseEndpoints)
app.UseIpRateLimiting();
```

---

### Appendix D: Israeli IP Ranges (Complete List)

```
79.176.0.0/13
80.178.0.0/15
80.246.0.0/15
80.250.0.0/15
82.80.128.0/17
82.166.0.0/15
85.64.0.0/13
86.57.0.0/17
86.109.0.0/16
87.68.0.0/14
87.236.0.0/14
88.198.0.0/15
89.138.0.0/15
90.128.0.0/11
91.90.88.0/21
91.199.9.0/24
92.126.0.0/16
94.188.0.0/14
94.230.0.0/16
109.186.0.0/15
109.228.0.0/15
132.64.0.0/12
141.226.0.0/16
146.185.128.0/17
147.161.128.0/17
149.3.0.0/17
151.233.0.0/16
176.12.0.0/15
176.63.0.0/16
178.137.0.0/16
178.173.128.0/17
185.2.12.0/22
185.4.16.0/22
188.64.0.0/13
188.120.128.0/17
212.116.128.0/17
213.57.0.0/17
```

---

### Appendix E: Contacts & Escalation

**Internal Contacts:**

| Role | Name | Email | Phone | Availability |
|------|------|-------|-------|--------------|
| CTO | [Name] | cto@petel-system.co.il | [Phone] | 24/7 |
| IT Manager | [Name] | it@petel-system.co.il | [Phone] | Business hours |
| On-Call Engineer | [Rotation] | oncall@petel-system.co.il | [Phone] | 24/7 |
| Security Team | [Name] | security@petel-system.co.il | [Phone] | Business hours |
| Compliance Officer | [Name] | compliance@petel-system.co.il | [Phone] | Business hours |

**External Contacts:**

| Vendor | Contact | Purpose | SLA |
|--------|---------|---------|-----|
| Azure Support | Portal + Phone | Infrastructure issues | 1 hour response (P1) |
| Penetration Test Firm | [TBD] | Security testing | As scheduled |
| SOC 2 Auditor | [TBD] | Compliance audit | As scheduled |
| Database Consultant | [TBD] | PostgreSQL expert | 4 hours response |

**Emergency Escalation:**

```
P1 Incident:
  → On-Call Engineer (immediate notification)
  → IT Manager (within 15 minutes)
  → CTO (within 30 minutes)
  → CEO (within 1 hour if data breach)

P2 Incident:
  → On-Call Engineer or Security Team
  → IT Manager (within 1 hour)
  → CTO (within 4 hours)
```

---

## Document Control

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2026-02-02 | System Architect | Initial roadmap created |
| | | | |
| | | | |

**Review Schedule:** Quarterly  
**Next Review:** May 2026  
**Document Owner:** CTO  
**Approvers:** CEO, Board of Directors (for budget approval)

---

## Conclusion

The Petel Educational Management System has made **excellent progress** toward SOC 2 compliance, achieving approximately **75% readiness**. The remaining work is well-defined and achievable within a 6-month timeline for production readiness and 18 months for full SOC 2 Type II certification.

**Key Success Factors:**
1. ✅ Strong technical foundation already in place
2. ✅ Clear action items with defined owners
3. ✅ Realistic timeline and budget
4. ✅ Management commitment to compliance
5. ✅ Use of Azure managed services (reduces complexity)

**Next Immediate Actions (This Week):**
1. Infrastructure team: Begin Azure Front Door setup for test
2. Development team: Implement security headers
3. Management: Review and approve budget
4. All: Assign specific owners to each task
5. Schedule weekly progress meetings

With disciplined execution of this plan, the Petel system will achieve SOC 2 compliance and provide customers with the security assurances they require.

---

**Document Status:** DRAFT - Pending Management Approval  
**Confidential:** Internal Use Only
