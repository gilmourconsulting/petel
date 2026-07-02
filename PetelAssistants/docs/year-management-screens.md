# Year Management Screens

## Purpose

After login, the user lands on the main dashboard and selects a **Hebrew school year**. That opens the year management hub for that year. All year-scoped work (assistants, entitlements) flows through this hub.

Domain context: assistants and entitlements are managed per Jewish school year. See `.cursor/prompts/context.md` for business rules.

## Navigation flow

```
/maindashboard
    └── click year button or modal
            └── /year/{YearId}          (YearManagement — hub)
                    ├── /year/{YearId}/assistants    (סייעות)
                    └── /year/{YearId}/entitlements  (זכאיות)
```

- **Main dashboard** (`MainDashboard.razor`, `/maindashboard`): shows current/previous year buttons and a "בחר שנה" modal. Clicking a year navigates to `/year/{yearId}` (does not only store year in session).
- **Year management hub** (`YearManagement.razor`, `/year/{YearId}`): displays the year name and two navigation cards — **סייעות** and **זכאיות**.
- **Assistants** (`Assistants.razor`, `/year/{YearId}/assistants`): stub page; full CRUD to be built.
- **Entitlements** (`Entitlements.razor`, `/year/{YearId}/entitlements`): stub page; full CRUD to be built.

## Session context

When the hub loads, it stores the selected year in session via `POST api/session/property`:

| Key | Value |
|-----|-------|
| `SelectedYearId` | `{YearId}` |
| `SelectedYearName` | Hebrew year name from `GET api/years/{YearId}` |

Downstream pages should read year from the route parameter `{YearId}` and/or session — not from client-supplied body fields.

## API

| Endpoint | Purpose |
|----------|---------|
| `GET api/years/context` | Dashboard: current year, previous year, all years |
| `GET api/years/{id}` | Single year lookup (id + yearName) |

Data source: `shared_schema.hebrew_years` via `YearsController` / `SharedDbContext`.

## Security actions

Run `PetelAssistants/SQL/add-year-management-actions.sql` to seed actions and assign them to existing roles.

| Screen | PageName | Page action (PAGE_ACCESS) | Button actions |
|--------|----------|----------------------------|----------------|
| Year hub | `yearmanagement` | `yearmanagement_page_action` | `yearmanagement_back`, `yearmanagement_assistants`, `yearmanagement_entitlements` |
| Assistants | `assistants` | `assistants_page_action` | `assistants_back` |
| Entitlements | `entitlements` | `entitlements_page_action` | `entitlements_back` |

- Pages inherit `SecurePageBase` with default `EnforcePageAccess => true`.
- Hub cards and back buttons use `SecureButton` with `HideIfNoAccess="true"` where appropriate.
- After running SQL, refresh the security cache (roles screen or API restart).

## Files

| File | Route |
|------|-------|
| `PetelAssistants.BlazorServer/Components/Pages/MainDashboard.razor` | `/maindashboard` |
| `PetelAssistants.BlazorServer/Components/Pages/YearManagement.razor` | `/year/{YearId:int}` |
| `PetelAssistants.BlazorServer/Components/Pages/Assistants.razor` | `/year/{YearId:int}/assistants` |
| `PetelAssistants.BlazorServer/Components/Pages/Entitlements.razor` | `/year/{YearId:int}/entitlements` |
| `PetelAssistants.Api/Controllers/YearsController.cs` | `api/years/*` |
| `PetelAssistants/SQL/add-year-management-actions.sql` | Security seed |

## Adding features under a year

When building assistants or entitlements screens:

1. Keep `{YearId}` in the route.
2. Scope data by `session.EntityId` (tenant) and the selected year.
3. Add page + button actions to SQL (follow `add-year-management-actions.sql` pattern).
4. Use `SecurePageBase` + `SecureButton` per existing pages (e.g. `Roles.razor`).
