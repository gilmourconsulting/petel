---
applyTo: 'PetelATH/**'
---

# PetelATH

**Read the full guide:** [docs/agents/apps/petel-ath.md](../../docs/agents/apps/petel-ath.md)

Reports/Excel/Word: [docs/agents/apps/petel-ath-reports-excel.md](../../docs/agents/apps/petel-ath-reports-excel.md)

## Critical inline rules

- Schema: `petel_schema` via `DatabaseSettings:SchemaName` — never hardcode in `[Table]` or `ToTable()`
- Audit creator: `created_user` / `CreatedUser`; updater: `update_user` / `UpdateUser`
- Use `GlobalFunctions` for Hebrew normalization and entity lookups in imports
- Council entity lookups: filter `EntityTypeId == 2` when resolving council entities
- Menu items: DB-driven — `@page` route must match `menu_items.reference`

Shared patterns: [docs/agents/core/backend-patterns.md](../../docs/agents/core/backend-patterns.md), [docs/agents/core/blazor-patterns.md](../../docs/agents/core/blazor-patterns.md)
