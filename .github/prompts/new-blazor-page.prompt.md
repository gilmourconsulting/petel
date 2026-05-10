---
mode: agent
description: Scaffold a complete new Blazor Server page for PetelATH — creates the .razor page, API controller, DTOs (both sides), and generates the SQL for the menu_items insert.
tools:
  - create_file
  - replace_string_in_file
  - read_file
  - run_in_terminal
---

# New Blazor Page Scaffold

You are creating a **complete new page** for the PetelATH application following established patterns. Gather all requirements, then generate every artifact.

## Step 1 — Gather Requirements

Ask the user for:
1. **Page name** (English, lowercase, no spaces — this becomes the route, e.g. `students`)
2. **Hebrew title** (for the menu item display text, e.g. `תלמידים`)
3. **Menu sort order** (integer, e.g. `50`)
4. **Main data entity** being displayed (e.g. "students with class and school year")
5. **API endpoint prefix** (e.g. `students`)
6. **Key fields** to show in the table (list field names + Hebrew labels)
7. **Actions needed** (view detail / edit inline / add new / delete / upload Excel)
8. **Does the page need session context** (e.g. selected school year, school)?

## Step 2 — Generate Artifacts

### 2a. DTOs — Blazor side (`PetelATH.BlazorServer/DTOs/{PageName}Dtos.cs`)

```csharp
namespace PetelATH.BlazorServer.DTOs
{
    public class {Entity}Dto
    {
        public int Id { get; set; }
        // ... fields from user input
    }

    public class Create{Entity}Request
    {
        // ... writable fields
    }

    public class Update{Entity}Request
    {
        public int Id { get; set; }
        // ... writable fields
    }
}
```

### 2b. DTOs — API side (`PetelATH.Api/DTOs/{PageName}Dtos.cs`)

```csharp
namespace PetelATH.Api.DTOs
{
    public class {Entity}Dto
    {
        // ... same shape as Blazor DTO (from DB projection)
    }

    public class Create{Entity}Request
    {
        // ... with [Required] annotations
    }
}
```

### 2c. API Controller (`PetelATH.Api/Controllers/{Entity}Controller.cs`)

Follow the `BaseController` pattern exactly:
- **No `[Authorize]` attribute** — use `GetCurrentSession()` for auth
- Inject `AppDbContext` and `GlobalFunctions` as needed
- Filter queries by `session.EntityId`
- Include audit fields on create/update (`CreatedUser`, `UpdateUser`)
- Use `AsNoTracking()` for reads, projections for list endpoints

```csharp
[ApiController]
[Route("api/[controller]")]
public class {Entity}Controller : BaseController
{
    private readonly AppDbContext _context;
    private readonly GlobalFunctions _globalFunctions;

    public {Entity}Controller(
        AppDbContext context,
        GlobalFunctions globalFunctions,
        UserSessionService userSessionService,
        ILogger<{Entity}Controller> logger)
        : base(userSessionService, logger)
    {
        _context = context;
        _globalFunctions = globalFunctions;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var session = GetCurrentSession();
        if (session == null)
            return Unauthorized(new { success = false, message = "נדרש אימות" });

        var entityId = int.Parse(session.EntityId);

        var items = await _context.{Entities}
            .AsNoTracking()
            .Where(e => e.EntityId == entityId)
            .Select(e => new {Entity}Dto { ... })
            .ToListAsync();

        return Ok(items);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Create{Entity}Request request)
    {
        var session = GetCurrentSession();
        if (session == null)
            return Unauthorized(new { success = false, message = "נדרש אימות" });

        int? userId = int.TryParse(session.UserId, out int uid) ? uid : null;

        var entity = new {Entity}
        {
            // ... map from request
            CreatedAt = DateTime.UtcNow,
            CreatedUser = userId,
            UpdatedAt = DateTime.UtcNow,
            UpdateUser = userId
        };

        _context.{Entities}.Add(entity);
        await _context.SaveChangesAsync();
        return Ok(new { success = true, id = entity.Id });
    }
}
```

### 2d. Blazor Page (`PetelATH.BlazorServer/Components/Pages/{PageName}.razor`)

Use the canonical template from `blazor-patterns.instructions.md`:
- `@page "/{pagename}"`
- `@layout MainLayout`
- `@inherits SecurePageBase`
- `protected override string PageName => "{pagename}";`
- `OnPageInitializedAsync()` for data loading
- Inline HTML table with `@foreach`, sortable headers
- `@bind="_filterText" @bind:event="oninput"` for live filter
- `<SecureButton>` for action buttons with `HideIfNoAccess="true"`
- Modal with `@ref` pattern if add/edit is needed
- All icons as `<img src="/images/icon_name.png" class="action-icon-natural" />`

### 2e. SQL — Menu Item

```sql
-- Run on all environments
INSERT INTO petel_schema.menu_items (name, reference, text, sort_order, is_active)
VALUES ('{pagename}', '/{pagename}', '{HebrewTitle}', {sort_order}, true)
ON CONFLICT DO NOTHING;
```

## Step 3 — Verify Checklist

After generating all files, confirm:
- [ ] `@page "/{pagename}"` route matches menu `reference` column exactly
- [ ] `protected override string PageName => "{pagename}";` matches `ActionName` prefix in `SecureButton`
- [ ] All `ApiService.GetAsync` calls use the API controller's route prefix
- [ ] Controller inherits `BaseController` (no `[Authorize]`)
- [ ] All DB writes include `CreatedUser` / `UpdateUser` from `session.UserId`
- [ ] DTOs exist on both API side and Blazor side
- [ ] No `window.` / `sessionStorage` / `AppConfig.getApiUrl()` references anywhere
