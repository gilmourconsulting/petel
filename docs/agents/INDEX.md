# Agent Documentation Index

Canonical location for all agent guidance: `docs/agents/`.  
Adapters: [AGENTS.md](../../AGENTS.md) (root), [.cursor/rules/](../../.cursor/rules/), [.github/copilot-instructions.md](../../.github/copilot-instructions.md).

## Core (shared platform)

| File | Load when |
|---|---|
| [core/pre-processing.md](core/pre-processing.md) | **Always** — before any code change |
| [core/architecture.md](core/architecture.md) | Monorepo layout, shared libs, local dev |
| [core/configuration.md](core/configuration.md) | appsettings, schema config, deployment, CSP |
| [core/backend-patterns.md](core/backend-patterns.md) | EF Core, BaseController, entities, migrations |
| [core/auth-security.md](core/auth-security.md) | JWT, session, password policy, OTP, document proxy |
| [core/blazor-patterns.md](core/blazor-patterns.md) | Any `.razor` file — SecurePageBase, ApiService, modals |
| [../azure-setup.md](../azure-setup.md) | Azure infra for new/other apps — region, naming, edge IP security, secrets, checklist |

## Reference

| File | Load when |
|---|---|
| [reference/conventions.md](reference/conventions.md) | Naming, response envelopes, file layout |
| [reference/audit-fields.md](reference/audit-fields.md) | Creator/updater column names per app |

## Applications

| File | Load when |
|---|---|
| [apps/petel-ath.md](apps/petel-ath.md) | Any change under `PetelATH/` |
| [apps/petel-ath-reports-excel.md](apps/petel-ath-reports-excel.md) | ATH reports, Excel import/export, Word templates |
| [apps/petel-assistants.md](apps/petel-assistants.md) | Any change under `PetelAssistants/` |
| [apps/petel-assistants-domain.md](apps/petel-assistants-domain.md) | Assistants business logic (years, entitlements, persons, yearly budget calculate, year elements rates) |
| [../../PetelAssistants/docs/year-management-screens.md](../../PetelAssistants/docs/year-management-screens.md) | Operational year hub + shared Year Elements hub navigation and screens |

## Playbooks

| File | Load when |
|---|---|
| [playbooks/new-blazor-page.md](playbooks/new-blazor-page.md) | Scaffold a full new page (razor + controller + DTOs + SQL) |
| [playbooks/new-entity-and-api.md](playbooks/new-entity-and-api.md) | New table, entity, migration, controller |

## Archive

| File | Notes |
|---|---|
| [archive/monorepo-restructure.md](archive/monorepo-restructure.md) | Historical — restructure complete |

---

## Decision table

| You are changing… | Read (in order) |
|---|---|
| Any code | pre-processing → app doc (if applicable) |
| `shared/Petel.Core/**` or `shared/Petel.BlazorCore/**` | architecture → backend-patterns or blazor-patterns |
| `PetelATH/**` API | petel-ath → backend-patterns → auth-security |
| `PetelATH/**` Blazor | blazor-patterns → petel-ath |
| `PetelATH/**` reports/Excel | petel-ath-reports-excel |
| `PetelAssistants/**` API | petel-assistants → backend-patterns → audit-fields |
| `PetelAssistants/**` Blazor | blazor-patterns → petel-assistants → petel-assistants-domain |
| Year Elements rates / budget calculate | petel-assistants-domain → year-management-screens |
| New Blazor page | new-blazor-page + app doc |
| New table/entity | new-entity-and-api + app doc |
| Azure resources / deploying a new app | [azure-setup.md](../azure-setup.md) + app doc |

## Maintenance mapping

| Change type | Update |
|---|---|
| Shared pattern | `core/*.md` |
| ATH domain | `apps/petel-ath.md` (+ reports doc if report-related) |
| Assistants tenancy/security | `apps/petel-assistants.md` |
| Assistants business rules | `apps/petel-assistants-domain.md` |
| Assistants personal entitlement upload (PDF/Excel) | `apps/petel-assistants-domain.md` § Personal entitlements file upload (+ `apps/petel-assistants.md` API/UI) |
| Blazor UI pattern | `core/blazor-patterns.md` |
| Azure infra / new app on Azure | `docs/azure-setup.md` (+ app doc resource table) |
| New repeatable workflow | New file under `playbooks/` |
