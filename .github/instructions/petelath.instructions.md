---
applyTo: 'PetelATH/**'
---

# PetelATH - Application-Specific Guide

**PetelATH** is the production educational management application for ATH (administering Hebrew schools). It manages schools, students, classes, school years, additional study programs, and financial tracking.

## Project Structure

```
PetelATH/
  PetelATH.Api/                 ← Backend (ASP.NET Core Web API, net9.0)
    Configuration/              ← DatabaseSettings, SecuritySettings binding
    Controllers/                ← All API controllers
    Data/AppDbContext.cs        ← EF Core DbContext (schema: petel_schema)
    DTOs/                       ← Request/Response DTOs
    Models/                     ← Entity classes
    Services/
      SystemAttributeCache.cs   ← Implements IAttributeCache (Petel.Core)
      GlobalFunctions.cs        ← Hebrew normalization, ID lookups
    Middleware/                 ← Rate limiting, logging
    Migrations/                 ← EF Core migrations
    Program.cs                  ← DI registration, middleware pipeline
    appsettings.json
    appsettings.Development.json
    appsettings.test.json
    appsettings.Production.json
    wwwroot/                    ← Static frontend assets (JS SPA)
  PetelATH.BlazorServer/        ← Frontend (Blazor Server, net9.0)
    Components/Pages/           ← Blazor pages (Login.razor, etc.)
    Program.cs                  ← DI, CSP, proxy registration
    appsettings.Development.json
    appsettings.test.json
    appsettings.Production.json
```

## Local Development

```bash
# ATH API (http://localhost:5082)
cd PetelATH/PetelATH.Api && dotnet run
# OR: double-click "Start Local Api.cmd"

# ATH Blazor (https://localhost:5001 / http://localhost:5000)
cd PetelATH/PetelATH.BlazorServer && dotnet run
# OR: double-click "Start Blazor Server.cmd"
```

## Database

- **Schema**: `petel_schema`
- **Connection string key**: `DefaultConnection`
- **EF Migrations history table**: `__EFMigrationsHistory` in `petel_schema`
- **AppDbContext**: `PetelATH.Api.Data.AppDbContext`

```json
// appsettings.Development.json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=petelappdb;Username=PetelAdmin;Password=..."
  },
  "Database": {
    "SchemaName": "petel_schema"
  }
}
```

## DI Registration (Program.cs)

Key services that must be registered:

```csharp
// SystemAttributeCache — implements IAttributeCache for Petel.Core services
builder.Services.AddSingleton<SystemAttributeCache>();
builder.Services.AddSingleton<IAttributeCache>(sp => sp.GetRequiredService<SystemAttributeCache>());

// UserSessionService from Petel.Core
builder.Services.AddSingleton<UserSessionService>();

// JwtTokenService from Petel.Core
builder.Services.AddSingleton<JwtTokenService>();

// GlobalFunctions — Hebrew normalization and entity lookups
builder.Services.AddScoped<GlobalFunctions>();

// Wire JwtTokenService into UserSessionService after build
using (var scope = app.Services.CreateScope())
{
    var jwtService = scope.ServiceProvider.GetRequiredService<JwtTokenService>();
    var sessionService = scope.ServiceProvider.GetRequiredService<UserSessionService>();
    sessionService.SetJwtTokenService(jwtService);
}
```

## Frontend Architecture

PetelATH uses a **Blazor Server** shell (`Login.razor`, `MainDashboard.razor`) that renders a **static JS/HTML SPA** loaded from the API's `wwwroot`. The SPA is the main UI; Blazor handles authentication and session lifecycle.

### Static Frontend (wwwroot in PetelATH.Api)

The SPA assets live in `PetelATH/PetelATH.Api/wwwroot/` (served by the API) and use a **page lifecycle** architecture:

- `index.html` — Application shell; infrastructure only
- `menu.html` — Side menu loaded dynamically from DB
- `page-lifecycle-config.js` — All pages registered here
- `page-lifecycle-manager.js` — Navigation engine (handles cleanup, session rules, history)
- `table-component.js` — `ReusableTable` component used by all data tables

### Page Lifecycle Management

**All pages must be registered** in `page-lifecycle-config.js`:

```javascript
window.PageLifecycleConfig = {
    pages: {
        'pagename': {
            file: 'page.html',
            title: 'כותרת',
            cleanup: 'cleanupPageName',    // or null
            init: 'initPageName',          // or null
            selfInitializing: false        // true = uses DOMContentLoaded
        }
    },
    navigationRules: [
        {
            from: 'student',
            to: '*',
            clearSession: ['SelectedStudentId', 'SelectedStudentData']
        }
    ]
};
```

**All navigation must use `window.navigateTo('pagename')`** — never manual HTML loading.

**Cleanup functions must be exported to `window`**:
```javascript
function cleanupMyPage() { /* ... */ }
window.cleanupMyPage = cleanupMyPage;
```

**Component variables must use `window` scope** to survive re-entry:
```javascript
window.myTable = window.myTable || null;  // ✅ NOT: let myTable = null;
```

### Database-Driven Menu System

Menu items live in `petel_schema.menu_items`. Adding a new page:

1. Insert into DB:
```sql
INSERT INTO petel_schema.menu_items (name, reference, text, sort_order, is_active)
VALUES ('newpage', '#newpage', 'כותרת', 100, true);
```

2. Create `newpage.html` in `wwwroot`

3. Register in `page-lifecycle-config.js`

4. `MenuController` loads items automatically on every login.

**Menu table schema:**
```sql
CREATE TABLE petel_schema.menu_items (
    id SERIAL PRIMARY KEY,
    name VARCHAR(50) NOT NULL,
    reference VARCHAR(100) NOT NULL,
    text VARCHAR(100) NOT NULL,
    action_id INTEGER NULL,
    sort_order INTEGER NOT NULL DEFAULT 0,
    is_active BOOLEAN NOT NULL DEFAULT true
);
```

### Standard Components

**All data tables** must use `ReusableTable` from `table-component.js`:

```javascript
const table = new ReusableTable('containerId', {
    tableName: 'entities',
    isReadOnly: false,
    allowAdd: true,
    allowEdit: true,
    allowDelete: false
});

// Action buttons column MUST be first
const columns = [
    {
        key: 'actions',
        label: 'פעולות',
        sortable: false,
        readOnly: true,
        render: (data) => `
            <button onclick="viewItem('${data.id}')">
                <img src="view_icon.png" alt="צפייה" class="action-icon-natural">
            </button>
        `
    },
    { key: 'id', label: 'מספר', sortable: true, readOnly: true },
    { key: 'name', label: 'שם', sortable: true, readOnly: false }
];

table.init(data, columns);
```

**Standard icon set** (PNG, use `.action-icon-natural` class, 15px):
- `view_icon.png` — view/preview
- `edit_icon.png` — edit
- `delete_icon.png` — delete
- `download_icon.png` — download
- `upload_icon.png` — upload
- `stats_icon.png` — statistics
- `Plus icon.png` — add new

**Table containers must support horizontal scrolling**:
```css
.table-container { overflow-x: auto; }
.data-table { min-width: 1200px; white-space: nowrap; }
```

**Security constraint — onclick handlers**: Do NOT use `event.stopPropagation()` in onclick attributes. It breaks `action-security.js`. The collapsible card header already excludes `.btn-icon` clicks:
```html
<!-- ❌ WRONG -->
<button onclick="event.stopPropagation(); showModal();">

<!-- ✅ CORRECT -->
<button onclick="showModal();">
```

### Collapsible Card Pattern

```html
<div class="detail-card collapsed">
    <div class="detail-card-header">
        <h2 class="detail-card-title">כותרת</h2>
        <div class="card-header-actions">
            <button id="addBtn" class="btn-icon" onclick="showAddModal();" style="display: none;">
                <img src="Plus icon.png" alt="הוסף" class="action-icon-natural">
            </button>
            <button class="collapse-toggle" aria-label="הרחב/כווץ">+</button>
        </div>
    </div>
    <div class="detail-card-content"><!-- content --></div>
</div>
```

JavaScript pattern:
```javascript
function initializeCollapsibleCards() {
    document.querySelectorAll('.detail-card').forEach(card => {
        const header = card.querySelector('.detail-card-header');
        const toggle = card.querySelector('.collapse-toggle');
        if (!header || !toggle || header.dataset.initialized === 'true') return;
        header.dataset.initialized = 'true';

        const addButton = card.querySelector('.btn-icon[id^="add"]');
        if (addButton) addButton.style.display = 'none';

        toggle.addEventListener('click', e => { e.stopPropagation(); toggleCardExpansion(card, toggle, addButton); });
        header.addEventListener('click', e => {
            if (e.target.closest('.btn-icon')) return;
            toggleCardExpansion(card, toggle, addButton);
        });
    });
}
```

## Key Domain Features

### School Year Attributes

Year-specific configuration stored in `petel_schema.school_year_attributes`:

```sql
CREATE TABLE petel_schema.school_year_attributes (
    id SERIAL PRIMARY KEY,
    year_id INTEGER NOT NULL REFERENCES petel_schema.hebrew_years(id) ON DELETE CASCADE,
    name VARCHAR(100) NOT NULL,
    description VARCHAR(200) NULL,
    value VARCHAR(500) NOT NULL,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    created_user INTEGER NULL REFERENCES petel_schema.users(id) ON DELETE SET NULL,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    update_user INTEGER NULL REFERENCES petel_schema.users(id) ON DELETE SET NULL,
    CONSTRAINT uk_year_attribute UNIQUE (year_id, name)
);
```

Standard attribute names:
- `additional_study_sessions_required` — required sessions for תל"ן programs

Fetch from frontend:
```javascript
const response = await fetch(
    AppConfig.getApiUrl(`schoolyearattributes/year/${yearId}/attribute/additional_study_sessions_required`),
    { headers: { 'Authorization': `Bearer ${token}` } }
);
if (response.ok) {
    const { data } = await response.json();
    document.getElementById('sessionsRemark').textContent = `מספר מפגשים נדרש: ${data.value}`;
}
```

### GlobalFunctions Service

Provides Hebrew-normalized entity lookups for all controllers and Excel import:

```csharp
var schoolId = await _globalFunctions.GetSchoolIdByName(schoolName);
var classId  = await _globalFunctions.GetClassIdByName(className, schoolYearId);
var yearId   = await _globalFunctions.GetSchoolYearIdByName(yearName);

// Static normalization (used in Excel import comparisons)
var normalized = GlobalFunctions.PureHebrewText(input);
```

Always use `GlobalFunctions` for entity lookups — never query directly from import code.

### Excel Import/Export

All Excel operations use **EPPlus 7.0.0**. Multi-stage validation pattern:

1. File format (extension + size ≤ 10 MB)
2. Header structure (column names/order)
3. Per-row data validation (required fields, types)
4. Business rules (duplicates in file, duplicates in DB)
5. Reference validation (FK entities exist — via `GlobalFunctions`)

Collect ALL errors; never throw on first error.

### Document Proxy (IP Restrictions)

The Blazor server proxies document downloads through to the API to bypass Azure IP restrictions:

```csharp
// PetelATH.BlazorServer/Program.cs
app.MapDocumentProxy();  // From Petel.BlazorCore.Extensions
```

Frontend uses `/api/documents/{id}/proxy` — no changes needed in JavaScript.

### Modal Form Layout

Group related fields side-by-side using flexbox:

```html
<div style="display: flex; gap: 15px; margin-bottom: 15px;">
    <div style="flex: 1;">
        <label>שעות שבועיות: <span style="color: red;">*</span></label>
        <input type="number" id="programHours" required style="width: 100%;">
    </div>
    <div style="flex: 1;">
        <label>מספר מפגשים: <span style="color: red;">*</span></label>
        <input type="number" id="programSessions" required style="width: 100%;">
        <small id="sessionsRemark" style="color: #6c757d;">טוען...</small>
    </div>
</div>
```

Load contextual hints from backend attributes — never hardcode a default number in the UI.

## Adding a New Page — Checklist

1. ✅ Create `wwwroot/newpage.html`
2. ✅ Add to `page-lifecycle-config.js` (file, title, cleanup, init, selfInitializing)
3. ✅ Add to `petel_schema.menu_items` (SQL)
4. ✅ Add navigation rules if page uses session keys
5. ✅ Implement `cleanupNewPage()` and export: `window.cleanupNewPage = cleanupNewPage`
6. ✅ Use `window` scope for all component variables
7. ✅ Navigate via `window.navigateTo('newpage')`

## Deployment

```powershell
.\Deploy-ATH.ps1 -Environment production          # Full deploy
.\Deploy-ATH.ps1 -Environment test                # Test environment
.\Deploy-ATH.ps1 -Environment production -ApiOnly  # API only
.\Deploy-ATH.ps1 -Environment production -BlazorOnly
.\Deploy-ATH.ps1 -Environment production -SkipBuild
```

**Azure Resources (israelcentral)**:

| Environment | Resource Group | API App | Blazor App |
|---|---|---|---|
| Test | `petel-test-rg` | `petel-test-api` | `petel-test-blazor` |
| Staging | `petel-staging-rg` | `petel-staging-api` | `petel-staging-blazor` |
| Production | `petel-prod-rg` | `petel-prod-api` | `petel-prod-blazor` |

**Runtime versions**: API = `DOTNETCORE:9.0`, Blazor = `DOTNETCORE:9.0` (update deploy script if still showing 8.0)

**Checklist before deploy**:
1. ✅ `appsettings.Production.json` has correct `ApiSettings.BaseUrl`
2. ✅ `ASPNETCORE_ENVIRONMENT=Production` set in Azure App Service
3. ✅ CSP `connect-src` includes API origin (auto-derived from `ApiSettings.BaseUrl`)
4. ✅ JWT secret key loaded from Azure Key Vault / App Service config
5. ✅ Email credentials (Gmail App Password) loaded from Azure Key Vault
6. ✅ `Security.OtpEnabled = true` in `appsettings.test.json` and `appsettings.Production.json`

## Authentication & Email OTP

### Feature Flag

`Security.OtpEnabled` in each API `appsettings*.json` controls whether the email OTP step is required after login:

| File | `OtpEnabled` | Reason |
|---|---|---|
| `appsettings.Development.json` | `false` | Skip OTP locally — faster dev cycle |
| `appsettings.test.json` | `true` | Test full 2FA flow |
| `appsettings.Production.json` | `true` | Always required in production |

Can also be toggled at runtime via the `Security_OtpEnabled` system attribute (DB wins over config):
```sql
UPDATE petel_schema.system_attributes SET value = 'false' WHERE name = 'Security_OtpEnabled';
-- Then call: POST /api/systemattributes/reload
```

### Email Service — DI Registration (Program.cs)

```csharp
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("Email"));
builder.Services.AddSingleton<IEmailService, SmtpEmailService>();
```

Key files:
- `Configuration/EmailSettings.cs` — POCO bound to `"Email"` config section
- `Services/IEmailService.cs` — single method `Task SendOtpAsync(string toEmail, string code, string userName)`
- `Services/SmtpEmailService.cs` — Gmail SMTP via MailKit; also contains `public static string MaskEmail(string email)`

### OTP API Endpoints

```
POST /api/otp/send       { TempToken }              → { Success, MaskedEmail }
POST /api/otp/validate   { TempToken, Code }        → LoginResponse
POST /api/otp/disable    { TempToken, Password }    → { Success }
GET  /api/otp/status     Bearer <token>             → { OtpEnabled }
```

### Database Columns

Three columns on `petel_schema.users` (added by `SQL/add-email-otp-columns.sql`):

| Column | Type | Notes |
|---|---|---|
| `email_otp_code` | `VARCHAR(100) NULL` | BCrypt hash — never plaintext |
| `email_otp_expiry` | `TIMESTAMPTZ NULL` | 10 min after code issued |
| `email_otp_attempts` | `INTEGER NOT NULL DEFAULT 0` | Cleared on success / new code |

Old TOTP columns (`otp_secret`, `otp_enabled`, `otp_verified`) are retained for rollback but are unused.

### Login Flow Summary

```
POST /api/auth/login
  → RequiresPasswordChange → change-password modal (checked first)
  → RequiresOtp            → email OTP modal (TempToken + MaskedEmail returned)
  → Success                → navigate to /maindashboard
```

`Login.razor` state: `_requiresOtp`, `_maskedEmail`, `_tempToken`, `_otpCode`. "שלח שוב" button calls `POST /api/otp/send` to resend.

## Common ATH Issues

**`relation "table_name" does not exist`**: Verify `HasDefaultSchema("petel_schema")` is in `OnModelCreating` and no entity has a hardcoded `Schema = "petel_schema"` attribute.

**Council dropdown shows `undefined`**: Use `councilName` not `councilShortName`. The API returns `councilName`.

**Page variables redeclare on re-entry**: Use `window.myVar` not `let myVar` at script scope.

**Hebrew text mismatch in Excel import**: Use `GlobalFunctions.PureHebrewText()` for normalization before comparing to DB values.
