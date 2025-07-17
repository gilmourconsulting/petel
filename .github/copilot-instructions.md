# Petel Educational Management System - AI Coding Guide

## Architecture Overview

**Multi-Tenant Educational SaaS**: .NET 9 Web API backend + Vanilla JavaScript RTL frontend for Hebrew schools/educational institutions.

- **Backend**: ASP.NET Core Web API (`PetelApp.Api/`) with PostgreSQL + Entity Framework Core
- **Frontend**: Vanilla HTML/CSS/JS SPA (`petelapp-frontend/public/`) with Hebrew RTL support
- **Database**: PostgreSQL with `petel_schema` namespace, multi-tenant architecture
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

### Multi-Tenant Request Flow
1. **TenantMiddleware** (`Middleware/TenantMiddleware.cs`) extracts tenant ID from headers/session
2. **UserSessionService** maintains session state with tenant context
3. All controllers inherit from `BaseController` which enforces tenant isolation
4. Database queries automatically scoped by tenant ID

### Frontend Architecture Patterns

**Single-Page Application with Module Loading**:
- `index.html` is the shell, loads sections dynamically via `fetch('section.html')`
- `menu.html` loaded into `#sideMenuContainer` on page load
- Navigation via `navigateTo(section)` function with browser history support
- School year context stored in `window.currentSchoolYear` object

**Key Files**:
- `config.js` - API endpoints and environment detection
- `table-component.js` - Reusable data table with edit/sort/filter capabilities
- `theme.css` + `styles.css` - CSS custom properties for theming

### Authentication & Session Management
```javascript
// Frontend session storage pattern
sessionStorage.setItem('authToken', token);
sessionStorage.setItem('userFullName', user.fullName);
sessionStorage.setItem('tenantId', tenant.id);

// Backend session service usage
public AuthController(UserSessionService userSessionService) {
    var session = userSessionService.GetUserSession();
}
```

### System Attributes Pattern
Dynamic configuration via `SystemAttributes` table loaded at startup:
```csharp
// Backend: SystemAttributeLoaderHostedService loads into memory
// Frontend: AppConfig.getApiUrl('systemAttributes') for runtime access
```

### Database Conventions
- All tables in `petel_schema` namespace
- Tenant ID isolation enforced at service layer
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
