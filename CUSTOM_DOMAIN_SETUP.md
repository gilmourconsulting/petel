# Custom Domain Setup for Blazor Test Environment

## Current State
- `petel.site` → HTML server (Azure App Service)
- `petel-test-blazor.azurewebsites.net` → Blazor server

## Target State
- `test.petel.site` → Blazor server
- `petel.site` → HTML server (or redirect to new URLs)

---

## Implementation Steps

### 1. DNS Configuration

**Add CNAME Record in your DNS provider:**
```
Type: CNAME
Name: test
Value: petel-test-blazor.azurewebsites.net
TTL: 3600
```

### 2. Azure App Service Custom Domain Setup

**Option A: Azure Portal**
1. Navigate to: `petel-test-blazor` App Service → Settings → Custom domains
2. Click **Add custom domain**
3. Enter domain: `test.petel.site`
4. Validate ownership (Azure will check CNAME record)
5. Click **Add custom domain**
6. **Add SSL Binding:**
   - Click on the domain → Add binding
   - Choose **App Service Managed Certificate** (free)
   - Select **SNI SSL**

**Option B: Azure CLI**
```bash
# Add custom domain
az webapp config hostname add \
  --resource-group petel-test-rg \
  --webapp-name petel-test-blazor \
  --hostname test.petel.site

# Create and bind free managed certificate
az webapp config ssl create \
  --resource-group petel-test-rg \
  --name petel-test-blazor \
  --hostname test.petel.site

az webapp config ssl bind \
  --resource-group petel-test-rg \
  --name petel-test-blazor \
  --certificate-thumbprint <thumbprint> \
  --ssl-type SNI
```

### 3. Update Blazor Configuration

**Update `appsettings.test.json`:**
```json
{
  "AllowedHosts": "test.petel.site;petel-test-blazor.azurewebsites.net",
  "BaseUrl": "https://test.petel.site"
}
```

### 4. Update CORS in Backend API

**In `PetelApp.Api/appsettings.test.json`:**
```json
{
  "AllowedOrigins": [
    "https://test.petel.site",
    "https://petel-test-blazor.azurewebsites.net"
  ]
}
```

### 5. Remove Custom Domain from HTML Server (Optional)

If you want to fully migrate away from HTML:
1. Azure Portal → HTML App Service → Custom domains
2. Select `petel.site` → Delete binding
3. Update DNS if needed

---

## Alternative: Path-Based Routing with Azure Front Door

### Architecture
```
petel.site              → Azure Front Door
  ├─ /ath/test/*       → Blazor Server Backend
  └─ /*                → HTML Server Backend
```

### Implementation

**1. Create Azure Front Door:**
```bash
az afd profile create \
  --profile-name petel-frontdoor \
  --resource-group petel-test-rg \
  --sku Standard_AzureFrontDoor

az afd endpoint create \
  --resource-group petel-test-rg \
  --profile-name petel-frontdoor \
  --endpoint-name petel-endpoint \
  --enabled-state Enabled
```

**2. Add Custom Domain:**
```bash
az afd custom-domain create \
  --resource-group petel-test-rg \
  --profile-name petel-frontdoor \
  --custom-domain-name petel-site \
  --host-name petel.site \
  --certificate-type ManagedCertificate
```

**3. Create Origin Groups:**
```bash
# Blazor Backend
az afd origin-group create \
  --resource-group petel-test-rg \
  --profile-name petel-frontdoor \
  --origin-group-name blazor-backend

az afd origin create \
  --resource-group petel-test-rg \
  --profile-name petel-frontdoor \
  --origin-group-name blazor-backend \
  --origin-name blazor-origin \
  --host-name petel-test-blazor.azurewebsites.net \
  --origin-host-header petel-test-blazor.azurewebsites.net \
  --priority 1 \
  --weight 1000 \
  --enabled-state Enabled \
  --http-port 80 \
  --https-port 443

# HTML Backend
az afd origin-group create \
  --resource-group petel-test-rg \
  --profile-name petel-frontdoor \
  --origin-group-name html-backend

az afd origin create \
  --resource-group petel-test-rg \
  --profile-name petel-frontdoor \
  --origin-group-name html-backend \
  --origin-name html-origin \
  --host-name <html-server>.azurewebsites.net \
  --origin-host-header <html-server>.azurewebsites.net \
  --priority 1 \
  --weight 1000 \
  --enabled-state Enabled \
  --http-port 80 \
  --https-port 443
```

**4. Create Routing Rules:**
```bash
# Blazor path-based routing
az afd route create \
  --resource-group petel-test-rg \
  --profile-name petel-frontdoor \
  --endpoint-name petel-endpoint \
  --route-name blazor-route \
  --origin-group blazor-backend \
  --supported-protocols Https \
  --https-redirect Enabled \
  --patterns-to-match "/ath/test/*" \
  --forwarding-protocol HttpsOnly

# HTML default routing
az afd route create \
  --resource-group petel-test-rg \
  --profile-name petel-frontdoor \
  --endpoint-name petel-endpoint \
  --route-name html-route \
  --origin-group html-backend \
  --supported-protocols Https \
  --https-redirect Enabled \
  --patterns-to-match "/*" \
  --forwarding-protocol HttpsOnly
```

**5. Configure Blazor for Path Base:**

In `PetelApp.BlazorServer/Program.cs`:
```csharp
// Configure path base for /ath/test
app.UsePathBase("/ath/test");

app.UseStaticFiles();
app.UseRouting();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
```

**6. Update Base Tag in `App.razor`:**
```html
<!DOCTYPE html>
<html lang="he" dir="rtl">
<head>
    <base href="/ath/test/" />
    <!-- Rest of head -->
</head>
```

---

## Cost Comparison

| Solution | Monthly Cost (Estimate) | Complexity |
|----------|------------------------|------------|
| **Subdomain** (`test.petel.site`) | $0 (included in App Service) | Low |
| **Azure Front Door Standard** | ~$35 + bandwidth | Medium |
| **Application Gateway** | ~$140 + bandwidth | High |

---

## Recommendation

**For Test Environment:** Use **Subdomain approach** (`test.petel.site`)
- ✅ No additional cost
- ✅ Simple DNS management
- ✅ Easy SSL certificate setup
- ✅ Clean separation

**For Production (if needed):** Consider **Azure Front Door**
- ✅ Global load balancing
- ✅ WAF capabilities
- ✅ Path-based routing
- ✅ Better performance

---

## DNS Propagation

After DNS changes:
- TTL-based propagation: 1-24 hours
- Check propagation: `nslookup test.petel.site`
- Test HTTPS after Azure binding: `https://test.petel.site`

---

## Rollback Plan

If issues occur:
1. Keep both domains active during transition
2. Test Blazor on `test.petel.site` before removing `petel.site` from HTML
3. Document all DNS/Azure changes
4. Have Azure Portal access ready

---

## Next Steps

1. [ ] Create DNS CNAME record for `test.petel.site`
2. [ ] Add custom domain in Azure Portal
3. [ ] Configure managed SSL certificate
4. [ ] Update Blazor configuration
5. [ ] Update API CORS settings
6. [ ] Test application on new domain
7. [ ] Remove old custom domain (if desired)
