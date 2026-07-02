# Audit Fields — Per-Application Naming

All operational tables include audit timestamps and user tracking. **Creator column naming differs between apps** — use the convention for the app you are editing.

## Summary

| Field | PetelATH (`petel_schema`) | PetelAssistants (`assist_schema`) |
|---|---|---|
| Created timestamp | `created_at` / `CreatedAt` | `created_at` / `CreatedAt` |
| Creator user FK | `created_user` / `CreatedUser` | `user_id` / `UserId` |
| Updated timestamp | `updated_at` / `UpdatedAt` | `updated_at` / `UpdatedAt` |
| Updater user FK | `update_user` / `UpdateUser` | `update_user` / `UpdateUser` |

Both apps use `update_user` for the last modifier. Only the **creator** column name differs.

## PetelATH

```csharp
[Column("created_at")]
public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

[Column("created_user")]
public int? CreatedUser { get; set; }

[Column("updated_at")]
public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

[Column("update_user")]
public int? UpdateUser { get; set; }
```

```sql
created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
created_user INTEGER NULL REFERENCES petel_schema.users(id) ON DELETE SET NULL,
updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
update_user INTEGER NULL REFERENCES petel_schema.users(id) ON DELETE SET NULL
```

Controller pattern:

```csharp
int? userId = int.TryParse(session.UserId, out int uid) ? uid : null;
entity.CreatedUser = userId;
entity.UpdateUser = userId;
```

## PetelAssistants

```csharp
[Column("created_at")]
public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

[Column("user_id")]
public int? UserId { get; set; }  // creator FK → users.id

[Column("updated_at")]
public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

[Column("update_user")]
public int? UpdateUser { get; set; }
```

```sql
created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
user_id INTEGER NULL REFERENCES assist_schema.users(id) ON DELETE SET NULL,
updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
update_user INTEGER NULL REFERENCES assist_schema.users(id) ON DELETE SET NULL
```

On create, set `UserId` and `UpdateUser` from `session.UserId`. On update, set `UpdateUser` only.

**Note:** On the `users` table itself, `user_id` is the creator of that user record — not the user's primary key (`id`).

## Assistants tenant tables

Every `assist_schema` table also requires `entity_id` (second column after `id`). See [apps/petel-assistants.md](../apps/petel-assistants.md).

## Anti-patterns

```csharp
// ❌ WRONG — using ATH names in Assistants
[Column("created_user")]
public int? CreatedUser { get; set; }

// ❌ WRONG — using Assistants names in ATH
[Column("user_id")]
public int? UserId { get; set; }  // on a non-users entity in PetelATH

// ✅ CORRECT — match the app you are in
```
