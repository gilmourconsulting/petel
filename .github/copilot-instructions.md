# Petel Educational Management System - AI Coding Guide

## Architecture Overview

**Educational Management System**: .NET 9 Web API backend + Blazor Server frontend for Hebrew schools/educational institutions.

- **Backend**: ASP.NET Core Web API (`PetelApp.Api/`) with PostgreSQL + Entity Framework Core
- **Frontend**: Blazor Server (`PetelApp.BlazorServer/`) with Hebrew RTL support and interactive UI
- **Database**: PostgreSQL with `petel_schema` namespace
- **Background Jobs**: Hangfire for system attribute loading and scheduled tasks

**Note**: The old vanilla JS frontend (`petelapp-frontend/`) has been archived and replaced with Blazor Server.

## Critical Development Workflows

### Local Development Setup
```bash
# Start backend API (from root)
cd PetelApp.Api && dotnet run
# OR: double-click "Start Local Api.cmd"

# Start Blazor frontend (from root) 
cd PetelApp.BlazorServer && dotnet run
# OR: double-click "Start Blazor Server.cmd"
```

Backend API runs on `http://localhost:5082`, Blazor frontend runs on `https://localhost:5001` or `http://localhost:5000`

## Configuration Management

### Application Configuration Pattern

**CRITICAL**: All environment-specific settings must be externalized to configuration files - **NEVER hardcoded**.

#### Backend Configuration Requirements

**1. Database Configuration**

All database settings must be in `appsettings.json` and `appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=petelappdb;Username=PetelAdmin;Password=..."
  },
  "Database": {
    "SchemaName": "petel_schema"
  }
}
```

**2. Database Schema Configuration Pattern**

**CRITICAL**: Use `HasDefaultSchema()` for all entities - DO NOT hardcode schema names.

```csharp
// ✅ CORRECT - AppDbContext.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PetelApp.Api.Configuration;

public class AppDbContext : DbContext
{
    private readonly string _schemaName;

    public AppDbContext(
        DbContextOptions<AppDbContext> options,
        IOptions<DatabaseSettings> dbSettings) 
        : base(options)
    {
        _schemaName = dbSettings.Value.SchemaName;
    }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ✅ Set default schema ONCE - applies to ALL entities
        modelBuilder.HasDefaultSchema(_schemaName);

        // ✅ Configure entities WITHOUT schema parameter
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");  // Schema from HasDefaultSchema
            entity.HasIndex(e => e.Username).IsUnique();
        });

        modelBuilder.Entity<School>(entity =>
        {
            entity.ToTable("schools");  // Schema from HasDefaultSchema
        });
    }
}
```

**3. Entity Class Pattern**

```csharp
// ✅ CORRECT - Entity class (School.cs, User.cs, etc.)
[Table("schools")]  // ✅ Table name only - NO schema parameter
public class School
{
    [Key]
    [Column("id")]
    public int Id { get; set; }
}

// ❌ WRONG - DO NOT include schema in attribute
[Table("schools", Schema = "petel_schema")]  // NO!
```

**4. Configuration Class Pattern**

```csharp
// Configuration/DatabaseSettings.cs
namespace PetelApp.Api.Configuration
{
    public class DatabaseSettings
    {
        public string SchemaName { get; set; } = "petel_schema";
    }
}
```

**5. Program.cs Registration**

```csharp
// Required using statements
using Microsoft.Extensions.Options;
using PetelApp.Api.Configuration;

// Register configuration
builder.Services.Configure<DatabaseSettings>(
    builder.Configuration.GetSection("Database"));

// Inject into DbContext
builder.Services.AddDbContext<AppDbContext>((serviceProvider, options) =>
{
    var dbSettings = serviceProvider.GetRequiredService<IOptions<DatabaseSettings>>().Value;
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsqlOptions => npgsqlOptions.MigrationsHistoryTable(
            "__EFMigrationsHistory", 
            dbSettings.SchemaName
        )
    );
});
```

**6. Required Using Statements in AppDbContext**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;  // ✅ Required for IOptions<T>
using PetelApp.Api.Configuration;    // ✅ Required for DatabaseSettings
```

#### Blazor Frontend Configuration Requirements

**1. Environment Configuration Pattern**

**CRITICAL**: Blazor API URLs must be in appsettings files - **NEVER hardcoded**.

```json
// ✅ CORRECT - appsettings.Development.json
{
  "ApiSettings": {
    "BaseUrl": "http://localhost:5082/api",
    "Timeout": 30
  }
}

// appsettings.Staging.json
{
  "ApiSettings": {
    "BaseUrl": "https://petel-test-api.azurewebsites.net/api",
    "Timeout": 30
  },
  "Security": {
    "Csp": {
      "ImgSrc": ["https://api.qrserver.com"]
    }
  }
}

// appsettings.Production.json
{
  "ApiSettings": {
    "BaseUrl": "https://petel-prod-api.azurewebsites.net/api",
    "Timeout": 30
  },
  "Security": {
    "Csp": {
      "ImgSrc": ["https://api.qrserver.com"]
    }
  }
}
```

**2. Content Security Policy Configuration**

CSP is automatically configured in `Program.cs` to include the API origin:

```csharp
// Build connect-src directive to include API URL
var apiBaseUrl = apiSettings?.BaseUrl ?? "";
var apiOrigin = "";
if (!string.IsNullOrEmpty(apiBaseUrl))
{
    var uri = new Uri(apiBaseUrl);
    apiOrigin = $"{uri.Scheme}://{uri.Host}";
}
var cspConnectSrcDirective = string.IsNullOrEmpty(apiOrigin) 
    ? "connect-src 'self'" 
    : $"connect-src 'self' {apiOrigin}";
```

**3. Anti-Patterns to Avoid**

```csharp
// ❌ WRONG - Hardcoded API URL in code
var apiUrl = "http://localhost:5082/api";  // NO!

// ❌ WRONG - Hardcoded CSP without API origin
context.Response.Headers.Add("Content-Security-Policy", 
    "connect-src 'self'");  // NO! This blocks external API calls

// ✅ CORRECT - Use configuration
@inject IOptions<ApiSettings> ApiSettings

var response = await ApiService.GetAsync<DataModel>("endpoint");
```

### Configuration Checklist for New Features

When adding new features, verify:

**Backend:**
1. ✅ Database connection string in `appsettings.json`
2. ✅ Schema name in `DatabaseSettings` configuration
3. ✅ `HasDefaultSchema(_schemaName)` in `AppDbContext`
4. ✅ Entity `[Table]` attributes have NO schema parameter
5. ✅ All `entity.ToTable()` calls have NO schema parameter
6. ✅ Required using statements in `AppDbContext`
7. ✅ `IOptions<DatabaseSettings>` injected into `AppDbContext` constructor

**Blazor Frontend:**
1. ✅ API URLs configured in `appsettings.{Environment}.json`
2. ✅ NO hardcoded URLs anywhere in code
3. ✅ Environment-specific appsettings files exist
4. ✅ CSP directives include API origin for `connect-src`

### Deployment Configuration

**Unified Deployment Script**: `Deploy-ToAzure.ps1`

The application uses a single, flexible PowerShell deployment script for all environments:

```powershell
# Deploy both API and Blazor to production
.\Deploy-ToAzure.ps1 -Environment production

# Deploy to test environment
.\Deploy-ToAzure.ps1 -Environment test

# Deploy to staging
.\Deploy-ToAzure.ps1 -Environment staging

# Deploy only API
.\Deploy-ToAzure.ps1 -Environment production -ApiOnly

# Deploy only Blazor
.\Deploy-ToAzure.ps1 -Environment production -BlazorOnly

# Skip build (use existing publish folders)
.\Deploy-ToAzure.ps1 -Environment production -SkipBuild

# Skip IP restrictions configuration
.\Deploy-ToAzure.ps1 -Environment production -SkipIpRestrictions
```

**Environment-Specific Configuration**:

**API** (`PetelApp.Api/`):
- `appsettings.Development.json` - Local development
- `appsettings.Staging.json` - Test and Staging environments
- `appsettings.Production.json` - Production environment

**Blazor** (`PetelApp.BlazorServer/`):
- `appsettings.Development.json` - Local development
- `appsettings.Staging.json` - Test and Staging environments
- `appsettings.Production.json` - Production environment (includes API URL and CSP allowlist)

**Deployment Process**:
1. Builds project in Release configuration
2. Creates deployment package (zip)
3. Deploys to Azure App Service
4. Configures environment variables (`ASPNETCORE_ENVIRONMENT`)
5. Optionally configures IP restrictions for API access

**Azure Resources by Environment**:
- **Test**: `petel-test-rg`, `petel-test-api`, `petel-test-blazor`
- **Staging**: `petel-staging-rg`, `petel-staging-api`, `petel-staging-blazor`
- **Production**: `petel-prod-rg`, `petel-prod-api`, `petel-prod-blazor`

### Common Configuration Errors and Fixes

**Error: `relation "schools" does not exist`**
- **Cause**: Schema not being applied to queries
- **Fix**: Verify `HasDefaultSchema(_schemaName)` is in `OnModelCreating`
- **Fix**: Remove all hardcoded `"petel_schema"` strings from `ToTable()` calls

**Error: `IOptions<DatabaseSettings> could not be found`**
- **Cause**: Missing using statement
- **Fix**: Add `using Microsoft.Extensions.Options;` to `AppDbContext.cs`

**Error: CSP blocks API connections (`connect-src 'self'`)**
- **Cause**: Content Security Policy doesn't include API origin
- **Fix**: API URL is automatically extracted from `ApiSettings.BaseUrl` in `Program.cs`
- **Fix**: Verify CSP includes `connect-src 'self' https://api-domain.com`

**Error: API calls fail in production**
- **Cause**: Wrong API URL in Blazor configuration
- **Fix**: Verify `appsettings.Production.json` has correct `ApiSettings.BaseUrl`
- **Fix**: Check Azure App Service configuration for `ASPNETCORE_ENVIRONMENT=Production`

### Benefits of This Architecture

✅ **Environment Portability**: Deploy to any environment without code changes
✅ **Multi-Tenant Support**: Different schemas per tenant via configuration
✅ **Security**: Sensitive URLs not in source control
✅ **Maintainability**: Single source of truth for all configuration
✅ **Flexibility**: Override via environment variables or build scripts
✅ **Testability**: Easy to switch between test/production databases

## Project-Specific Patterns

### Entity-Based Request Flow
1. **UserSessionService** maintains full session state on the server
2. All controllers inherit from `BaseController` which provides session access methods
3. Database queries are scoped by user's EntityId
4. Session data is stored in memory with the UserSessionService

### Database-Driven Menu System

**Architecture**: Navigation menu items are stored in the database and loaded dynamically based on user permissions.

#### Menu Database Schema

```sql
-- menu_items table
CREATE TABLE petel_schema.menu_items (
    id SERIAL PRIMARY KEY,
    name VARCHAR(50) NOT NULL,              -- Used in navigateTo() function
    reference VARCHAR(100) NOT NULL,         -- HTML href attribute
    text VARCHAR(100) NOT NULL,              -- Display text (Hebrew)
    action_id INTEGER NULL,                  -- For permission-based filtering
    sort_order INTEGER NOT NULL DEFAULT 0,   -- Display order
    is_active BOOLEAN NOT NULL DEFAULT true  -- Enable/disable items
);
```

#### Menu Entity Model

```csharp
// Models/MenuItem.cs
[Table("menu_items")]
public class MenuItem
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("name")]
    [MaxLength(50)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [Column("reference")]
    [MaxLength(100)]
    public string Reference { get; set; } = string.Empty;

    [Required]
    [Column("text")]
    [MaxLength(100)]
    public string Text { get; set; } = string.Empty;

    [Column("action_id")]
    public int? ActionId { get; set; }

    [Column("sort_order")]
    public int SortOrder { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;
}
```

#### Menu Controller Pattern

```csharp
// Controllers/MenuController.cs
public class MenuController : BaseController
{
    private readonly AppDbContext _context;

    public MenuController(
        AppDbContext context,
        UserSessionService userSessionService,
        ILogger<MenuController> logger)
        : base(userSessionService, logger)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetMenuItems()
    {
        try
        {
            var session = GetCurrentSession();
            if (session == null)
            {
                return Unauthorized(new { success = false, message = "נדרש אימות" });
            }

            // TODO: Filter by user privileges when implementing security
            // For now, return all active items with null action_id
            var menuItems = await _context.MenuItems
                .AsNoTracking()
                .Where(m => m.IsActive && m.ActionId == null)
                .OrderBy(m => m.SortOrder)
                .Select(m => new
                {
                    id = m.Id,
                    name = m.Name,
                    reference = m.Reference,
                    text = m.Text,
                    sortOrder = m.SortOrder
                })
                .ToListAsync();

            return Ok(menuItems);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading menu items");
            return StatusCode(500, new
            {
                success = false,
                message = "שגיאה בטעינת תפריט",
                error = ex.Message
            });
        }
    }
}
```

#### Frontend Menu Integration

```javascript
// menu.html - Load menu items from backend
async function loadMenuItems() {
    console.log('📋 Loading menu items from backend...');
    
    try {
        const authToken = sessionStorage.getItem('authToken');
        if (!authToken) {
            console.error('❌ No auth token found');
            return;
        }

        const response = await fetch(AppConfig.getApiUrl('menu'), {
            method: 'GET',
            headers: {
                'Authorization': `Bearer ${authToken}`,
                'Content-Type': 'application/json'
            }
        });

        if (!response.ok) {
            throw new Error(`Failed to load menu items: ${response.status}`);
        }

        const menuItems = await response.json();
        console.log(`✅ Loaded ${menuItems.length} menu items`);

        renderMenuItems(menuItems);

    } catch (error) {
        console.error('❌ Error loading menu items:', error);
        showMenuError();
    }
}

function renderMenuItems(menuItems) {
    const container = document.getElementById('menuItemsContainer');
    if (!container || menuItems.length === 0) return;

    let menuHtml = '';
    menuItems.forEach((item, index) => {
        const isActive = index === 0 ? 'active' : '';
        menuHtml += `
            <a href="${item.reference}" 
               class="menu-item ${isActive}" 
               onclick="navigateTo('${item.name}'); return false;">
                <span class="menu-item-text">${item.text}</span>
            </a>
        `;
    });

    container.innerHTML = menuHtml;
}
```

#### Menu Management Workflow

**Adding a New Menu Item**:

1. Insert into database:
```sql
INSERT INTO petel_schema.menu_items (name, reference, text, action_id, sort_order, is_active)
VALUES ('newpage', '#newpage', 'עמוד חדש', NULL, 100, true);
```

2. Create corresponding page: `newpage.html`

3. Register in `page-lifecycle-config.js`:
```javascript
'newpage': {
    file: 'newpage.html',
    title: 'עמוד חדש',
    cleanup: 'cleanupNewPage',
    init: null,
    selfInitializing: true
}
```
4. Menu item automatically appears for all users (if `action_id` is NULL)

**Permission-Based Menu Items** (Future Implementation):

```csharp
// When implementing security:
var userPrivileges = await GetUserPrivileges(session.UserId);

var menuItems = await _context.MenuItems
    .AsNoTracking()
    .Where(m => m.IsActive && 
        (m.ActionId == null || userPrivileges.Contains(m.ActionId.Value)))
    .OrderBy(m => m.SortOrder)
    .ToListAsync();
```

#### Menu Best Practices

✅ **All menu items in database** - No hardcoded menu arrays in frontend
✅ **Permission ready** - `action_id` field prepared for future security
✅ **Ordered by sort_order** - Easy to reorder via database updates
✅ **Active flag** - Disable items without deletion
✅ **Session validation** - Menu endpoint requires authentication
✅ **Error handling** - Graceful fallback if menu loading fails
✅ **Hebrew text** - All display text in Hebrew following RTL patterns

#### Anti-Patterns to Avoid

```javascript
// ❌ WRONG - Hardcoded menu items in frontend
const menuItems = [
    { name: 'dashboard', text: 'עמוד ראשי' },
    { name: 'users', text: 'משתמשים' }
];

// ❌ WRONG - Manual menu HTML construction without backend
document.getElementById('menu').innerHTML = `
    <a href="#dashboard">עמוד ראשי</a>
    <a href="#users">משתמשים</a>
`;

// ✅ CORRECT - Load from backend
await loadMenuItems();
```

### Frontend Architecture Patterns

**Single-Page Application with Module Loading**:
- `index.html` is the shell, loads sections dynamically via `fetch('section.html')`
- `menu.html` loaded into `#sideMenuContainer` on page load
- Menu items loaded from backend database via `MenuController`
- Navigation via `navigateTo(section)` function with browser history support
- School year context retrieved from backend session data

### Page Lifecycle Management

**Centralized Navigation System**: All page loading, cleanup, and navigation is managed through the `PageLifecycleManager` with configuration-driven rules.

**Architecture Components**:
1. **`page-lifecycle-config.js`** - Configuration file defining all pages and navigation rules
2. **`page-lifecycle-manager.js`** - Core navigation engine handling page lifecycle
3. **`index.html`** - Application shell providing infrastructure utilities only
4. Individual page files - Contain page-specific logic and cleanup functions

#### Page Configuration Pattern

**All pages must be registered** in `page-lifecycle-config.js`:

```javascript
window.PageLifecycleConfig = {
    pages: {
        'pagename': {
            file: 'page.html',              // HTML file to load
            title: 'עמוד לדוגמה',            // Page title (Hebrew)
            cleanup: 'cleanupPageName',     // Cleanup function name (or null)
            init: 'initializePageName',     // Init function name (or null)
            selfInitializing: false         // true = uses DOMContentLoaded, false = needs explicit init
        }
    },
    
    navigationRules: [
        {
            from: 'sourcepage',             // Page navigating from
            to: 'targetpage',               // Page navigating to
            clearSession: [                 // Session keys to clear
                'SelectedStudentId',
                'SelectedStudentData'
            ]
        },
        {
            from: '*',                      // Wildcard - matches any source page
            to: 'maindashboard',
            clearSession: [
                'SelectedStudentId',
                'SelectedStudentData',
                'SelectedSchoolId',
                'SelectedSchoolName'
            ]
        }
    ]
};
```

#### Page Types

**Self-Initializing Pages** (`selfInitializing: true`):
- Initialize via their own `DOMContentLoaded` event handlers
- `PageLifecycleManager` only loads HTML and executes scripts
- No explicit init function call needed
- Examples: `maindashboard`, `schooldashboard`, `schooldetails`, `systemattributes`

```javascript
// Page configuration
'maindashboard': {
    file: 'maindashboard.html',
    cleanup: null,
    init: null,
    selfInitializing: true  // ✅ Handles own initialization
}

// In maindashboard.html
document.addEventListener('DOMContentLoaded', function() {
    // Page initialization logic here
    loadDashboardCardData('alertsCard');
    updateContextButtonLabels();
});
```

**Explicitly-Initialized Pages** (`selfInitializing: false`):
- Require explicit function call after loading
- `PageLifecycleManager` calls the init function after a delay
- Examples: `students`, `student`, `schoollist`

```javascript
// Page configuration
'students': {
    file: 'students.html',
    cleanup: 'cleanupStudentsPage',
    init: 'loadStudentsData',        // ✅ Will be called by lifecycle manager
    selfInitializing: false
}

// In students.html
async function loadStudentsData() {
    // Fetch and display students
}

window.loadStudentsData = loadStudentsData;  // Must export to window
```

#### Page Cleanup Pattern

**Every page with reusable components MUST have a cleanup function**:

```javascript
// In page.html
function cleanupPageName() {
    console.log('🧹 Cleaning up page...');
    
    try {
        // 1. Cleanup component instances
        if (window.myComponent) {
            if (typeof window.myComponent.cleanup === 'function') {
                window.myComponent.cleanup();
            }
            window.myComponent = null;
        }
        
        // 2. Remove global references
        if (window['documentsTableInstance_containerId']) {
            delete window['documentsTableInstance_containerId'];
        }
        
        // 3. Clear container HTML
        const container = document.getElementById('myTableContainer');
        if (container) {
            container.innerHTML = '<div class="loading-spinner">טוען...</div>';
        }
        
        // 4. Clear page-specific data
        if (window.currentPageData) {
            window.currentPageData = null;
        }
        
        console.log('✅ Page cleanup complete');
    } catch (error) {
        console.error('❌ Error during cleanup:', error);
    }
}

// ✅ MUST export to window for PageLifecycleManager
window.cleanupPageName = cleanupPageName;
```

#### Component Variable Scope

**CRITICAL**: Use `window` scope for all component variables to prevent redeclaration errors on page re-entry:

```javascript
// ❌ WRONG - Will cause "already declared" error on return
let documentsComponent = null;
let studentsTable = null;

// ✅ CORRECT - Can be safely reassigned
window.documentsComponent = window.documentsComponent || null;
window.studentsTable = window.studentsTable || null;

// OR use conditional declaration
if (typeof documentsComponent === 'undefined') {
    var documentsComponent = null;
}
```

**Why**: When `PageLifecycleManager` reloads a page, the script is re-executed. Using `let`/`const` in script scope causes redeclaration errors because the variable still exists in memory from the previous visit.

#### Navigation Pattern

**All navigation MUST use `window.navigateTo()`**:

```javascript
// ✅ CORRECT - Single navigation call
async function navigateToStudents() {
    console.log('🔄 Navigating to students...');
    
    // Optional: Clear session data (if not in navigationRules)
    await window.SessionState.setProperty('SelectedStudentId', '');
    
    // Navigate - PageLifecycleManager handles everything else
    if (typeof window.navigateTo === 'function') {
        await window.navigateTo('students');
    } else {
        console.error('❌ window.navigateTo not available');
    }
}

// ❌ WRONG - Manual HTML loading
async function navigateToStudents() {
    const response = await fetch('students.html');
    const html = await response.text();
    document.getElementById('dynamicContent').innerHTML = html;
    executeScriptsInContainer(...);
    history.pushState(...);
    // Missing: cleanup, session rules, state tracking
}
```

#### What PageLifecycleManager Handles

When you call `window.navigateTo('targetpage')`, the lifecycle manager automatically:

1. ✅ **Cleanup** - Calls `cleanupCurrentPage()` for the page you're leaving
2. ✅ **Session Rules** - Clears session data per `navigationRules` configuration
3. ✅ **Table Cleanup** - Removes all `ReusableTable` and component instances from memory
4. ✅ **HTML Loading** - Fetches target page HTML with error handling
5. ✅ **Script Execution** - Re-executes scripts in loaded content
6. ✅ **Browser History** - Updates URL and history state
7. ✅ **Initialization** - Calls init function (for non-self-initializing pages)
8. ✅ **State Tracking** - Updates `currentPage` and `previousPage`

#### Session Data Clearing Rules

**Use navigationRules instead of manual clearing**:

```javascript
// ❌ WRONG - Manual clearing in every navigation function
async function navigateToStudents() {
    await window.SessionState.setProperty('SelectedStudentId', '');
    await window.SessionState.setProperty('SelectedStudentData', '');
    await window.navigateTo('students');
}

async function navigateToSchoolDashboard() {
    await window.SessionState.setProperty('SelectedStudentId', '');
    await window.SessionState.setProperty('SelectedStudentData', '');
    await window.navigateTo('schooldashboard');
}

// ✅ CORRECT - Define once in page-lifecycle-config.js
navigationRules: [
    {
        from: 'student',
        to: '*',  // Any destination from student page
        clearSession: ['SelectedStudentId', 'SelectedStudentData']
    }
]

// Then navigation is simple:
async function navigateToStudents() {
    await window.navigateTo('students');  // Session clearing happens automatically
}

async function navigateToSchoolDashboard() {
    await window.navigateTo('schooldashboard');  // Session clearing happens automatically
}
```

#### index.html Responsibilities

**index.html is infrastructure ONLY** - no page-specific logic:

```javascript
// ✅ KEEP in index.html - Infrastructure functions
window.SessionState = { /* ... */ };
window.navigateTo = async function(section, fromPopstate) { 
    await window.PageLifecycleManager.navigateTo(section, fromPopstate); 
};
window.checkAuthentication = function() { /* ... */ };
window.executeScriptsInContainer = function(container) { /* ... */ };
window.loadUserInfo = async function() { /* ... */ };
window.logout = function() { /* ... */ };

// ❌ REMOVE from index.html - Page loaders (use PageLifecycleManager)
window.loadMainDashboard = async function() { /* ... */ };        // NO!
window.loadSchoolListPage = async function() { /* ... */ };       // NO!
window.loadStudentsPage = async function() { /* ... */ };         // NO!
```

#### Anti-Patterns to Avoid

```javascript
// ❌ Manual cleanup calls in navigation functions
async function navigateToStudents() {
    cleanupStudentDocuments();  // NO! - PageLifecycleManager does this
    await window.navigateTo('students');
}

// ❌ Duplicate HTML loading logic in multiple files
async function loadPage() {
    const response = await fetch('page.html');  // NO! - Use navigateTo()
    // ... 50 lines of manual loading
}

// ❌ Page-level variables without window scope
let myComponent = null;  // NO! - Use window.myComponent

// ❌ Missing cleanup function export
function cleanupMyPage() { /* ... */ }
// Missing: window.cleanupMyPage = cleanupMyPage;  // REQUIRED!

// ❌ Using event.stopPropagation() in onclick attributes (SECURITY VIOLATION)
<button onclick="event.stopPropagation(); myFunction();">  // NO! - Breaks action-security.js

// ✅ CORRECT - No event manipulation in onclick
<button onclick="myFunction();">  // Header click handler already checks for .btn-icon
```

#### Adding a New Page - Checklist

1. ✅ Create `newpage.html` with page content and script
2. ✅ Add page configuration to `page-lifecycle-config.js`:
   ```javascript
   'newpage': {
       file: 'newpage.html',
       title: 'כותרת בעברית',
       cleanup: 'cleanupNewPage',
       init: 'initNewPage',  // or null if self-initializing
       selfInitializing: false
   }
   ```
3. ✅ Add to database menu_items table:
   ```sql
   INSERT INTO petel_schema.menu_items (name, reference, text, sort_order)
   VALUES ('newpage', '#newpage', 'עמוד חדש', 100);
   ```
4. ✅ Add navigation rules if page clears session data:
   ```javascript
   { from: 'newpage', to: '*', clearSession: ['Key1', 'Key2'] }
   ```
5. ✅ Implement cleanup function in page:
   ```javascript
   function cleanupNewPage() { /* ... */ }
   window.cleanupNewPage = cleanupNewPage;
   ```
6. ✅ Use `window` scope for all component variables:
   ```javascript
   window.myComponent = window.myComponent || null;
   ```
7. ✅ Export init function to window (if needed):
   ```javascript
   window.initNewPage = initNewPage;
   ```
8. ✅ Navigate using `window.navigateTo('newpage')`

**That's it!** No changes to `index.html` or `PageLifecycleManager` needed.

#### Benefits of This Architecture

- **90% less code** - No duplicate HTML loading logic in each page
- **Single source of truth** - All lifecycle rules in one configuration file
- **Automatic cleanup** - No manual cleanup calls needed
- **Memory safety** - Components properly cleaned up on navigation
- **No redeclaration errors** - Proper variable scoping prevents issues
- **Configuration-driven** - Add pages without code changes
- **Session management** - Automatic session data clearing per rules
- **Browser history** - Full back/forward button support
- **Maintainable** - Changes in one place affect all pages consistently
- **Database-driven menu** - Menu items managed via database

### Standard Components

**Standard Table Component**:
- **ALL tables must use ReusableTable component** from `table-component.js`
- **Action buttons column MUST be the first column in all tables** for consistency and accessibility
- Constructor: `new ReusableTable(containerId, options)`
- Options: `{ tableName, isReadOnly, allowAdd, allowEdit, allowDelete }`
- Columns format: `{ key, label, sortable, readOnly, render }`
- Example implementation:
```javascript
const table = new ReusableTable('tableContainer', {
    tableName: 'students',
    isReadOnly: false,
    allowAdd: true,
    allowEdit: true,
    allowDelete: false
});

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

**Standard Icon Set**:
- **ALL icons must use provided PNG icon set** - NO emoji or Unicode symbols
- Icon class: `.action-icon-natural` for 15px natural color icons
- Standard icons:
  - `view_icon.png` - View/preview actions (👁️ replacement)
  - `edit_icon.png` - Edit/modify actions (✏️ replacement)  
  - `delete_icon.png` - Delete/remove actions (🗑️ replacement)
  - `download_icon.png` - Download actions (📥 replacement)
  - `upload_icon.png` - Upload/copy actions (📤 replacement)
  - `stats_icon.png` - Statistics/reports actions (📊 replacement)
  - `Plus icon.png` - Add new item actions (➕ replacement)
- Button styling:
```css
.btn-icon {
    padding: 4px 6px;
    border: 1px solid #dee2e6;
    border-radius: 4px;
    background-color: transparent;
    cursor: pointer;
}

.action-icon-natural {
    width: 15px;
    height: 15px;
    object-fit: contain;
}
```

**Context Buttons Layout**:
- **Context buttons must be positioned between the side menu and main content section**
- Use fixed positioning relative to the side menu width
- Standard layout structure:
```html
<div class="page-container">
    <div class="side-menu">
        <!-- Menu content (~260px width) -->
    </div>
    <div class="context-buttons-section">
        <button class="context-btn" onclick="navigationAction()">
            Button Text
        </button>
    </div>
    <div class="main-content">
        <!-- Main section content -->
    </div>
</div>
```

**Security Constraint - onclick Handlers**:
- **CRITICAL**: Do NOT use `event.stopPropagation()` or any event manipulation in onclick attributes
- **Reason**: The action-security.js system evaluates onclick handlers, and `event` is undefined in that context
- **Solution**: Collapsible card headers already check for `.btn-icon` clicks, so buttons are automatically excluded from collapse triggers

```html
<!-- ❌ WRONG - Security violation -->
<button onclick="event.stopPropagation(); showModal();">Open</button>

<!-- ✅ CORRECT - No event manipulation needed -->
<button onclick="showModal();">Open</button>

<!-- The header click handler already has: -->
<script>
header.addEventListener('click', function (e) {
    if (e.target.closest('.btn-icon')) {
        return;  // ✅ Buttons don't trigger collapse
    }
    toggleCardExpansion(card, toggle, addButton);
});
</script>
```

**Table Horizontal Scrolling**:
- **All table containers must support horizontal scrolling for wide content**
- Apply `overflow-x: auto` to table containers
- Set minimum table width to trigger scrolling when needed
- Standard table scroll implementation:
```css
.table-container {
    overflow-x: auto;
    overflow-y: visible;
    max-width: 100%;
    border: 1px solid #dee2e6;
    border-radius: 8px;
}

.data-table {
    min-width: 1200px; /* Minimum width to trigger scroll */
    white-space: nowrap;
}
```

### Excel Import/Export Pattern

**Standard Implementation**: All Excel operations use EPPlus library with consistent error handling and validation.

**Required Package**: 
```xml
<PackageReference Include="EPPlus" Version="7.0.0" />
```

#### Import Pattern (Backend)

```csharp
[HttpPost("import")]
public async Task<IActionResult> ImportFromExcel(IFormFile file)
{
    if (file == null || file.Length == 0)
        return BadRequest("No file uploaded");

    if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
        return BadRequest("Only .xlsx files are supported");

    if (file.Length > 10 * 1024 * 1024)  // 10MB limit
        return BadRequest("File too large (max 10MB)");

    var session = GetCurrentSession();
    var errors = new List<string>();
    var importedCount = 0;

    try
    {
        using var stream = new MemoryStream();
        await file.CopyToAsync(stream);
        
        using var package = new ExcelPackage(stream);
        var worksheet = package.Workbook.Worksheets[0];
        var rowCount = worksheet.Dimension.Rows;

        // Stage 1: Header validation
        var expectedHeaders = new Dictionary<int, string>
        {
            { 1, "מזהה" },
            { 2, "שם" },
            { 3, "כיתה" }
        };

        for (int col = 1; col <= expectedHeaders.Count; col++)
        {
            var header = worksheet.Cells[1, col].Text.Trim();
            if (header != expectedHeaders[col])
            {
                return BadRequest($"Invalid header in column {col}. Expected '{expectedHeaders[col]}', got '{header}'");
            }
        }

        // Stage 2: Duplicate detection in file
        var duplicateIds = new HashSet<string>();
        var existingIds = await _context.Students
            .Where(s => s.SchoolYearId == schoolYearId)
            .Select(s => s.StudentId)
            .ToListAsync();

        // Stage 3: Row processing with validation
        for (int row = 2; row <= rowCount; row++)
        {
            try
            {
                var id = worksheet.Cells[row, 1].Text.Trim();
                var name = worksheet.Cells[row, 2].Text.Trim();
                var className = worksheet.Cells[row, 3].Text.Trim();

                // Required field validation
                if (string.IsNullOrWhiteSpace(id))
                {
                    errors.Add($"Row {row}: Missing required ID");
                    continue;
                }

                // Duplicate in file check
                if (duplicateIds.Contains(id))
                {
                    errors.Add($"Row {row}: Duplicate ID '{id}' in import file");
                    continue;
                }
                duplicateIds.Add(id);

                // Duplicate in database check
                if (existingIds.Contains(id))
                {
                    errors.Add($"Row {row}: ID '{id}' already exists in database");
                    continue;
                }

                // Use GlobalFunctions for entity resolution
                var classId = await _globalFunctions.GetClassIdByName(className, schoolYearId);
                if (classId == null)
                {
                    errors.Add($"Row {row}: Class '{className}' not found");
                    continue;
                }

                // Create entity
                var entity = new MyEntity
                {
                    Id = id,
                    Name = name,
                    ClassId = classId.Value
                };

                _context.MyEntities.Add(entity);
                importedCount++;
            }
            catch (Exception ex)
            {
                errors.Add($"Row {row}: {ex.Message}");
            }
        }

        if (importedCount > 0)
        {
            await _context.SaveChangesAsync();
        }

        return Ok(new
        {
            ImportedCount = importedCount,
            ErrorCount = errors.Count,
            Errors = errors.Take(50).ToList(),  // Limit to first 50
            HasMoreErrors = errors.Count > 50
        });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error importing Excel file");
        return StatusCode(500, $"Error processing file: {ex.Message}");
    }
}
```

#### Export Pattern (Backend)

```csharp
[HttpGet("export")]
public async Task<IActionResult> ExportToExcel()
{
    var session = GetCurrentSession();
    
    try
    {
        var data = await _context.MyEntities
            .Where(e => e.EntityId == int.Parse(session.EntityId))
            .Include(e => e.RelatedEntity)  // Use navigation properties
            .ToListAsync();

        using var package = new ExcelPackage();
        var worksheet = package.Workbook.Worksheets.Add("נתונים");

        // Headers with RTL support
        var headers = new[] { "מזהה", "שם", "תיאור" };
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = worksheet.Cells[1, i + 1];
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
            cell.Style.Fill.BackgroundColor.SetColor(Color.LightBlue);
        }

        // Data rows
        for (int i = 0; i < data.Count; i++)
        {
            var item = data[i];
            var row = i + 2;
            
            worksheet.Cells[row, 1].Value = item.Id;
            worksheet.Cells[row, 2].Value = item.Name;
            worksheet.Cells[row, 3].Value = item.Description;
        }

        // Formatting
        worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();
        worksheet.View.RightToLeft = true;

        var stream = new MemoryStream(package.GetAsByteArray());
        var fileName = $"Export_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
        
        return File(stream, 
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", 
            fileName);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error exporting to Excel");
        return StatusCode(500, "Error generating Excel file");
    }
}
```

#### Frontend Integration

```javascript
// Upload Excel file
async function uploadExcel() {
    const fileInput = document.getElementById('excelFileInput');
    const file = fileInput.files[0];
    
    if (!file) {
        alert('אנא בחר קובץ');
        return;
    }

    const formData = new FormData();
    formData.append('file', file);

    try {
        const response = await fetch(AppConfig.getApiUrl('myentities/import'), {
            method: 'POST',
            headers: {
                'Authorization': `Bearer ${sessionStorage.getItem('authToken')}`
            },
            body: formData
        });

        const result = await response.json();
        
        if (response.ok) {
            let message = `יובאו ${result.importedCount} רשומות בהצלחה`;
            if (result.errorCount > 0) {
                message += `\n${result.errorCount} שגיאות התרחשו`;
                console.error('Import errors:', result.errors);
            }
            alert(message);
            await loadData();  // Refresh page data
        } else {
            alert(`שגיאה: ${result}`);
        }
    } catch (error) {
        console.error('Error uploading file:', error);
        alert('שגיאה בהעלאת הקובץ');
    }
}

// Download Excel file
async function downloadExcel() {
    try {
        const response = await fetch(AppConfig.getApiUrl('myentities/export'), {
            headers: {
                'Authorization': `Bearer ${sessionStorage.getItem('authToken')}`
            }
        });

        if (response.ok) {
            const blob = await response.blob();
            const url = window.URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = url;
            a.download = `export_${new Date().toISOString().slice(0,10)}.xlsx`;
            document.body.appendChild(a);
            a.click();
            window.URL.revokeObjectURL(url);
            document.body.removeChild(a);
        } else {
            alert('שגיאה בהורדת הקובץ');
        }
    } catch (error) {
        console.error('Error downloading file:', error);
        alert('שגיאה בהורדת הקובץ');
    }
}
```

#### Import Validation Best Practices

**Multi-Stage Validation**:
1. ✅ File format validation (extension, size)
2. ✅ Structure validation (headers, column count)
3. ✅ Data type validation (per column)
4. ✅ Business logic validation (required fields, duplicates)
5. ✅ Reference validation (foreign keys exist)

**Error Collection**:
- ✅ Collect ALL errors, don't stop on first error
- ✅ Include row number and column in error messages
- ✅ Return summary with counts and detailed error list
- ✅ Log errors for debugging

**Best Practices**:
```csharp
// ✅ CORRECT - Collect errors and continue
if (string.IsNullOrWhiteSpace(value))
{
    errors.Add($"Row {row}: Invalid value in column {col}");
    continue;
}

// ❌ WRONG - Throwing on first error
if (string.IsNullOrWhiteSpace(value))
    throw new Exception("Invalid value");  // NO!

// ✅ CORRECT - Use GlobalFunctions for lookups
var classId = await _globalFunctions.GetClassIdByName(className, yearId);

// ❌ WRONG - Direct database query
var classId = _context.SchoolClasses
    .FirstOrDefault(c => c.ClassName == className)?.Id;  // NO!
```

## Authentication & Session Management

### Authentication Flow
1. User logs in via `/api/auth/login` with username/password
2. Backend validates credentials, creates JWT token
3. Frontend stores token in `sessionStorage`
4. All subsequent API calls include `Authorization: Bearer {token}` header
5. Backend validates token and retrieves user session from `UserSessionService`

### Session Properties Pattern
**Backend**: Properties stored in `UserSession` object via `UserSessionService`
**Frontend**: Use `SessionState` object for temporary client-side state

```javascript
// Set session property
await window.SessionState.setProperty('SelectedStudentId', studentId);

// Get session property
const studentId = await window.SessionState.getProperty('SelectedStudentId');

// Clear specific property
await window.SessionState.setProperty('SelectedStudentId', '');

// Clear multiple properties (via navigationRules)
navigationRules: [
    { from: 'student', to: '*', clearSession: ['SelectedStudentId', 'SelectedStudentData'] }
]
```

### BaseController Pattern
All API controllers inherit from `BaseController` which provides:
- `GetCurrentSession()` - Retrieves full user session
- `GetSessionProperty(key)` - Gets specific session property
- Automatic EntityId scoping for all queries
- **NO `[Authorize]` attribute** - uses manual session validation

```csharp
public class MyController : BaseController
{
    public async Task<IActionResult> GetData()
    {
        var session = GetCurrentSession();
        if (session == null)
        {
            return Unauthorized(new { success = false, message = "נדרש אימות" });
        }
        
        var entityId = int.Parse(session.EntityId);
        
        var data = await _context.MyEntities
            .Where(e => e.EntityId == entityId)
            .ToListAsync();
            
        return Ok(data);
    }
}
```

**IMPORTANT**: Controllers do NOT use `[Authorize]` attribute. Session validation is done manually via `GetCurrentSession()` in each endpoint.

### Document Proxy Pattern (IP Restrictions)

**Purpose**: When the API has IP restrictions that only allow server-to-server calls, browsers cannot directly access API endpoints. A proxy endpoint in the Blazor app forwards browser requests to the API.

**Note**: The system uses Azure App Service IP restrictions (Israeli IPs only) for geographic filtering.

**Architecture**:
```
Browser (with user token) → Blazor Proxy (forwards token) → API (validates token) → Document
                             ↑ Server IP is allowed           ↑ User auth verified
```

**Benefits**:
- ✅ Maintains security - API still validates user's JWT token
- ✅ Bypasses IP restrictions - Blazor server IP is in API allowlist
- ✅ No code changes in API - uses existing authentication
- ✅ Transparent to frontend - JavaScript still uses normal fetch with Authorization header

**Implementation in Blazor Program.cs**:

```csharp
// Required using statements
using Microsoft.Extensions.Options;
using PetelApp.BlazorServer.Models;

// In middleware pipeline (after UseAntiforgery())
app.MapGet("/api/documents/{documentId}/proxy", async (
    long documentId, 
    HttpContext httpContext,
    IHttpClientFactory httpClientFactory,
    IOptions<ApiSettings> apiSettings,
    ILogger<Program> logger) =>
{
    try
    {
        logger.LogInformation("📥 Document proxy request for ID: {DocumentId}", documentId);
        
        // ✅ Extract Authorization header from browser request
        if (!httpContext.Request.Headers.TryGetValue("Authorization", out var authHeader) ||
            string.IsNullOrEmpty(authHeader))
        {
            logger.LogWarning("⚠️ No authorization header in proxy request");
            return Results.Unauthorized();
        }

        // ✅ Create HTTP client and forward browser's token to API
        var client = httpClientFactory.CreateClient("PetelApi");
        client.DefaultRequestHeaders.Add("Authorization", authHeader.ToString());
        
        var apiUrl = $"{apiSettings.Value.BaseUrl}/Documents/{documentId}/download";
        logger.LogDebug("Proxying request to: {ApiUrl}", apiUrl);
        
        var apiResponse = await client.GetAsync(apiUrl);
        
        if (!apiResponse.IsSuccessStatusCode)
        {
            logger.LogWarning("⚠️ API returned {StatusCode} for document {DocumentId}", 
                apiResponse.StatusCode, documentId);
            
            if (apiResponse.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                return Results.Unauthorized();
            
            if (apiResponse.StatusCode == System.Net.HttpStatusCode.NotFound)
                return Results.NotFound(new { error = "מסמך לא נמצא" });
            
            return Results.Problem($"שגיאה בטעינת המסמך: {apiResponse.StatusCode}");
        }
        
        var content = await apiResponse.Content.ReadAsByteArrayAsync();
        var contentType = apiResponse.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
        
        // ✅ Extract filename from Content-Disposition header
        var fileName = $"document_{documentId}";
        if (apiResponse.Content.Headers.ContentDisposition?.FileName != null)
        {
            fileName = apiResponse.Content.Headers.ContentDisposition.FileName.Trim('"');
        }
        
        logger.LogInformation("✅ Returning document {DocumentId}, size: {Size} bytes", 
            documentId, content.Length);
        
        return Results.File(content, contentType, fileName);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "❌ Error proxying document {DocumentId}", documentId);
        return Results.Problem("שגיאה בטעינת המסמך");
    }
})
.DisableAntiforgery(); // ✅ Required for GET requests from browser
```

**Frontend Integration** (no changes needed):

```javascript
// blazorHelpers.js - existing code works unchanged
viewFileWithAuth: async function (url, token) {
    const response = await fetch(url, {
        method: 'GET',
        headers: {
            'Authorization': `Bearer ${token}` // ✅ Forwarded by proxy
        }
    });
    
    const blob = await response.blob();
    const blobUrl = window.URL.createObjectURL(blob);
    window.open(blobUrl, '_blank');
}

// Blazor component - use proxy URL
var downloadUrl = $"/api/documents/{documentId}/proxy";
await JSRuntime.InvokeVoidAsync("BlazorHelpers.viewFileWithAuth", downloadUrl, token);
```

**API Endpoint** (existing, no changes):

```csharp
// DocumentsController.cs - works as-is
[HttpGet("{id}/download")]
public async Task<IActionResult> DownloadDocument(long id)
{
    var session = GetCurrentSession();
    if (session == null)
        return Unauthorized(new { error = "נדרש אימות" });

    var document = await _context.Documents.FindAsync(id);
    
    return File(document.FileBlob, contentType, fileName);
}
```

**When to Use This Pattern**:
- ✅ API has IP restrictions (Azure App Service IP filtering)
- ✅ Browser needs to download/view files from API
- ✅ Need to maintain user authentication with JWT tokens
- ✅ Server-to-server calls are allowed in security architecture

**Anti-Patterns**:
```csharp
// ❌ WRONG - Using ApiService in Minimal API endpoint
app.MapGet("/proxy", async (ApiService apiService) =>
{
    var file = await apiService.GetFileAsync(...); // NO! ApiService needs Blazor circuit
});

// ❌ WRONG - Not forwarding Authorization header
var client = httpClientFactory.CreateClient("PetelApi");
var response = await client.GetAsync(url); // NO! Missing user's token

// ✅ CORRECT - Forward browser's Authorization header
client.DefaultRequestHeaders.Add("Authorization", authHeader.ToString());
```

**Troubleshooting**:
- **404 errors**: Verify `UseRouting()` is called before `MapGet()` in Program.cs
- **401 errors**: Check Authorization header is being forwarded correctly
- **403 errors**: Verify Blazor server IP is in API's IP allowlist (Azure App Service IP restrictions)

## Entity Framework Patterns

### Database Context Configuration

**CRITICAL**: Always use `HasDefaultSchema()` - never hardcode schema names in entity configurations.

```csharp
public class AppDbContext : DbContext
{
    private readonly string _schemaName;

    public AppDbContext(
        DbContextOptions<AppDbContext> options,
        IOptions<DatabaseSettings> dbSettings) 
        : base(options)
    {
        _schemaName = dbSettings.Value.SchemaName;
    }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ✅ Set default schema ONCE
        modelBuilder.HasDefaultSchema(_schemaName);

        // ✅ Configure entities WITHOUT schema parameter
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasIndex(e => e.Username).IsUnique();
        });
    }
}
```

### Entity Class Patterns

**Standard entity attributes**:
```csharp
[Table("table_name")]  // ✅ Table name only - NO schema
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
}
```

### Navigation Properties (CRITICAL)

**All relationships MUST include navigation properties** for proper EF Core functionality:

```csharp
// Entity with foreign key relationship
public class SchoolStudent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }
    
    // ✅ REQUIRED: Foreign key property
    [ForeignKey("SchoolYear")]
    [Column("school_year_id")]
    public int SchoolYearId { get; set; }
    
    // ✅ REQUIRED: Navigation property (never null)
    public virtual SchoolYear SchoolYear { get; set; } = null!;
    
    // ✅ Optional relationship
    [ForeignKey("SchoolClass")]
    [Column("class_id")]
    public int? ClassId { get; set; }
    
    // ✅ Nullable navigation property
    public virtual SchoolClass? SchoolClass { get; set; }
}

// Parent entity with collection
public class SchoolYear
{
    [Key]
    [Column("id")]
    public int Id { get; set; }
    
    [Column("year_name")]
    public string YearName { get; set; } = string.Empty;
    
    // ✅ REQUIRED: Collection navigation property
    public virtual ICollection<SchoolStudent> Students { get; set; } = new List<SchoolStudent>();
}
```

**Configuration in AppDbContext**:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);
    modelBuilder.HasDefaultSchema(_schemaName);
    
    modelBuilder.Entity<SchoolStudent>(entity =>
    {
        entity.ToTable("school_students");
        
        // ✅ Configure required relationship
        entity.HasOne(s => s.SchoolYear)
            .WithMany(y => y.Students)
            .HasForeignKey(s => s.SchoolYearId)
            .OnDelete(DeleteBehavior.Restrict);
            
        // ✅ Configure optional relationship
        entity.HasOne(s => s.SchoolClass)
            .WithMany(c => c.Students)
            .HasForeignKey(s => s.ClassId)
            .OnDelete(DeleteBehavior.SetNull);
    });
}
```

**Benefits of Navigation Properties**:
- ✅ Enables eager loading: `.Include(s => s.SchoolYear)`
- ✅ Prevents N+1 query problems
- ✅ Provides IntelliSense for related data
- ✅ Enforces referential integrity
- ✅ Simplifies projection queries

**Loading Strategies**:

```csharp
// ✅ Eager loading (small related data)
var students = await _context.SchoolStudents
    .Include(s => s.SchoolYear)
    .Include(s => s.SchoolClass)
    .Where(s => s.SchoolYearId == yearId)
    .ToListAsync();

// ✅ Projection (large datasets, specific fields only)
var studentDtos = await _context.SchoolStudents
    .Where(s => s.SchoolYearId == yearId)
    .Select(s => new StudentDto
    {
        Id = s.Id,
        Name = s.Name,
        YearName = s.SchoolYear.YearName,
        ClassName = s.SchoolClass != null ? s.SchoolClass.ClassName : null
    })
    .ToListAsync();

// ❌ WRONG - Lazy loading causes N+1 queries
var students = await _context.SchoolStudents.ToListAsync();
foreach (var student in students)
{
    var yearName = student.SchoolYear.YearName;  // Separate query per student!
}
```

**Anti-Patterns**:
```csharp
// ❌ WRONG - Missing navigation property
public class SchoolStudent
{
    public int SchoolYearId { get; set; }
    // Missing: public virtual SchoolYear SchoolYear { get; set; }
}

// ❌ WRONG - Accessing navigation without Include
var students = await _context.SchoolStudents.ToListAsync();
var yearName = students[0].SchoolYear.YearName;  // NullReferenceException!

// ✅ CORRECT - Include navigation property
var students = await _context.SchoolStudents
    .Include(s => s.SchoolYear)
    .ToListAsync();
var yearName = students[0].SchoolYear.YearName;  // Works!
```

### Query Patterns

**Entity Scoping**: Always filter by user's EntityId
```csharp
var session = GetCurrentSession();
var entityId = int.Parse(session.EntityId);

var data = await _context.Students
    .Where(s => s.EntityId == entityId)
    .ToListAsync();
```

**Async/Await**: Always use async methods
```csharp
// ✅ CORRECT
var students = await _context.Students.ToListAsync();

// ❌ WRONG
var students = _context.Students.ToList();  // Blocks thread
```

**Projections for Performance**:
```csharp
// ✅ CORRECT - Only select needed fields
var data = await _context.Students
    .Select(s => new { s.Id, s.Name, s.ClassName })
    .ToListAsync();

// ❌ WRONG - Loading entire entity when not needed
var data = await _context.Students
    .ToListAsync()
    .Select(s => new { s.Id, s.Name, s.ClassName });
```

## Global Helper Functions

### GlobalFunctions Service
Provides centralized lookup and normalization functions:

```csharp
// Get entity IDs by name (with Hebrew normalization)
var schoolId = await _globalFunctions.GetSchoolIdByName(schoolName);
var classId = await _globalFunctions.GetClassIdByName(className, schoolYearId);
var yearId = await _globalFunctions.GetSchoolYearIdByName(yearName);

// Hebrew text normalization
var normalized = GlobalFunctions.PureHebrewText(input);  // Static method
```

**Usage in Controllers**:
```csharp
public class MyController : BaseController
{
    private readonly GlobalFunctions _globalFunctions;
    
    public MyController(AppDbContext context, GlobalFunctions globalFunctions)
        : base(context)
    {
        _globalFunctions = globalFunctions;
    }
    
    public async Task<IActionResult> GetByName(string name)
    {
        var id = await _globalFunctions.GetEntityIdByName(name);
        if (id == null)
            return NotFound($"Entity '{name}' not found");
            
        // Use resolved ID...
    }
}
```

**Hebrew Normalization Pattern**:
```csharp
// Use for comparing Excel input to database values
var normalizedInput = GlobalFunctions.PureHebrewText(excelValue);
var match = await _context.Entities
    .Where(e => GlobalFunctions.PureHebrewText(e.Name) == normalizedInput)
    .FirstOrDefaultAsync();
```

## Standard Database Table Structure

**CRITICAL**: All tables in the system MUST follow standardized naming and structure patterns.

### Required Audit Fields

**Every table must include these audit fields**:

```sql
created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
created_user INTEGER NULL REFERENCES petel_schema.users(id) ON DELETE SET NULL,
updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
update_user INTEGER NULL REFERENCES petel_schema.users(id) ON DELETE SET NULL
```

**Entity Model Pattern**:
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

### Field Naming Conventions

**✅ CORRECT Patterns**:
- Use concise names: `name`, `value`, `description` (NOT `attribute_name`, `attribute_value`)
- Table name provides context, so field names should be minimal
- Use underscores for multi-word fields: `school_year_id`, `created_at`
- Hebrew descriptions: Always include a `description` field for Hebrew UI labels

**❌ WRONG Patterns**:
```sql
-- NO! Redundant prefix from table name
CREATE TABLE school_year_attributes (
    attribute_name VARCHAR(100),  -- Should be just "name"
    attribute_value VARCHAR(500)  -- Should be just "value"
);

-- NO! Missing audit fields
CREATE TABLE my_table (
    id SERIAL PRIMARY KEY,
    name VARCHAR(100)
    -- Missing: created_at, created_user, updated_at, update_user
);
```

### Standard Table Template

**Use this template for all new tables**:

```sql
CREATE TABLE petel_schema.table_name (
    id SERIAL PRIMARY KEY,
    
    -- Foreign keys (if applicable)
    parent_id INTEGER NOT NULL REFERENCES petel_schema.parent_table(id) ON DELETE CASCADE,
    
    -- Business fields
    name VARCHAR(100) NOT NULL,
    description VARCHAR(200) NULL,  -- Hebrew description for UI
    value VARCHAR(500) NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT true,
    
    -- Audit fields (REQUIRED)
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    created_user INTEGER NULL REFERENCES petel_schema.users(id) ON DELETE SET NULL,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    update_user INTEGER NULL REFERENCES petel_schema.users(id) ON DELETE SET NULL,
    
    -- Constraints
    CONSTRAINT uk_table_unique UNIQUE (parent_id, name)
);

-- Indexes
CREATE INDEX idx_table_name_parent_id ON petel_schema.table_name(parent_id);
CREATE INDEX idx_table_name_name ON petel_schema.table_name(name);
CREATE INDEX idx_table_name_created_user ON petel_schema.table_name(created_user);
CREATE INDEX idx_table_name_update_user ON petel_schema.table_name(update_user);
```

### Controller Pattern for User Tracking

**Always populate user audit fields**:

```csharp
[HttpPost]
public async Task<IActionResult> Create([FromBody] CreateRequest request)
{
    var session = GetCurrentSession();
    int? userId = int.TryParse(session.UserId, out int uid) ? uid : null;

    var entity = new MyEntity
    {
        Name = request.Name,
        Description = request.Description,
        Value = request.Value,
        CreatedAt = DateTime.UtcNow,
        CreatedUser = userId,  // ✅ Track who created
        UpdatedAt = DateTime.UtcNow,
        UpdateUser = userId
    };

    _context.MyEntities.Add(entity);
    await _context.SaveChangesAsync();
    return Ok(new { success = true });
}

[HttpPut("{id}")]
public async Task<IActionResult> Update(int id, [FromBody] UpdateRequest request)
{
    var session = GetCurrentSession();
    int? userId = int.TryParse(session.UserId, out int uid) ? uid : null;

    var entity = await _context.MyEntities.FindAsync(id);
    
    entity.Name = request.Name;
    entity.Value = request.Value;
    entity.UpdatedAt = DateTime.UtcNow;
    entity.UpdateUser = userId;  // ✅ Track who updated

    await _context.SaveChangesAsync();
    return Ok(new { success = true });
}
```

### Migration Script Pattern

**Use idempotent migrations with proper checks**:

```sql
-- Check if table exists
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT FROM pg_tables
        WHERE schemaname = 'petel_schema'
        AND tablename = 'my_table'
    ) THEN
        CREATE TABLE petel_schema.my_table (
            -- Table definition here
        );
        
        -- Create indexes
        CREATE INDEX idx_my_table_field ON petel_schema.my_table(field);
        
        RAISE NOTICE 'Table my_table created successfully';
    ELSE
        RAISE NOTICE 'Table my_table already exists';
    END IF;
END
$$;

-- Insert seed data
INSERT INTO petel_schema.my_table (field1, field2, description, value)
VALUES 
    (1, 'key1', 'תיאור בעברית', 'value1'),
    (2, 'key2', 'תיאור אחר', 'value2')
ON CONFLICT (unique_field) DO NOTHING;
```

### Benefits of Standard Structure

✅ **Full audit trail** - Know who created/modified every record
✅ **Consistent patterns** - Easy to learn and maintain
✅ **Hebrew support** - Description field for UI localization
✅ **Referential integrity** - Proper foreign key constraints
✅ **Performance** - Standard indexes on common query fields
✅ **Idempotent migrations** - Safe to run multiple times

### Common Mistakes to Avoid

```sql
-- ❌ WRONG - Redundant field names
CREATE TABLE school_attributes (
    attribute_name VARCHAR(100),
    attribute_value VARCHAR(500)
);

-- ✅ CORRECT - Concise field names
CREATE TABLE school_attributes (
    name VARCHAR(100),
    value VARCHAR(500),
    description VARCHAR(200)
);

-- ❌ WRONG - Missing audit fields
CREATE TABLE my_table (
    id SERIAL PRIMARY KEY,
    name VARCHAR(100)
);

-- ✅ CORRECT - Complete audit fields
CREATE TABLE my_table (
    id SERIAL PRIMARY KEY,
    name VARCHAR(100),
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    created_user INTEGER NULL,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    update_user INTEGER NULL
);

-- ❌ WRONG - No Hebrew description
CREATE TABLE options (
    id SERIAL PRIMARY KEY,
    code VARCHAR(50),
    value VARCHAR(100)
);

-- ✅ CORRECT - Include Hebrew description
CREATE TABLE options (
    id SERIAL PRIMARY KEY,
    code VARCHAR(50),
    description VARCHAR(200),  -- For Hebrew UI label
    value VARCHAR(100)
);
```

## School Year Attributes Pattern

**Purpose**: Store year-specific configuration values that vary across school years (e.g., required sessions for additional study programs).

### Database Schema

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

### Entity Model

```csharp
[Table("school_year_attributes")]
public class SchoolYearAttribute
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [ForeignKey("HebrewYear")]
    [Column("year_id")]
    public int YearId { get; set; }

    [Required]
    [Column("name")]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Column("description")]
    [MaxLength(200)]
    public string? Description { get; set; }

    [Required]
    [Column("value")]
    [MaxLength(500)]
    public string Value { get; set; } = string.Empty;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("created_user")]
    public int? CreatedUser { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [Column("update_user")]
    public int? UpdateUser { get; set; }

    // Navigation property
    public virtual HebrewYear? HebrewYear { get; set; }
}
```

### Controller Pattern

```csharp
[HttpGet("year/{yearId}/attribute/{attributeName}")]
public async Task<IActionResult> GetAttributeValue(int yearId, string attributeName)
{
    var session = GetCurrentSession();
    if (session == null)
        return Unauthorized(new { success = false, message = "נדרש אימות" });

    var attribute = await _context.SchoolYearAttributes
        .AsNoTracking()
        .Where(sya => sya.YearId == yearId && sya.Name == attributeName)
        .Select(sya => new
        {
            sya.Id,
            sya.YearId,
            sya.Name,
            sya.Description,
            sya.Value
        })
        .FirstOrDefaultAsync();

    if (attribute == null)
        return NotFound(new { success = false, message = $"מאפיין '{attributeName}' לא נמצא" });

    return Ok(new { success = true, data = attribute });
}

[HttpPost]
public async Task<IActionResult> CreateOrUpdateAttribute([FromBody] SchoolYearAttributeRequest request)
{
    var session = GetCurrentSession();
    int? userId = int.TryParse(session.UserId, out int uid) ? uid : null;

    var existing = await _context.SchoolYearAttributes
        .FirstOrDefaultAsync(sya => sya.YearId == request.YearId && sya.Name == request.Name);

    if (existing != null)
    {
        existing.Value = request.Value;
        existing.Description = request.Description;
        existing.UpdatedAt = DateTime.UtcNow;
        existing.UpdateUser = userId;
    }
    else
    {
        var newAttr = new SchoolYearAttribute
        {
            YearId = request.YearId,
            Name = request.Name,
            Description = request.Description,
            Value = request.Value,
            CreatedAt = DateTime.UtcNow,
            CreatedUser = userId,
            UpdatedAt = DateTime.UtcNow,
            UpdateUser = userId
        };
        _context.SchoolYearAttributes.Add(newAttr);
    }

    await _context.SaveChangesAsync();
    return Ok(new { success = true });
}
```

### Frontend Integration

```javascript
// Load year-specific attribute for UI guidance
async function loadYearAttributes() {
    try {
        const yearId = await window.SessionState.getProperty('SelectedSchoolYearId');
        const response = await fetch(
            AppConfig.getApiUrl(`schoolyearattributes/year/${yearId}/attribute/additional_study_sessions_required`),
            {
                headers: { 'Authorization': `Bearer ${sessionStorage.getItem('authToken')}` }
            }
        );

        if (response.ok) {
            const result = await response.json();
            const requiredSessions = result.data.value;
            document.getElementById('sessionsRemark').textContent = 
                `מספר מפגשים נדרש: ${requiredSessions}`;
        }
    } catch (error) {
        console.error('Error loading year attributes:', error);
    }
}
```

### Standard Attributes

**Common attribute names**:
- `additional_study_sessions_required` - Required sessions for additional study programs (מפגשי תל"ן נדרשים)
- Additional attributes can be added as needed

### Benefits

✅ **Year-specific configuration** - Different values per school year without code changes
✅ **Database-driven** - No hardcoded values in frontend or backend
✅ **Flexible schema** - String-based values support any data type
✅ **Unique constraint** - Prevents duplicate attributes per year
✅ **Cascading deletes** - Automatic cleanup when year is deleted
✅ **User tracking** - Full audit trail with created_user and update_user

### Best Practices

```csharp
// ✅ CORRECT - Use consistent attribute names
const string SESSIONS_REQUIRED = "additional_study_sessions_required";
var attribute = await GetAttributeValue(yearId, SESSIONS_REQUIRED);

// ✅ CORRECT - Parse value with validation
if (int.TryParse(attribute.Value, out int sessions))
{
    // Use sessions value
}

// ❌ WRONG - Hardcoded values
const int DEFAULT_SESSIONS = 30;  // NO! Use database attribute

// ❌ WRONG - Magic strings scattered in code
var attr = await GetAttributeValue(yearId, "sessions");  // NO! Use constant
```

## Hebrew/RTL Specific Patterns

### CSS RTL Support
```css
/* Apply to containers with Hebrew text */
.rtl-content {
    direction: rtl;
    text-align: right;
}

/* Excel worksheets automatically get RTL in export code */
worksheet.View.RightToLeft = true;
```

### Hebrew Text Input
- Use `GlobalFunctions.PureHebrewText()` for normalization
- Handles diacritics, whitespace, and special characters
- Essential for matching user input to database values

### Form Labels and Buttons
- All labels in Hebrew (right-aligned)
- Button text in Hebrew
- Use consistent terminology across the application

## Common Development Issues & Solutions

### Council Data Structure

**Problem**: Council dropdown shows "undefined" or incorrect data

**Root Cause**: Backend API returns `councilName` property, but frontend code was using `councilShortName`

**Solution**: Always use `councilName` for display and selection

```javascript
// ✅ CORRECT - Use councilName from API
const councilOptions = window.councils.map(c =>
    `<option value="${c.id}">${c.councilName}</option>`
).join('');

// ❌ WRONG - councilShortName may not exist or be undefined
const councilOptions = window.councils.map(c =>
    `<option value="${c.id}">${c.councilShortName}</option>`  // NO!
).join('');
```

**Dynamic Council Dropdown Pattern**:

```javascript
// ✅ Autocomplete/search dropdown for councils
function setupCouncilAutocomplete() {
    const searchInput = document.getElementById('schoolCouncilSearch');
    const hiddenInput = document.getElementById('schoolCouncil');
    const dropdown = document.getElementById('councilDropdown');

    // Filter councils as user types
    searchInput.addEventListener('input', function() {
        const query = this.value.toLowerCase();
        const filtered = window.councils.filter(c => 
            c.councilName.toLowerCase().includes(query)
        );
        
        // Display filtered results in dropdown
        showDropdown(filtered);
    });

    // Keyboard navigation (Arrow Up/Down, Enter, Escape)
    // Click outside to close
    // Selection updates both visible input and hidden ID
}
```

**Benefits**:
- ✅ Faster selection for large lists (100+ councils)
- ✅ Better UX - user can type to search
- ✅ Keyboard navigation support
- ✅ Mobile-friendly

### Navigation Property Null Reference

**Problem**: `NullReferenceException` when accessing navigation property

```csharp
var yearName = student.SchoolYear.YearName;  // ❌ NullReferenceException
```

**Solution**: Use eager loading or projection

```csharp
// ✅ Option 1: Eager loading
var students = await _context.Students
    .Include(s => s.SchoolYear)
    .ToListAsync();

// ✅ Option 2: Projection
var data = await _context.Students
    .Select(s => new { s.Id, YearName = s.SchoolYear.YearName })
    .ToListAsync();
```

### Excel Import Text Mismatch

**Problem**: Hebrew/numeric text from Excel doesn't match database despite appearing identical

**Solution**: Use `GlobalFunctions.PureHebrewText()` for normalization

```csharp
// ❌ WRONG - Direct comparison
var classId = classes.FirstOrDefault(c => c.ClassName == excelClassName)?.Id;

// ✅ CORRECT - Normalized comparison
var normalizedInput = GlobalFunctions.PureHebrewText(excelClassName);
var classId = classes
    .FirstOrDefault(c => GlobalFunctions.PureHebrewText(c.ClassName) == normalizedInput)
    ?.Id;
```

### Component Redeclaration Error

**Problem**: `Identifier 'myComponent' has already been declared` when returning to page

**Solution**: Use `window` scope for all component variables

```javascript
// ❌ WRONG - Page scope
let myComponent = null;

// ✅ CORRECT - Window scope
window.myComponent = window.myComponent || null;
```

### Session Property Returns Null

**Problem**: `session.GetProperty("Key")` returns null unexpectedly

**Solution**: Verify property was set and check navigation rules

```javascript
// Frontend: Set property with exact key
await window.SessionState.setProperty('MyKey', value);

// Backend: Get with exact key (case-sensitive)
var value = session.GetProperty("MyKey");

// Check page-lifecycle-config.js - property might be cleared on navigation
navigationRules: [
    { from: 'page1', to: 'page2', clearSession: ['MyKey'] }
]
```

### Schema Not Applied to Queries

**Problem**: `relation "table_name" does not exist` error

**Solution**: Verify `HasDefaultSchema`

### Modal Form Layout Pattern

**Purpose**: Create consistent, user-friendly modal forms with optimal field grouping and inline actions.

#### Side-by-Side Field Layout

**Use flexbox for related fields that benefit from horizontal grouping**:

```html
<!-- ✅ Weekly Hours and Sessions side by side -->
<div style="display: flex; gap: 15px; margin-bottom: 15px;">
    <div style="flex: 1;">
        <label for="programHours" style="display: block; margin-bottom: 5px; font-weight: 600;">
            שעות שבועיות: <span style="color: red;">*</span>
        </label>
        <input type="number" id="programHours" required
            style="width: 100%; padding: 8px; border: 1px solid #dee2e6; border-radius: 4px;">
    </div>
    <div style="flex: 1;">
        <label for="programSessions" style="display: block; margin-bottom: 5px; font-weight: 600;">
            מספר מפגשים: <span style="color: red;">*</span>
        </label>
        <input type="number" id="programSessions" required
            style="width: 100%; padding: 8px; border: 1px solid #dee2e6; border-radius: 4px;">
        <small id="sessionsRemark" style="color: #6c757d; display: block; margin-top: 4px;">
            מספר מפגשים נדרש: 32
        </small>
    </div>
</div>

<!-- ✅ Cost and Hourly Cost side by side -->
<div style="display: flex; gap: 15px; margin-bottom: 15px;">
    <div style="flex: 1;">
        <label for="programCost">עלות:</label>
        <input type="number" id="programCost" step="0.01"
            style="width: 100%; direction: ltr; text-align: left;">
    </div>
    <div style="flex: 1;">
        <label for="programHourlyCost">עלות שעתית:</label>
        <input type="number" id="programHourlyCost" step="0.01"
            style="width: 100%; direction: ltr; text-align: left;">
    </div>
</div>
```

#### Inline Action Buttons

**Add action buttons next to fields for related operations**:

```html
<div style="margin-bottom: 15px;">
    <label for="programStudents">מספר תלמידים: <span style="color: red;">*</span></label>
    <div style="display: flex; gap: 8px; align-items: flex-start;">
        <input type="number" id="programStudents" required
            style="flex: 1; padding: 8px;">
        <button type="button" id="updateStudentCountBtn" 
            onclick="updateStudentCountFromClass()" 
            title="עדכן לפי מספר תלמידים בכיתה"
            style="padding: 8px 12px; white-space: nowrap;">
            <img src="view_icon.png" alt="עדכן" class="action-icon-natural">
            <span>עדכן מכיתה</span>
        </button>
    </div>
</div>
```

```javascript
// Function to update field value from external data
async function updateStudentCountFromClass() {
    const classSelect = document.getElementById('programClass');
    const studentsInput = document.getElementById('programStudents');
    
    if (!classSelect.value) {
        alert('אנא בחר כיתה תחילה');
        return;
    }
    
    const response = await fetch(AppConfig.getApiUrl(`students?classId=${classSelect.value}`));
    if (response.ok) {
        const data = await response.json();
        studentsInput.value = data.count;
        
        // Trigger change event for dependent calculations
        studentsInput.dispatchEvent(new Event('change'));
    }
}
```

#### Dynamic Hints from Backend

**Load contextual hints from database attributes**:

```javascript
// Load year-specific guidance text
async function loadYearAttributes() {
    const yearId = await window.SessionState.getProperty('SelectedSchoolYearId');
    
    const response = await fetch(
        AppConfig.getApiUrl(`schoolyearattributes/year/${yearId}/attribute/additional_study_sessions_required`)
    );
    
    if (response.ok) {
        const result = await response.json();
        document.getElementById('sessionsRemark').textContent = 
            `מספר מפגשים נדרש: ${result.data.attributeValue}`;
    }
}

// Call when modal opens
await loadYearAttributes();
```

#### Best Practices

**Field Grouping**:
- ✅ Group related fields horizontally (weekly hours + sessions, cost + hourly cost)
- ✅ Use `display: flex; gap: 15px;` for consistent spacing
- ✅ Set `flex: 1` on child divs for equal width distribution
- ✅ Keep labels and hints within each field's container

**Action Buttons**:
- ✅ Place action buttons adjacent to the field they affect
- ✅ Use `white-space: nowrap` to prevent text wrapping
- ✅ Use descriptive tooltips with `title` attribute
- ✅ Trigger dependent calculations via `dispatchEvent(new Event('change'))`

**Dynamic Content**:
- ✅ Load contextual hints from backend attributes (avoid hardcoding)
- ✅ Update hint text asynchronously after modal renders
- ✅ Use semantic IDs for hint elements (e.g., `sessionsRemark`)

**Anti-Patterns**:
```html
<!-- ❌ WRONG - Fields in separate rows when they're related -->
<div><label>שעות שבועיות:</label><input></div>
<div><label>מספר מפגשים:</label><input></div>

<!-- ✅ CORRECT - Group related fields -->
<div style="display: flex; gap: 15px;">
    <div style="flex: 1;"><label>שעות שבועיות:</label><input></div>
    <div style="flex: 1;"><label>מספר מפגשים:</label><input></div>
</div>

<!-- ❌ WRONG - Hardcoded hint text -->
<small>ברירת מחדל: 30 מפגשים</small>

<!-- ✅ CORRECT - Dynamic hint from backend -->
<small id="sessionsRemark">טוען...</small>
```

### Collapsible Card Pattern

**Standard Implementation**: All detail cards use consistent collapsible pattern with CSS-based animations.

#### HTML Structure

```html
<div class="content-card">
    <!-- Collapsible Card -->
    <div class="detail-card collapsed">
        <div class="detail-card-header">
            <h2 class="detail-card-title">כותרת הכרטיס</h2>
            <div class="card-header-actions">
                <!-- Optional: Edit button -->
                <button id="editBtn" class="btn-icon" onclick="event.stopPropagation();">
                    <img src="edit_icon.png" alt="עריכה" class="action-icon-natural">
                </button>
                <!-- Optional: Add button (hidden when collapsed) -->
                <button id="addBtn" class="btn-icon" onclick="event.stopPropagation(); showAddModal();" title="הוסף פריט" style="display: none;">
                    <img src="Plus icon.png" alt="הוסף" class="action-icon-natural">
                </button>
                <!-- Required: Collapse toggle -->
                <button class="collapse-toggle" aria-label="הרחב/כווץ">+</button>
            </div>
        </div>
        <div class="detail-card-content">
            <!-- Card content here -->
        </div>
    </div>
</div>

#### JavaScript Structure

// Initialize collapsible cards (call once per page)
function initializeCollapsibleCards() {
    console.log('🎴 Initializing collapsible cards...');

    document.querySelectorAll('.detail-card').forEach(card => {
        const header = card.querySelector('.detail-card-header');
        const toggle = card.querySelector('.collapse-toggle');

        if (!header || !toggle) return;

        // Prevent duplicate initialization
        if (header.dataset.initialized === 'true') {
            return;
        }
        header.dataset.initialized = 'true';

        // Get action buttons
        const addButton = card.querySelector('.btn-icon[id^="add"]');

        // Hide add buttons initially (cards start collapsed)
        if (addButton) addButton.style.display = 'none';

        // Toggle button click handler
        toggle.addEventListener('click', function (e) {
            e.stopPropagation();
            toggleCardExpansion(card, toggle, addButton);
        });

        // Header click handler (excluding buttons)
        header.addEventListener('click', function (e) {
            if (e.target.closest('.btn-icon')) {
                return;
            }
            toggleCardExpansion(card, toggle, addButton);
        });
    });

    console.log('✅ Collapsible cards initialized');
}

// Toggle card expansion
function toggleCardExpansion(card, toggle, addButton) {
    const isCollapsed = card.classList.contains('collapsed');

    if (isCollapsed) {
        // Expand
        card.classList.remove('collapsed');
        card.classList.add('expanded');
        toggle.textContent = '×';

        // Show add button when expanded
        if (addButton) {
            addButton.style.display = 'inline-flex';
        }
    } else {
        // Collapse
        card.classList.remove('expanded');
        card.classList.add('collapsed');
        toggle.textContent = '+';

        // Hide add button when collapsed
        if (addButton) {
            addButton.style.display = 'none';
        }
    }
}

## Security Implementation

### JWT Token Authentication

**Architecture**: Application uses signed JWT tokens instead of GUID-based session tokens for enhanced security.

**Required Package**:
```xml
<PackageReference Include="System.IdentityModel.Tokens.Jwt" Version="8.15.0" />// Services/JwtTokenService.cs
public class JwtTokenService
{
    private readonly SecuritySettings _securitySettings;
    private readonly ILogger<JwtTokenService> _logger;
    private readonly SymmetricSecurityKey _signingKey;

    public JwtTokenService(
        IOptions<SecuritySettings> securitySettings,
        ILogger<JwtTokenService> logger)
    {
        _securitySettings = securitySettings.Value;
        _logger = logger;
        
        // Initialize signing key from configuration
        var keyBytes = Encoding.UTF8.GetBytes(_securitySettings.Jwt.SecretKey);
        _signingKey = new SymmetricSecurityKey(keyBytes);
    }

    public string GenerateSessionToken(UserSession session)
    {
        var claims = new[]
        {
            new Claim("SessionId", session.SessionId),
            new Claim("UserId", session.UserId.ToString()),
            new Claim("Username", session.Username),
            new Claim("EntityId", session.EntityId)
        };

        var credentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256);
        
        var token = new JwtSecurityToken(
            issuer: _securitySettings.Jwt.Issuer,
            audience: _securitySettings.Jwt.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(_securitySettings.Jwt.ExpirationHours),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public (bool isValid, string? sessionId) ValidateTokenAndGetSessionId(string token)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = _signingKey,
                ValidateIssuer = true,
                ValidIssuer = _securitySettings.Jwt.Issuer,
                ValidateAudience = true,
                ValidAudience = _securitySettings.Jwt.Audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };

            var principal = tokenHandler.ValidateToken(token, validationParameters, out _);
            var sessionId = principal.FindFirst("SessionId")?.Value;

            return (true, sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "JWT token validation failed");
            return (false, null);
        }
    }

    public string GenerateTempOtpToken(string username)
    {
        var claims = new[] { new Claim("Username", username) };
        var credentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256);
        
        var token = new JwtSecurityToken(
            issuer: _securitySettings.Jwt.Issuer,
            audience: _securitySettings.Jwt.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}// appsettings.json
{
  "Security": {
    "Jwt": {
      "SecretKey": "LOADED_FROM_KEY_VAULT",
      "Issuer": "PetelApp",
      "Audience": "PetelAppUsers",
      "ExpirationHours": 8
    }
  }
}// Configuration/SecuritySettings.cs
public class SecuritySettings
{
    public JwtSettings Jwt { get; set; } = new();
    
    public class JwtSettings
    {
        public string SecretKey { get; set; } = string.Empty;
        public string Issuer { get; set; } = "PetelApp";
        public string Audience { get; set; } = "PetelAppUsers";
        public int ExpirationHours { get; set; } = 8;
    }
}// Register JWT service
builder.Services.Configure<SecuritySettings>(
    builder.Configuration.GetSection("Security"));

builder.Services.AddSingleton<JwtTokenService>();

// Initialize JWT service in UserSessionService
var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var jwtService = scope.ServiceProvider.GetRequiredService<JwtTokenService>();
    var sessionService = scope.ServiceProvider.GetRequiredService<UserSessionService>();
    sessionService.SetJwtTokenService(jwtService);
}// Session/UserSessionService.cs
public class UserSessionService
{
    private JwtTokenService? _jwtTokenService;

    public void SetJwtTokenService(JwtTokenService jwtTokenService)
    {
        _jwtTokenService = jwtTokenService;
    }

    public string CreateSessionWithFullData(User user, List<Role> userRoles, int entityId)
    {
        var session = new UserSession
        {
            SessionId = Guid.NewGuid().ToString(),
            UserId = user.Id,
            Username = user.Username,
            EntityId = entityId.ToString(),
            Roles = userRoles,
            LoginTime = DateTime.UtcNow
        };

        _sessions.TryAdd(session.SessionId, session);
        
        // Return JWT token instead of GUID
        return _jwtTokenService?.GenerateSessionToken(session) ?? session.SessionId;
    }

    public UserSession? GetUserSession(string token)
    {
        // Try JWT validation first
        if (_jwtTokenService != null)
        {
            var (isValid, sessionId) = _jwtTokenService.ValidateTokenAndGetSessionId(token);
            if (isValid && sessionId != null && _sessions.TryGetValue(sessionId, out var session))
            {
                return session;
            }
        }
        
        // Fallback to GUID lookup for backward compatibility
        if (_sessions.TryGetValue(token, out var directSession))
        {
            return directSession;
        }

        return null;
    }
}

## Password Expiration & Change Flow

### Overview

When a user's password has expired (or an admin has forced a reset), the login API returns `RequiresPasswordChange: true` with a `TempToken`. The Blazor login page catches this and presents a change-password modal **before** completing login. No redirect or separate page is involved.

### Login Response Fields

```csharp
// LoginResponseDto.cs
public bool RequiresPasswordChange { get; set; }
public string? PasswordExpirationMessage { get; set; }  // Hebrew reason shown in modal
public string? TempToken { get; set; }                  // Short-lived JWT with userId claim
```

### HandleLogin Flow (Login.razor)

```
login response
 ├─ RequiresPasswordChange → show change-password modal, store TempToken
 ├─ RequiresOtpSetup       → show OTP setup modal
 ├─ RequiresOtp            → show OTP verify modal
 └─ Success                → navigate to /maindashboard
```

**CRITICAL**: `RequiresPasswordChange` is checked **before** OTP checks so an expired-password user is never accidentally sent to OTP flow.

### Password Policy — Single Regex Attribute

Policy is stored as a **single regex string** in `system_attributes`:

| name | value_type | default value |
|---|---|---|
| `Security_PasswordPolicy` | `string` | `^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{6,20}$` |

**Note**: The `value` column must be `varchar(200)` to hold the regex. The migration SQL widens it:
```sql
ALTER TABLE petel_schema.system_attributes
    ALTER COLUMN value TYPE varchar(200);
```

To change policy without restarting the service:
```sql
UPDATE petel_schema.system_attributes
SET value = '<new-regex>'
WHERE name = 'Security_PasswordPolicy';
```
Then call `POST /api/systemattributes/reload`.

### Password Policy Endpoint (Backend owns interpretation)

```
GET /api/auth/password-policy   (public, no auth required)
```

Returns the regex translated into Hebrew requirement strings:

```json
{
  "requirements": [
    "בין 6 ל-20 תווים",
    "לפחות אות קטנה אחת (a-z)",
    "לפחות אות גדולה אחת (A-Z)",
    "לפחות ספרה אחת (0-9)",
    "לפחות תו מיוחד אחד (@$!%*?&)"
  ]
}
```

**CRITICAL**: The regex is **never evaluated or interpreted on the frontend**. The Blazor login page calls this endpoint once on load and displays the returned strings as hints. All regex matching happens in `AuthController`.

### AuthController Pattern

```csharp
// GET /api/auth/password-policy
[HttpGet("password-policy")]
public IActionResult GetPasswordPolicy()
{
    const string defaultPolicy = @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{6,20}$";
    var policyAttr = _attributeCache.GetAttributeByName("Security_PasswordPolicy");
    var policyRegex = !string.IsNullOrWhiteSpace(policyAttr?.Value) ? policyAttr.Value : defaultPolicy;
    return Ok(new { requirements = GetPasswordRequirements(policyRegex) });
}

// POST /api/auth/change-expired-password
// Requires: { TempToken, OldPassword, NewPassword }
// TempToken decoded to get userId claim (no active session needed)
// Validates: not empty → regex match → new ≠ old → BCrypt update
// Returns: { success: true/false, message }   (no token — user logs in again)
```

`GetPasswordRequirements(string pattern)` is a **private static** helper on `AuthController`. It parses common lookahead patterns from the regex and returns Hebrew strings. It is called in both endpoints above — nowhere else.

### Login.razor Pattern

```csharp
// State
private List<string> _passwordRequirements = new();   // loaded from API
private bool _requiresPasswordChange = false;
private string _passwordExpirationMessage = "";
private string? _tempToken;
private string _newPassword = "";
private string _confirmNewPassword = "";
private string _passwordChangeErrorMessage = "";

// On page init (alongside entities, version, env indicator)
await LoadPasswordPolicy();   // calls GET /api/auth/password-policy

// HandleLogin
if (response.RequiresPasswordChange)
{
    _requiresPasswordChange = true;
    _tempToken = response.TempToken;
    _passwordExpirationMessage = response.PasswordExpirationMessage ?? "נדרשת החלפת סיסמה";
    return;
}
```

### Change-Password Modal

- Yellow warning banner showing `_passwordExpirationMessage` (the Hebrew reason from the backend)
- Password requirements hint rendered from `_passwordRequirements` without any local interpretation
- New password + confirm password fields with show/hide toggles
- **Local validation only**: empty check and confirm-match check
- **All regex validation is done by the API** — the error `message` from the `400 BadRequest` body is displayed directly in the modal (`white-space: pre-line` for multi-line display)
- On success: modal closes, password field cleared, login form shows "הסיסמה שונתה בהצלחה. אנא התחבר עם הסיסמה החדשה" — user must log in again with the new password

### ChangeExpiredPasswordDto (API)

```csharp
public class ChangeExpiredPasswordDto
{
    public string TempToken { get; set; }    // JWT signed by JwtTokenService, contains userId claim
    public string OldPassword { get; set; }  // Verified via BCrypt before accepting new password
    public string NewPassword { get; set; }  // Validated against Security_PasswordPolicy regex
}
```

### Anti-Patterns to Avoid

```csharp
// ❌ WRONG - Evaluating regex on frontend
if (!Regex.IsMatch(_newPassword, _passwordPolicyRegex)) { ... }  // NO! Backend only.

// ❌ WRONG - Interpreting regex on frontend
private static List<string> GetPasswordRequirements(string pattern) { ... }  // NO! Backend only.

// ❌ WRONG - Hardcoded password minimum length
if (request.NewPassword.Length < 6) { ... }  // NO! Read from Security_PasswordPolicy attribute.

// ❌ WRONG - Multiple separate boolean attributes
Security_PasswordMinLength    // NO! Use single regex attribute
Security_PasswordRequireDigit // NO!
Security_PasswordRequireUppercase // NO!

// ✅ CORRECT - Single regex attribute, interpreted server-side
var policyAttr = _attributeCache.GetAttributeByName("Security_PasswordPolicy");
if (!Regex.IsMatch(request.NewPassword, policyRegex))
{
    var message = "הסיסמה אינה עומדת בדרישות המדיניות: " +
                  string.Join(", ", GetPasswordRequirements(policyRegex));
    return BadRequest(new { success = false, message });
}
```

### SQL Migration

See `SQL/add-password-policy-attributes.sql`. Run on all environments once. Uses `ON CONFLICT (name) DO NOTHING` so it is safe to re-run.