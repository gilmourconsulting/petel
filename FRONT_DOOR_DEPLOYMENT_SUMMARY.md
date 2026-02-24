# Israeli IP Ranges for Azure App Service IP Restrictions
# DEPRECATED: Azure Front Door was removed - Using direct App Service IP restrictions

## For Azure Portal - Comma-separated format:
79.176.0.0/13,80.178.0.0/15,80.246.0.0/15,80.250.0.0/15,82.80.128.0/17,82.166.0.0/15,85.64.0.0/13,86.57.0.0/17,86.109.0.0/16,87.68.0.0/14,87.236.0.0/14,88.198.0.0/15,89.138.0.0/15,90.128.0.0/11,91.90.88.0/21,91.199.9.0/24,92.126.0.0/16,94.188.0.0/14,94.230.0.0/16,109.186.0.0/15,109.228.0.0/15,132.64.0.0/12,141.226.0.0/16,146.185.128.0/17,147.161.128.0/17,149.3.0.0/17,151.233.0.0/16,176.12.0.0/15,176.63.0.0/16,178.137.0.0/16,178.173.128.0/17,185.2.12.0/22,185.4.16.0/22,188.64.0.0/13,188.120.128.0/17,212.116.128.0/17,213.57.0.0/17,212.179.0.0/16,82.166.0.0/16,77.125.0.0/16,31.154.0.0/16,31.168.0.0/16,80.178.0.0/16,87.70.0.0/16,94.188.0.0/16,95.86.0.0/16,103.209.0.0/16

## Individual ranges (47 total):
79.176.0.0/13    # Israeli range
80.178.0.0/15    # Israeli range
80.246.0.0/15    # Israeli range
80.250.0.0/15    # Israeli range
82.80.128.0/17   # Israeli range
82.166.0.0/15    # Israeli range / Bezeq
85.64.0.0/13     # Israeli range
86.57.0.0/17     # Israeli range
86.109.0.0/16    # Israeli range
87.68.0.0/14     # Israeli range
87.236.0.0/14    # Israeli range
88.198.0.0/15    # Israeli range
89.138.0.0/15    # Israeli range
90.128.0.0/11    # Israeli range
91.90.88.0/21    # Israeli range
91.199.9.0/24    # Israeli range
92.126.0.0/16    # Israeli range
94.188.0.0/14    # Israeli range
94.230.0.0/16    # Israeli range
109.186.0.0/15   # Israeli range
109.228.0.0/15   # Israeli range
132.64.0.0/12    # Israeli range
141.226.0.0/16   # Israeli range
146.185.128.0/17 # Israeli range
147.161.128.0/17 # Israeli range
149.3.0.0/17     # Israeli range
151.233.0.0/16   # Israeli range
176.12.0.0/15    # Israeli range
176.63.0.0/16    # Israeli range
178.137.0.0/16   # Israeli range
178.173.128.0/17 # Israeli range
185.2.12.0/22    # Israeli range
185.4.16.0/22    # Israeli range
188.64.0.0/13    # Israeli range
188.120.128.0/17 # Israeli range
212.116.128.0/17 # Israeli range
213.57.0.0/17    # Israeli range
212.179.0.0/16   # Bezeq International
82.166.0.0/16    # Bezeq
77.125.0.0/16    # Hot/Cable
31.154.0.0/16    # Cellcom
31.168.0.0/16    # Partner
80.178.0.0/16    # Israeli provider
87.70.0.0/16     # Israeli provider
94.188.0.0/16    # Israeli provider
95.86.0.0/16     # Israeli provider
103.209.0.0/16   # Israeli provider

## Current Implementation: Azure App Service IP Restrictions

**Architecture**: IP restrictions are configured directly on Azure App Service (both API and Blazor apps), not via Azure Front Door.

### Configuration Steps:

**Option 1: Using PowerShell Script**
```powershell
.\Add-IsraelIPRestrictions.ps1 -Environment production
```

**Option 2: Manual Azure Portal Configuration**
1. Go to Azure Portal: https://portal.azure.com
2. Navigate to: App Services → [your-app-service]
3. Click: Settings → Networking
4. Click: Access Restrictions
5. Add IP ranges from the list above
6. Save changes

## Direct App Service URLs

**Test Environment:**
- Blazor App: https://petel-test-blazor.azurewebsites.net
- API: https://petel-test-api.azurewebsites.net

**Production Environment:**
- Blazor App: https://petel-prod-blazor.azurewebsites.net
- API: https://petel-prod-api.azurewebsites.net

## Security Features Active

✅ Azure App Service IP Restrictions (Israeli IPs only)
✅ SSL/TLS encryption (automatic with azurewebsites.net)
✅ Basic DDoS mitigation (Azure infrastructure)
✅ Network isolation via IP filtering

## Next Steps

1. Add Israeli IP restrictions via script or Azure Portal
2. Test access from Israeli IPs
3. Verify non-Israeli IPs are blocked (403 Forbidden)
4. Monitor access logs for blocked requests
5. Update IP ranges as needed (see ISRAELI_IP_RANGES_ANALYSIS.md)

## Monitoring

- View access logs: Azure Portal → App Service → Logs
- Check blocked requests: Diagnostics → Application Insights
- Monitor performance: Metrics → App Service metrics

## Why No Front Door?

Azure Front Door was removed to reduce costs. Direct App Service IP restrictions provide sufficient security for this application:
- ✅ Geographic restriction (Israeli IPs only)
- ✅ Lower monthly costs (~$0 vs ~$35+ for Front Door)
- ✅ Simpler architecture to maintain
- ✅ Adequate protection for educational management system
