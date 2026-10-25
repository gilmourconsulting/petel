---
name: ath-entity-api
description: >-
  Add a PetelATH table, EF entity, migration, or new API controller. Use when
  creating a database table, entity class, EF configuration, SQL migration, or
  a new controller under PetelATH. For a new endpoint on an existing controller,
  use ath-api-change instead.
---

# ATH entity and API

Read these before writing files:

1. `docs/agents/playbooks/new-entity-and-api.md`
2. `docs/agents/core/backend-patterns.md`
3. `docs/agents/reference/audit-fields.md`

ATH only. Schema is `petel_schema` through `AppDbContext` and `DatabaseSettings:SchemaName`. Do not touch `PetelAssistants/**`.

## Required shape

- Idempotent SQL under `PetelATH/SQL/`. Creator column is `created_user`, not `user_id`
- Entity lives in `PetelATH.Api/Models/`. `[Table("name")]` has no `Schema`. Every foreign key has a navigation property
- `OnModelCreating` uses `HasDefaultSchema(_schemaName)` once. Entity config calls `ToTable("name")` with no schema argument
- New controller inherits `BaseController`. No `[Authorize]`. Each endpoint calls `GetCurrentSession()` and filters by `session.EntityId`
- Reads use `AsNoTracking()` and project to DTOs. Creates set `CreatedUser`. Updates set `UpdateUser`

## Do not

Hardcode `petel_schema` on an entity. Add `[Authorize]`. Use the Assistants creator column `user_id`.
