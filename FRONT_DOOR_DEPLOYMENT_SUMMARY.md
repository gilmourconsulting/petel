# Israeli IP Ranges for Front Door WAF Configuration
# Copy and paste these into Azure Portal custom rules

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

## Manual Configuration Steps:

### Step 1: Add Allow Rule for Israeli IPs
1. Go to Azure Portal: https://portal.azure.com
2. Navigate to: Front Door WAF Policies → petelWafTest
3. Click: Settings → Custom rules
4. Click: + Add custom rule
5. Configure:
   - Name: AllowIsraeliIPs
   - Priority: 100
   - Rule type: Match rule
   - Condition type: IP address
   - Operation: IP match
   - IP addresses: [paste comma-separated list above]
   - Action: Allow
6. Click: Add

### Step 2: Add Geo-Blocking Rule
1. Click: + Add custom rule  
2. Configure:
   - Name: BlockNonIsraeliGeo
   - Priority: 500
   - Rule type: Match rule
   - Condition type: Geo location
   - Match: Negate condition = YES
   - Countries: IL (Israel)
   - Action: Block
3. Click: Add

### Step 3: Save Changes
1. Click: Save at the top
2. Wait for deployment (2-3 minutes)

## Front Door Endpoint

Your test environment Front Door URL:
https://petel-test-egeqaadabmd3fagh.z01.azurefd.net

- Blazor App: https://petel-test-egeqaadabmd3fagh.z01.azurefd.net/
- API: https://petel-test-egeqaadabmd3fagh.z01.azurefd.net/api

## Security Features Active

✅ DDoS Protection (automatic with Front Door)
✅ OWASP Core Rule Set 1.0
✅ Bot Protection
✅ SSL/TLS encryption
✅ Premium tier features

## Next Steps

1. Add Israeli IP restrictions manually (steps above)
2. Test the endpoint from Israel
3. Verify WAF is blocking non-Israeli IPs
4. Update DNS to point to Front Door
5. Configure custom domain (optional)

## Monitoring

- View WAF logs: Azure Portal → Front Door → Logs
- Check blocked requests: Diagnostics → WAF logs
- Monitor performance: Metrics → Front Door metrics
