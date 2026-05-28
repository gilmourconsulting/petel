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
  PetelATH.BlazorServer/        ← Frontend (Blazor Server, net9.0)
    Components/
      Pages/                    ← Blazor pages (Login.razor, Students.razor, SchoolDetails.razor, etc.)
      Layout/                   ← MainLayout.razor, NavMenu.razor (DB-driven), EmptyLayout.razor
      Shared/                   ← Reusable components (SecureButton.razor, SchoolTracksTable.razor, etc.)
      Modals/                   ← Modal components (StudentUploadModal.razor, etc.)
      Security/                 ← AuthenticationGuard.razor
    DTOs/                       ← Blazor-side data transfer objects (~33 DTOs)
    Services/                   ← ApiService, TokenService, ActionSecurityService
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

// DocumentTemplateEngine — Word/DOCX generation via MiniWord (Petel.Core)
builder.Services.AddScoped<Petel.Core.Documents.DocumentTemplateEngine>();

// DocumentTemplateService — scans .docx for {{placeholder}} tokens (Petel.Core)
builder.Services.AddScoped<Petel.Core.Documents.DocumentTemplateService>();

// Wire JwtTokenService into UserSessionService after build
using (var scope = app.Services.CreateScope())
{
    var jwtService = scope.ServiceProvider.GetRequiredService<JwtTokenService>();
    var sessionService = scope.ServiceProvider.GetRequiredService<UserSessionService>();
    sessionService.SetJwtTokenService(jwtService);
}
```

## Frontend Architecture

PetelATH uses a **pure Blazor Server** frontend. There are no HTML files, no JavaScript SPA, and no `page-lifecycle-config.js`. Everything is `.razor` components rendered on the server with `@rendermode InteractiveServer`.

### Component Hierarchy

```
App.razor                     ← Root HTML shell + Blazor script injection
  Routes.razor                ← Router; binds MainLayout as default
    Layout/
      MainLayout.razor        ← Top bar, side menu, AuthenticationGuard wrapper
        NavMenu.razor         ← DB-driven side navigation (loads from MenuController)
        AuthenticationGuard.razor  ← Redirects to /login if not authenticated
      EmptyLayout.razor       ← Used by Login.razor (no nav)
    Pages/                    ← One .razor file per page route
      Login.razor             (@page "/login")
      MainDashboard.razor     (@page "/maindashboard")
      Students.razor          (@page "/students")
      SchoolDetails.razor     (@page "/schooldetails")
      ...                     (32+ pages total)
    Shared/                   ← Reusable components
      SecureButton.razor      ← Permission-gated button
      SchoolTracksTable.razor ← Sortable table component
      SortableTableBase.cs    ← Base class for table components
      ...
    Modals/                   ← Modal dialog components
      StudentUploadModal.razor
      AddSchoolModal.razor
      ...
    Security/
      AuthenticationGuard.razor
```

### Database-Driven Menu System

Menu items live in `petel_schema.menu_items`. `NavMenu.razor` loads them via `ApiService.GetAsync<List<MenuItemDto>>("menu")` on init, then navigates using `NavigationManager.NavigateTo()`.

Adding a new menu item:

```sql
INSERT INTO petel_schema.menu_items (name, reference, text, sort_order, is_active)
VALUES ('newpage', '/newpage', 'כותרת עברית', 100, true);
```

Note: `reference` must be a Blazor route path (e.g. `/newpage`), not a hash fragment.

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

### Standard Icon Set

All icons are PNG files in `/images/`. Use `<img src="/images/view_icon.png" alt="צפייה" class="action-icon-natural">` in Razor markup. Do **not** use emoji.

- `/images/view_icon.png` — view/preview
- `/images/edit_icon.png` — edit
- `/images/delete_icon.png` — delete
- `/images/download_icon.png` — download
- `/images/upload_icon.png` — upload
- `/images/stats_icon.png` — statistics
- `/images/Plus icon.png` — add new

```css
.btn-icon { padding: 4px 6px; border: 1px solid #dee2e6; border-radius: 4px; background: transparent; cursor: pointer; }
.action-icon-natural { width: 15px; height: 15px; object-fit: contain; }
```

### Table Horizontal Scrolling

All table containers must support horizontal scrolling:
```css
.table-container { overflow-x: auto; overflow-y: visible; }
.data-table { min-width: 1200px; white-space: nowrap; }

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

Fetch from Blazor frontend:
```csharp
// In a Razor page @code block
var attr = await ApiService.GetAsync<SchoolYearAttributeDto>(
    $"schoolyearattributes/year/{yearId}/attribute/additional_study_sessions_required");
if (attr != null)
    _sessionsRemark = $"מספר מפגשים נדרש: {attr.Value}";
```

### Council Entity Structure

The `petel_schema.councils` table is the master list of Israeli local authorities. It now includes three additional fields:

| Column | Type | Description |
|---|---|---|
| `long_name` | `VARCHAR(100) NULL` | Full name for templates and screens (שם מלא) |
| `council_type_id` | `INT NULL FK` | FK → `council_types.id` (סוג רשות) |
| `district_id` | `INT NULL FK` | FK → `districts.id` (מחוז) |

**Lookup tables** (seeded once, rarely change):

- `council_types` — עירייה · מועצה מקומית · מועצה אזורית
- `districts` — חיפה · הדרום · תל אביב · המרכז · אזור יהודה והשומרון · ירושלים · הצפון

Both tables have `id`, `name`, `sort_order`, `is_active`, and full audit fields.

**Entity model** (`Data/Council.cs`):
```csharp
// New fields on Council:
public string? LongName { get; set; }       // long_name — use in templates
public int? CouncilTypeId { get; set; }     // FK
public int? DistrictId { get; set; }        // FK
public virtual CouncilType? CouncilType { get; set; }
public virtual District? District { get; set; }
```

**API endpoints** (all in `SystemAttributesController`):

| Endpoint | Auth | Returns |
|---|---|---|
| `GET /api/systemattributes/councils` | None | `{ id, councilName, councilCode }` — existing, unchanged |
| `GET /api/systemattributes/councils/extended` | Required | `{ id, councilCode, name, longName, councilTypeId, councilTypeName, districtId, districtName }` |
| `GET /api/systemattributes/council-types` | None | `{ id, name }` list |
| `GET /api/systemattributes/districts` | None | `{ id, name }` list |

**SQL migration**: `SQL/add-council-type-district.sql` — idempotent, creates the two tables with seed data and adds the three columns to `councils`.

**EF migration**: `Migrations/20260528120000_AddCouncilTypeAndDistrict.cs`

**Anti-patterns**:
```csharp
// ❌ WRONG — extended data via old endpoint
var councils = await ApiService.GetAsync<List<CouncilDto>>("systemattributes/councils");
var typeName = councils[0].CouncilTypeName;  // field doesn't exist here

// ✅ CORRECT — use extended endpoint when type/district/longName is needed
var councils = await ApiService.GetAsync<ApiResponse<List<CouncilExtendedDto>>>("systemattributes/councils/extended");
```

### Entity Type System

The `petel_schema.entities` table stores all entities (schools, councils/local authorities, networks, etc.). The `entity_type_id` column identifies the kind of entity:

| `entity_type_id` | Type | Description |
|---|---|---|
| 1 | School | An individual school (בית ספר) |
| 2 | Council | A local authority / municipality (רשות מקומית) |
| 3 | Network | An educational network / owner org (רשת / בעלות) |

**Critical**: The `entities.council` column (mapped as `CouncilId` in `Entity.cs`) is a FK to `councils.id`. This column is set on **multiple entity types** — for example, school entities also have `CouncilId` set to indicate which council they belong to. Therefore:

> **Always filter by `EntityTypeId == 2` when looking up the entity that *represents* a council.**

```csharp
// ✅ CORRECT — only council-type entities
var councilEntityMap = (await context.Entities
    .AsNoTracking()
    .Where(e => e.EntityTypeId == 2 && e.CouncilId.HasValue && councilIds.Contains(e.CouncilId.Value))
    .Select(e => new { e.CouncilId, e.Id })
    .ToListAsync())
    .GroupBy(e => e.CouncilId!.Value)
    .ToDictionary(g => g.Key, g => g.First().Id);

// ❌ WRONG — returns school entities too (both have CouncilId set)
var councilEntityMap = (await context.Entities
    .AsNoTracking()
    .Where(e => e.CouncilId.HasValue && councilIds.Contains(e.CouncilId.Value))  // Missing EntityTypeId == 2!
    ...
```

Council entities are created in `TransactionAccountsController.CreateCouncilEntity` with `EntityTypeId = 2, CouncilId = request.CouncilId`.

### Document Links — Council Entity Pattern

When generating a per-council document (e.g. `CouncilExcelGenerationService`), the document must be linked to both:
1. The **owner entity** (the network/authority that ran the generation, `entityId` parameter)
2. The **council entity** — the type-2 entity whose `CouncilId` matches the council being processed

Build the council→entity map once before the processing loop, then use it per iteration:

```csharp
// Step 1: Build map ONCE before loop (filter EntityTypeId == 2 is mandatory)
var councilIds = councils.Select(c => c.CouncilId).Distinct().ToList();
var councilEntityMap = (await context.Entities
    .AsNoTracking()
    .Where(e => e.EntityTypeId == 2 && e.CouncilId.HasValue && councilIds.Contains(e.CouncilId.Value))
    .Select(e => new { e.CouncilId, e.Id })
    .ToListAsync())
    .GroupBy(e => e.CouncilId!.Value)
    .ToDictionary(g => g.Key, g => g.First().Id);

// Step 2: Per document, add both links
context.Set<DocumentLink>().Add(new DocumentLink { DocumentId = document.Id, EntityId = entityId });     // owner
if (councilEntityMap.TryGetValue(council.CouncilId, out var councilEntityId))
{
    context.Set<DocumentLink>().Add(new DocumentLink { DocumentId = document.Id, EntityId = councilEntityId });  // council
}
else
{
    _logger.LogWarning("No type-2 entity found for councilId={CouncilId}", council.CouncilId);
}
```

On **update** (new version of existing document), copy all links from the old version, then also ensure the council entity link exists (it may be missing if the old version predates this pattern):

```csharp
foreach (var link in existingDoc.DocumentLinks)
    context.Set<DocumentLink>().Add(new DocumentLink { DocumentId = newVersion.Id, EntityId = link.EntityId, ... });

if (councilEntityMap.TryGetValue(council.CouncilId, out var councilEntityId))
{
    bool alreadyLinked = existingDoc.DocumentLinks.Any(dl => dl.EntityId == councilEntityId);
    if (!alreadyLinked)
        context.Set<DocumentLink>().Add(new DocumentLink { DocumentId = newVersion.Id, EntityId = councilEntityId });
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

### Report & Document Generation System

PetelATH supports generating both Excel (`.xlsx`) and Word (`.docx`) documents from database-driven templates. Both formats share the same tables, controllers, and Blazor pages — the `format` column on `report_definitions` controls which engine is used.

#### Database Tables

| Table | EF Model Class | Description |
|---|---|---|
| `report_definitions` | `ReportDefinition` | Report metadata + `format` (`"excel"` \| `"word"`) |
| `report_queries` | `ReportQuery` | SQL/data-source query per report |
| `report_templates` | `ReportTemplate` | Template file blob (`.xlsx` or `.docx`) |
| `report_parameters` | `ReportParameter` | Runtime parameter definitions |

**SQL migration**: `SQL/rename-report-tables.sql` — renames `excel_report_*` → `report_*` and adds `format` column. Idempotent.

#### Blazor Pages

| Page | Route | Purpose |
|---|---|---|
| `Reports.razor` | `/reports` | List / manage report definitions |
| `ReportBuilder.razor` | `/reports/{id}/builder` | Configure data sources and queries |
| `ReportTemplateMapping.razor` | `/reports/{id}/template` | Map placeholders to data |

Menu item: `name='reports'`, `reference='/reports'` (see `SQL/update-reports-menu-item.sql`).

#### API Controllers

| Controller | Route | Responsibility |
|---|---|---|
| `ReportsController` | `api/reports` | CRUD for definitions, trigger generation |
| `ReportTemplatesController` | `api/reporttemplates` | Upload/download/scan template files |

`ReportsController.GenerateReport` branches on `report.Format`:
- `"word"` → calls `GenerateWordTemplateReportAsync()`, returns `.docx` with Word MIME type
- else → calls `GenerateTemplateReportAsync()`, returns `.xlsx`

`ReportTemplatesController` accepts `.xlsx` and `.docx` on upload; routes placeholder scanning to the correct service based on filename extension.

#### Shared Services (`Petel.Core/Documents/`)

| Class | Purpose |
|---|---|
| `DocumentTemplateEngine` | Generates `.docx` from a template blob using MiniWord. Uses temp files internally — never expose the temp path. |
| `DocumentTemplateService` | Scans a `.docx` blob for `{{...}}` placeholder tokens (for template mapping UI). |

**Word template syntax**:
- `{{DataSourceName_FieldName}}` — scalar value (single-row dataset)
- `{{listName}}` in a table row — collection binding (dataset name is the list key)

#### Anti-Patterns

```csharp
// ❌ WRONG — old names (all removed)
_context.ExcelReportDefinitions
new ExcelReportDefinition()

// ✅ CORRECT — unified names
_context.ReportDefinitions
new ReportDefinition { Format = "excel" }   // or "word"

// ❌ WRONG — hardcoded format check bypasses engine routing
if (fileName.EndsWith(".docx")) { ... }  // NO — use report.Format from DB

// ✅ CORRECT — engine selected via Format field
if (report.Format == "word")
    return await GenerateWordTemplateReportAsync(...);
```

### Document Proxy (IP Restrictions)

The Blazor server proxies document downloads through to the API to bypass Azure IP restrictions:

```csharp
// PetelATH.BlazorServer/Program.cs
app.MapDocumentProxy();  // From Petel.BlazorCore.Extensions
```

Blazor components download documents via `@inject IJSRuntime JSRuntime` calling `BlazorHelpers.viewFileWithAuth` with the proxy URL `/api/documents/{id}/proxy`. The user's JWT token is forwarded server-side.

### Modal Form Layout

Group related fields side-by-side using flexbox in Razor markup:

```razor
<div style="display: flex; gap: 15px; margin-bottom: 15px;">
    <div style="flex: 1;">
        <label>שעות שבועיות: <span style="color: red;">*</span></label>
        <input type="number" @bind="_programHours" style="width: 100%;" />
    </div>
    <div style="flex: 1;">
        <label>מספר מפגשים: <span style="color: red;">*</span></label>
        <input type="number" @bind="_programSessions" style="width: 100%;" />
        <small style="color: #6c757d;">@_sessionsRemark</small>
    </div>
</div>
```

Load contextual hints from backend attributes on modal open — never hardcode a default number.

## Adding a New Blazor Page — Checklist

1. ✅ Create `Components/Pages/NewPage.razor` with `@page "/newpage"` and `@layout MainLayout`
2. ✅ Inherit `SecurePageBase`: `@inherits SecurePageBase`
3. ✅ Implement `protected override string PageName => "newpage";`
4. ✅ Override `OnPageInitializedAsync()` for data loading (not `OnInitializedAsync`)
5. ✅ Create DTOs in `DTOs/NewPageDtos.cs` matching the API response shape
6. ✅ Add API controller endpoint inheriting `BaseController`
7. ✅ Insert DB menu item:
   ```sql
   INSERT INTO petel_schema.menu_items (name, reference, text, sort_order, is_active)
   VALUES ('newpage', '/newpage', 'כותרת בעברית', 100, true);
   ```
8. ✅ Wrap action buttons in `<SecureButton>` with `ActionName`, `ScreenName`, `OnClick`
9. ✅ Use `ApiService.GetAsync<T>` / `PostAsync<Req,Resp>` for all API calls
10. ✅ Use `@inject NavigationManager Navigation` + `Navigation.NavigateTo("/otherpage")` for navigation

**That's it!** No changes to `NavMenu.razor` needed — the menu item is DB-driven.

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

## Excel Report Generation System

A definition-driven engine for generating Excel files from live database data. Supports three report types; the **template** type is the most powerful and covers complex formatted reports like council-student lists.

### Architecture Overview

```
Blazor UI (ExcelReports.razor)
  └─ POST /api/excelreports/{id}/generate  { RuntimeParams }
       └─ ExcelReportsController
            ├─ query_builder / advanced_sql → ExcelGenerationService (simple tabular export)
            └─ template → ReportTemplateEngine
                 ├─ Reads definition_json → ReportDefinition (parameters + dataSources)
                 ├─ For each dataSource → AthExcelEntityRegistry.QueryEntityAsync()
                 └─ Fills template.xlsx blob → returns filled .xlsx bytes
```

### Database Tables

Created by `SQL/add-excel-reports.sql` (idempotent). Run on every environment before using reports.

| Table | Purpose |
|---|---|
| `petel_schema.excel_report_definitions` | Report metadata, `report_type`, `definition_json` |
| `petel_schema.excel_report_queries` | Query config for `query_builder` / `advanced_sql` type |
| `petel_schema.excel_report_templates` | Template .xlsx blob + `cell_mappings_json` (NOT NULL, default `'[]'`) |
| `petel_schema.excel_report_parameters` | DB-stored parameter schema (used when no `definition_json`) |

**CRITICAL**: `excel_report_templates.cell_mappings_json` is `TEXT NOT NULL DEFAULT '[]'`. Always set `CellMappingsJson = "[]"` — never `null` — in C# code.

Also run `SQL/add-definition-json-column.sql` to add the `definition_json TEXT NULL` column to `excel_report_definitions` (if not already present).

### Report Types

| Type | Description |
|---|---|
| `query_builder` | Entity + field/filter/sort config; generates a simple auto-formatted table |
| `advanced_sql` | Raw SQL query; generates a simple table |
| `template` | Designer uploads an .xlsx template; engine fills tokens and expands collection rows |

### Template Syntax (for `template` type)

The `ReportTemplateEngine` (in `shared/Petel.Core/Excel/ReportTemplateEngine.cs`) processes an .xlsx file:

**Scalar placeholders** — replaced with a single value from a data source:
```
{{dsName.FieldName}}
```

**Collection block** — expands one row per record. Three consecutive rows:
```
Row N:   {{#dsName}}           ← start marker row (deleted after expansion)
Row N+1: {{dsName.Field}} ...  ← template data row (styles copied per record)
Row N+2: {{/dsName}}           ← end marker row (deleted after expansion)
```

After expansion, any SUM formulas below the block auto-adjust because EPPlus `InsertRow` shifts them.

**Value formatting rules:**
- `null` → empty string
- `DateOnly` / `DateTime` → `dd/MM/yyyy`
- `decimal` → `N2` (two decimal places)
- `bool` → `כן` / `לא`
- Single-token cells: raw typed value is preserved so Excel treats numbers/dates correctly in formulas

### Definition JSON (`definition_json` column)

Stored as JSON in `excel_report_definitions.definition_json`. Parsed at generation time by `ReportTemplateEngine`.

```json
{
  "parameters": [
    {
      "name": "hebrew_year_id",
      "type": "year_selector",
      "label": "שנת לימודים",
      "required": true
    },
    {
      "name": "sending_council_id",
      "type": "council_selector",
      "label": "רשות שולחת",
      "required": true
    }
  ],
  "dataSources": [
    {
      "name": "header",
      "entity": "OwnerEntity",
      "type": "scalar",
      "filters": [],
      "sort": []
    },
    {
      "name": "students",
      "entity": "StudentsWithSchool",
      "type": "collection",
      "filters": [
        { "field": "SendingCouncil", "operator": "eq", "paramName": "sending_council_id" }
      ],
      "sort": [
        { "field": "SchoolName", "direction": "asc" },
        { "field": "LastName",   "direction": "asc" }
      ]
    }
  ]
}
```

**Parameter types:** `session_entity`, `session_year`, `year_selector`, `council_selector`, `entity_selector`, `school_selector`, `text`, `enum`, `number`

- `session_entity` and `session_year` are resolved automatically from the logged-in session — they are never shown to the user in the run modal.
- All other types render a UI input in the "Run Report" modal.

**DataSource types:** `scalar` (first row only, for header/context data), `collection` (all rows, expanded in template).

**Filter operators** applied in-memory: `eq`, `neq`, `contains`, `gt`, `lt`, `gte`, `lte`

### Entity Registry (`AthExcelEntityRegistry`)

`PetelATH.Api/Services/AthExcelEntityRegistry.cs` implements `IExcelEntityRegistry` from `Petel.Core`. It defines all exportable entities and handles data retrieval scoped to the current entity/year.

**Available entities:**

| Entity Name | Description | Scope |
|---|---|---|
| `Students` | Students for current entity + year | School / Council |
| `Schools` | Schools linked to current entity | Council / Admin |
| `SchoolClasses` | Classes for current entity + year | School |
| `AdditionalStudyPrograms` | Additional study programs | School |
| `Transactions` | Financial transactions (cross-year ok) | School |
| `TransactionAccounts` | Transaction accounts (cross-year ok) | School |
| `OwnerEntity` | The logged-in user's organisation (scalar) | Any |
| `Council` | All councils (for council selector filters) | Any |
| `StudentsWithSchool` | Students joined with school name + council | Council / Admin |

#### Adding a New Entity to the Registry

1. Add a `Query*Async` private method to `AthExcelEntityRegistry` returning `List<Dictionary<string, object?>>` where keys are PascalCase field names.
2. Add a case to the `switch` in `QueryEntityAsync`.
3. Add an `ExcelEntityDescriptor` entry in `BuildDescriptors()` listing all fields with Hebrew labels.
4. For `definition_json` reports, use the entity name in `dataSources[].entity`.

```csharp
// Step 1 — query method
private async Task<List<Dictionary<string, object?>>> QueryMyEntityAsync(
    ExcelEntityContext context, CancellationToken ct)
{
    var rows = await _context.MyEntities
        .AsNoTracking()
        .Where(e => e.EntityId == context.EntityId)
        .Select(e => new Dictionary<string, object?> {
            ["Id"]   = (object?)e.Id,
            ["Name"] = e.Name
        })
        .ToListAsync(ct);
    return rows;
}

// Step 2 — switch case
"MyEntity" => await QueryMyEntityAsync(context, cancellationToken),

// Step 3 — descriptor
new ExcelEntityDescriptor
{
    Name           = "MyEntity",
    LabelHe        = "ישות שלי",
    IsAccountEntity = false,
    Fields         = new List<ExcelEntityFieldDescriptor>
    {
        new() { Name = "Id",   LabelHe = "מזהה",  FieldType = "number" },
        new() { Name = "Name", LabelHe = "שם",    FieldType = "text"   }
    }
}
```

### Adding a New Template Report — Checklist

1. ✅ Design the .xlsx template with token cells and `{{#ds}}` / `{{/ds}}` rows
2. ✅ Write the `definition_json` (parameters + dataSources)
3. ✅ Insert the report record into the DB (see `SQL/insert-council-students-report.sql` as example):
   ```sql
   INSERT INTO petel_schema.excel_report_definitions
       (name, description, report_type, allow_cross_year, requires_entity_context,
        is_active, sort_order, definition_json)
   VALUES ('שם הדוח', 'תיאור', 'template', false, true, true, 10, '<definition_json>');
   ```
4. ✅ In the Blazor UI → Excel Reports page → Edit → upload the .xlsx template file
5. ✅ Run the report via the "Run" button — the engine fills and downloads the file

### DI Registration (Program.cs)

```csharp
// Scoped — one per request
builder.Services.AddScoped<ExcelTemplateService>();
builder.Services.AddScoped<IExcelEntityRegistry, AthExcelEntityRegistry>();
builder.Services.AddScoped<ExcelGenerationService>();
builder.Services.AddScoped<ReportTemplateEngine>();
```

### API Endpoints

| Method | Route | Purpose |
|---|---|---|
| `GET` | `/api/excelreports` | List all active reports |
| `POST` | `/api/excelreports` | Create report definition |
| `PUT` | `/api/excelreports/{id}` | Update report definition |
| `DELETE` | `/api/excelreports/{id}` | Soft delete |
| `GET` | `/api/excelreports/{id}/params` | Get parameter schema for run modal |
| `POST` | `/api/excelreports/{id}/generate` | Generate and download .xlsx |
| `POST` | `/api/excelreports/preview` | Preview first 10 rows as JSON |
| `GET` | `/api/excelreports/entities` | List all registry entities |
| `GET` | `/api/excelreports/entities/{name}/fields` | Get field descriptors |
| `POST` | `/api/excelreporttemplates/{id}/upload` | Upload .xlsx template |
| `GET` | `/api/excelreporttemplates/{id}/download` | Download template file |
| `GET` | `/api/excelreporttemplates/{id}/scan` | Scan template placeholders |
| `PUT` | `/api/excelreporttemplates/{id}/mappings` | Save cell mapping JSON |

### Sample Report: Council Students

`SQL/insert-council-students-report.sql` + `SQL/Templates/council-students-report-definition.json` + `SQL/Templates/council-students-template.xlsx`

Parameters: `hebrew_year_id` (year_selector), `sending_council_id` (council_selector)

Data sources: `header` (OwnerEntity scalar), `council` (Council scalar filtered by council_id), `students` (StudentsWithSchool collection filtered + sorted)

### Common Issues

**500 on template upload**: Usually means `excel_report_templates` table does not exist. Run `SQL/add-excel-reports.sql`.

**`CellMappingsJson null constraint violation`**: The column is `NOT NULL`. Always use `CellMappingsJson = "[]"` in both INSERT and UPDATE branches of `UploadTemplate`.

**`definition_json column does not exist`**: Run `SQL/add-definition-json-column.sql`.

**Template tokens not replaced**: Verify `dsName` in the token (`{{dsName.FieldName}}`) exactly matches the `name` property in the matching `dataSources[]` entry of `definition_json` (case-insensitive at runtime).

**Collection block not expanded**: The `{{#dsName}}` and `{{/dsName}}` marker rows must each exist as cells. The template data row must be immediately below the start marker.

---

## Common ATH Issues

**`relation "table_name" does not exist`**: Verify `HasDefaultSchema("petel_schema")` is in `OnModelCreating` and no entity has a hardcoded `Schema = "petel_schema"` attribute.

**Council dropdown shows `undefined`**: Use `councilName` not `councilShortName`. The API returns `councilName`.

**Page variables redeclare on re-entry**: Use `window.myVar` not `let myVar` at script scope.

**Hebrew text mismatch in Excel import**: Use `GlobalFunctions.PureHebrewText()` for normalization before comparing to DB values.
