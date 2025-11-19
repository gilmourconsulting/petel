# Petel Educational Management System - AI Coding Guide

## Architecture Overview

**Educational Management System**: .NET 9 Web API backend + Vanilla JavaScript RTL frontend for Hebrew schools/educational institutions.

- **Backend**: ASP.NET Core Web API (`PetelApp.Api/`) with PostgreSQL + Entity Framework Core
- **Frontend**: Vanilla HTML/CSS/JS SPA (`petelapp-frontend/public/`) with Hebrew RTL support
- **Database**: PostgreSQL with `petel_schema` namespace
- **Background Jobs**: Hangfire for system attribute loading and scheduled tasks

## Critical Development Workflows

### Local Development Setup
```bash
# Start backend (from root)
cd PetelApp.Api && dotnet run
# OR: double-click "Start Local Api.cmd"

# Start frontend (from root) 
cd petelapp-frontend && npx serve public
# OR: double-click "Start Frontend.cmd"
```

Backend runs on `http://localhost:5082`, frontend on `http://localhost:3000`

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
            // ... relationships
        });

        modelBuilder.Entity<School>(entity =>
        {
            entity.ToTable("schools");  // Schema from HasDefaultSchema
            // ... relationships
        });

        // Continue for all entities - NO schema in ToTable()
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
    
    // ... properties
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

#### Frontend Configuration Requirements

**1. Environment Configuration Pattern**

**CRITICAL**: Frontend API URLs must be in environment configuration files - **NEVER hardcoded**.

```javascript
// ✅ CORRECT - env-config.js (development)
window.ENV_CONFIG = {
    API_BASE_URL: 'http://localhost:5082/api',
    ENVIRONMENT: 'development'
};

// ✅ Create environment-specific files
// env-config.production.js
window.ENV_CONFIG = {
    API_BASE_URL: 'https://api.petel-system.co.il/api',
    ENVIRONMENT: 'production'
};

// env-config.staging.js
window.ENV_CONFIG = {
    API_BASE_URL: 'https://staging-api.petel-system.co.il/api',
    ENVIRONMENT: 'staging'
};
```

**2. Centralized Configuration Usage**

```javascript
// ✅ CORRECT - config.js uses environment configuration
const ENV_CONFIG = window.ENV_CONFIG || {
    API_BASE_URL: 'http://localhost:5082/api',
    ENVIRONMENT: 'development'
};

const AppConfig = {
    apiBaseUrl: ENV_CONFIG.API_BASE_URL,  // ✅ From environment
    environment: ENV_CONFIG.ENVIRONMENT,
    
    getApiUrl(endpoint) {
        return `${this.apiBaseUrl}/${endpoint}`;
    },
    
    getDefaultFetchOptions() {
        const authToken = sessionStorage.getItem('authToken');
        return {
            headers: {
                'Content-Type': 'application/json',
                'Authorization': authToken ? `Bearer ${authToken}` : ''
            }
        };
    }
};

window.AppConfig = AppConfig;
```

**3. HTML Load Order**

```html
<head>
    <!-- ✅ Load environment config FIRST -->
    <script src="env-config.js"></script>
    <!-- Then load other scripts -->
    <script src="config.js"></script>
</head>
```

**4. Anti-Patterns to Avoid**

```javascript
// ❌ WRONG - Hardcoded API URL
const apiUrl = 'http://localhost:5082/api';  // NO!

// ❌ WRONG - Duplicate AppConfig declaration
const AppConfig = {
    apiBaseUrl: 'http://localhost:5082/api'  // NO! - Use centralized config
};

// ❌ WRONG - Missing environment configuration
// Every page must use window.AppConfig, not define its own

// ✅ CORRECT - Use centralized configuration
const response = await fetch(AppConfig.getApiUrl('schools'));
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

**Frontend:**
1. ✅ API URLs use `AppConfig.getApiUrl()` helper
2. ✅ NO hardcoded URLs anywhere in code
3. ✅ Environment-specific config files exist
4. ✅ `env-config.js` loaded BEFORE `config.js`
5. ✅ NO duplicate `AppConfig` declarations in pages
6. ✅ Deployment scripts copy correct env config

### Deployment Configuration

**1. Backend Deployment**

Environment-specific `appsettings.json` files:
- `appsettings.Development.json` - Local development
- `appsettings.Staging.json` - Staging environment
- `appsettings.Production.json` - Production environment

Each can override:
- Connection strings
- Schema names
- API keys
- Feature flags

**2. Frontend Deployment**

Deployment script pattern:

```bash
#!/bin/bash
# deploy.sh

ENVIRONMENT=$1

if [ -z "$ENVIRONMENT" ]; then
    echo "Usage: ./deploy.sh [development|staging|production]"
    exit 1
fi

echo "🚀 Deploying for environment: $ENVIRONMENT"

# Copy environment-specific config
if [ -f "public/env-config.$ENVIRONMENT.js" ]; then
    cp "public/env-config.$ENVIRONMENT.js" "public/env-config.js"
    echo "✅ Using env-config.$ENVIRONMENT.js"
else
    echo "❌ Environment config file not found"
    exit 1
fi

# Continue with deployment...
```

### Common Configuration Errors and Fixes

**Error: `relation "schools" does not exist`**
- **Cause**: Schema not being applied to queries
- **Fix**: Verify `HasDefaultSchema(_schemaName)` is in `OnModelCreating`
- **Fix**: Remove all hardcoded `"petel_schema"` strings from `ToTable()` calls

**Error: `IOptions<DatabaseSettings> could not be found`**
- **Cause**: Missing using statement
- **Fix**: Add `using Microsoft.Extensions.Options;` to `AppDbContext.cs`

**Error: `Identifier 'AppConfig' has already been declared`**
- **Cause**: Duplicate `AppConfig` declaration in page
- **Fix**: Remove page-level declaration, use centralized `config.js`

**Error: API calls return 404**
- **Cause**: Wrong API URL for environment
- **Fix**: Verify correct `env-config.js` is deployed
- **Fix**: Check `window.AppConfig.apiBaseUrl` in browser console

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

### Frontend Architecture Patterns

**Single-Page Application with Module Loading**:
- `index.html` is the shell, loads sections dynamically via `fetch('section.html')`
- `menu.html` loaded into `#sideMenuContainer` on page load
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

// ❌ Session data in frontend storage
sessionStorage.setItem('studentId', id);  // NO! - Use backend session

// ❌ Page configuration missing from page-lifecycle-config.js
// Every page must be registered in configuration
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
3. ✅ Add navigation rules if page clears session data:
   ```javascript
   { from: 'newpage', to: '*', clearSession: ['Key1', 'Key2'] }
   ```
4. ✅ Implement cleanup function in page:
   ```javascript
   function cleanupNewPage() { /* ... */ }
   window.cleanupNewPage = cleanupNewPage;
   ```
5. ✅ Use `window` scope for all component variables:
   ```javascript
   window.myComponent = window.myComponent || null;
   ```
6. ✅ Export init function to window (if needed):
   ```javascript
   window.initNewPage = initNewPage;
   ```
7. ✅ Navigate using `window.navigateTo('newpage')`

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

#### Debugging Page Lifecycle

**Console logging shows lifecycle flow**:
```
🔄 PageLifecycleManager: Navigating from student to students
🧹 Cleaning up page: student
✅ cleanupStudentPage() executed successfully
🧹 Clearing table component instances...
🗑️ Clearing session data for navigation student → students: ['SelectedStudentId', 'SelectedStudentData']
📄 Loading students.html...
✅ Successfully navigated to students
🚀 Explicitly initializing page: students
✅ loadStudentsData() executed successfully
```

**Common Issues**:
- "Identifier already declared" → Use `window` scope for variables
- "Cleanup function not found" → Export to window: `window.cleanupPage = cleanupPage`
- Session not clearing → Add navigation rule to `page-lifecycle-config.js`
- Page not initializing → Check `selfInitializing` flag and init function export
- Component still in memory → Implement proper cleanup function
**Standard Table Component**:
- **ALL tables must use ReusableTable component** from `table-component.js`
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
    { key: 'id', label: 'מספר', sortable: true, readOnly: true },
    { key: 'name', label: 'שם', sortable: true, readOnly: false },
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
    }
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
- CSS positioning example:
```css
.context-buttons-section {
    position: fixed;
    left: 280px; /* Menu width (260px) + margin (20px) */
    top: 50%;
    transform: translateY(-50%);
    display: flex;
    flex-direction: column;
    gap: 10px;
    z-index: 1000;
    width: 200px;
}

.content-card {
    margin-left: 500px; /* Menu + Context buttons + margins */
    margin-right: 20px;
}
```
- **Mobile responsive**: Switch to bottom positioning when screen width < 768px
- **Responsive breakpoints**: Adjust left positioning for tablet (240px) and large screens (300px)

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

.data-table th,
.data-table td {
    min-width: 120px; /* Minimum column width */
    white-space: nowrap;
}
```
- **Custom scrollbar styling**: Use webkit scrollbar styles for better UX
- **Mobile adjustments**: Reduce font size and padding on mobile devices
- **ReusableTable integration**: Ensure table component containers have scroll capability

### Authentication & Session Management

#### Session Data Architecture

**Two Types of Session Data**:
1. **Identity Data** (immutable during session)
   - UserId, Username, UserFullName
   - EntityId, EntityName, EntityTypeId
   - Set at login, never modified
   - Stored as UserSession class properties

2. **Session Parameters** (mutable during session)
   - CurrentSchoolYearId, SelectedSchoolId, filter states, etc.
   - Can change during session
   - Stored generically via `SetProperty(key, value)`
   - **No code changes needed when adding new parameters**

#### Backend Implementation

**CRITICAL**: Controllers must inherit from `BaseController` and inject `UserSessionService` from `PetelApp.Api.Session` namespace.

```csharp
using PetelApp.Api.Session;  // ✅ REQUIRED - UserSessionService is in Session namespace

// UserSession structure
public class UserSession
{
    // IDENTITY DATA - Properties set at login
    public string UserId { get; set; }
    public string UserFullName { get; set; }
    public string EntityId { get; set; }
    public string EntityName { get; set; }
    public string EntityTypeId { get; set; }
    
    // GENERIC STORAGE - For session parameters
    private readonly Dictionary<string, string> _properties = new();
    
    public void SetProperty(string key, string value);
    public string? GetProperty(string key);
    public Dictionary<string, string> GetAllProperties();
}

// Controller usage - MUST inherit from BaseController
public class MyController : BaseController 
{
    private readonly AppDbContext _context;
    
    // ✅ CORRECT - Inject UserSessionService and pass to base
    public MyController(
        AppDbContext context,
        UserSessionService userSessionService,  // From PetelApp.Api.Session
        ILogger<MyController> logger)
        : base(userSessionService, logger)
    {
        _context = context;
    }
    
    public IActionResult GetData()
    {
        var session = GetCurrentSession();  // Inherited from BaseController
        
        // Access identity data (properties)
        var entityId = session.EntityId;
        var userId = session.UserId;
        
        // Access session parameters (generic storage)
        var schoolYearId = session.GetProperty("CurrentSchoolYearId");
        var selectedSchool = session.GetProperty("SelectedSchoolId");
        
        // Set session parameters
        session.SetProperty("LastViewedPage", "Dashboard");
        
        return Ok(data);
    }
}
```

**Required Namespaces**:
- `using PetelApp.Api.Session;` - For `UserSessionService`
- `using PetelApp.Api.Controllers;` - For `BaseController`

**Controller Inheritance Pattern**:
```csharp
// ✅ CORRECT
public class MyController : BaseController
{
    public MyController(
        UserSessionService userSessionService,  // Must inject
        ILogger<MyController> logger)           // Must inject
        : base(userSessionService, logger)      // Must pass to base
    {
    }
}

// ❌ WRONG - Missing BaseController inheritance
public class MyController : ControllerBase  // NO!

// ❌ WRONG - Missing UserSessionService injection
public MyController(ILogger<MyController> logger)  // NO!

// ❌ WRONG - Not passing to base constructor
public MyController(UserSessionService service, ILogger logger)
{
    // Missing: : base(service, logger)
}
```
**Session API Endpoints**:
- `GET /api/session` - Get identity data + all properties
- `POST /api/session/property` - Set: `{ "key": "CurrentSchoolYearId", "value": "123" }`
- `GET /api/session/property/{key}` - Get specific parameter
- `GET /api/session/properties` - Get all parameters
- `DELETE /api/session/property/{key}` - Remove parameter

#### Frontend Token-Only Storage

**CRITICAL**: Frontend stores **ONLY** the auth token in sessionStorage.

```javascript
// ✅ CORRECT - Only auth token
sessionStorage.setItem('authToken', token);
const token = sessionStorage.getItem('authToken');

// ❌ WRONG - No session data in frontend
localStorage.setItem('userId', userId);              // DON'T DO THIS
sessionStorage.setItem('schoolYearId', yearId);      // DON'T DO THIS
sessionStorage.setItem('entityId', entityId);        // DON'T DO THIS

// ✅ CORRECT - Get from backend
const session = await sessionManager.getSessionInfo();
const userId = session.userId;
const entityId = await sessionManager.getSessionProperty('CurrentSchoolYearId');

// ✅ CORRECT - Set parameter in backend
await sessionManager.setSessionProperty('CurrentSchoolYearId', yearId);

// ✅ CORRECT - Get parameter from backend
const yearId = await sessionManager.getSessionProperty('CurrentSchoolYearId');
```

**SessionManager API** (`session-manager.js`):
```javascript
class SessionManager {
    // Token management (frontend storage)
    setToken(token)
    getToken()
    clearToken()
    isAuthenticated()
    
    // Session data (backend API calls)
    async getSessionInfo()                    // Identity + all properties
    async setSessionProperty(key, value)      // Set parameter
    async getSessionProperty(key)             // Get parameter
    async getAllSessionProperties()           // Get all parameters
    
    logout()
}
```

**Common Session Parameters**:
- `CurrentSchoolYearId` - Active school year
- `SelectedSchoolId` - For multi-school entities
- `FilterSettings_{PageName}` - Page filters
- `LastViewedPage` - Navigation state
- Custom parameters (add without code changes)

#### When to Use Properties vs Generic Storage

**Use Direct Properties** (UserSession class):
- User identity: UserId, Username, UserFullName
- User's entity: EntityId, EntityName, EntityTypeId
- Session metadata: SessionId, CreatedAt, LastAccessedAt
- **These NEVER change during session**

**Use Generic Storage** (SetProperty/GetProperty):
- School year selection
- School selection (multi-school entities)
- Page filters and view state
- Report parameters
- **Any data that can change during session**
- **Prevents code changes for new parameters**

### System Attributes vs Session Data

**CRITICAL DISTINCTION**: System Attributes and Session Data are completely separate concepts.

#### System Attributes (Global Configuration)
- **Scope**: Application-wide, shared by ALL users and sessions
- **Storage**: In-memory cache (`SystemAttributeCache`), loaded from database at startup
- **Lifecycle**: Loaded once, persists for application lifetime
- **Access**: Available without authentication (`[AllowAnonymous]`)
- **Purpose**: System configuration, dropdown values, feature flags, constants
- **Examples**: 
  - School types list
  - Grade levels
  - Subject codes
  - Feature toggles
  - System-wide settings

```csharp
// Backend: Access system attributes
public class MyController : BaseController 
{
    private readonly SystemAttributeCache _systemCache;
    
    public IActionResult GetData()
    {
        // System attributes - global configuration
        var schoolTypes = _systemCache.GetAttributesByCategory(1);
        
        // User session - user-specific data
        var session = GetCurrentSession();
        var entityId = session.EntityId;
        
        return Ok(data);
    }
}
```

```javascript
// Frontend: System attributes available without session
const response = await fetch(AppConfig.getApiUrl('systemAttributes'));
const systemAttrs = await response.json();
```

#### Session Data (User Context)
- **Scope**: User-specific, isolated per session
- **Storage**: In-memory per session (`UserSessionService`)
- **Lifecycle**: Created at login, destroyed at logout
- **Access**: Requires authentication token
- **Purpose**: User identity, current selections, navigation state
- **Types**:
  1. **Identity Data** (immutable): UserId, EntityId, UserFullName
  2. **Session Parameters** (mutable): CurrentSchoolYearId, SelectedFilters

```javascript
// Frontend: Session data requires authentication
const session = await sessionManager.getSessionInfo(); // Needs auth token
const userId = session.userId;
const schoolYearId = await sessionManager.getSessionProperty('CurrentSchoolYearId');
```

#### When to Use Each

**Use System Attributes When:**
- Data is the same for ALL users (dropdown options, system config)
- Data rarely changes (loaded at startup)
- No authentication needed to access
- Data comes from system configuration tables

**Use Session Data When:**
- Data is user-specific (current selections, preferences)
- Data changes during user's session
- Requires user authentication
- Related to user's current context/state

#### Anti-Patterns to Avoid

```javascript
// ❌ WRONG - Storing system config in session
await sessionManager.setSessionProperty('SchoolTypes', JSON.stringify(types)); // NO!

// ✅ CORRECT - Get system config from system attributes
const types = await fetch(AppConfig.getApiUrl('systemAttributes/by-category/1'));

// ❌ WRONG - Storing user selections in system attributes
systemCache.SetAttribute('CurrentUserSchoolYear', yearId); // NO!

// ✅ CORRECT - Store user selections in session
await sessionManager.setSessionProperty('CurrentSchoolYearId', yearId);
```

#### Access Patterns

**System Attributes**:
- Endpoint: `/api/systemAttributes` (no auth required)
- Frontend: `config.js` AppConfig helper
- Backend: Inject `SystemAttributeCache` service
- Refresh: Rarely (admin action or app restart)

**Session Data**:
- Endpoint: `/api/session` (requires auth token)
- Frontend: `session-manager.js` SessionManager helper
- Backend: Inherit from `BaseController`, use `GetCurrentSession()`
- Refresh: Per request or on-demand

### System Attributes Pattern
Dynamic configuration via `SystemAttributes` table loaded at startup:
```csharp
// Backend: SystemAttributeLoaderHostedService loads into memory at startup
// SystemAttributeCache provides singleton access
// Frontend: AppConfig.getApiUrl('systemAttributes') for runtime access
// NO AUTHENTICATION REQUIRED - these are global configuration values
```

### Global Helper Functions

**Backend Utility Service**: `GlobalFunctions` provides reusable helper methods for common data operations and text processing.

**File Location**: `PetelApp.Api/Services/GlobalSystemFunctions.cs`

#### Service Registration and Usage

```csharp
// Service is registered in Program.cs as scoped service
builder.Services.AddScoped<GlobalFunctions>();

// Inject into controllers or other services
public class MyController : BaseController
{
    private readonly GlobalFunctions _globalFunctions;

    public MyController(GlobalFunctions globalFunctions)
    {
        _globalFunctions = globalFunctions;
    }
    
    public async Task<IActionResult> ProcessData()
    {
        // Use instance methods
        var classId = await _globalFunctions.GetClassIdByName("א-1", schoolYearId);
        
        // Use static methods (no injection needed)
        var pureText = GlobalFunctions.PureHebrewText("א-1");
        
        return Ok();
    }
}
```

#### Available Functions

**1. Pure Hebrew Text and Numbers** (Static Method)
- **Purpose**: Extract only Hebrew letters (א-ת) and digits (0-9) from text
- **Removes**: Spaces, dashes, punctuation, Latin letters, special characters
- **Use Case**: Normalizing text for comparison (class names, council names, IDs with formatting)
```csharp
// Static - no injection needed
var pure = GlobalFunctions.PureHebrewText("א-1");      // Returns: "א1"
var pure = GlobalFunctions.PureHebrewText("ב׳ 2");     // Returns: "ב2"
var pure = GlobalFunctions.PureHebrewText("כיתה 12");  // Returns: "כיתה12"
var pure = GlobalFunctions.PureHebrewText("3rd ג");    // Returns: "3ג"
```

**2. Get School Year by IDs**
- **Purpose**: Find school_year ID by year_id and school_id
- **Database**: Queries `SchoolYears` table
- **Returns**: `int?` (null if not found)
```csharp
var schoolYearId = await _globalFunctions.GetSchoolYearByIds(
    yearId: 5,      // Hebrew year ID from hebrew_years.id
    schoolId: 123   // School entity ID from entities.id
);
```

**3. Get School Year by Hebrew Year and Symbol**
- **Purpose**: Multi-step lookup: Hebrew year text → school symbol → school_year ID
- **Steps**: 
  1. Find year_id from `HebrewYears` by year_name
  2. Find school_id from `Entities` by symbol
  3. Call `GetSchoolYearByIds()`
- **Returns**: `int?` (null if any step fails)
```csharp
var schoolYearId = await _globalFunctions.GetSchoolYearByHebrewYearAndSymbol(
    hebrewYear: "תשפ״ה",     // Hebrew year text from hebrew_years.year_name
    schoolSymbol: "1234"     // School symbol from entities.symbol
);
```

**4. Get Class ID by Name**
- **Purpose**: Find class ID by comparing pure Hebrew/numeric text of class names
- **Comparison**: Uses `PureHebrewText()` to normalize both input and database names
- **Database**: Queries `SchoolClasses` filtered by school_year_id
- **Returns**: `int?` (null if not found)
```csharp
var classId = await _globalFunctions.GetClassIdByName(
    className: "א-1",        // Class name (with or without punctuation/spaces)
    schoolYearId: 42         // School year context from school_years.id
);
// Matches against normalized class names: "א-1", "א 1", "א1" all become "א1"
```

**5. Get Council by Name**
- **Purpose**: Find council ID by comparing pure Hebrew/numeric text of council short names
- **Comparison**: Uses `PureHebrewText()` to normalize against `council_short_name` field
- **Database**: Queries `Councils` table
- **Returns**: `int?` (null if not found)
```csharp
var councilId = await _globalFunctions.GetCouncilByName(
    councilName: "ירושלים"  // Council name (compares to councils.council_short_name)
);
```

**6. Get Council by Code**
- **Purpose**: Find council ID by exact council code match
- **Database**: Queries `Councils` table by `council_code` field
- **Returns**: `int?` (null if not found)
```csharp
var councilId = await _globalFunctions.GetCouncilByCode(
    councilCode: "3000"      // Council code from councils.council_code
);
```

#### Best Practices

**When to Use GlobalFunctions**:
- ✅ Text normalization for Hebrew/numeric comparisons
- ✅ Common lookup patterns used across multiple controllers
- ✅ Entity resolution by name/code/symbol
- ✅ Multi-step data retrieval workflows
- ✅ Import/export operations requiring fuzzy matching

**Error Handling**:
- All async methods return `null` on failure (not throwing exceptions)
- Always check for null before using results
- Log context when null is returned for debugging
```csharp
var classId = await _globalFunctions.GetClassIdByName(className, yearId);
if (classId == null)
{
    _logger.LogWarning("Class not found: {ClassName} in year {YearId}", className, yearId);
    return NotFound($"Class '{className}' not found");
}
```

**Combining Functions**:
```csharp
// Example: Process student import by class name and Hebrew year
var schoolYearId = await _globalFunctions.GetSchoolYearByHebrewYearAndSymbol(
    hebrewYear: "תשפ״ה",
    schoolSymbol: "1234"
);

if (schoolYearId == null)
{
    return NotFound("School year not found for תשפ״ה at school 1234");
}

var classId = await _globalFunctions.GetClassIdByName(
    className: studentData.ClassName,
    schoolYearId: schoolYearId.Value
);

if (classId == null)
{
    return NotFound($"Class '{studentData.ClassName}' not found in school year {schoolYearId}");
}

// Process student with resolved IDs
var student = new SchoolStudent
{
    ClassId = classId.Value,
    SchoolYearId = schoolYearId.Value,
    // ... other fields
};
```

**Static vs Instance Methods**:
- **Static**: `PureHebrewText()` - Text processing only, no database access
  - Call directly: `GlobalFunctions.PureHebrewText(text)`
  - No dependency injection needed
  - Can be used in static contexts
  
- **Instance**: All other methods - Require database access via `AppDbContext`
  - Require injection: `_globalFunctions.GetSchoolYearByIds(...)`
  - Must be registered as scoped service
  - Participate in EF Core change tracking

**Performance Considerations**:
- Name-based lookups load all records into memory for comparison
  - `GetClassIdByName()` loads all classes for the school year
  - `GetCouncilByName()` loads all councils
- Use code-based lookups when possible for better performance
  - `GetCouncilByCode()` uses indexed database query
- Consider caching results for frequently-called lookups

**Integration with File Imports**:
```csharp
// Example: Excel import with fuzzy matching
foreach (var row in excelRows)
{
    // Normalize input data
    var normalizedClass = GlobalFunctions.PureHebrewText(row["כיתה"]);
    var normalizedCouncil = GlobalFunctions.PureHebrewText(row["רשות"]);
    
    // Resolve IDs using global functions
    var classId = await _globalFunctions.GetClassIdByName(row["כיתה"], schoolYearId);
    var councilId = await _globalFunctions.GetCouncilByName(row["רשות"]);
    
    if (classId == null || councilId == null)
    {
        // Log validation error with original and normalized values
        errors.Add($"Row {rowNum}: Class '{row["כיתה"]}' (normalized: '{normalizedClass}') " +
                   $"or Council '{row["רשות"]}' (normalized: '{normalizedCouncil}') not found");
        continue;
    }
    
    // Create record with resolved IDs
}
```

## Hebrew/RTL Specific Patterns

- HTML `lang="he" dir="rtl"` on all pages
- CSS variables in `theme.css` for RTL-aware spacing
- Date formatting: `new Date().toLocaleDateString('he-IL')`
- Form layouts use CSS Grid with `grid-template-areas` for RTL compatibility
- **Hebrew text normalization**: Always use `GlobalFunctions.PureHebrewText()` for comparisons
- **Mixed Hebrew/numeric content**: `PureHebrewText()` preserves both Hebrew letters and digits

## Integration Points

### API Communication Pattern
```javascript
// All API calls through AppConfig helper
fetch(AppConfig.getApiUrl('systemAttributes'))
    .then(response => response.json())
    .then(data => /* handle response */);
```

### Cross-Component Communication
- School year changes dispatch `schoolYearChanged` CustomEvent
- Components listen via `window.addEventListener('schoolYearChanged', handler)`
- Global functions exposed on `window` object for inter-module access

## Security Patterns

- **Frontend**: Session storage for auth tokens, automatic logout on token expiry
- **Backend**: Session-based auth with entity validation middleware
- **CORS**: Development allows localhost, production requires explicit domain configuration
- **SQL**: Entity Framework prevents injection, parameterized queries only

## Common Gotchas

- Frontend scripts in loaded HTML must be re-executed manually via DOM manipulation
- Entity ID must be present in session for most API endpoints (except `/api/systemattributes`)
- PostgreSQL connection strings in `appsettings.json` use specific database names
- Hebrew text requires UTF-8 encoding and RTL CSS considerations
- **All tables MUST use ReusableTable component** - no manual table HTML
- **All icons MUST use provided PNG set** - no emoji, Unicode symbols, or custom icons
- **Context buttons MUST be positioned to the left of main section** - use fixed/sticky positioning
- **GlobalFunctions must be injected** - except for static `PureHebrewText()` method
- **Always normalize Hebrew text before comparison** - use `PureHebrewText()` for fuzzy matching
- **Check for null after GlobalFunctions calls** - all lookup methods return `int?`

### Documents Table Component

**Specialized Component**: `DocumentsTableComponent` from `documents-table.js` provides a complete document management interface with upload, download, delete, and filtering capabilities.

**Purpose**: Manage entity-specific documents (school documents, student documents, etc.) with type categorization, file upload/download, and CRUD operations.

#### Component Architecture

**File Location**: `petelapp-frontend/public/documents-table.js`

**Key Features**:
- Document type filtering (dropdown + pills)
- File upload with drag-and-drop support
- Download/delete actions per document
- Automatic refresh after operations
- Entity-scoped document lists (school, student, etc.)
- Backend session integration for context

#### Basic Usage Pattern

```javascript
// ✅ CORRECT - Use window scope to prevent redeclaration
window.documentsComponent = window.documentsComponent || null;

/**
 * Initialize documents table component
 */
async function initializeDocuments() {
    try {
        console.log('📄 Initializing documents table...');

        // Get entity context from backend session
        const entityId = await window.SessionState.getProperty('SelectedStudentId');
        const selectedYearId = await window.SessionState.getProperty('SelectedYearId');

        if (!entityId) {
            console.error('❌ No entity ID in session');
            const container = document.getElementById('documentsTableContainer');
            if (container) {
                container.innerHTML = `<div class="table-error">לא נמצא מזהה ישות</div>`;
            }
            return;
        }

        // ✅ Create component instance
        window.documentsComponent = new DocumentsTableComponent('documentsTableContainer', {
            showUploadForm: false,          // Show upload UI inline (false = use modal)
            allowDelete: false,             // Enable delete buttons
            allowDownload: true,            // Enable download buttons
            allowUpload: true,              // Enable upload functionality
            entityId: entityId,             // Entity ID (student, school, etc.)
            entityType: 'student',          // Entity type: 'student', 'school', etc.
            yearId: selectedYearId,         // School year context (optional)
            onUploadSuccess: (result) => {
                console.log('📄 Document uploaded:', result);
                // Optional: Show success message, refresh data
            },
            onDeleteSuccess: (documentId) => {
                console.log('🗑 Document deleted:', documentId);
                // Optional: Show success message
            },
            onError: (error) => {
                console.error('❌ Document operation error:', error);
                alert('שגיאה בפעולה על המסמך');
            }
        });

        // ✅ Create global reference for button onclick handlers
        window['documentsTableInstance_documentsTableContainer'] = window.documentsComponent;

        // ✅ Initialize component (loads document types and documents)
        await window.documentsComponent.init();

        console.log('✅ Documents table initialized');
    } catch (error) {
        console.error('❌ Error initializing documents:', error);
        const container = document.getElementById('documentsTableContainer');
        if (container) {
            container.innerHTML = `<div class="table-error">שגיאה בטעינת רשימת המסמכים</div>`;
        }
    }
}

// ✅ Export to window for PageLifecycleManager
window.initializeDocuments = initializeDocuments;
```

#### Configuration Options

```javascript
new DocumentsTableComponent(containerId, {
    // UI Controls
    showUploadForm: false,        // true = inline upload form, false = modal dialog
    allowDelete: true,            // Show/hide delete buttons
    allowDownload: true,          // Show/hide download buttons
    allowUpload: true,            // Enable upload functionality
    
    // Entity Context
    entityId: '123',              // Required: Entity ID (student_id, school_id, etc.)
    entityType: 'student',        // Required: 'student', 'school', etc.
    yearId: '5',                  // Optional: School year context for filtering
    
    // Callbacks
    onUploadSuccess: (result) => {
        // Called after successful upload
        // result contains: { documentId, fileName, message }
    },
    onDeleteSuccess: (documentId) => {
        // Called after successful delete
        // documentId: ID of deleted document
    },
    onDownloadSuccess: (documentId, fileName) => {
        // Called after successful download
        // Optional: Track downloads, show notifications
    },
    onError: (error) => {
        // Called on any operation error
        // error contains: { message, operation, details }
    },
    onFilterChange: (documentTypeId) => {
        // Called when document type filter changes
        // documentTypeId: Selected type ID (null = all types)
    }
});
```

#### Entity Type Values

**Standard Entity Types**:
- `'student'` - Student documents (assignments, reports, etc.)
- `'school'` - School-level documents (policies, forms, etc.)
- `'class'` - Class-specific documents (syllabi, schedules, etc.)
- `'teacher'` - Teacher documents (certifications, etc.)

#### HTML Container Structure

```html
<!-- Document management section in page -->
<div class="section-card">
    <div class="section-header">
        <h2>מסמכים</h2>
        <div class="section-actions">
            <button onclick="documentsComponent.showUploadDialog()" class="btn-primary">
                <img src="upload_icon.png" alt="העלאה" class="action-icon-natural">
                העלאת מסמך
            </button>
        </div>
    </div>
    
    <!-- ✅ Container for DocumentsTableComponent -->
    <div id="documentsTableContainer">
        <div class="loading-spinner">טוען מסמכים...</div>
    </div>
</div>
```

#### Component Methods

**Public Methods** (after initialization):

```javascript
// Refresh document list
await documentsComponent.refresh();

// Show upload dialog (if modal mode)
documentsComponent.showUploadDialog();

// Filter by document type
documentsComponent.filterByType(documentTypeId);  // null = show all

// Get current filter state
const currentFilter = documentsComponent.getCurrentFilter();

// Get all loaded documents
const documents = documentsComponent.getDocuments();

// Get available document types
const types = documentsComponent.getDocumentTypes();
```

#### Cleanup Pattern

**CRITICAL**: Always cleanup component when leaving page:

```javascript
/**
 * Cleanup documents component when leaving page
 */
function cleanupDocuments() {
    try {
        console.log('🧹 Cleaning up documents component...');

        if (window.documentsComponent) {
            // Call component cleanup if it has one
            if (typeof window.documentsComponent.cleanup === 'function') {
                window.documentsComponent.cleanup();
            }
            
            // Clear table reference
            if (window.documentsComponent.documentsTable) {
                window.documentsComponent.documentsTable = null;
            }
            
            // Clear document types
            if (window.documentsComponent.documentTypes) {
                window.documentsComponent.documentTypes = [];
            }
            
            // Null the component
            window.documentsComponent = null;
        }

        // Remove global reference
        if (window['documentsTableInstance_documentsTableContainer']) {
            delete window['documentsTableInstance_documentsTableContainer'];
        }

        // Clear container HTML
        const container = document.getElementById('documentsTableContainer');
        if (container) {
            container.innerHTML = '<div class="loading-spinner">טוען מסמכים...</div>';
        }

        console.log('✅ Documents component cleanup complete');
    } catch (error) {
        console.error('❌ Error during documents cleanup:', error);
    }
}

// ✅ Export cleanup function
window.cleanupDocuments = cleanupDocuments;
```

#### Integration with Page Lifecycle

**In page cleanup function**:

```javascript
function cleanupStudentPage() {
    console.log('🧹 Cleaning up student page...');
    
    try {
        // ✅ Call documents cleanup
        if (typeof cleanupDocuments === 'function') {
            cleanupDocuments();
        }
        
        // Other page cleanup...
        
        console.log('✅ Student page cleanup complete');
    } catch (error) {
        console.error('❌ Error during student page cleanup:', error);
    }
}

window.cleanupStudentPage = cleanupStudentPage;
```

#### Backend API Endpoints

**Documents API** (`/api/documents`):

```csharp
// GET /api/documents/entity/{entityType}/{entityId}
// Get all documents for entity
// Optional query param: ?yearId=5

// POST /api/documents/upload
// Upload document (multipart/form-data)
// Body: { file, documentTypeId, entityType, entityId, yearId? }

// GET /api/documents/{id}/download
// Download document by ID
// Returns: File stream with content-disposition header

// DELETE /api/documents/{id}
// Delete document by ID
// Returns: 200 OK or 404 Not Found
```

**Document Types API** (`/api/documenttypes`):

```csharp
// GET /api/documenttypes
// Get all document types for dropdowns
// Returns: [{ id, typeName, categoryId, isActive }]

// GET /api/documenttypes/by-category/{categoryId}
// Get document types filtered by category
```

#### Styling and Customization

**CSS Classes** (defined in `documents-table.js`):

```css
/* Document table container */
.documents-table-container {
    width: 100%;
    overflow-x: auto;
}

/* Document type filter pills */
.document-type-filters {
    display: flex;
    gap: 8px;
    margin-bottom: 15px;
    flex-wrap: wrap;
}

.filter-pill {
    padding: 6px 12px;
    border-radius: 20px;
    background-color: #f0f0f0;
    cursor: pointer;
    transition: all 0.2s;
}

.filter-pill.active {
    background-color: var(--primary-color);
    color: white;
}

/* Upload dialog modal */
.upload-modal {
    position: fixed;
    top: 0;
    left: 0;
    width: 100%;
    height: 100%;
    background-color: rgba(0, 0, 0, 0.5);
    z-index: 2000;
}

.upload-modal-content {
    background-color: white;
    border-radius: 8px;
    padding: 20px;
    max-width: 500px;
    margin: 50px auto;
}

/* Document action buttons */
.doc-action-btn {
    padding: 4px 8px;
    border: 1px solid #dee2e6;
    border-radius: 4px;
    background-color: transparent;
    cursor: pointer;
    margin-left: 5px;
}

.doc-action-btn:hover {
    background-color: #f8f9fa;
}
```

#### Common Use Cases

**1. Student Documents Page**:
```javascript
// In student.html
window.documentsComponent = window.documentsComponent || null;

async function initializeStudentDocuments() {
    const studentId = await window.SessionState.getProperty('SelectedStudentId');
    const yearId = await window.SessionState.getProperty('SelectedYearId');
    
    window.documentsComponent = new DocumentsTableComponent('documentsTableContainer', {
        entityId: studentId,
        entityType: 'student',
        yearId: yearId,
        allowUpload: true,
        allowDelete: false,
        allowDownload: true
    });
    
    await window.documentsComponent.init();
}
```

**2. School Documents Page**:
```javascript
// In schooldocuments.html
window.documentsComponent = window.documentsComponent || null;

async function initializeSchoolDocuments() {
    const schoolId = await window.SessionState.getProperty('SelectedSchoolId');
    const yearId = await window.SessionState.getProperty('SelectedYearId');
    
    window.documentsComponent = new DocumentsTableComponent('documentsTableContainer', {
        entityId: schoolId,
        entityType: 'school',
        yearId: yearId,
        allowUpload: true,
        allowDelete: true,  // School admins can delete
        allowDownload: true,
        showUploadForm: false  // Use modal for upload
    });
    
    await window.documentsComponent.init();
}
```

**3. Read-Only Document List**:
```javascript
// View-only mode for students/parents
window.documentsComponent = new DocumentsTableComponent('documentsTableContainer', {
    entityId: studentId,
    entityType: 'student',
    allowUpload: false,
    allowDelete: false,
    allowDownload: true
});
```

#### Error Handling

**Component errors are handled via callbacks**:

```javascript
window.documentsComponent = new DocumentsTableComponent('documentsTableContainer', {
    entityId: studentId,
    entityType: 'student',
    onError: (error) => {
        console.error('❌ Document error:', error);
        
        // Show user-friendly message based on error type
        switch (error.operation) {
            case 'upload':
                alert('שגיאה בהעלאת המסמך. אנא נסה שוב.');
                break;
            case 'download':
                alert('שגיאה בהורדת המסמך. אנא נסה שוב.');
                break;
            case 'delete':
                alert('שגיאה במחיקת המסמך. אנא נסה שוב.');
                break;
            default:
                alert('שגיאה בטעינת המסמכים. אנא רענן את העמוד.');
        }
    }
});
```

#### Performance Considerations

**Component is optimized for**:
- ✅ Lazy loading of documents (loads on init)
- ✅ File size validation before upload
- ✅ Progress indication for uploads
- ✅ Efficient filtering (client-side after initial load)
- ✅ Debounced search/filter operations

**Best practices**:
- Always cleanup component when leaving page
- Use `yearId` filter to limit initial document load
- Implement file size limits (handled by backend)
- Show loading states during operations

#### Anti-Patterns to Avoid

```javascript
// ❌ WRONG - Not using window scope
let documentsComponent = new DocumentsTableComponent(...);  // Will cause redeclaration error

// ❌ WRONG - Missing cleanup
function cleanupPage() {
    // Missing: cleanupDocuments()
}

// ❌ WRONG - Not handling errors
new DocumentsTableComponent('container', {
    entityId: id,
    entityType: 'student'
    // Missing: onError callback
});

// ❌ WRONG - Creating multiple instances for same container
window.documentsComponent = new DocumentsTableComponent('container', {...});
window.documentsComponent = new DocumentsTableComponent('container', {...});  // NO!

// ❌ WRONG - Not checking for entity ID
const entityId = await window.SessionState.getProperty('SelectedStudentId');
// Missing: if (!entityId) return;
window.documentsComponent = new DocumentsTableComponent('container', {
    entityId: entityId  // Could be null!
});
```

#### Integration Checklist

When adding documents to a page:

1. ✅ Use `window` scope for component variable
2. ✅ Add HTML container with unique ID
3. ✅ Get entity ID from backend session
4. ✅ Validate entity ID exists before creating component
5. ✅ Provide all required configuration options
6. ✅ Implement error handling via `onError` callback
7. ✅ Create global reference for onclick handlers
8. ✅ Implement cleanup function that:
   - Calls component cleanup method
   - Nulls the component instance
   - Removes global references
   - Clears container HTML
9. ✅ Export cleanup to window
10. ✅ Call cleanup in page lifecycle cleanup function
11. ✅ Add upload button in UI if needed
12. ✅ Test navigation away and return (no redeclaration errors)

#### Complete Example

**student.html** (complete implementation):

```html
<!-- HTML -->
<div class="section-card">
    <div class="section-header">
        <h2>מסמכי תלמיד</h2>
        <div class="section-actions">
            <button onclick="documentsComponent.showUploadDialog()" class="btn-primary">
                <img src="upload_icon.png" alt="העלאה" class="action-icon-natural">
                העלאת מסמך
            </button>
        </div>
    </div>
    <div id="documentsTableContainer">
        <div class="loading-spinner">טוען מסמכים...</div>
    </div>
</div>

<script src="documents-table.js"></script>
<script>
// ✅ Use window scope
window.documentsComponent = window.documentsComponent || null;

/**
 * Initialize student documents table
 */
async function initializeStudentDocuments() {
    try {
        console.log('📄 Initializing student documents...');

        const studentId = await window.SessionState.getProperty('SelectedStudentId');
        const yearId = await window.SessionState.getProperty('SelectedYearId');

        if (!studentId) {
            console.error('❌ No student ID in session');
            const container = document.getElementById('documentsTableContainer');
            if (container) {
                container.innerHTML = `<div class="table-error">לא נמצא מזהה תלמיד</div>`;
            }
            return;
        }

        window.documentsComponent = new DocumentsTableComponent('documentsTableContainer', {
            showUploadForm: false,
            allowDelete: false,
            allowDownload: true,
            allowUpload: true,
            entityId: studentId,
            entityType: 'student',
            yearId: yearId,
            onUploadSuccess: (result) => {
                console.log('📄 Document uploaded:', result);
                alert('המסמך הועלה בהצלחה');
            },
            onDeleteSuccess: (documentId) => {
                console.log('🗑 Document deleted:', documentId);
                alert('המסמך נמחק בהצלחה');
            },
            onError: (error) => {
                console.error('❌ Document error:', error);
                alert('שגיאה בפעולה על המסמך');
            }
        });

        window['documentsTableInstance_documentsTableContainer'] = window.documentsComponent;
        await window.documentsComponent.init();

        console.log('✅ Student documents initialized');
    } catch (error) {
        console.error('❌ Error initializing documents:', error);
        const container = document.getElementById('documentsTableContainer');
        if (container) {
            container.innerHTML = `<div class="table-error">שגיאה בטעינת המסמכים</div>`;
        }
    }
}

/**
 * Cleanup documents component
 */
function cleanupStudentDocuments() {
    try {
        console.log('🧹 Cleaning up student documents...');

        if (window.documentsComponent) {
            if (typeof window.documentsComponent.cleanup === 'function') {
                window.documentsComponent.cleanup();
            }
            
            if (window.documentsComponent.documentsTable) {
                window.documentsComponent.documentsTable = null;
            }
            
            if (window.documentsComponent.documentTypes) {
                window.documentsComponent.documentTypes = [];
            }
            
            window.documentsComponent = null;
        }

        if (window['documentsTableInstance_documentsTableContainer']) {
            delete window['documentsTableInstance_documentsTableContainer'];
        }

        const container = document.getElementById('documentsTableContainer');
        if (container) {
            container.innerHTML = '<div class="loading-spinner">טוען מסמכים...</div>';
        }

        console.log('✅ Student documents cleanup complete');
    } catch (error) {
        console.error('❌ Error during documents cleanup:', error);
    }
}

/**
 * Page cleanup
 */
function cleanupStudentPage() {
    console.log('🧹 Cleaning up student page...');
    
    try {
        cleanupStudentDocuments();
        // Other cleanup...
        console.log('✅ Student page cleanup complete');
    } catch (error) {
        console.error('❌ Error during page cleanup:', error);
    }
}

// Export functions
window.initializeStudentDocuments = initializeStudentDocuments;
window.cleanupStudentDocuments = cleanupStudentDocuments;
window.cleanupStudentPage = cleanupStudentPage;
</script>
```

This provides a complete, production-ready documents management implementation following all coding standards and lifecycle patterns.