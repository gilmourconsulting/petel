# Year Management Screens

## Purpose

After login, the user lands on the main dashboard and selects a **Hebrew school year**. That opens the **operational** year management hub for that year. All year-scoped tenant work (assistants, entitlements, yearly budget) flows through this hub.

Separately, system admins configure **shared year-dependent rates** (equal across entities) via the side-menu hub **ניהול שנה** (`/year-elements`). See domain doc § Year Elements hub.

Domain context: assistants and entitlements are managed per Jewish school year. See [petel-assistants-domain.md](../../docs/agents/apps/petel-assistants-domain.md) for business rules.

## Navigation flow

```
/maindashboard
    └── click year button or modal
            └── /year/{YearId}          (YearManagement — operational hub)
                    ├── /year/{YearId}/assistants       (סייעות)
                    ├── /year/{YearId}/entitlements     (זכאויות)
                    ├── /year/{YearId}/org-units        (בתי ספר וגנים)
                    └── /year/{YearId}/yearly-budget    (תקציב שנתי)

Side menu
    └── ניהול שנה → /year-elements     (Year Elements — shared year rates hub)
            ├── tab: class-assistant-hours
            └── tab: hour-value
```

- **Main dashboard** (`MainDashboard.razor`, `/maindashboard`): shows current/previous year buttons, a "בחר שנה" modal, and context buttons **העלאת קובץ שכר** (`SalaryUploadModal`) and **נתוני שכר** (`/salaries`). Clicking a year navigates to `/year/{yearId}` (does not only store year in session).
- **Year management hub** (`YearManagement.razor`, `/year/{YearId}`): displays the year name, navigation cards — **סייעות**, **זכאויות**, **בתי ספר וגנים**, **תקציב שנתי** — and context buttons for salary / Meitar tools.
- **Year Elements hub** (`YearElements.razor`, `/year-elements`): shared admin multi-tab screen for year-scoped configuration (tabs: class assistant budget hours, hour monetary value). Menu item **ניהול שנה**.
- **Salaries view** (`Salaries.razor`, `/salaries`): read-only salary table with period and text filters (not Hebrew-year scoped; calendar year/month).
- **Assistants** (`Assistants.razor`, `/year/{YearId}/assistants`): person CRUD for the logged-in authority.
- **Entitlements** (`Entitlements.razor`, `/year/{YearId}/entitlements`): combined personal + institutional entitlements for the year. Context button **חילוץ אישורים מ-PDF** (`PersonalApprovalsPdfModal`) converts a Ministry personal-approval PDF to Excel (no entitlement DB writes).
- **Org units** (`OrgUnits.razor`, `/year/{YearId}/org-units`): manage tenant-owned institutions (schools and kindergartens in `assist_schema.institutions`; also available at `/org-units` from the main menu).
- **Yearly budget** (`YearlyBudget.razor`, `/year/{YearId}/yearly-budget`): versioned yearly budget by assistant type, with equal monthly split and **חשב תקציב** on open versions.

**System admin (not year-scoped operational data):**

- **הגדרות מערכת** (`SystemData.razor`, `/system-data`) — shared reference (attributes, assistant types, Hebrew years CRUD, etc.)
- **Year Elements** (`YearElements.razor`, `/year-elements`) — year-dependent shared rates (not System Data)
- Legacy redirects: `/assistant-types`, `/hebrew-years` → system-data tabs

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
| `POST api/personalapprovalspdf/convert` | Personal approvals PDF → Excel (`contentBase64`); no DB writes |
| `GET api/persons` | List persons for tenant (latest snapshot each) |
| `GET api/persons/search?term=` | Search by name or national ID |
| `GET api/persons/{id}` | Person snapshot (details + address + phones) |
| `GET api/persons/{id}/history` | Detail version history |
| `GET api/persons/phone-types` | Shared phone type lookup |
| `POST api/persons` | Create person |
| `PUT api/persons/{id}` | Update person (creates new detail version when needed) |
| `POST api/personsfileupload/preview` | Preview Excel/CSV headers + suggested field mappings |
| `POST api/personsfileupload/upload` | Import persons (create new; skip existing IDs) |
| `GET api/salaryfileupload/period-exists` | Check if salary data exists for period |
| `GET/PUT api/salaryfileupload/mapping` | Entity-level salary column map |
| `POST api/salaryfileupload/preview` | Preview salary file headers + mappings |
| `POST api/salaryfileupload/upload` | Import salaries for period (optional replace) |
| `GET api/salaries?year=&month=` | List salary rows for view screen |
| `GET api/yearly-budgets?yearId=` | Last yearly budget for year, or empty shell if none |
| `GET api/yearly-budgets/{id}` | Specific budget version |
| `PUT api/yearly-budgets/{id}` | Save open budget (year lines; months re-split) |
| `POST api/yearly-budgets/{id}/calculate` | Calculate hours (class_help + personal) and amounts from entitlements + shared rates |
| `PUT api/yearly-budgets/{id}/lock` | Lock open version |
| `POST api/yearly-budgets/new-version?yearId=` | Create first version (0) or next from locked last |
| `PUT api/yearly-budgets/{id}/delete` | Soft-delete version |
| `GET api/class-assistant-budget-hours?yearId=` | Shared class assistant pricing matrix for a Hebrew year (hours + participation % per school level × classification) |
| `PUT api/class-assistant-budget-hours` | Upsert pricing records for a year |
| `GET api/budget-hour-values?yearId=` | Shared monetary hour value for a Hebrew year |
| `PUT api/budget-hour-values` | Upsert hour value for a year |

## Security actions

Run SQL scripts in order (see below). After running SQL, refresh the security cache (roles screen or API restart).

| Screen | PageName | Page action | Button actions |
|--------|----------|-------------|----------------|
| Main dashboard | `maindashboard` | (page access not enforced) | `maindashboard_salary_upload`, `maindashboard_salaries_view` |
| Year hub | `yearmanagement` | `yearmanagement_page_action` | `yearmanagement_back`, `yearmanagement_assistants`, `yearmanagement_entitlements`, `yearmanagement_org_units`, `yearmanagement_yearly_budget`, `yearmanagement_salary_upload`, `yearmanagement_entitlements_upload`, `yearmanagement_salaries_view` |
| Salaries view | `salaries` | `salaries_page_action` | `salaries_back`, `salaries_refresh` |
| Assistants | `assistants` | `assistants_page_action` | `assistants_back`, `assistants_refresh`, `assistants_add`, `assistants_upload`, `assistants_view_details`, `assistants_edit`, `assistants_view_history` |
| Entitlements | `entitlements` | `entitlements_page_action` | back, refresh, add, edit, deactivate, allocations, `entitlements_personal_approvals_pdf` |
| Yearly budget | `yearly_budget` | `yearly_budget_page_action` | `yearly_budget_back`, `yearly_budget_refresh`, `yearly_budget_calculate`, `yearly_budget_save`, `yearly_budget_lock`, `yearly_budget_new_version`, `yearly_budget_delete` |
| Year Elements | `year_elements` | `year_elements_page_action` | `year_elements_back`, `year_elements_refresh`, `year_elements_class_hours_save`, `year_elements_hour_value_save` |
| Org units | `org_units` | `org_units_page_action` | back, refresh, add, edit, activate, deactivate |
| Assistant types | `assistant_types` | `assistant_types_page_action` | back, refresh, add, edit |
| Hebrew years | `hebrew_years` | `hebrew_years_page_action` | back, refresh, edit |

- Pages inherit `SecurePageBase` with default `EnforcePageAccess => true`.
- Hub cards and back buttons use `SecureButton` with `HideIfNoAccess="true"` where appropriate.

## SQL scripts (run order)

After user-management scripts:

1. `PetelAssistants/SQL/add-persons.sql`
2. `PetelAssistants/SQL/add-persons-actions.sql`
3. `PetelAssistants/SQL/add-persons-upload-action.sql` — Excel upload button (`assistants_upload`)
4. `PetelAssistants/SQL/add-assistants-view-details-action.sql` — view details button (`assistants_view_details`)
5. `PetelAssistants/SQL/add-entitlements-foundation.sql` — Hebrew year column fix, assistant types, org hierarchy
6. `PetelAssistants/SQL/add-entitlements.sql` — entitlements table
7. `PetelAssistants/SQL/add-entitlements-actions.sql` — security actions + menu items
8. `PetelAssistants/SQL/add-year-org-units-nav.sql` — year hub card for org units
9. `PetelAssistants/SQL/add-salary-upload.sql` — salary tables + upload buttons
10. `PetelAssistants/SQL/add-salaries-view-actions.sql` — salary view screen + nav buttons
11. `PetelAssistants/SQL/add-yearly-budget.sql` — yearly budget tables
12. `PetelAssistants/SQL/add-yearly-budget-actions.sql` — yearly budget page + hub card actions
13. `PetelAssistants/SQL/add-class-assistant-budget-hours.sql` — shared rates table, Year Elements menu/actions, `yearly_budget_calculate`
14. `PetelAssistants/SQL/add-class-assistant-budget-hours-participation.sql` — `ministry_participation_pct` field on each year/school_level/classification record
15. `PetelAssistants/SQL/seed-class-assistant-budget-hours-tashpu.sql` — optional seed for תשפו
16. `PetelAssistants/SQL/add-budget-hour-value.sql` — shared `budget_hour_values` + `year_elements_hour_value_save`
17. `PetelAssistants/SQL/add-personal-approvals-pdf-action.sql` — Entitlements PDF→Excel button (`entitlements_personal_approvals_pdf`)

## Files

| File | Route |
|------|-------|
| `MainDashboard.razor` | `/maindashboard` |
| `YearManagement.razor` | `/year/{YearId:int}` |
| `YearElements.razor` | `/year-elements` |
| `YearElementsTabs/YearElementsClassAssistantHoursTab.razor` | (tab under year-elements) |
| `YearElementsTabs/YearElementsHourValueTab.razor` | (tab under year-elements) |
| `SalaryUploadModal.razor` | (modal from dashboard / year hub) |
| `Salaries.razor` | `/salaries` |
| `Assistants.razor` | `/year/{YearId:int}/assistants` |
| `Entitlements.razor` | `/year/{YearId:int}/entitlements` |
| `PersonalApprovalsPdfModal.razor` | (modal from entitlements — PDF→Excel) |
| `YearlyBudget.razor` | `/year/{YearId:int}/yearly-budget` |
| `OrgUnits.razor` | `/org-units`, `/year/{YearId:int}/org-units` |
| `AssistantTypes.razor` | `/assistant-types` |
| `HebrewYears.razor` | `/hebrew-years` |

## Adding features under a year

1. Keep `{YearId}` in the route.
2. Scope data by `session.EntityId` (tenant) and the selected year.
3. Add page + button actions to SQL (follow `add-entitlements-actions.sql` pattern).
4. Use `SecurePageBase` + `SecureButton` per existing pages.

## Adding shared year elements

1. Add a tab under `/year-elements` (not System Data).
2. Store rates in `shared_schema` keyed by `hebrew_year_id` (no `entity_id`).
3. Add security actions `year_elements_*` and document in domain + this file.
4. Wire tenant calculate/budget logic to read those shared rates.

Assistant-to-entitlement assignments (year-scoped assistant registration) are planned as a follow-on feature.
