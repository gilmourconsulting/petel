# Plan: Fix Instruction Files for Pure Blazor Architecture

## Problem Summary

The instruction files describe an old HTML/JavaScript SPA architecture that no longer exists.
The actual implementation is **100% Blazor Server** (.razor files) with no standalone HTML pages.

### What the instructions say (WRONG):
- Frontend is a JS SPA in `PetelATH.Api/wwwroot/` with `index.html`, `page-lifecycle-config.js`, `table-component.js`
- Navigation uses `window.navigateTo('pagename')`
- All tables use JavaScript `ReusableTable` class
- Session uses `window.SessionState.setProperty()` and `sessionStorage`
- Pages registered in `page-lifecycle-config.js`
- Cleanup functions exported to `window`

### What the code actually does (RIGHT):
- Frontend is in `PetelATH.BlazorServer/Components/` with .razor files
- 32+ Razor pages (`Components/Pages/*.razor`)
- Navigation uses Blazor `NavigationManager.NavigateTo()` + `NavLink` component
- Tables are HTML tables inside Razor components, or dedicated Razor table components (inheriting `SortableTableBase<T>`)
- Session uses `SessionStateService` (injected Blazor service)
- Pages are Blazor routes (`@page "/route"`) + registered in `petel_schema.menu_items` DB
- Layout: `MainLayout.razor`, `NavMenu.razor` (DB-driven), `AuthenticationGuard.razor`
- Authentication: `SecurePageBase`, `SecureButton.razor`
- API calls: `ApiService.GetAsync<T>()`, `PostAsync<Req,Resp>()` etc.

---

## Steps

### Phase 1: Rewrite `petelath.instructions.md`

1. **Remove these sections entirely:**
   - "Static Frontend (wwwroot in PetelATH.Api)" (entire section)
   - "Page Lifecycle Management" (entire section)
   - JavaScript `ReusableTable` from Standard Components
   - JavaScript collapsible card pattern
   - Old "Adding a New Page — Checklist" (wwwroot/.html based)

2. **Fix these sections (replace JS code with Blazor/C#):**
   - "Database-Driven Menu System" frontend code → NavMenu.razor pattern
   - "School Year Attributes" frontend fetch → `ApiService.GetAsync<T>()`
   - "Modal Form Layout" section → Blazor modal pattern

3. **Add new sections:**
   - **Blazor Frontend Architecture** - component hierarchy (App.razor → Routes.razor → Layout → Pages → Shared/Modals)
   - **Blazor Page Pattern** - `@page`, `@layout`, `@inherits SecurePageBase`, `@inject`, `@code` block
   - **SecurePageBase** - how protected pages inherit it, what it provides
   - **ApiService Call Patterns** - `GetAsync<T>`, `PostAsync<Req,Resp>`, `PutAsync`, `DeleteAsync`, `GetFileAsync`
   - **SessionStateService** - how to get/set session properties from Blazor
   - **Navigation** - `NavigationManager.NavigateTo("/route")`, `NavLink`
   - **Table Patterns** - inline HTML tables in Razor vs. dedicated Razor table components (`SortableTableBase<T>`)
   - **Blazor Modal Pattern** - `@ref`, component modals (e.g., `StudentUploadModal.razor`)
   - **SecureButton** - permission-gated buttons with `ActionName`, `ScreenName`, `OnClick`
   - **Adding a New Blazor Page — Checklist** (correct version)
   - **DTOs in BlazorServer** - where they live, how they map to API responses

### Phase 2: Full restructure of `copilot-instructions.md`

4. **Reorganize into clear sections:**
   - Architecture Overview (keep)
   - Configuration Management (keep - backend patterns)
   - Authentication & Session Management (keep backend, fix frontend references)
   - Entity Framework Patterns (keep)
   - Standard Database Table Structure (keep)
   - Excel Import/Export Backend (keep)
   - Hebrew/RTL Patterns (keep backend, simplify)
   - Document Proxy Pattern (keep)
   - Security Implementation - JWT/OTP (keep)
   - Deployment (keep)

5. **Remove entirely:**
   - "Frontend Architecture Patterns" (JS SPA with index.html, module loading)
   - "Page Lifecycle Management" (entire section ~150 lines)
   - "Standard Components" (JavaScript ReusableTable, window.navigateTo)
   - "Database-Driven Menu System" JavaScript frontend code
   - "Modal Form Layout" JavaScript patterns
   - "Collapsible Card Pattern" JavaScript patterns
   - All `window.SessionState.setProperty()`, `sessionStorage`, `AppConfig.getApiUrl()` references

### Phase 3: Create new instruction/prompt files

6. **Create `.github/instructions/blazor-patterns.instructions.md`** (new file)
   - `applyTo: '**/*.razor'`
   - Canonical Blazor page template
   - SecurePageBase inheritance
   - ApiService call examples
   - SessionStateService usage
   - Navigation patterns
   - Table patterns (inline + component)
   - Form patterns (filter bars, modals)
   - Icon usage in Blazor (img src="/images/view_icon.png")

7. **Create `.github/prompts/new-blazor-page.prompt.md`** (agent mode file)
   - Agent that scaffolds a complete new page end-to-end
   - Prompts user for page name, route, display title, API endpoint needs
   - Creates: .razor page, API controller, DTOs (Blazor + API), DB menu_items SQL
   - Follows all established patterns (SecurePageBase, ApiService, etc.)

8. **Update `petelassistants.instructions.md`** - minor fixes only
   - Fix any remaining JavaScript frontend examples
   - Add reference to `blazor-patterns.instructions.md`

---

## Relevant Files

- `.github/instructions/petelath.instructions.md` — primary target, major rewrite of frontend sections
- `.github/instructions/petelassistants.instructions.md` — minor updates
- `.github/copilot-instructions.md` — remove wrong sections, keep backend patterns
- `PetelATH/PetelATH.BlazorServer/Components/Pages/Students.razor` — reference for page pattern
- `PetelATH/PetelATH.BlazorServer/Components/Layout/NavMenu.razor` — reference for menu pattern
- `PetelATH/PetelATH.BlazorServer/Components/Shared/SchoolTracksTable.razor` — reference for table components
- `PetelATH/PetelATH.BlazorServer/Services/ApiService.cs` — reference for API call patterns

---

## Instruction Files Going Forward

| File | applyTo | Purpose |
|---|---|---|
| `.github/copilot-instructions.md` | All files | Global: DB patterns, EF, auth, deployment |
| `.github/instructions/petelath.instructions.md` | `PetelATH/**` | ATH-specific: schema, controllers, Blazor pages |
| `.github/instructions/petelassistants.instructions.md` | `PetelAssistants/**` | Assistants-specific: schema, scaffolding |
| `.github/instructions/blazor-patterns.instructions.md` | `**/*.razor` | Blazor component patterns (both apps) |
| `.github/prompts/new-blazor-page.prompt.md` | N/A (reusable prompt) | Scaffold a new page end-to-end |

---

## Verification

1. After rewriting `petelath.instructions.md`, check no references to `window.`, `sessionStorage`, `.html` files, `table-component.js`, `page-lifecycle`, or `AppConfig.getApiUrl()`
2. After updating `copilot-instructions.md`, verify removed sections are gone
3. New `blazor-patterns.instructions.md` should align with actual `Students.razor` pattern
4. New prompt file should produce working scaffolding when used
