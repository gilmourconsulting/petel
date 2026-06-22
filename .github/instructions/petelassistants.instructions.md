---
applyTo: 'PetelAssistants/**'
---

# PetelAssistants - Application-Specific Guide

**PetelAssistants** is a new application in the Petel monorepo. It is currently being built out. Both the API and Blazor frontend share the `Petel.Core` and `Petel.BlazorCore` libraries with PetelATH.

## Project Structure

```
PetelAssistants/
  PetelAssistants.Api/
    Controllers/                    ← API controllers (inherit BaseController)
    Data/
      AppDbContext.cs               ← AssistDbContext — assist_schema (tenant-scoped)
      SharedDbContext.cs            ← SharedDbContext — shared_schema (global reference)
    Models/                         ← Entity models
    Services/SystemAttributeCache.cs ← Implements IAttributeCache (Petel.Core)
    Tenancy/
      IEntityScoped.cs              ← Interface for all assist_schema entities
      ITenantContext.cs             ← Tenant EntityId resolver
      HttpTenantContext.cs          ← HTTP-request-scoped implementation
    SQL/bootstrap.sql               ← One-time schema setup script
    Program.cs
    appsettings.json
    appsettings.Development.json
  PetelAssistants.BlazorServer/     ← Frontend (Blazor Web App, net9.0, Server interactivity)
    Program.cs
    appsettings.json
    appsettings.Development.json
```

## Local Development

```bash
# PetelAssistants API
cd PetelAssistants/PetelAssistants.Api && dotnet run

# PetelAssistants Blazor
cd PetelAssistants/PetelAssistants.BlazorServer && dotnet run
```

## Database — Dual-Schema Multi-Tenancy

PetelAssistants uses **two fixed PostgreSQL schemas** regardless of tenant count. Adding a new local authority is a single `INSERT` into `shared_schema.entities` — no schema or infrastructure changes.

| Schema | DbContext | Purpose |
|---|---|---|
| `shared_schema` | `SharedDbContext` | Global reference data — entities, entity types, system attributes. No `entity_id`. |
| `assist_schema` | `AssistDbContext` | All operational tenant-scoped data. Every table has mandatory `entity_id`. |

### Configuration

```json
// appsettings.Development.json (PetelAssistants.Api)
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=petelappdb;Username=PetelAdmin;Password=..."
  },
  "Database": {
    "SchemaName": "assist_schema"
  },
  "SharedDatabase": {
    "SchemaName": "shared_schema"
  }
}
```

Never hardcode schema names in `[Table]` attributes or `ToTable()` calls. Both contexts read their schema from the respective `DatabaseSettings` / `SharedDatabaseSettings` configuration classes.

### Database Bootstrap

Run `PetelAssistants/SQL/bootstrap.sql` once per environment to create both schemas and seed reference data.

## Multi-Tenancy Rules

### Tenant-scoped entities (assist_schema)

Every entity in `assist_schema` MUST:
1. Implement `IEntityScoped` from `PetelAssistants.Api.Tenancy`
2. Have `[Column("entity_id")] public int EntityId { get; set; }`
3. Be registered in `AssistDbContext.OnModelCreating` with a `HasQueryFilter`

```csharp
// ✅ CORRECT — new tenant-scoped entity
[Table("my_entities")]
public class MyEntity : IEntityScoped
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("entity_id")]
    public int EntityId { get; set; }

    [Required]
    [Column("name")]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    // Audit fields (required)
    [Column("created_at")]  public DateTime CreatedAt   { get; set; } = DateTime.UtcNow;
    [Column("created_user")] public int? CreatedUser   { get; set; }
    [Column("updated_at")]  public DateTime UpdatedAt   { get; set; } = DateTime.UtcNow;
    [Column("update_user")] public int? UpdateUser     { get; set; }
}
```

In `AssistDbContext.OnModelCreating`:
```csharp
modelBuilder.Entity<MyEntity>(entity =>
{
    entity.ToTable("my_entities");
    entity.HasQueryFilter(e => _tenantContext.EntityId != 0 && e.EntityId == _tenantContext.EntityId);
});
```

### Global query filter — login endpoint exception

During login, `ITenantContext.EntityId` is `0` (no session yet). The global filter would block all user lookups. Use `IgnoreQueryFilters()` in the login endpoint only:

```csharp
// ✅ CORRECT — login must bypass the filter, but scopes explicitly with the provided EntityId
var user = await _context.Users
    .IgnoreQueryFilters()
    .FirstOrDefaultAsync(u => u.Username == request.Username
                            && u.EntityId == request.EntityId
                            && u.IsActive);
```

### Shared reference data (shared_schema)

Global lookup tables (entities, entity types, system attributes) are in `SharedDbContext`. They have no `entity_id` and no global query filter.

### Cross-entity persons

An assistant who works for two authorities appears as two independent rows in `assist_schema.persons`, each with a different `entity_id`. There is no FK or unique constraint linking them across authorities. National ID (`id_number`) must be AES-encrypted at rest (use `DataEncryptionService` from `Petel.Core`). Any deduplication logic is application-layer only — never in SQL.

## Shared Libraries

### Blazor Frontend

PetelAssistants.BlazorServer uses **pure Blazor Server** with `@rendermode InteractiveServer`. There are no HTML files, no JavaScript SPA, and no `page-lifecycle-config.js`. All UI is in `.razor` components.

**See [blazor-patterns.instructions.md](blazor-patterns.instructions.md)** for the canonical Blazor page template, SecurePageBase usage, ApiService call patterns, modal pattern, table pattern, icon usage, and anti-patterns. Those patterns apply to both PetelATH and PetelAssistants.

Both projects reference the shared libraries:

- `PetelAssistants.Api` → `shared/Petel.Core`
- `PetelAssistants.BlazorServer` → `shared/Petel.BlazorCore`

### Using Petel.Core in PetelAssistants.Api

Controllers that work with tenant-scoped data inject `AssistDbContext`. Controllers that need shared reference data inject `SharedDbContext`. Some controllers inject both.

```csharp
using Petel.Core.Controllers;
using Petel.Core.Session;
using PetelAssistants.Api.Data;

[ApiController]
[Route("api/[controller]")]
public class MyController : BaseController
{
    private readonly AssistDbContext _context;
    // private readonly SharedDbContext _shared;  ← inject when shared lookups are needed

    public MyController(AssistDbContext context, UserSessionService sessionService, ILogger<MyController> logger)
        : base(sessionService, logger)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetData()
    {
        var session = GetCurrentSession();
        if (session == null)
            return Unauthorized(new { success = false, message = "נדרש אימות" });

        // Use session.EntityId, session.UserId, etc.
        return Ok(new { });
    }
}
```

**No `[Authorize]` attribute** — all auth is done manually via `GetCurrentSession()`.

### SystemAttributeCache

`PetelAssistants.Api/Services/SystemAttributeCache.cs` implements `IAttributeCache` from `Petel.Core.Abstractions`. It must be registered in DI:

```csharp
// Program.cs
builder.Services.AddSingleton<SystemAttributeCache>();
builder.Services.AddSingleton<IAttributeCache>(sp => sp.GetRequiredService<SystemAttributeCache>());
builder.Services.AddSingleton<UserSessionService>();
builder.Services.AddSingleton<JwtTokenService>();

// After app.Build():
using (var scope = app.Services.CreateScope())
{
    var jwtService = scope.ServiceProvider.GetRequiredService<JwtTokenService>();
    var sessionService = scope.ServiceProvider.GetRequiredService<UserSessionService>();
    sessionService.SetJwtTokenService(jwtService);
}
```

`SystemAttributeCache` exposes a `Load(IEnumerable<(string Name, string Value)>)` method. Wire up your database attributes table to call `Load(...)` on startup (use Hangfire or `IHostedService`).

### Using Petel.BlazorCore in PetelAssistants.BlazorServer

All shared Blazor services are available via DI after registering them in `Program.cs`:

```csharp
using Petel.BlazorCore.Services;
using Petel.BlazorCore.Models;
using Petel.BlazorCore.Extensions;

// Register shared services
builder.Services.Configure<ApiSettings>(builder.Configuration.GetSection("ApiSettings"));
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<AuthenticationService>();
builder.Services.AddScoped<ApiService>();
builder.Services.AddScoped<SessionStateService>();
builder.Services.AddSingleton<SessionTimeoutService>();

// Register HTTP client for API calls
builder.Services.AddHttpClient("AssistantsApi", client =>
{
    var apiSettings = builder.Configuration.GetSection("ApiSettings").Get<ApiSettings>();
    client.BaseAddress = new Uri(apiSettings?.BaseUrl ?? "");
    client.Timeout = TimeSpan.FromSeconds(apiSettings?.Timeout ?? 30);
});

// Map document proxy (if needed)
app.MapDocumentProxy();
```

**Blazor frontend configuration:**

```json
// appsettings.Development.json (PetelAssistants.BlazorServer)
{
  "ApiSettings": {
    "BaseUrl": "http://localhost:PORT/api",
    "Timeout": 30
  }
}
```

## Adding New Features

### New Entity

1. Create the entity class in `PetelAssistants.Api/Models/`:
```csharp
[Table("my_entities")]  // ✅ Table name only — NO schema
public class MyEntity
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("name")]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("created_user")]
    public int? CreatedUser { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [Column("update_user")]
    public int? UpdateUser { get; set; }
}
```

2. Add `DbSet<MyEntity>` to `AppDbContext.cs` and configure in `OnModelCreating`:
```csharp
public DbSet<MyEntity> MyEntities { get; set; }

// In OnModelCreating:
modelBuilder.Entity<MyEntity>(entity =>
{
    entity.ToTable("my_entities");  // No schema — HasDefaultSchema handles it
});
```

3. Create a migration:
```bash
cd PetelAssistants/PetelAssistants.Api
dotnet ef migrations add AddMyEntity
dotnet ef database update
```

### New Controller

```csharp
[ApiController]
[Route("api/[controller]")]
public class MyEntitiesController : BaseController
{
    private readonly AppDbContext _context;

    public MyEntitiesController(
        AppDbContext context,
        UserSessionService sessionService,
        ILogger<MyEntitiesController> logger)
        : base(sessionService, logger)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var session = GetCurrentSession();
        if (session == null)
            return Unauthorized(new { success = false, message = "נדרש אימות" });

        var items = await _context.MyEntities
            .AsNoTracking()
            .OrderBy(e => e.Name)
            .ToListAsync();

        return Ok(items);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateMyEntityDto dto)
    {
        var session = GetCurrentSession();
        if (session == null)
            return Unauthorized(new { success = false, message = "נדרש אימות" });

        int? userId = int.TryParse(session.UserId, out int uid) ? uid : null;

        var entity = new MyEntity
        {
            Name = dto.Name,
            CreatedAt = DateTime.UtcNow,
            CreatedUser = userId,
            UpdatedAt = DateTime.UtcNow,
            UpdateUser = userId
        };

        _context.MyEntities.Add(entity);
        await _context.SaveChangesAsync();

        return Ok(new { success = true, id = entity.Id });
    }
}
```

### Database Table Template

```sql
CREATE TABLE assistants_schema.my_entities (
    id SERIAL PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    description VARCHAR(200) NULL,
    is_active BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    created_user INTEGER NULL REFERENCES assistants_schema.users(id) ON DELETE SET NULL,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    update_user INTEGER NULL REFERENCES assistants_schema.users(id) ON DELETE SET NULL
);

CREATE INDEX idx_my_entities_name ON assistants_schema.my_entities(name);
```

Always use idempotent migrations:
```sql
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT FROM pg_tables WHERE schemaname = 'assistants_schema' AND tablename = 'my_entities'
    ) THEN
        CREATE TABLE assistants_schema.my_entities ( ... );
        RAISE NOTICE 'Table my_entities created';
    END IF;
END $$;
```

## Authentication Setup

PetelAssistants uses the same auth stack as PetelATH. Copy these files from `PetelATH.Api` and adapt for `assistants_schema`:

| File | Notes |
|---|---|
| `Controllers/AuthController.cs` | Login, password change, password policy |
| `Controllers/OtpController.cs` | Email OTP send/validate/disable/status |
| `Services/AuthService.cs` | Core login logic, OTP flag check |
| `Services/IEmailService.cs` + `SmtpEmailService.cs` | Gmail SMTP via MailKit |
| `Configuration/EmailSettings.cs` | Bound to `"Email"` config section |

### Required appsettings sections

```json
{
  "Security": {
    "Jwt": {
      "SecretKey": "LOADED_FROM_KEY_VAULT_OR_ENV",
      "Issuer": "PetelAssistants",
      "Audience": "PetelAssistantsUsers",
      "ExpirationHours": 8
    },
    "OtpEnabled": false
  },
  "Email": {
    "SmtpHost": "smtp.gmail.com",
    "SmtpPort": 587,
    "FromAddress": "LOADED_FROM_KEY_VAULT",
    "Username": "LOADED_FROM_KEY_VAULT",
    "Password": "LOADED_FROM_KEY_VAULT"
  }
}
```

Set `OtpEnabled: false` in `appsettings.Development.json`, `true` in test/Production.

### Required DI registrations

```csharp
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("Email"));
builder.Services.AddSingleton<IEmailService, SmtpEmailService>();
```

### Required DB columns on `assistants_schema.users`

```sql
ALTER TABLE assistants_schema.users ADD COLUMN IF NOT EXISTS email_otp_code VARCHAR(100) NULL;
ALTER TABLE assistants_schema.users ADD COLUMN IF NOT EXISTS email_otp_expiry TIMESTAMPTZ NULL;
ALTER TABLE assistants_schema.users ADD COLUMN IF NOT EXISTS email_otp_attempts INTEGER NOT NULL DEFAULT 0;
```

### Login flow

```
POST /api/auth/login
  → RequiresPasswordChange → change-password modal (checked first)
  → RequiresOtp            → email OTP modal (TempToken + MaskedEmail)
  → Success                → navigate to app
```

See `petelath.instructions.md` → **Authentication & Email OTP** and `copilot-instructions.md` → **Email OTP** for full implementation details. The pattern is identical — only the schema name and JWT issuer/audience differ.

## Deployment

```powershell
.\Deploy-Assistants.ps1 -Environment production
.\Deploy-Assistants.ps1 -Environment test
.\Deploy-Assistants.ps1 -Environment production -ApiOnly
.\Deploy-Assistants.ps1 -Environment production -BlazorOnly
.\Deploy-Assistants.ps1 -Environment production -SkipBuild
```

**Azure Resources**: PetelAssistants currently shares the PetelATH App Service infrastructure. The `$envConfig` in `Deploy-Assistants.ps1` points to the same App Service names as `Deploy-ATH.ps1`. Update those values when dedicated resources are provisioned.

| Environment | Resource Group | API App | Blazor App |
|---|---|---|---|
| Test | `petel-test-rg` | `petel-test-api` | `petel-test-blazor` |
| Staging | `petel-staging-rg` | `petel-staging-api` | `petel-staging-blazor` |
| Production | `petel-prod-rg` | `petel-prod-api` | `petel-prod-blazor` |

> When PetelAssistants gets its own Azure App Services, update `BlazorAppName` and `ApiAppName` in `Deploy-Assistants.ps1`.

## Development Roadmap

PetelAssistants is a greenfield application. Build features in this order:

1. **Authentication** — Copy ATH `AuthController`, adapt for `assistants_schema.users`
2. **Core Entities** — Define domain models and EF migrations
3. **SystemAttributes** — Wire up `SystemAttributeCache` with a DB-backed attributes table
4. **API Endpoints** — Controllers for each domain area (inherit `BaseController`)
5. **Blazor UI** — Pages using `Petel.BlazorCore` services (`ApiService`, `SessionStateService`); follow patterns in `blazor-patterns.instructions.md`

All patterns (DB config, EF schema, audit fields, session management, JWT, Blazor components) are identical to PetelATH — see `copilot-instructions.md`, `petelath.instructions.md`, and `blazor-patterns.instructions.md` for canonical examples.
