# Petel Monorepo — Copilot Instructions

**Canonical docs:** [AGENTS.md](../AGENTS.md) and [docs/agents/INDEX.md](../docs/agents/INDEX.md).

Before any code change, follow [docs/agents/core/pre-processing.md](../docs/agents/core/pre-processing.md).

## Quick rules

- Controllers inherit `BaseController`; no `[Authorize]` — use `GetCurrentSession()` per endpoint
- Entities: `[Table("name")]` without `Schema=`; schema via `HasDefaultSchema()` in DbContext
- Blazor: `SecurePageBase`, `OnPageInitializedAsync()`, `ApiService`, `SessionStateService`
- No hardcoded URLs or schema names
- Audit creator: ATH = `created_user`; Assistants = `user_id` — see [audit-fields.md](../docs/agents/reference/audit-fields.md)

## Documentation map

| Doc | Path | When |
|---|---|---|
| Index | [docs/agents/INDEX.md](../docs/agents/INDEX.md) | Route to scoped docs |
| Architecture | [docs/agents/core/architecture.md](../docs/agents/core/architecture.md) | Shared libs, local dev |
| Configuration | [docs/agents/core/configuration.md](../docs/agents/core/configuration.md) | appsettings, deployment |
| Backend | [docs/agents/core/backend-patterns.md](../docs/agents/core/backend-patterns.md) | EF, entities, migrations |
| Auth | [docs/agents/core/auth-security.md](../docs/agents/core/auth-security.md) | JWT, session, OTP |
| Blazor | [docs/agents/core/blazor-patterns.md](../docs/agents/core/blazor-patterns.md) | All `.razor` files |
| PetelATH | [docs/agents/apps/petel-ath.md](../docs/agents/apps/petel-ath.md) | `PetelATH/**` |
| ATH reports | [docs/agents/apps/petel-ath-reports-excel.md](../docs/agents/apps/petel-ath-reports-excel.md) | Reports/Excel/Word |
| PetelAssistants | [docs/agents/apps/petel-assistants.md](../docs/agents/apps/petel-assistants.md) | `PetelAssistants/**` |
| Assistants domain | [docs/agents/apps/petel-assistants-domain.md](../docs/agents/apps/petel-assistants-domain.md) | Business rules |
| Conventions | [docs/agents/reference/conventions.md](../docs/agents/reference/conventions.md) | Naming, responses |
| New Blazor page | [docs/agents/playbooks/new-blazor-page.md](../docs/agents/playbooks/new-blazor-page.md) | Page scaffold |
| New entity | [docs/agents/playbooks/new-entity-and-api.md](../docs/agents/playbooks/new-entity-and-api.md) | Table + API scaffold |

Path-scoped instruction files in `.github/instructions/` contain critical inline rules and point to the canonical docs above.
