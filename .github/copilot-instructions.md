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

#### Backend Session Storage (Primary)
```csharp
// UserSession class structure
public class UserSession
{
    public string SessionId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string UserFullName { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;  // User's Entity ID preserved throughout session
    public string EntityName { get; set; } = string.Empty;
    public string EntityTypeId { get; set; } = string.Empty;
    // Other properties...
}

// Session service usage in controllers
public class MyController : BaseController 
{
    private readonly UserSessionService _userSessionService;
    
    public MyController(UserSessionService userSessionService) 
    {
        _userSessionService = userSessionService;
    }
    
    public IActionResult MyEndpoint()
    {
        var session = GetCurrentSession(); // From BaseController
        var entityId = session?.EntityId;
        // Use entityId for data access
    }
}
```

#### Frontend Session Token Only
```javascript
// Frontend stores only the auth token, not full session data
sessionStorage.setItem('authToken', token);

// API calls include auth token in header
fetch(AppConfig.getApiUrl('endpoint'), {
    headers: {
        'Authorization': `Bearer ${sessionStorage.getItem('authToken')}`
    }
})
```

### System Attributes Pattern
Dynamic configuration via `SystemAttributes` table loaded at startup:
```csharp
// Backend: SystemAttributeLoaderHostedService loads into memory
// Frontend: AppConfig.getApiUrl('systemAttributes') for runtime access
```

### Database Conventions
- All tables in `petel_schema` namespace
- Entity Framework conventions: `PascalCase` properties → `snake_case` columns
- Audit fields: `created_at`, `updated_at` with triggers

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
- **Backend**: Session-based auth with tenant validation middleware
- **CORS**: Development allows localhost, production requires explicit domain configuration
- **SQL**: Entity Framework prevents injection, parameterized queries only

## Common Gotchas

- Frontend scripts in loaded HTML must be re-executed manually via DOM manipulation
- Tenant ID must be present in session for most API endpoints (except `/api/systemattributes`)
- PostgreSQL connection strings in `appsettings.json` use specific database names
- Hebrew text requires UTF-8 encoding and RTL CSS considerations
- **All tables MUST use ReusableTable component** - no manual table HTML
- **All icons MUST use provided PNG set** - no emoji, Unicode symbols, or custom icons
- **Context buttons MUST be positioned to the left of main section** - use fixed/sticky positioning
