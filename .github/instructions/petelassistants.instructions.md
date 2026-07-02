---
applyTo: 'PetelAssistants/**'
---

# PetelAssistants

**Read the full guide:** [docs/agents/apps/petel-assistants.md](../../docs/agents/apps/petel-assistants.md)

Domain rules: [docs/agents/apps/petel-assistants-domain.md](../../docs/agents/apps/petel-assistants-domain.md)

## Critical inline rules

- Two schemas: `shared_schema` (SharedDbContext, no `entity_id`) and `assist_schema` (AssistDbContext, mandatory `entity_id` + `HasQueryFilter`)
- Never accept `entity_id` from client — always from `session.EntityId`
- Audit creator: `user_id` / `UserId`; updater: `update_user` / `UpdateUser`
- `IgnoreQueryFilters()` only on login path
- Required endpoints: all five `SecurityController` + `GET session/timeout-config` (missing → 10 min client default)

Shared patterns: [docs/agents/core/backend-patterns.md](../../docs/agents/core/backend-patterns.md), [docs/agents/reference/audit-fields.md](../../docs/agents/reference/audit-fields.md)
