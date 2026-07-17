# Year Management Screens

## Purpose

After login, the user lands on the main dashboard and selects a **Hebrew school year**. That opens the year management hub for that year. All year-scoped work (assistants, entitlements) flows through this hub.

Domain context: assistants and entitlements are managed per Jewish school year. See [petel-assistants-domain.md](../../docs/agents/apps/petel-assistants-domain.md) for business rules.

## Navigation flow

```
/maindashboard
    └── click year button or modal
            └── /year/{YearId}          (YearManagement — hub)
                    ├── /year/{YearId}/assistants                      (סייעות)
                    ├── /year/{YearId}/entitlements/institutional      (זכאויות מוסדיות)
                    ├── /year/{YearId}/entitlements/personal         (זכאויות אישיות)
                    └── /year/{YearId}/org-units                     (בתי ספר וגנים)
```

- **Main dashboard** (`MainDashboard.razor`, `/maindashboard`): shows current/previous year buttons and a "בחר שנה" modal. Clicking a year navigates to `/year/{yearId}` (does not only store year in session).
- **Year management hub** (`YearManagement.razor`, `/year/{YearId}`): displays the year name and navigation cards — **סייעות**, **זכאויות מוסדיות**, **זכאויות אישיות**, **בתי ספר וגנים**.
- **Assistants** (`Assistants.razor`, `/year/{YearId}/assistants`): person CRUD for the logged-in authority.
- **Institutional entitlements** (`InstitutionalEntitlements.razor`): school/kindergarten/class entitlements for the year.
- **Personal entitlements** (`PersonalEntitlements.razor`): pupil (external id) entitlements for the year.
- **Org units** (`OrgUnits.razor`, `/year/{YearId}/org-units`): manage tenant-owned institutions (schools and kindergartens in `assist_schema.institutions`; also available at `/org-units` from the main menu).
- **Legacy route** `/year/{YearId}/entitlements` redirects to institutional entitlements.

**System admin (not year-scoped):**

- **Assistant types** (`AssistantTypes.razor`, `/assistant-types`)
- **Hebrew years** (`HebrewYears.razor`, `/hebrew-years`)

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
| `GET api/years/{id}` | Single year lookup (name + dates + flags) |
| `GET api/years/admin` | System admin: all Hebrew years |
| `PUT api/years/{id}` | System admin: update year dates and flags |
| `GET api/assistant-types` | Active assistant types (optional `includeInactive`) |
| `POST/PUT api/assistant-types` | System admin: manage assistant types |
| `GET api/org-units?type=` | Institutions (schools/kindergartens) for tenant |
| `POST/PUT api/org-units` | Create/update institutions |
| `PUT api/org-units/{id}/activate\|deactivate` | Toggle institution |
| `GET api/entitlements?yearId=&kind=` | List entitlements (institutional or personal) |
| `GET api/entitlements/{id}` | Single entitlement |
| `POST api/entitlements` | Create entitlement |
| `PUT api/entitlements/{id}` | Update entitlement |
| `PUT api/entitlements/{id}/deactivate` | Soft-delete entitlement |
| `GET api/persons` | List persons for tenant (latest snapshot each) |
| `GET api/persons/search?term=` | Search by name or national ID |
| `GET api/persons/{id}` | Person snapshot (details + address + phones) |
| `GET api/persons/{id}/history` | Detail version history |
| `GET api/persons/phone-types` | Shared phone type lookup |
| `POST api/persons` | Create person |
| `PUT api/persons/{id}` | Update person (creates new detail version when needed) |

## Security actions

Run SQL scripts in order (see below). After running SQL, refresh the security cache (roles screen or API restart).

| Screen | PageName | Page action | Button actions |
|--------|----------|-------------|----------------|
| Year hub | `yearmanagement` | `yearmanagement_page_action` | `yearmanagement_back`, `yearmanagement_assistants`, `yearmanagement_institutional_entitlements`, `yearmanagement_personal_entitlements`, `yearmanagement_org_units` |
| Assistants | `assistants` | `assistants_page_action` | `assistants_back`, `assistants_refresh`, `assistants_add`, `assistants_edit`, `assistants_view_history` |
| Institutional entitlements | `institutional_entitlements` | `institutional_entitlements_page_action` | back, refresh, add, edit, deactivate |
| Personal entitlements | `personal_entitlements` | `personal_entitlements_page_action` | back, refresh, add, edit, deactivate |
| Org units | `org_units` | `org_units_page_action` | back, refresh, add, edit, activate, deactivate |
| Assistant types | `assistant_types` | `assistant_types_page_action` | back, refresh, add, edit |
| Hebrew years | `hebrew_years` | `hebrew_years_page_action` | back, refresh, edit |

- Pages inherit `SecurePageBase` with default `EnforcePageAccess => true`.
- Hub cards and back buttons use `SecureButton` with `HideIfNoAccess="true"` where appropriate.

## SQL scripts (run order)

After user-management scripts:

1. `PetelAssistants/SQL/add-persons.sql`
2. `PetelAssistants/SQL/add-persons-actions.sql`
3. `PetelAssistants/SQL/add-entitlements-foundation.sql` — Hebrew year column fix, assistant types, org hierarchy
4. `PetelAssistants/SQL/add-entitlements.sql` — entitlements table
5. `PetelAssistants/SQL/add-entitlements-actions.sql` — security actions + menu items
6. `PetelAssistants/SQL/add-year-org-units-nav.sql` — year hub card for org units

## Files

| File | Route |
|------|-------|
| `MainDashboard.razor` | `/maindashboard` |
| `YearManagement.razor` | `/year/{YearId:int}` |
| `Assistants.razor` | `/year/{YearId:int}/assistants` |
| `InstitutionalEntitlements.razor` | `/year/{YearId:int}/entitlements/institutional` |
| `PersonalEntitlements.razor` | `/year/{YearId:int}/entitlements/personal` |
| `Entitlements.razor` | `/year/{YearId:int}/entitlements` (redirect) |
| `OrgUnits.razor` | `/org-units`, `/year/{YearId:int}/org-units` |
| `AssistantTypes.razor` | `/assistant-types` |
| `HebrewYears.razor` | `/hebrew-years` |

## Adding features under a year

1. Keep `{YearId}` in the route.
2. Scope data by `session.EntityId` (tenant) and the selected year.
3. Add page + button actions to SQL (follow `add-entitlements-actions.sql` pattern).
4. Use `SecurePageBase` + `SecureButton` per existing pages.

Assistant-to-entitlement assignments (year-scoped assistant registration) are planned as a follow-on feature.
