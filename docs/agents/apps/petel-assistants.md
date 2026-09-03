# PetelAssistants — Application Guide

> Canonical: `docs/agents/apps/petel-assistants.md`. Domain rules: [petel-assistants-domain.md](petel-assistants-domain.md). Year screens: [PetelAssistants/docs/year-management-screens.md](../../PetelAssistants/docs/year-management-screens.md). Audit columns: [audit-fields.md](../reference/audit-fields.md).

**PetelAssistants** manages school assistants per local authority and school year. Both the API and Blazor frontend share `Petel.Core` and `Petel.BlazorCore` with PetelATH but use separate domain implementations.

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

## Year Management Screens

See [PetelAssistants/docs/year-management-screens.md](../../PetelAssistants/docs/year-management-screens.md) for navigation flow (operational `/year/{YearId}` hub vs shared **ניהול שנה** `/year-elements`), session keys, API endpoints, and security actions. Domain rules for budget calculate, shared year rates, and the add-missing-assistant-types prompt: [petel-assistants-domain.md](petel-assistants-domain.md).

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
    [Column("created_at")]  public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [Column("user_id")]     public int? UserId        { get; set; }  // creator FK → users.id
    [Column("updated_at")]  public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    [Column("update_user")] public int? UpdateUser    { get; set; }
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

### Person file upload (Excel/CSV)

Bulk create from the Assistants screen (`PersonUploadModal` → `PersonsFileUploadController`):

| Endpoint | Purpose |
|---|---|
| `POST api/personsfileupload/preview` | Headers + suggested mappings + available fields |
| `POST api/personsfileupload/upload` | Multipart file + `mappingJson` (system field → file column) |

Duplicate `id_number` within the tenant is skipped. Name mapping: full `name` (split) **or** `first_name` + `last_name`. Domain rules: [petel-assistants-domain.md](petel-assistants-domain.md) § Persons.

### Salary file upload (Excel/CSV)

Manual upload from Main Dashboard / Year Management (`SalaryUploadModal` → `SalaryFileUploadController`). SQL: `PetelAssistants/SQL/add-salary-upload.sql`.

| Endpoint | Purpose |
|---|---|
| `GET api/salaryfileupload/period-exists` | Whether salaries already exist for year/month |
| `GET/PUT api/salaryfileupload/mapping` | Entity-level column map + `idIncludesCheckDigit` |
| `POST api/salaryfileupload/preview` | Headers + suggested/saved mappings |
| `POST api/salaryfileupload/upload` | Multipart file + mapping + period + replace/save flags |
| `GET api/salaries?year=&month=` | List salary rows for period (view screen) |
| `POST api/salaries/recheck?year=&month=` | Re-match salary rows to persons + allocations; rebuilds summary + anomalies |
| `GET/POST/PUT api/salary-department-mappings` | Tenant payroll department → assistant type |
| `GET api/salary-department-mappings/unmapped` | Distinct file departments not in the map |
| `GET api/salary-month-summaries?year=&month=` | Latest process summary vs locked budget (all payments) |
| `GET api/salary-month-summaries/for-year?yearId=` | Latest-process summary lines for every month in a Hebrew year |
| `GET api/salary-anomalies?year=&month=` | Anomaly details for latest process |
| `PUT api/salary-anomalies/{id}/status` | Update anomaly status + notes |
| `GET api/statuses?object=` | Shared statuses lookup |

Tables: `salaries`, `salary_upload_processes`, `salary_upload_warnings`, `salary_field_mappings`, `salary_department_mappings`, `salary_month_summaries`, `salary_anomalies`. View UI: `/salaries`, `/salaries/month-summary`, `/salaries/anomalies`, `/salary-department-mappings`. SQL: `add-salary-upload.sql`, `add-monthly-ops.sql`. Domain rules: [petel-assistants-domain.md](petel-assistants-domain.md) § Salary file upload.

### Institutional entitlements file upload (Excel/CSV)

Manual upload from Year Management (`EntitlementUploadModal` → `EntitlementFileUploadController`). SQL: `PetelAssistants/SQL/add-entitlement-upload.sql`. Callers always pass `yearId` (API is reusable from other screens).

| Endpoint | Purpose |
|---|---|
| `GET/PUT api/entitlementfileupload/mapping` | Entity-level column map |
| `POST api/entitlementfileupload/preview` | Headers + suggested/saved mappings |
| `POST api/entitlementfileupload/upload` | Multipart file + mapping + `yearId` + `saveMapping` → counts + invalid list + orphan list |
| `POST api/entitlementfileupload/cancel-orphans` | Logical cancel (version) for selected orphan entitlement ids |

Tables: `entitlement_field_mappings`, `entitlement_upload_processes`; institutions gain `symbol` (סמל מוסד). Action: `yearmanagement_entitlements_upload`. Validity columns + `entitlements_resolve_invalid`: `add-entitlement-validity.sql`. Domain rules: [petel-assistants-domain.md](petel-assistants-domain.md) § Institutional entitlements file upload.

### Personal entitlements file upload (PDF / Excel)

Upload personal (`student_help`) entitlements from the Entitlements screen (`PersonalEntitlementUploadModal` → `PersonalEntitlementUploadController`). PDF is converted via existing `POST api/personalapprovalspdf/convert`, then optionally downloaded, then imported. SQL: `PetelAssistants/SQL/add-personal-entitlement-upload.sql`.

| Endpoint | Purpose |
|---|---|
| `GET/PUT api/personalentitlementupload/mapping` | Entity-level personal column map |
| `POST api/personalentitlementupload/preview` | Headers + suggested/saved mappings |
| `POST api/personalentitlementupload/upload` | Multipart file + mapping + `yearId` + `saveMapping` → counts + invalid list + orphan list |
| `POST api/personalentitlementupload/cancel-orphans` | Logical cancel (version) for selected orphan personal entitlement ids |
| `POST api/personalapprovalspdf/convert` | PDF → Excel (reuse; no entitlement DB writes) |

Tables: `personal_entitlement_field_mappings`; process audit reuses `entitlement_upload_processes`. Actions: `entitlements_personal_upload`, `entitlements_resolve_invalid`. Domain rules: [petel-assistants-domain.md](petel-assistants-domain.md) § Personal entitlements file upload.

### Personal approvals PDF → Excel (convert)

Convert Ministry “אישור תומכת חינוך אישית” PDF (one approval per page) to Excel. Used by the personal entitlement upload wizard; convert alone does not write entitlements.

| Piece | Location |
|---|---|
| UI entry | Entitlements upload wizard (PDF branch) |
| API | `PersonalApprovalsPdfController` → `POST api/personalapprovalspdf/convert` (multipart `file`, max ~20MB, `.pdf` only) |
| Parser | `PersonalApprovalsPdfParser` (PdfPig + ClosedXML write) |
| SQL action | `PetelAssistants/SQL/add-personal-approvals-pdf-action.sql` → `entitlements_personal_approvals_pdf` (legacy; UI uses `entitlements_personal_upload`) |
| Package | `PdfPig` 0.1.10 on `PetelAssistants.Api` (not the compromised `UglyToad.PdfPig` id on NuGet) |

**Response:** `{ success, fileName, contentBase64, rowCount, errorCount, errors[] }`.

**Excel columns (order):** תאריך אישור, שם רשות, סמל רשות, שם פרטי, שם משפחה, ת.ז. תלמיד, קוד תומכת חינוך, מסגרת, שם מוסד, סמל מוסד, שעות, מתאריך, עד תאריך, השתתפות הרשות (Excel `%` number format).

Domain/extraction rules: [petel-assistants-domain.md](petel-assistants-domain.md) § Personal entitlements file upload. Screen map: [year-management-screens.md](../../PetelAssistants/docs/year-management-screens.md).

## Meitar MUTAVIM retrieve

Pull ministry MUTAVIM rows for the logged-in authority’s period from PetelMeitar into Assistants. Entry points: context buttons on Main Dashboard and Year Management (`MeitarRetrieveModal` → `MeitarDataController`). SQL: `PetelAssistants/SQL/add-meitar-mutavim-retrieve.sql`.

| Endpoint | Purpose |
|---|---|
| `GET api/meitardata/period-exists` | Whether MUTAVIM rows exist for year/month |
| `POST api/meitardata/retrieve` | Query Meitar + persist for a single period (`replaceExisting` for override); builds month summary |
| `GET api/meitardata/period-exists-range` | Same check across a `from`–`to` period range (max 24 months) |
| `POST api/meitardata/retrieve-range` | Same as `retrieve`, looped per period across a `from`–`to` range (max 24 months); one modal (`MeitarRetrieveModal`) drives both single- and multi-period retrieve using `YYYY/MM` inputs |
| `GET api/meitar-month-summaries?year=&month=` | Latest process summary vs locked budget |

Tables: `meitar_retrieve_processes`, `meitar_mutavim`, `meitar_month_summaries`. `meitar_topics.assistant_type_id` is the shared topic→type map. Actions: `maindashboard_meitar_retrieve`, `yearmanagement_meitar_retrieve`. Domain rules: [petel-assistants-domain.md](petel-assistants-domain.md) § Meitar data integration.

---

## Architecture Governance — Multi-Tenancy Rules

> These rules apply to every feature added to PetelAssistants. They are non-negotiable.

### 1 — Database Schema Structure

**Two fixed schemas. Never add a third.**

| Schema | DbContext | What goes here |
|---|---|---|
| `shared_schema` | `SharedDbContext` | Entity types, entities (local authorities), cities, assistant types, system attributes, any lookup that has no tenant owner |
| `assist_schema` | `AssistDbContext` | Users, roles, persons (assistants, pupils), assignments, all operational data |

**Rules:**

- Every `assist_schema` table **MUST** have `entity_id INTEGER NOT NULL` as its second column (after `id`).
- `shared_schema` tables **MUST NOT** have `entity_id`.
- Adding a new local authority = one `INSERT INTO shared_schema.entities`. No DDL, no migration, no infrastructure change.
- Schema-per-tenant is **explicitly excluded**. Do not use it, suggest it, or design for it.

**Shared table candidates (shared_schema):**
- `entities` — local authorities (tenants) only
- `entity_types` — authority, etc.
- `assistant_types` — type codes for educational support staff (`position_type`, `position_hours`)
- `hebrew_years`, `ministry_participation_options`, `meitar_data_filter_values`
- `meitar_topics` — Meitar topic lookup + optional `assistant_type_id` for month summary (managed on `/system-data`)
- `statuses` — shared status lookup (`object` + `code`; salary anomalies seeded as `new` / `settled` / `note`)
- `cities` — city/settlement lookup
- `system_attributes` — global key-value config

Admin UI for shared lookups: `/system-data` (הגדרות מערכת). SQL: `PetelAssistants/SQL/add-system-data-hub.sql`.

**Tenant table candidates (assist_schema):**
- `users`, `roles`, `user_roles`, `permissions`
- `institutions` — schools and kindergartens owned by the authority
- `persons` — assistants, pupils (each row is owned by exactly one authority)
- `salaries`, `salary_department_mappings`, `salary_month_summaries`, `salary_anomalies`
- `meitar_mutavim`, `meitar_month_summaries`
- `assignments`, `placements`, `attendance`
- Any table that stores data entered by a specific authority

**SQL idempotent migration pattern:**

```sql
-- assist_schema table (always include entity_id)
DO $$ BEGIN
    IF NOT EXISTS (
        SELECT FROM pg_tables WHERE schemaname = 'assist_schema' AND tablename = 'my_table'
    ) THEN
        CREATE TABLE assist_schema.my_table (
            id          SERIAL PRIMARY KEY,
            entity_id   INTEGER NOT NULL REFERENCES shared_schema.entities(id) ON DELETE CASCADE,
            name        VARCHAR(100) NOT NULL,
            is_active   BOOLEAN NOT NULL DEFAULT true,
            created_at  TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
            user_id      INTEGER NULL,
            updated_at  TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
            update_user  INTEGER NULL
        );
        CREATE INDEX idx_my_table_entity_id ON assist_schema.my_table(entity_id);
        RAISE NOTICE 'Table assist_schema.my_table created';
    END IF;
END $$;
```

---

### 2 — API-Layer Tenant Isolation

**The session is the source of truth. The client never supplies `entity_id`.**

- `entity_id` is **never** accepted from the request body, query string, or route parameter for write operations. It is always read from `session.EntityId`.
- Every controller endpoint that touches `assist_schema` data must:
  1. Call `GetCurrentSession()` and return `401` if null.
  2. Parse `session.EntityId` to obtain the verified tenant id.
  3. Let the `AssistDbContext` global query filter enforce row-level isolation automatically.
- Shared (global) reference data endpoints (`SharedDbContext`) do not require `entity_id` scoping but still require a valid session unless the endpoint is explicitly public (e.g. entities list for login dropdowns).

**Cross-tenant read is never allowed.** If business logic requires comparing data across two authorities, that logic must run in application code with each tenant's data fetched separately under its own session context — never via a single SQL query with no filter.

**Login exception:**

```csharp
// Login only — bypass global filter and scope explicitly with the provided EntityId
var user = await _context.Users
    .IgnoreQueryFilters()
    .FirstOrDefaultAsync(u => u.Username == dto.Username
                            && u.EntityId == dto.EntityId
                            && u.IsActive);
```

`IgnoreQueryFilters()` is used **only** in the login path. Everywhere else the filter handles isolation automatically.

**Anti-patterns:**

```csharp
// ❌ WRONG — trusting client-supplied entity_id
var entityId = dto.EntityId;  // NO

// ❌ WRONG — adding a redundant manual filter when global filter is already active
var rows = await _context.MyEntities
    .Where(e => e.EntityId == entityId)  // REDUNDANT if HasQueryFilter is registered
    .ToListAsync();

// ✅ CORRECT — global filter is sufficient; no manual Where needed
var rows = await _context.MyEntities
    .AsNoTracking()
    .ToListAsync();
```

---

### 3 — EF Core Model Requirements

**`AssistDbContext` — tenant-scoped (assist_schema)**

Every entity must:

1. Implement `IEntityScoped` (from `PetelAssistants.Api.Tenancy`).
2. Declare `[Column("entity_id")] public int EntityId { get; set; }`.
3. Be registered with a `HasQueryFilter` in `OnModelCreating`.

```csharp
// AssistDbContext.OnModelCreating — required pattern for every entity
modelBuilder.Entity<MyEntity>(entity =>
{
    entity.ToTable("my_entities");   // HasDefaultSchema("assist_schema") applies
    entity.HasQueryFilter(e =>
        _tenantContext.EntityId != 0 &&
        e.EntityId == _tenantContext.EntityId);
});
```

`_tenantContext` is an `ITenantContext` resolved per HTTP request. On login the `EntityId` is `0`, which causes the filter to return no rows — that is intentional; login uses `IgnoreQueryFilters()` explicitly.

**`SharedDbContext` — global reference (shared_schema)**

- No `IEntityScoped`, no `entity_id`, no `HasQueryFilter`.
- `OnModelCreating` calls `modelBuilder.HasDefaultSchema("shared_schema")`.
- Entities here are read-only from most controllers; writes are restricted to admin/seed operations.

**Never call `IgnoreQueryFilters()` on `AssistDbContext` outside the login path.** If a query needs it for a legitimate reason, that reason must be documented as a code comment.

---

### 4 — Blazor Frontend Concerns

**The frontend never selects or transmits `entity_id`.**

- The logged-in user's authority is established at login (the authority may be pre-selected from a shared lookup or embedded in the JWT). After that, no Blazor page should expose an authority-selection control or pass `entity_id` in API requests.
- All API calls use `ApiService` from `Petel.BlazorCore`; the `Authorization: Bearer <token>` header carries the session, which the API uses to derive `entity_id` server-side.
- Shared reference data (city lists, assistant types, etc.) is fetched from dedicated public or lightly-authenticated endpoints backed by `SharedDbContext`. These responses may be cached client-side and reused across pages — they contain no tenant-specific data.
- Never hardcode an authority name or `entity_id` in a Razor file or DTO.

**Shared vs. tenant data in forms:**

```razor
@* ✅ CORRECT — shared lookup dropdown, no entity_id sent *@
<select @bind="_selectedCityId">
    @foreach (var city in _cities)  @* loaded from shared_schema via GET /api/cities *@
    {
        <option value="@city.Id">@city.Name</option>
    }
</select>

@* ❌ WRONG — embedding tenant id in form payload *@
<input type="hidden" name="entityId" value="@_entityId" />
```

**Page initialisation pattern (tenant-scoped data):**

```csharp
protected override async Task OnPageInitializedAsync()
{
    var session = await SessionStateService.GetSessionAsync();
    if (session == null) return;          // SecurePageBase redirects to login

    // No entity_id parameter needed — API global filter handles isolation
    _items = await ApiService.GetAsync<List<MyItemDto>>("myentities");
    _cities = await ApiService.GetAsync<List<CityDto>>("cities");  // shared lookup
}
```

---

## Shared Libraries

### Blazor Frontend

PetelAssistants.BlazorServer uses **pure Blazor Server** with `@rendermode InteractiveServer`. There are no HTML files, no JavaScript SPA, and no `page-lifecycle-config.js`. All UI is in `.razor` components.

**See [core/blazor-patterns.md](../core/blazor-patterns.md)** for the canonical Blazor page template, SecurePageBase usage, ApiService call patterns, modal pattern, table pattern, icon usage, and anti-patterns. Those patterns apply to both PetelATH and PetelAssistants.

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

**Tenant-scoped entities go in `assist_schema` (the default):**

1. Create the entity class in `PetelAssistants.Api/Models/`:
```csharp
[Table("my_entities")]  // ✅ Table name only — NO schema parameter
public class MyEntity : IEntityScoped   // ✅ Required for all assist_schema entities
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("entity_id")]               // ✅ Mandatory on every assist_schema table
    public int EntityId { get; set; }

    [Required]
    [Column("name")]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Column("created_at")]  public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [Column("user_id")]     public int? UserId        { get; set; }  // creator FK → users.id
    [Column("updated_at")]  public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    [Column("update_user")] public int? UpdateUser    { get; set; }
}
```

2. Add `DbSet<MyEntity>` to **`AssistDbContext.cs`** and register the global query filter in `OnModelCreating`:
```csharp
public DbSet<MyEntity> MyEntities { get; set; }

// In OnModelCreating — query filter is REQUIRED:
modelBuilder.Entity<MyEntity>(entity =>
{
    entity.ToTable("my_entities");  // Schema comes from HasDefaultSchema("assist_schema")
    entity.HasQueryFilter(e => _tenantContext.EntityId != 0 && e.EntityId == _tenantContext.EntityId);
});
```

3. Create a migration:
```bash
cd PetelAssistants/PetelAssistants.Api
dotnet ef migrations add AddMyEntity --context AssistDbContext
dotnet ef database update --context AssistDbContext
```

**Global reference entities go in `shared_schema` — use `SharedDbContext` instead, no `IEntityScoped`, no query filter.**

### New Controller

```csharp
[ApiController]
[Route("api/[controller]")]
public class MyEntitiesController : BaseController
{
    private readonly AssistDbContext _context;   // ✅ AssistDbContext for tenant-scoped data
    // private readonly SharedDbContext _shared; ← inject when shared lookups are needed

    public MyEntitiesController(
        AssistDbContext context,
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

        // ✅ No manual entity_id filter needed — AssistDbContext global query filter handles it
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

        // ✅ EntityId always comes from the verified session — NEVER from the request body
        int entityId = int.Parse(session.EntityId);
        int? userId = int.TryParse(session.UserId, out int uid) ? uid : null;

        var entity = new MyEntity
        {
            EntityId = entityId,    // ✅ Set from session
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

**For `assist_schema` tables** (tenant-scoped — the default for all operational data):

```sql
CREATE TABLE assist_schema.my_entities (
    id SERIAL PRIMARY KEY,
    entity_id INTEGER NOT NULL REFERENCES shared_schema.entities(id) ON DELETE CASCADE,
    name VARCHAR(100) NOT NULL,
    description VARCHAR(200) NULL,
    is_active BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    user_id    INTEGER NULL REFERENCES assist_schema.users(id) ON DELETE SET NULL,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    update_user INTEGER NULL REFERENCES assist_schema.users(id) ON DELETE SET NULL
);

CREATE INDEX idx_my_entities_entity_id ON assist_schema.my_entities(entity_id);
CREATE INDEX idx_my_entities_name ON assist_schema.my_entities(entity_id, name);
```

**For `shared_schema` tables** (global reference data — no `entity_id`):

```sql
CREATE TABLE shared_schema.my_lookup (
    id SERIAL PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    description VARCHAR(200) NULL,
    is_active BOOLEAN NOT NULL DEFAULT true
);
```

Always use idempotent migrations:
```sql
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT FROM pg_tables WHERE schemaname = 'assist_schema' AND tablename = 'my_entities'
    ) THEN
        CREATE TABLE assist_schema.my_entities ( ... );
        RAISE NOTICE 'Table my_entities created';
    END IF;
END $$;
```

## Security Layer

### Architecture overview

```
Blazor page / SecureButton
    └─► ActionSecurityService       (PetelAssistants.BlazorServer/Services)
            └─► POST api/security/verify-action-secure
                    └─► ActionAuthorizationService  (singleton, in-memory cache)
                            └─► assist_schema.roles_actions + shared_schema.actions
```

`SecurePageBase.cs` calls `verify-action-secure` with `PAGE_ACCESS` on `OnInitializedAsync`.  
`SecureButton.razor` calls `verify-action-secure` with `BUTTON_CLICK` before executing its `OnClick`.

### Required `SecurityController` endpoints

All five must exist; the Blazor shared stack calls them. Missing endpoints degrade silently.

| Endpoint | `ActionAuthorizationService` method | Request DTO |
|---|---|---|
| `GET  security/user-actions` | `GetUserAllowedActionIds(userId, entityId)` | — |
| `POST security/verify-onclick` | `VerifyActionByNameAsync(…, "ONCLICK_BUTTON", screenName)` — `FunctionName` is the action name | `OnclickAccessRequest { ScreenName, FunctionName }` |
| `POST security/verify-menu` | `VerifyActionByNameAsync(…, "MENU_NAVIGATION", "")` — `MenuItemName` is the action name | `MenuAccessRequest { MenuItemName }` |
| `POST security/verify-action` | `VerifyUserActionAccessAsync(userId, entityId, actionId)` | `ActionAccessRequest { ActionId }` |
| `POST security/verify-action-secure` | Full audit path via `VerifyActionByNameAsync` + writes `ActionAuditLog` | `SecureActionRequest` |

Parsing pattern used in every SecurityController endpoint:
```csharp
if (!int.TryParse(session.UserId, out int userId) ||
    !int.TryParse(session.EntityId, out int entityId))
    return BadRequest(new { success = false, message = "מזהה משתמש או רשות לא תקין" });
```

### Required `SessionController` endpoints

| Endpoint | Purpose |
|---|---|
| `GET  session/timeout-config` | **Critical.** `SessionTimeoutService.InitializeAsync()` calls this on every page load. Returns `{ timeoutMinutes, warningMinutes: 2 }`. If missing, client defaults to 10 min regardless of the DB attribute. |
| `GET  session/properties` | Debug modal in `MainLayout.razor` (`session/properties`). |
| `DELETE session/property/{key}` | Session property cleanup. |

`timeout-config` implementation — inject `IAttributeCache` and `SecuritySettings`:
```csharp
int timeoutMinutes = _securitySettings.SessionTimeoutMinutes; // config fallback
var val = _attributeCache.GetAttributeValue("Security_SessionTimeoutMinutes");
if (int.TryParse(val, out int db) && db > 0) timeoutMinutes = db;
return Ok(new { timeoutMinutes, warningMinutes = 2 });
```

### Required `AuthController` endpoints

| Endpoint | Behaviour |
|---|---|
| `POST auth/logout` | Reads `Authorization: Bearer <token>` header, calls `_sessionService.InvalidateSession(token)`. |
| `GET  auth/check` | Reads token from header, calls `_sessionService.GetUserSession(token)`, returns `{ isAuthenticated, user }`. |

### Session timeout — layered enforcement

| Layer | Source | Re-read on each request? |
|---|---|---|
| **Blazor client idle timer** | `GET session/timeout-config` → `Security_SessionTimeoutMinutes` in `shared_schema.system_attributes` | On `InitializeAsync()` only |
| **API inactivity check** (`UserSessionService.IsSessionValid`) | `IAttributeCache.GetAttributeValue("Security_SessionTimeoutMinutes")` | ✅ Yes — re-read each call |
| **JWT absolute expiry** (`JwtTokenService`) | `JWT_ExpirationHours` attribute read **once at construction** — before `SystemAttributeLoaderHostedService` runs, so always falls back to `appsettings.json` `Security:Jwt:ExpirationHours` | ❌ No — startup race; known limitation |

### `ActionAuthorizationService` — available methods

| Method | Signature | Use |
|---|---|---|
| `VerifyActionByNameAsync` | `(userId, entityId, actionName, eventType, screenName, reference?)` | All name-based checks |
| `VerifyUserActionAccessAsync` | `(userId, entityId, actionId)` | ID-based check (`verify-action`) |
| `GetUserAllowedActionIds` | `(userId, entityId)` → `List<int>` | `user-actions` endpoint |
| `GetActionByName` | `(actionName)` → `SystemAction?` | Cache lookup |
| `RefreshCacheAsync` / `InvalidateUserCache` | — | Cache management |

`EventType` → action type name mapping (hardcoded in `ActionAuthorizationService`):

| EventType | action_types.name |
|---|---|
| `MENU_NAVIGATION` | `menu_item` |
| `BUTTON_CLICK` / `ONCLICK_BUTTON` / `BUTTON_VISIBLE_CHECK` | `button` |
| `PAGE_ACCESS` | `page_action` |
| `API_ENDPOINT` | `api_endpoint` |

---

## Authentication Setup

PetelAssistants uses the same auth stack as PetelATH. Copy these files from `PetelATH.Api` and adapt for `assist_schema`:

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

### Required DB columns on `assist_schema.users`

```sql
ALTER TABLE assist_schema.users ADD COLUMN IF NOT EXISTS email_otp_code VARCHAR(100) NULL;
ALTER TABLE assist_schema.users ADD COLUMN IF NOT EXISTS email_otp_expiry TIMESTAMPTZ NULL;
ALTER TABLE assist_schema.users ADD COLUMN IF NOT EXISTS email_otp_attempts INTEGER NOT NULL DEFAULT 0;
```

### Login flow

```
POST /api/auth/login
  → RequiresPasswordChange → change-password modal (checked first)
  → RequiresOtp            → email OTP modal (TempToken + MaskedEmail)
  → Success                → navigate to app
```

See [petel-ath.md](petel-ath.md) → **Authentication & Email OTP** and [core/auth-security.md](../core/auth-security.md) → **Email OTP** for full implementation details. The pattern is identical — only the schema name and JWT issuer/audience differ.

## Deployment

```powershell
.\Deploy-Assistants.ps1 -Environment production
.\Deploy-Assistants.ps1 -Environment test
.\Deploy-Assistants.ps1 -Environment production -ApiOnly
.\Deploy-Assistants.ps1 -Environment production -BlazorOnly
.\Deploy-Assistants.ps1 -Environment production -SkipBuild
```

**Azure Resources**: PetelAssistants has dedicated App Service infrastructure separate from PetelATH. All environments use `israelcentral`, .NET 9.0 runtime.

| Environment | Resource Group | App Service Plan | API App | Blazor App |
|---|---|---|---|---|
| Test | `petel-assist-test-rg` | `petel-assist-test-plan` | `petel-assist-test-api` | `petel-assist-test-blazor` |
| Staging | `petel-assist-staging-rg` | `petel-assist-staging-plan` | `petel-assist-staging-api` | `petel-assist-staging-blazor` |
| Production | `petel-assist-prod-rg` | `petel-assist-prod-plan` | `petel-assist-prod-api` | `petel-assist-prod-blazor` |

**Test URLs**: API `https://petel-assist-test-api.azurewebsites.net`, Blazor `https://petel-assist-test-blazor.azurewebsites.net`

**Secrets** (no Key Vault — set directly as Azure App Service Application Settings on the API app):

| App Setting Key | Description |
|---|---|
| `ConnectionStrings__DefaultConnection` | PostgreSQL connection string (shared DB server) |
| `Security__Jwt__SecretKey` | JWT signing key (≥32 chars) |
| `Security__DataEncryption__EncryptionKey` | AES-256 base64 key |
| `Email__FromAddress` / `Email__Username` / `Email__Password` | Gmail SMTP credentials |

## Development Roadmap

PetelAssistants is a greenfield application. Build features in this order:

1. **Authentication** — Copy ATH `AuthController`, adapt for `assist_schema.users`
2. **Core Entities** — Define domain models and EF migrations
3. **SystemAttributes** — Wire up `SystemAttributeCache` with a DB-backed attributes table
4. **API Endpoints** — Controllers for each domain area (inherit `BaseController`)
5. **Blazor UI** — Pages using `Petel.BlazorCore` services (`ApiService`, `SessionStateService`); follow [core/blazor-patterns.md](../core/blazor-patterns.md)

All shared patterns (DB config, EF schema, session, JWT, Blazor components) are in [docs/agents/core/](../core/). ATH-specific examples: [petel-ath.md](petel-ath.md).
