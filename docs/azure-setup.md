# Petel Azure Setup — Shared Reference

> Audience: teams building **other apps** that should follow the same Azure patterns as PetelATH and PetelAssistants.  
> Canonical agent docs still live under `docs/agents/`; this file is the cross-app infrastructure guide.

## Goals

- Keep every Petel app in the same region, environment model, and naming scheme.
- Separate **public UI** (Blazor) from **private API** (server-to-server only in production).
- Prefer configuration and App Service settings over hardcoded URLs, secrets, or schema names.
- Reuse the same deploy shape: Linux App Services on .NET 9, zip deploy via Azure CLI.

## High-level architecture

Each application is deployed as **two App Services** behind one App Service Plan (per environment):

```
Browser
  → [Production only] Azure Front Door Premium + WAF (Israel GeoMatch)
  → Blazor App Service  (public entry for users)
  → API App Service     (Blazor calls API server-side; not exposed on Front Door)
  → Azure Database for PostgreSQL Flexible Server
```

**Rules that other apps should keep:**

| Rule | Why |
|---|---|
| Region = `israelcentral` | Data residency / latency for IL users |
| Blazor is the only public origin on Front Door | Users never hit the API hostname directly |
| API allowlist = Blazor outbound IPs | API is invisible from the internet |
| No `/api/*` route on Front Door | Blazor Server calls API over Azure outbound networking |
| `ASPNETCORE_ENVIRONMENT` set on each App Service | Loads the correct `appsettings.*.json` |

Azure Portal / ARM management always bypasses App Service access restrictions — no special rule needed for ops.

## Environments

| Logical env | Typical use | `ASPNETCORE_ENVIRONMENT` set by deploy scripts |
|---|---|---|
| **test** | Shared integration / QA | `Staging` |
| **staging** | Pre-prod validation | `Staging` |
| **production** | Live | `Production` |

Local development uses `Development` and is not Azure-hosted.

## Naming convention

Use a short app slug, then environment, then resource role:

```
petel[-{app}]-{env}-{role}
```

Examples:

| Piece | ATH (default / legacy) | Assistants | New app example |
|---|---|---|---|
| Slug | *(omitted)* | `assist` | `payroll` |
| Resource group | `petel-test-rg` | `petel-assist-test-rg` | `petel-payroll-test-rg` |
| App Service Plan | `petel-test-plan` | `petel-assist-test-plan` | `petel-payroll-test-plan` |
| API app | `petel-test-api` | `petel-assist-test-api` | `petel-payroll-test-api` |
| Blazor app | `petel-test-blazor` | `petel-assist-test-blazor` | `petel-payroll-test-blazor` |
| PostgreSQL | `petel-*-db[-NNNN]` | shared or dedicated | `petel-payroll-*-db[-NNNN]` |
| Key Vault | `petel-kv-*-NNNN` | optional | `petel-kv-payroll-*-NNNN` |
| Front Door (prod) | `petel-frontdoor-prod` | *(add when needed)* | `petel-payroll-frontdoor-prod` |

Hostnames follow Azure defaults: `{app-name}.azurewebsites.net`.

**Tags** used on production infrastructure (recommended for new apps):

- `Environment` — Production / Staging / Test  
- `Application` — product name  
- `ManagedBy` — Infrastructure  
- `CostCenter` — as required by finance  

## Current app inventory

### PetelATH

| Environment | Resource Group | Plan | API | Blazor |
|---|---|---|---|---|
| Test | `petel-test-rg` | `petel-test-plan` | `petel-test-api` | `petel-test-blazor` |
| Staging | `petel-staging-rg` | `petel-staging-plan` | `petel-staging-api` | `petel-staging-blazor` |
| Production | `petel-prod-rg` | `petel-prod-plan` | `petel-prod-api` | `petel-prod-blazor` |

- Runtime: `DOTNETCORE:9.0` (Linux) for API and Blazor  
- Production plan SKU (infra script): `P1V3`  
- Deploy: `.\Deploy-ATH.ps1 -Environment {test|staging|production}`  
- Production Front Door: `.\Deploy-ATH-FrontDoor-Prod.ps1`

### PetelAssistants

Dedicated resource groups (not shared with ATH):

| Environment | Resource Group | Plan | API | Blazor |
|---|---|---|---|---|
| Test | `petel-assist-test-rg` | `petel-assist-test-plan` | `petel-assist-test-api` | `petel-assist-test-blazor` |
| Staging | `petel-assist-staging-rg` | `petel-assist-staging-plan` | `petel-assist-staging-api` | `petel-assist-staging-blazor` |
| Production | `petel-assist-prod-rg` | `petel-assist-prod-plan` | `petel-assist-prod-api` | `petel-assist-prod-blazor` |

- Runtime: `DOTNETCORE:9.0` (Linux)  
- Deploy: `.\Deploy-Assistants.ps1 -Environment {test|staging|production}`  
- Test URLs:  
  - API `https://petel-assist-test-api.azurewebsites.net`  
  - Blazor `https://petel-assist-test-blazor.azurewebsites.net`

### PetelMeitar

Single Blazor Server Web app + Playwright Worker (not dual API/Blazor). Scripts live in the **PetelMeitar** repo (`c:\dev\PetelMeitar`).

| Environment | Resource Group | Plan | Web | Worker |
|---|---|---|---|---|
| Production | `petel-meitar-prod-rg` | `petel-meitar-prod-plan` (B2) | `petel-meitar-prod-web` | `petel-meitar-prod-worker` (Container App) |

- Web: `DOTNETCORE:9.0` Linux App Service in `israelcentral`  
- Worker: Container Apps env `petel-meitar-prod-env` in **`westeurope`** (ACA not available in israelcentral); image in ACR `petelmeitaracr`  
- DB: shared `petel-prod-db-4407` with schemas `meitar_raw` / `meitar_data`  
- Infra: `.\Setup-Meitar-Production-Infrastructure.ps1`  
- Deploy: `.\Deploy-Meitar.ps1 -Environment production`  
- Web URL: `https://petel-meitar-prod-web.azurewebsites.net`  
- Hangfire: Web is client-only (`Hangfire__RunServer=false`); Worker runs the Hangfire server + Chromium scrapes  

### Database

- Engine: **Azure Database for PostgreSQL — Flexible Server**  
- Apps use schema-per-app (or schema-per-tenant) via config (`Database:SchemaName`), not hardcoded schema in entities.  
- Example hosts used historically:  
  - Test: `petel-test-db.postgres.database.azure.com`  
  - Prod: `petel-prod-db-4407.postgres.database.azure.com`  
- Username format for Azure: `username@servername` when required by the client.

Apps may share a PostgreSQL server with separate databases/schemas, or get a dedicated server — prefer **dedicated resource groups per app**, even if the DB server is shared.

## Production edge security (Front Door pattern)

Canonical production traffic for ATH:

```
Browser (Israeli IPs only)
  → Front Door Premium (`petel-frontdoor-prod`)
  → WAF (`petelWafProd`) — GeoMatch ≠ IL → 403; OWASP + Bot rules
  → Blazor (`petel-prod-blazor`)
  → API (`petel-prod-api`) via Blazor outbound IPs only
```

| Resource | Example name | Notes |
|---|---|---|
| Front Door profile | `petel-frontdoor-prod` | Premium SKU, lives in prod RG |
| Endpoint | `petel-prod` | `*.azurefd.net` hostname |
| WAF policy | `petelWafProd` | Prevention mode |
| Managed rules | DRS 2.1 + Bot Manager 1.0 | OWASP + bots |
| Custom rule | `BlockNonIsrael` | GeoMatch country ≠ IL |
| Blazor access | Allow `AzureFrontDoor.Backend`, then DenyAll | Blocks direct `.azurewebsites.net` |
| API access | Allow Blazor `outboundIpAddresses`, then DenyAll | No Front Door route to API |

**Prefer GeoMatch over long Israeli CIDR lists** — one WAF rule, Microsoft-maintained country mapping, no 512-rule App Service limit issues.

Blazor outbound IPs are tied to the App Service Plan. They change mainly on plan migration or region move. After such a change, refresh API access restrictions (ATH: re-run `Deploy-ATH-FrontDoor-Prod.ps1`, optionally `-DryRun` first).

> Historical note: some older docs describe App Service Israeli CIDR allowlists *instead of* Front Door. The current production pattern for ATH is Front Door + GeoMatch + private API. New apps should follow that pattern unless there is an explicit cost/ops decision otherwise.

## Secrets and configuration

### Application settings (required on every API App Service)

Use double-underscore keys so they override `appsettings`:

| Setting | Purpose |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Staging` or `Production` |
| `ConnectionStrings__DefaultConnection` | PostgreSQL connection string |
| `Security__Jwt__SecretKey` | JWT signing key (≥ 32 chars) |
| `Security__DataEncryption__EncryptionKey` | AES-256 key (base64) |
| `Email__FromAddress` / `Email__Username` / `Email__Password` | SMTP (OTP mail) |

**Assistants today:** secrets are set directly as App Service application settings (no Key Vault required).  
**ATH / production infra scripts:** also support Azure Key Vault + Key Vault references (`@Microsoft.KeyVault(...)`).

Either approach is fine for a new app; Key Vault is preferred when multiple apps or operators need shared secret rotation.

### App config files (in repo, no secrets)

| File | Role |
|---|---|
| `appsettings.json` | Non-secret defaults |
| `appsettings.Development.json` | Local |
| `appsettings.test.json` / Staging / Production | Environment URLs and feature flags |

Blazor must set `ApiSettings:BaseUrl` per environment (never hardcode). CSP `connect-src` is derived from that URL in `Program.cs`.

## Deployment model

1. `az login` (Azure CLI authenticated).  
2. Build/publish Release.  
3. Zip package (`tar.exe` / zip).  
4. `az webapp deploy` to API and/or Blazor.  
5. Set `ASPNETCORE_ENVIRONMENT` and Linux runtime (`DOTNETCORE|9.0`).  
6. Restart / health-check.  
7. Optionally refresh Blazor→API IP allowlists (`-SkipIpRestrictions` to skip).

Per-app scripts at repo root:

| App | Script |
|---|---|
| ATH | `Deploy-ATH.ps1` |
| Assistants | `Deploy-Assistants.ps1` |
| ATH Front Door (prod) | `Deploy-ATH-FrontDoor-Prod.ps1` |
| One-time prod infra (ATH-oriented) | `Setup-Production-Infrastructure.ps1` |

Flags commonly supported: `-ApiOnly`, `-BlazorOnly`, `-SkipBuild`, `-SkipIpRestrictions`, `-DryRun` / `-WafOnly` on Front Door scripts.

## Checklist: add a new app on the same Azure pattern

1. **Choose slug** — e.g. `payroll` → `petel-payroll-{env}-*`.  
2. **Create three resource groups** — test / staging / production in `israelcentral`.  
3. **Per environment create** — App Service Plan (Linux), API web app, Blazor web app, both `DOTNETCORE:9.0`.  
4. **Database** — Flexible Server (shared or dedicated) + schema/database for the app.  
5. **Wire secrets** — App Settings and/or Key Vault; never commit connection strings or JWT keys.  
6. **Config** — `ApiSettings:BaseUrl` pointing at the API hostname; `Database:SchemaName` from config.  
7. **Deploy script** — clone `Deploy-Assistants.ps1` / `Deploy-ATH.ps1`, update `$envConfig` paths and names.  
8. **Production hardening** — Front Door Premium → Blazor only; WAF GeoMatch IL; API locked to Blazor outbound IPs.  
9. **Validate** — login + Blazor→API round-trip; confirm direct API URL is blocked from the internet; confirm non-IL blocked at WAF.

## What other apps should *not* copy

- Hardcoded API URLs or schema names in C# / Razor.  
- Exposing the API on Front Door or leaving `.azurewebsites.net` open in production.  
- Relying on long Israeli CIDR allowlists as the primary geo control when Front Door GeoMatch is available.  
- Sharing one App Service between unrelated apps (use separate RGs / plans / apps).  
- Putting secrets only in `appsettings.*.json` committed to git.

## Related docs in this repo

| Doc | Use when |
|---|---|
| [docs/agents/core/configuration.md](agents/core/configuration.md) | appsettings, schema, CSP, deploy commands |
| [docs/agents/apps/petel-ath.md](agents/apps/petel-ath.md) | ATH resources + Front Door details |
| [docs/agents/apps/petel-assistants.md](agents/apps/petel-assistants.md) | Assistants resources + App Settings secrets |
| [docs/agents/core/auth-security.md](agents/core/auth-security.md) | JWT, OTP, IP restriction implications for Blazor→API |
| `Setup-Production-Infrastructure.ps1` | Example one-shot prod resource creation |
| `Deploy-ATH-FrontDoor-Prod.ps1` | Canonical Front Door + private API script |

## Maintenance

When you provision a new Petel app or change shared Azure conventions (region, naming, Front Door rules, secret strategy), update **this file** and the app-specific section in `docs/agents/apps/`. Do not duplicate long resource tables in multiple places without a pointer here.
