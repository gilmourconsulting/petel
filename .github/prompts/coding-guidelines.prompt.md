---
agent: agent, ask, edit, plan
description: Reference the correct instruction files before making any code change or addition in the Petel monorepo.
---

Before making any code change, verify the relevant instruction files below are loaded for the files being modified.
Before writing any code, read the relevant instruction files below and ensure your change adheres to the guidelines specified. If you are unsure which instructions apply, review the "Applies To" column for each file. Always follow the key rules outlined in the instruction files to maintain consistency and quality across the codebase.

Before starting any changes read the pre-prompts in `.github/prompts/Pre%20processing%20prompts.md` to ensure you are following the best practices for coding and problem-solving.

## Instruction File Reference

| File | Applies To | Load When |
|---|---|---|
| `.github/copilot-instructions.md` | All files | Always — covers DB/EF, auth, session, deployment, shared patterns |
| `.github/instructions/blazor-patterns.instructions.md` | `**/*.razor` | Any `.razor` file change |
| `.github/instructions/petelath.instructions.md` | `PetelATH/**` | Any change under `PetelATH/` |
| `.github/instructions/petelassistants.instructions.md` | `PetelAssistants/**` | Any change under `PetelAssistants/` |

## Key Rules (Quick Reference)

**Backend (API)**
- All controllers inherit `BaseController`; no `[Authorize]` — use `GetCurrentSession()` per endpoint
- All entities: `[Table("name")]` with no `Schema=` parameter; schema set via `HasDefaultSchema()` in `AppDbContext`
- Always include audit fields: `created_at`, `created_user`, `updated_at`, `update_user`
- Navigation properties required on all FK relationships

**Blazor Frontend**
- All pages inherit `SecurePageBase`; override `OnPageInitializedAsync()`, not `OnInitializedAsync()`
- Use `ApiService` for all HTTP calls — never raw `HttpClient` or `fetch`
- Use `SessionStateService` for session data — never read cookies or local storage directly
- Use `NavigationManager.NavigateTo()` — never `window.location` or JS navigation
- No HTML files, no JavaScript SPA code, no `sessionStorage`, no `AppConfig.getApiUrl()`

**Configuration**
- No hardcoded URLs, schema names, or environment-specific values in code
- API URLs in `appsettings.{Environment}.json` under `ApiSettings:BaseUrl`
- Schema name from `DatabaseSettings:SchemaName` configuration

## Scaffold Prompts

- New Blazor page (razor + controller + DTOs + SQL): use `.github/prompts/new-blazor-page.prompt.md`
