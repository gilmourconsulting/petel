# Petel Monorepo — Agent Guide

Single entry point for AI agents working in this repository.

## Repository map

| Area | Path | App doc |
|---|---|---|
| PetelATH (schools, students, reports) | `PetelATH/` | [docs/agents/apps/petel-ath.md](docs/agents/apps/petel-ath.md) |
| PetelAssistants (assistants, entitlements) | `PetelAssistants/` | [docs/agents/apps/petel-assistants.md](docs/agents/apps/petel-assistants.md) |
| Shared backend | `shared/Petel.Core/` | [docs/agents/core/architecture.md](docs/agents/core/architecture.md) |
| Shared Blazor | `shared/Petel.BlazorCore/` | [docs/agents/core/architecture.md](docs/agents/core/architecture.md) |

Both apps share platform libraries but implement **separate** domain code (DbContext, entities, controllers, Blazor pages, security actions).

## Loading order (every task)

1. [docs/agents/core/pre-processing.md](docs/agents/core/pre-processing.md) — behavioral rules (think first, minimal diff)
2. [docs/agents/INDEX.md](docs/agents/INDEX.md) — pick scoped docs for the files you are changing
3. Task playbooks under [docs/agents/playbooks/](docs/agents/playbooks/) when scaffolding

## Quick rules

**Backend:** Controllers inherit `BaseController`; no `[Authorize]` — use `GetCurrentSession()` per endpoint. Entities use `[Table("name")]` without `Schema=`. Schema via `HasDefaultSchema()` in DbContext. Navigation properties on all FKs.

**Blazor:** Pages inherit `SecurePageBase`; override `OnPageInitializedAsync()`, not `OnInitializedAsync()`. Use `ApiService`, `SessionStateService`, `NavigationManager` — no raw HttpClient, sessionStorage, or JS navigation.

**Configuration:** No hardcoded URLs or schema names. API URLs in `ApiSettings:BaseUrl`; schema in `DatabaseSettings:SchemaName`.

**Audit fields:** Column names differ by app — see [docs/agents/reference/audit-fields.md](docs/agents/reference/audit-fields.md).

## Task shortcuts

| Task | Read |
|---|---|
| New Blazor page | [playbooks/new-blazor-page.md](docs/agents/playbooks/new-blazor-page.md) + app doc |
| New entity + API | [playbooks/new-entity-and-api.md](docs/agents/playbooks/new-entity-and-api.md) + app doc |
| Blazor UI change | [core/blazor-patterns.md](docs/agents/core/blazor-patterns.md) |
| ATH reports/Excel | [apps/petel-ath-reports-excel.md](docs/agents/apps/petel-ath-reports-excel.md) |
| Assistants business rules | [apps/petel-assistants-domain.md](docs/agents/apps/petel-assistants-domain.md) |

Full routing table: [docs/agents/INDEX.md](docs/agents/INDEX.md)

## Maintenance

When you add or change functionality, update the **one** canonical doc listed in INDEX.md for that change type. Do not duplicate guidance in `.cursor/` or `.github/` — those are thin adapters only.
