# Playbook — New Entity and API

Scaffold a new database table, EF entity, and API controller. Branch by application.

## Step 1 — Classify the data

| Question | PetelATH | PetelAssistants |
|---|---|---|
| Schema | `petel_schema` via `AppDbContext` | `assist_schema` via `AssistDbContext` |
| Tenant scope | Filter by `session.EntityId` in queries | `entity_id` column + `HasQueryFilter` |
| Global reference? | Rare — usually in same schema | Use `SharedDbContext` / `shared_schema` — no `entity_id` |
| Creator audit column | `created_user` / `CreatedUser` | `user_id` / `UserId` |

Read [reference/audit-fields.md](../reference/audit-fields.md) before writing SQL or entities.

## Step 2 — SQL migration (idempotent)

```sql
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT FROM pg_tables
        WHERE schemaname = 'YOUR_SCHEMA'
        AND tablename = 'my_entities'
    ) THEN
        CREATE TABLE YOUR_SCHEMA.my_entities (
            id SERIAL PRIMARY KEY,
            -- Assistants only: entity_id as second column
            name VARCHAR(100) NOT NULL,
            is_active BOOLEAN NOT NULL DEFAULT true,
            created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
            -- ATH: created_user | Assistants: user_id
            updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
            update_user INTEGER NULL
        );
        RAISE NOTICE 'Table my_entities created';
    END IF;
END $$;
```

Place scripts under `PetelATH/SQL/` or `PetelAssistants/SQL/`.

## Step 3 — EF entity

**PetelATH** — `PetelATH.Api/Models/`:

```csharp
[Table("my_entities")]
public class MyEntity
{
    [Key][Column("id")] public int Id { get; set; }
    [Required][Column("name")] public string Name { get; set; } = string.Empty;
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [Column("created_user")] public int? CreatedUser { get; set; }
    [Column("updated_at")] public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    [Column("update_user")] public int? UpdateUser { get; set; }
}
```

**PetelAssistants** — `PetelAssistants.Api/Models/` — implement `IEntityScoped`:

```csharp
[Table("my_entities")]
public class MyEntity : IEntityScoped
{
    [Key][Column("id")] public int Id { get; set; }
    [Required][Column("entity_id")] public int EntityId { get; set; }
    [Required][Column("name")] public string Name { get; set; } = string.Empty;
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [Column("user_id")] public int? UserId { get; set; }
    [Column("updated_at")] public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    [Column("update_user")] public int? UpdateUser { get; set; }
}
```

Register in DbContext with navigation properties for any FKs. See [core/backend-patterns.md](../core/backend-patterns.md).

**Assistants query filter** (required in `AssistDbContext.OnModelCreating`):

```csharp
entity.HasQueryFilter(e => _tenantContext.EntityId != 0 && e.EntityId == _tenantContext.EntityId);
```

## Step 4 — DbContext + migration

```bash
# ATH
cd PetelATH/PetelATH.Api
dotnet ef migrations add AddMyEntity
dotnet ef database update

# Assistants
cd PetelAssistants/PetelAssistants.Api
dotnet ef migrations add AddMyEntity --context AssistDbContext
dotnet ef database update --context AssistDbContext
```

## Step 5 — API controller

Both apps: inherit `BaseController`, no `[Authorize]`, `GetCurrentSession()` first.

**ATH** — scope manually:

```csharp
var entityId = int.Parse(session.EntityId);
var items = await _context.MyEntities
    .AsNoTracking()
    .Where(e => e.EntityId == entityId)
    .ToListAsync();
```

**Assistants** — global filter handles isolation; do not trust client `entity_id`:

```csharp
var items = await _context.MyEntities.AsNoTracking().ToListAsync();
// On create:
EntityId = int.Parse(session.EntityId),
UserId = int.TryParse(session.UserId, out int uid) ? uid : null,
```

## Step 6 — Verify

- [ ] `[Table]` has no `Schema=` parameter
- [ ] Audit fields match app convention ([audit-fields.md](../reference/audit-fields.md))
- [ ] Navigation properties on all FKs
- [ ] Controller inherits `BaseController`, session checked per action
- [ ] Assistants: `IEntityScoped` + `HasQueryFilter` (if tenant-scoped)
- [ ] No hardcoded schema names or API URLs

## Related

- Full page scaffold: [new-blazor-page.md](new-blazor-page.md)
- App guides: [petel-ath.md](../apps/petel-ath.md), [petel-assistants.md](../apps/petel-assistants.md)
