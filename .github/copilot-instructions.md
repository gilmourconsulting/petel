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
```

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

```csharp
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

// Controller usage
public class MyController : BaseController 
{
    public IActionResult GetData()
    {
        var session = GetCurrentSession();
        
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
const entityId = session.entityId;

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

## Hebrew/RTL Specific Patterns

- HTML `lang="he" dir="rtl"` on all pages
- CSS variables in `theme.css` for RTL-aware spacing
- Date formatting: `new Date().toLocaleDateString('he-IL')`
- Form layouts use CSS Grid with `grid-template-areas` for RTL compatibility

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
