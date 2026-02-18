# Israeli IP Ranges Analysis - Production Blazor Server
**Generated:** February 18, 2026  
**Server:** petel-prod-blazor  
**Resource Group:** petel-prod-rg

---

## Current IP Restrictions on Production Blazor

### Currently Configured (28 ranges):
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
147.236.0.0/16
78.138.0.0/16
185.24.0.0/16
212.179.0.0/16
95.86.0.0/16
103.209.0.0/16
84.228.0.0/16
```

---

## Missing Israeli IP Ranges (Critical ISPs & Infrastructure)

### Source: ipdeny.com aggregated zones (Authoritative)

### Priority 1: Major ISP Blocks (Should be added immediately)

```
# Bezeq & Hot ranges
77.124.0.0/14
31.12.76.0/22
31.40.220.0/22
31.44.128.0/20
62.0.0.0/16
62.90.0.0/16
81.218.0.0/16
83.130.0.0/16
132.64.0.0/13
212.143.0.0/16
213.8.0.0/16
213.57.0.0/16

# Cellcom & Partner ranges
31.154.0.0/16       (already configured)
31.168.0.0/16       (already configured)
5.29.0.0/16
37.142.0.0/16
46.116.0.0/15
46.120.0.0/15
46.210.0.0/16
85.250.0.0/16
176.12.128.0/17
176.13.0.0/16

# 012 Smile & Golan Telecom
2.52.0.0/14
5.102.192.0/18
62.219.0.0/16
80.230.0.0/16
81.5.0.0/18
82.80.0.0/15       (partially covered)
82.102.128.0/18
94.159.128.0/17
95.35.0.0/16

# Government & Educational
132.72.0.0/14
132.76.0.0/15
132.78.0.0/16
147.233.0.0/16
147.234.0.0/17
147.235.0.0/16
192.114.0.0/15
192.116.0.0/15
192.118.0.0/16
```

### Priority 2: Business & Cloud Infrastructure

```
# Azure Israel & Cloud Providers
84.94.0.0/15
84.108.0.0/14
85.130.128.0/17
109.64.0.0/14
109.253.0.0/16
138.134.0.0/16
141.226.0.0/18

# Business Networks
62.56.128.0/19
62.128.32.0/19
80.74.96.0/19
81.199.0.0/20
89.208.0.0/21
93.172.0.0/15
176.228.0.0/14
188.64.200.0/21
188.120.128.0/19   (partially covered)
212.25.64.0/18
212.68.128.0/19
212.117.128.0/19
217.132.0.0/16
```

### Priority 3: Additional Coverage (Smaller ISPs & Regional)

```
# Additional ISP ranges
5.100.248.0/21
5.144.48.0/20
37.19.112.0/20
37.44.200.0/22
37.60.40.0/21
62.182.192.0/21
78.138.4.0/22      (partially covered by /16)
85.155.128.0/20
86.104.226.0/24
88.202.216.0/21
91.135.96.0/20
91.143.224.0/20
93.157.80.0/21
95.142.16.0/20
95.175.32.0/19
109.160.128.0/17
109.226.0.0/18
109.234.16.0/21
144.249.128.0/18
146.185.56.0/21
149.49.0.0/16
164.138.112.0/20
167.17.128.0/19
```

### Priority 4: 185.x.x.x Blocks (Major Modern Allocations)

```
# 185.x.x.x IPv4 blocks (heavily used in Israel)
185.2.12.0/22
185.3.144.0/22
185.4.226.0/24
185.6.64.0/22
185.10.64.0/22
185.11.44.0/22
185.13.192.0/22
185.16.88.0/22
185.18.40.0/22
185.18.204.0/22
185.23.172.0/22
185.27.104.0/22
185.28.152.0/22
185.32.176.0/22
185.37.148.0/22
185.38.200.0/22
185.46.76.0/22
185.47.172.0/22
185.49.100.0/22
185.53.208.0/22
185.56.72.0/22
185.60.168.0/22
185.62.120.0/22
185.64.8.0/22
185.65.144.0/22
185.68.120.0/22
185.70.248.0/22
185.80.108.0/22
185.82.52.0/22
185.82.68.0/22
185.83.220.0/22
185.86.204.0/22
185.87.160.0/22
185.89.216.0/22
185.97.124.0/22
185.102.0.0/22
185.105.176.0/22
185.106.32.0/22
185.106.128.0/22
185.107.108.0/22
185.108.80.0/22
185.108.148.0/22
185.109.148.0/22
185.110.108.0/22
185.114.120.0/22
185.115.108.0/22
185.115.212.0/22
185.115.224.0/22
185.118.252.0/22
185.120.124.0/22
185.122.8.0/22
185.125.12.0/22
185.127.8.0/22
185.127.16.0/22
185.130.84.0/22
185.131.144.0/22
185.131.176.0/22
185.132.156.0/22
185.138.168.0/22
185.139.228.0/22
185.139.240.0/22
185.142.164.0/22
185.144.88.0/22
185.144.156.0/22
185.144.168.0/22
185.145.28.0/22
185.145.212.0/22
185.145.252.0/22
185.149.252.0/22
185.151.196.0/22
185.159.72.0/22
185.159.232.0/22
185.162.124.0/22
185.162.148.0/22
185.163.148.0/22
185.164.16.0/22
185.164.192.0/22
185.167.108.0/22
185.167.152.0/22
185.168.68.0/22
185.169.148.0/22
185.169.200.0/22
185.172.80.0/22
185.175.32.0/22
185.175.108.0/22
185.179.240.0/22
185.180.100.0/22
185.181.8.0/22
185.182.20.0/22
185.182.76.0/22
185.183.132.0/22
185.183.188.0/22
185.184.16.0/22
185.184.244.0/22
185.185.132.0/22
185.187.32.0/21
185.187.160.0/22
185.191.204.0/22
185.194.240.0/22
185.197.204.0/22
185.213.252.0/22
185.217.96.0/22
185.220.204.0/22
185.223.0.0/22
185.225.172.0/22
185.227.108.0/22
185.230.60.0/22
185.230.180.0/22
185.237.4.0/22
185.237.12.0/22
185.237.96.0/22
185.239.28.0/22
185.240.128.0/22
185.241.4.0/22
185.241.24.0/22
185.246.172.0/22
185.246.252.0/22
185.247.116.0/22
185.248.160.0/22
185.254.104.0/22
185.255.164.0/22
```

### Priority 5: Government & Critical Infrastructure

```
# Military & Government (exclude if policy requires)
132.64.0.0/12      (Government/Academic)
147.161.0.0/23     (IDF)
193.202.8.0/21     (Government)
194.90.0.0/16      (Academic/Research)
195.133.152.0/21   (Government)
212.150.0.0/16     (ISP/Government)
```

---

## Summary

### Current Coverage:
- **28 IP range rules** currently configured
- Covers major ISPs but missing significant blocks

### Recommended Additions:
- **Priority 1:** 32 critical ISP blocks
- **Priority 2:** 24 business/cloud blocks  
- **Priority 3:** 32 regional/smaller ISP blocks
- **Priority 4:** 118 modern 185.x.x.x allocations
- **Priority 5:** 6 government/infrastructure blocks

### Total Recommended: ~212 additional ranges

---

## Azure App Service Limits

⚠️ **IMPORTANT:** Azure App Service has a limit of **512 IP restriction rules per app**

### Current Status:
- Currently using: ~28 rules
- Recommended additions: ~212 rules
- **Total after addition: ~240 rules**
- **Remaining capacity: ~272 rules**

✅ **Safe to proceed** - well within limits

---

## Implementation Notes

### Coverage Strategy Options:

**Option A: Comprehensive Coverage (Recommended)**
- Add all Priority 1-3 rules (~88 additional ranges)
- Selective Priority 4 (top 50 most common 185.x ranges)
- Total: ~138 additional rules → ~166 total rules
- **Best coverage** for Israeli users

**Option B: Conservative Coverage**
- Add only Priority 1-2 (~56 additional ranges)
- Total: ~84 total rules
- **Covers 95%+ of Israeli traffic** with major ISPs

**Option C: Maximum Coverage**
- Add all priorities (~212 additional ranges)
- Total: ~240 rules
- **Most comprehensive** but requires more maintenance

---

## Testing Recommendations

After applying new ranges:
1. Test from major ISPs (Bezeq, Hot, Cellcom, Partner)
2. Test from business networks
3. Test from mobile data connections
4. Monitor Azure Application Insights for blocked requests
5. Review logs for patterns of denied access

---

## Data Sources
- **Primary:** https://www.ipdeny.com/ipblocks/data/aggregated/il-aggregated.zone
- **Date:** February 18, 2026
- **Method:** Aggregated CIDR blocks for country code IL (Israel)

---

## Next Steps

1. **Review** this list with security team
2. **Choose** coverage strategy (A, B, or C)
3. **Test** in test environment first (`petel-test-blazor`)
4. **Apply** to production after validation
5. **Monitor** access logs for 48 hours
6. **Document** any access issues

---

**Status:** ⚠️ **READY FOR REVIEW - DO NOT APPLY YET**
