# Layer 4 — Code Conventions
_PetelAssistants · ASP.NET Core 9 · Blazor Server · PostgreSQL / EF Core 9_

> **Scope**: Syntax-level rules and idioms observed in the PetelAssistants codebase.  
> For architecture, patterns, and configuration see `.github/copilot-instructions.md`.  
> For Blazor-specific scaffolding see `.github/instructions/blazor-patterns.instructions.md`.

---

## 1. Naming

### 1.1 C# identifiers

| Kind | Convention | Example |
|---|---|---|
| Class / Record | PascalCase | `StudentsController`, `AuthService` |
| Interface | `I` prefix + PascalCase | `IAuthService`, `IEmailService` |
| Abstract base class | PascalCase | `BaseController`, `SecurePageBase` |
| Method | PascalCase | `GetCurrentSession()`, `LoadDataAsync()` |
| Public property | PascalCase | `EntityId`, `IsLastVersion` |
| Private / protected field | `_camelCase` | `_context`, `_logger`, `_studentService` |
| Local variable | camelCase | `session`, `studentId`, `entityId` |
| Constant | PascalCase or SCREAMING_SNAKE | `NoPermitStatusId = 7` |
| Async method | Suffix `Async` | `GetStudentsAsync`, `CompleteLoginAsync` |
| Generic type param | Single capital or descriptive | `T`, `TResponse`, `TRequest` |

```csharp
// ✅ CORRECT
private readonly AppDbContext _context;
private readonly ILogger<StudentsController> _logger;

public async Task<IActionResult> GetStudentById(int id)
{
    var session = GetCurrentSession();
    var student = await _context.SchoolStudents.FindAsync(id);
    ...
}

// ❌ WRONG
private AppDbContext Context;        // Not private field style
private ILogger logger;              // Missing underscore, missing type param
public IActionResult getStudentById(int Id) { ... }  // Not async, lowercase method, uppercase param
```

### 1.2 Files

| Kind | Convention | Example |
|---|---|---|
| Controller | `{Entity}Controller.cs` | `StudentsController.cs` |
| Service | `{Name}Service.cs` | `AuthService.cs`, `StudentService.cs` |
| Service interface | `I{Name}Service.cs` | `IAuthService.cs` |
| EF entity | Singular noun | `SchoolStudent.cs`, `Council.cs` |
| DTO (API layer) | `{Name}Dto.cs` | `SchoolDto.cs`, `PricingDtos.cs` |
| Blazor page | `{PageName}.razor` + optional `{PageName}.razor.cs` | `Students.razor`, `AccountTransactions.razor.cs` |
| Blazor modal | `{Name}Modal.razor` | `StudentUploadModal.razor` |
| Configuration POCO | `{Name}Settings.cs` | `DatabaseSettings.cs`, `SecuritySettings.cs` |

### 1.3 Namespaces

Follow project-folder hierarchy exactly:

```csharp
namespace PetelAssistants.Api.Controllers    // PetelAssistants.Api / Controllers /
namespace PetelAssistants.Api.Services
namespace PetelAssistants.Api.Data
namespace PetelAssistants.Api.DTOs
namespace PetelAssistants.Api.Configuration
namespace PetelAssistants.BlazorServer.Components.Pages
namespace PetelAssistants.BlazorServer.Services
namespace PetelAssistants.BlazorServer.DTOs
namespace Petel.Core.Session         // shared library
```

### 1.4 Database

| Kind | Convention | Example |
|---|---|---|
| Table | `snake_case` plural | `school_students`, `hebrew_years` |
| Column | `snake_case` | `id_number`, `school_year_id`, `is_last_version` |
| View | `snake_case` + `_vw` suffix | `council_summary_vw` |
| Index | `ix_{table}_{column(s)}` | `ix_hours_budget_entity_year_type` |
| Unique constraint | `uk_{table}_{description}` | `uk_year_attribute` |
| FK constraint | implicit via EF or explicit name | — |
| Primary key | always `id SERIAL PRIMARY KEY` | — |

---

## 2. Project & File Structure

```
PetelAssistants/
  PetelAssistants.Api/
    Configuration/   ← settings POCOs (DatabaseSettings, SecuritySettings…)
    Controllers/     ← one file per aggregate/feature; all inherit BaseController
    Data/            ← EF entities + AppDbContext
    DTOs/            ← request/response DTOs used in API layer only
    Middleware/      ← ASP.NET middleware classes
    Migrations/      ← EF generated migration files
    Models/          ← non-EF models (view models, report definitions)
    Services/        ← business logic; interfaces live alongside implementation
    Session/         ← UserSession, UserSessionService (local overrides of Petel.Core)
    Program.cs
  PetelAssistants.BlazorServer/
    Components/
      Layout/        ← MainLayout.razor, NavMenu.razor
      Modals/        ← *Modal.razor components
      Pages/         ← one .razor (+ optional .razor.cs) per page
      Security/      ← SecureButton.razor
      Shared/        ← reusable non-modal components
    DTOs/            ← Blazor-side DTOs (mirror or extend API DTOs)
    Services/        ← ApiService, SessionStateService, ActionSecurityService…
    wwwroot/images/  ← icon PNG files (standard set — no emoji replacements)
    Program.cs
shared/
  Petel.Core/        ← JWT, session, encryption (backend only)
  Petel.BlazorCore/  ← ApiService base, TokenService, SessionStateService…
```

**Rule**: Never place business logic in a controller. Controllers validate auth, call services, and format responses. Business logic lives in `Services/`.

---

## 3. API Controllers

### 3.1 Class-level boilerplate

```csharp
[ApiController]
[Route("api/[controller]")]
public class StudentsController : BaseController
{
    private readonly AppDbContext _context;
    private readonly StudentService _studentService;

    public StudentsController(
        AppDbContext context,
        StudentService studentService,
        UserSessionService userSessionService,
        ILogger<StudentsController> logger)          // ILogger<T> — typed to this controller
        : base(userSessionService, logger)
    {
        _context = context;
        _studentService = studentService;
    }
```

- Always inherit `BaseController` (from `Petel.Core`).
- Always pass `ILogger<T>` (where `T` is this controller's type, **not** `BaseController`).
- Never use `[Authorize]` — session is validated manually in each action.

### 3.2 Action method pattern

Every public action follows this exact sequence:

```csharp
[HttpGet("{id}")]
public async Task<IActionResult> GetStudentById(int id)
{
    try
    {
        // 1. Auth check — always first
        var session = GetCurrentSession();
        if (session == null)
            return Unauthorized(new { success = false, message = "נדרש אימות" });

        // 2. Input validation
        if (!int.TryParse(session.EntityId, out int entityId))
            return BadRequest(new { success = false, message = "מזהה ישות לא תקין" });

        // 3. Business / data logic
        var student = await _context.SchoolStudents
            .AsNoTracking()
            .Where(s => s.Id == id)
            .Select(s => new { s.Id, s.FirstName, s.LastName })
            .FirstOrDefaultAsync();

        if (student == null)
            return NotFound(new { success = false, message = "תלמיד לא נמצא" });

        // 4. Success response
        return Ok(new { success = true, data = student });
    }
    catch (Exception ex)
    {
        // 5. Exception handler — last resort
        _logger.LogError(ex, "Error loading student {StudentId}", id);
        return StatusCode(500, new { success = false, message = "שגיאה בטעינת פרטי תלמיד", error = ex.Message });
    }
}
```

### 3.3 HTTP status codes

| Situation | Status | Response shape |
|---|---|---|
| Success | `200 OK` | `{ success: true, data: … }` |
| Created | `200 OK` | `{ success: true, message: "…", data: { id } }` |
| Bad request / validation fail | `400 BadRequest` | `{ success: false, message: "…" }` |
| No valid session | `401 Unauthorized` | `{ success: false, message: "נדרש אימות" }` |
| Not found | `404 NotFound` | `{ success: false, message: "… לא נמצא" }` |
| Server error | `500 StatusCode` | `{ success: false, message: "שגיאה ב…", error: ex.Message }` |

> **`error: ex.Message` in 500 responses**: acceptable in current codebase; do not expose stack traces.

---

## 4. Response Shape

All API responses use a consistent envelope:

```csharp
// Success with data
return Ok(new { success = true, data = result });

// Success with message
return Ok(new { success = true, message = "הפעולה הושלמה בהצלחה" });

// Error
return BadRequest(new { success = false, message = "…" });
```

Blazor DTOs match this with typed wrappers:

```csharp
// ApiResponseDto.cs (Blazor DTOs)
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public T? Data { get; set; }
}
```

Usage in Blazor:
```csharp
var response = await ApiService.GetAsync<ApiResponse<List<StudentDto>>>("students");
if (response?.Success == true) _students = response.Data ?? new();
```

---

## 5. EF Core Conventions

### 5.1 Entity class pattern

```csharp
[Table("school_students")]          // Table name only — NO Schema parameter
public class SchoolStudent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("first_name")]
    public string? FirstName { get; set; }

    [Required]
    [Column("school_year_id")]
    public int SchoolYearId { get; set; }

    // FK + navigation (both required together)
    [ForeignKey("Status")]
    [Column("status")]
    public int? StatusId { get; set; }
    public virtual Status? Status { get; set; }

    // Collection navigation on parent side
    // public virtual ICollection<SchoolStudentPricingElement> PricingElements { get; set; } = new List<>();
}
```

Rules:
- `[Table("snake_case")]` — never include `Schema =` parameter.
- `[Column("snake_case")]` on every mapped property.
- Always declare both the FK scalar property **and** the navigation property.
- Navigation properties are `virtual`.
- Non-nullable navigations: `= null!;` initialiser; nullable: `?`.

### 5.2 DbContext

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);

    // ONCE — applies to all entities
    modelBuilder.HasDefaultSchema(_schemaName);   // from IOptions<DatabaseSettings>

    modelBuilder.Entity<SchoolStudent>(entity =>
    {
        entity.ToTable("school_students");          // No schema

        entity.HasOne(s => s.Status)
            .WithMany()
            .HasForeignKey(s => s.StatusId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasIndex(e => e.SchoolYearId);
    });
}
```

### 5.3 Query patterns

```csharp
// ✅ Read queries — always AsNoTracking + projection
var students = await _context.SchoolStudents
    .AsNoTracking()
    .Where(s => s.SchoolYearId == yearId && s.IsLastVersion)
    .Select(s => new
    {
        s.Id,
        s.FirstName,
        s.LastName,
        ClassName = _context.SchoolClasses
            .Where(c => c.Id == s.ClassId)
            .Select(c => c.Name)
            .FirstOrDefault()
    })
    .ToListAsync();

// ✅ Write — track the entity
var student = await _context.SchoolStudents.FindAsync(id);
student!.StatusId = 8;
await _context.SaveChangesAsync();

// ❌ WRONG — no AsNoTracking on read
var students = await _context.SchoolStudents.ToListAsync();

// ❌ WRONG — sync call
var count = _context.SchoolStudents.Count();
```

### 5.4 Encryption in OnModelCreating

Sensitive fields are encrypted via `HasConversion`. Follow the existing pattern in `AppDbContext` exactly — do not manually call `DataEncryptionService` in controllers or services:

```csharp
entity.Property(e => e.IdNumber)
    .HasConversion(
        v => v != null ? _encryptionService.Encrypt(v) : null,
        v => v != null ? _encryptionService.Decrypt(v) : null
    );
```

Encrypted fields: `persons.id_number`, `persons.email`, `persons.phone_number`, `school_students.id_number`, `school_students.street`, `users.otp_secret`, `users.email`.

---

## 6. Services

### 6.1 DI lifetimes

| Pattern | Lifetime | Examples |
|---|---|---|
| Stateful singletons (caches, session store) | `Singleton` | `UserSessionService`, `JwtTokenService`, `SystemAttributeCache`, `SchoolAttributeCache`, `ActionAuthorizationService` |
| Cache loaders | `HostedService` (singleton) | `SystemAttributeLoaderHostedService` |
| Business logic | `Scoped` | `AuthService`, `StudentService`, `StudentPricingService` |
| Batch jobs | `Transient` | `CouncilExcelGenerationService`, `CouncilWordGenerationService` |
| Email | `Singleton` | `SmtpEmailService` (stateless) |

### 6.2 Interface pattern

Define an interface when the service is injectable across projects or has test-double value:

```csharp
// Services/IAuthService.cs
public interface IAuthService
{
    Task<LoginResponseDto> LoginAsync(LoginRequestDto request);
    Task<bool> VerifyPasswordAsync(User user, string password);
}

// Services/AuthService.cs
public class AuthService : IAuthService { ... }

// Program.cs
builder.Services.AddScoped<IAuthService, AuthService>();
```

Internal/concrete-only services (e.g. `StudentService`) may skip the interface.

### 6.3 Constructor injection

```csharp
public class AuthService : IAuthService
{
    private readonly AppDbContext _context;
    private readonly ILogger<AuthService> _logger;
    private readonly SecuritySettings _securitySettings;

    public AuthService(
        AppDbContext context,
        ILogger<AuthService> logger,
        IOptions<SecuritySettings> securitySettings)
    {
        _context = context;
        _logger = logger;
        _securitySettings = securitySettings.Value;  // unwrap IOptions here
    }
}
```

---

## 7. DTOs

### 7.1 Location

DTOs are **not** shared between projects. Each project has its own:

- `PetelAssistants.Api/DTOs/` — request/response types used in controllers
- `PetelAssistants.BlazorServer/DTOs/` — types used by Blazor pages and services

Simple controller-local DTOs (one-off request bodies) may be defined at the **bottom of the controller file** rather than in `DTOs/`:

```csharp
// PersonsController.cs — at the bottom after the class
public class CreatePersonDto
{
    public string? IdNumber { get; set; }
    public string FirstName { get; set; } = string.Empty;
    ...
}
```

### 7.2 DTO naming

| Purpose | Pattern | Example |
|---|---|---|
| Create request | `Create{Entity}Dto` | `CreatePersonDto` |
| Update request | `Update{Entity}Dto` | `UpdatePersonDto` |
| Read / list | `{Entity}Dto` | `StudentDto`, `SchoolDto` |
| Nested / summary | `{Entity}SummaryDto` | `StudentSummaryDto` |
| Grouped file | `{Feature}Dtos.cs` (plural) | `PricingDtos.cs`, `SecurityDTOs.cs` |

### 7.3 Nullability

Use C# nullable annotations throughout:

```csharp
public class StudentDto
{
    public int Id { get; set; }                  // value type — no ?
    public string? IdNumber { get; set; }         // optional string — ?
    public string FirstName { get; set; } = string.Empty;  // required string — initialise
    public DateOnly? StartDate { get; set; }      // optional date — ?
}
```

---

## 8. Blazor Pages

### 8.1 Page file header

```razor
@page "/students"
@layout MainLayout
@inherits SecurePageBase
@using PetelAssistants.BlazorServer.DTOs
@using PetelAssistants.BlazorServer.Services
@inject ApiService ApiService
@inject SessionStateService SessionStateService
```

### 8.2 Code-behind split (`.razor.cs`)

Use a `.razor.cs` partial class when the `@code` block exceeds ~150 lines:

```csharp
// AccountTransactions.razor.cs
public partial class AccountTransactions : SecurePageBase
{
    protected override string PageName => "accounttransactions";
    // ...
}
```

### 8.3 Required SecurePageBase contract

```csharp
@code {
    // MUST declare
    protected override string PageName => "students";    // lowercase, matches action DB record

    // MUST override instead of OnInitializedAsync
    protected override async Task OnPageInitializedAsync()
    {
        await LoadData();
    }
}
```

### 8.4 Private field naming in `@code`

All component state fields use `_camelCase`:

```csharp
private List<StudentDto> _students = new();
private bool _isLoading = true;
private string _filterName = string.Empty;
private StudentUploadModal? _uploadModal;    // modal ref — nullable
```

### 8.5 Navigation

```csharp
// ✅ NavigationManager (injected)
Navigation.NavigateTo("/students");

// ❌ WRONG — no JSRuntime window.location
await JSRuntime.InvokeVoidAsync("eval", "window.location='/students'");
```

### 8.6 SecureButton for gated actions

```razor
<SecureButton
    ActionName="students_deleteStudent"
    ScreenName="@PageName"
    FunctionName="DeleteStudent"
    ActionParams="@($"studentId={student.Id}")"
    OnClick="() => DeleteStudent(student.Id)"
    CssClass="btn-icon"
    HideIfNoAccess="true">
    <img src="/images/delete_icon.png" alt="מחיקה" class="action-icon-natural">
</SecureButton>
```

ActionName format: `{pageName}_{actionName}` — all lowercase, underscore separator.

---

## 9. Logging

### 9.1 Logger source

Always use `ILogger<T>` typed to the current class. In controllers, the generic is the controller type (passed to `BaseController` which holds it as `_logger`):

```csharp
public StudentsController(
    ...
    ILogger<StudentsController> logger)   // ✅ typed to this controller
    : base(userSessionService, logger)
```

In services, inject directly:

```csharp
private readonly ILogger<AuthService> _logger;
```

### 9.2 Log levels

| Situation | Level | Method |
|---|---|---|
| Successful data loads, key steps | Information | `_logger.LogInformation(…)` |
| Auth failures, missing session, invalid input | Warning | `_logger.LogWarning(…)` |
| Caught exceptions, server errors | Error | `_logger.LogError(ex, …)` |
| Verbose/debug (dev only) | Debug | `_logger.LogDebug(…)` |

### 9.3 Structured logging syntax

```csharp
// ✅ Structured parameters — NOT string interpolation
_logger.LogInformation("Loading students for entity {EntityId}", entityId);
_logger.LogWarning("No valid session for user {Username}", loginRequest.Username);
_logger.LogError(ex, "Error loading student {StudentId}", id);

// ❌ WRONG — interpolated string loses structured data
_logger.LogInformation($"Loading students for entity {entityId}");
```

### 9.4 Emoji conventions in logs

Emojis are used informally in some areas (`✅`, `🚫`, `❌`, `🔄`) — acceptable for human readability in development and ops contexts. Not required for new code but do not remove them from existing messages.

---

## 10. Error Handling

### 10.1 Controller catch block

```csharp
catch (Exception ex)
{
    _logger.LogError(ex, "Error {Verb}ing {Entity} {Id}", "load", "student", id);
    return StatusCode(500, new
    {
        success = false,
        message = "שגיאה ב…",      // Hebrew, user-facing
        error = ex.Message           // Technical detail — OK for internal apps
    });
}
```

### 10.2 Services — re-throw or return result

Services may let exceptions propagate (controller catches them) **or** return a typed result:

```csharp
// Option A — propagate (controller wraps)
public async Task<User?> ValidateUserAsync(string username, string password, int entityId)
{
    var user = await _context.Users.FirstOrDefaultAsync(…);
    if (user == null || !await VerifyPasswordAsync(user, password))
        return null;
    return user;
}

// Option B — typed result (for complex flows)
public async Task<(bool IsLocked, string? Message)> CheckUserLockStatusAsync(User user)
{
    if (user.IsLocked)
        return (true, "חשבון המשתמש נעול");
    return (false, null);
}
```

Never swallow exceptions silently without logging.

---

## 11. Security Conventions

### 11.1 Session validation

First line of every non-trivial action:

```csharp
var session = GetCurrentSession();
if (session == null)
    return Unauthorized(new { success = false, message = "נדרש אימות" });
```

### 11.2 Entity scoping

All data queries MUST scope by the user's EntityId:

```csharp
if (!int.TryParse(session.EntityId, out int entityId))
    return BadRequest(new { success = false, message = "מזהה ישות לא תקין בסשן" });

var data = await _context.MyEntities
    .Where(e => e.EntityId == entityId)
    .ToListAsync();
```

### 11.3 Passwords

```csharp
// ✅ Hash on creation/change (work factor 12)
var hash = BCrypt.Net.BCrypt.HashPassword(plainPassword, 12);

// ✅ Verify on login
bool valid = BCrypt.Net.BCrypt.Verify(loginRequest.Password, user.PasswordHash);

// ❌ NEVER store or compare plaintext passwords
```

### 11.4 Soft delete

Logical deletion always uses `StatusId = 8` (named "נמחק"):

```csharp
student.StatusId = 8;
await _context.SaveChangesAsync();
```

Queries on active records must exclude status 8:

```csharp
.Where(s => s.IsLastVersion && s.StatusId != 8)
```

### 11.5 Version history pattern

Mutable domain objects (students, school attributes, additional-study programs) use:

| Column | Type | Meaning |
|---|---|---|
| `master_{entity}_id` | `int NOT NULL` | Groups all versions of the same logical record |
| `version` | `int NOT NULL DEFAULT 1` | Monotonically increasing version number |
| `is_last_version` | `bool NOT NULL DEFAULT true` | Only one record per `master_id` is `true` |

Creating a new version: set `is_last_version = false` on the current record, insert a new row with incremented `version` and `is_last_version = true`.

---

## 12. Database / SQL Conventions

### 12.1 Audit columns — required on every table

```sql
created_at   TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
created_user INTEGER   NULL REFERENCES petel_schema.users(id) ON DELETE SET NULL,
updated_at   TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
update_user  INTEGER   NULL REFERENCES petel_schema.users(id) ON DELETE SET NULL
```

### 12.2 Migration scripts

- Idempotent (`IF NOT EXISTS`, `ON CONFLICT DO NOTHING`).
- Placed in `/SQL/` at repo root.
- Include a `RAISE NOTICE` at the end for visibility.

### 12.3 OnDelete behaviour

| Relationship type | Behaviour |
|---|---|
| Parent deleted → child orphaned (soft) | `ON DELETE RESTRICT` |
| Child meaningless without parent | `ON DELETE CASCADE` |
| FK becomes NULL when parent deleted | `ON DELETE SET NULL` |

---

## 13. Hebrew / RTL Conventions

- All **user-facing strings** (labels, messages, button text, error messages) are **Hebrew**.
- API error messages in the `message` field are Hebrew; `error` field (technical) may be English.
- Logger messages are in **English** — structured logging values (names, IDs) may be Hebrew where they come from user data.
- Excel exports: `worksheet.View.RightToLeft = true`.
- Blazor containers with Hebrew text: `direction: rtl; text-align: right;` (either via CSS class or inline style).
- Normalise Hebrew user input before database comparison: `GlobalFunctions.PureHebrewText(input)`.
- Hebrew gershayim `״` (U+05F4) vs `"` (U+0022) — use `NormalizeQuotes()` helper before comparing class names (see `Students.razor`).

---

## 14. Configuration & Environment

- **Never hardcode** connection strings, API base URLs, schema names, secrets, or port numbers.
- Use `IOptions<T>` — unwrap `.Value` in the constructor, store the plain POCO.
- Environment files: `appsettings.Development.json`, `appsettings.test.json`, `appsettings.Production.json`.
- Schema name: always from `IOptions<DatabaseSettings>` → `_schemaName` → `modelBuilder.HasDefaultSchema(_schemaName)`.

---

## 15. Excel / EPPlus Conventions

```csharp
ExcelPackage.LicenseContext = LicenseContext.NonCommercial;  // always set before new ExcelPackage()
using var package = new ExcelPackage();

var ws = package.Workbook.Worksheets.Add("שם גיליון");
ws.View.RightToLeft = true;

// Header row: bold + blue background + white text + right-aligned
var cell = ws.Cells[1, col];
cell.Value = "כותרת";
cell.Style.Font.Bold = true;
cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
cell.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(0x21, 0x96, 0xF3));
cell.Style.Font.Color.SetColor(Color.White);
cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;

ws.Cells[ws.Dimension.Address].AutoFitColumns();
return File(package.GetAsByteArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
```

Date formatting in cells: `date.ToString("dd/MM/yyyy")`.  
File name convention: `{EntityHebrew}_{YearLabel}_{DateTime.Now:yyyyMMdd}.xlsx`.

---

## 16. Common Anti-Patterns — Quick Reference

| ❌ Don't | ✅ Do instead |
|---|---|
| `[Authorize]` on controllers | Manual `GetCurrentSession()` check |
| `_context.Students.ToList()` | `.AsNoTracking().Select(…).ToListAsync()` |
| Hardcode schema: `ToTable("schools", Schema = "petel_schema")` | `ToTable("schools")` + `HasDefaultSchema` |
| `new AppDbContext(…)` directly | Inject via constructor DI |
| String interpolation in log: `$"Error for {id}"` | Structured: `"Error for {Id}", id` |
| `window.location = '/page'` in Blazor | `Navigation.NavigateTo("/page")` |
| `OnInitializedAsync` override in page | `OnPageInitializedAsync` override |
| Inline modal HTML in 5+ places | Extract to `*Modal.razor` component |
| Catch and swallow without log | Always `_logger.LogError(ex, …)` |
| `ExcelPackage.LicenseContext` missing | Always set before `new ExcelPackage()` |
| Plaintext sensitive data in DB | Encryption via `HasConversion` in `AppDbContext` |
| Hardcoded `userId == 1` admin checks | Use `ActionAuthorizationService` |
