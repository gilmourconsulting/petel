# Year Management Screens

## Purpose

After login, the user lands on the main dashboard and selects either a **Gregorian calendar year** (treasurer / municipal books, Jan–Dec) or a **Hebrew school year** (operational work). School-year-scoped tenant work (assistants, entitlements, yearly budget) flows through `/year/{YearId}`. Calendar-year views at `/gregorian/{Year}` compose overlapping school-year budgets and actuals; they are read-only.

Separately, system admins configure **shared year-dependent rates** (equal across entities) via the side-menu hub **ניהול שנה** (`/year-elements`). See domain doc § Year Elements hub.

Domain context: assistants and entitlements are managed per Jewish school year. Municipal fiscal year is Gregorian. See [petel-assistants-domain.md](../../docs/agents/apps/petel-assistants-domain.md) for business rules.

## Navigation flow

```
/maindashboard
    ├── בתי ספר וגנים → /org-units     (institutions; tenant-owned, not year-scoped)
    ├── left: gregorian year → /gregorian/{Year}   (treasurer hub, Jan–Dec)
    │       ├── /gregorian/{Year}/assistants
    │       ├── /gregorian/{Year}/entitlements
    │       ├── /gregorian/{Year}/budget
    │       ├── /salaries?fromGregorianYear=
    │       └── /meitar-data?fromGregorianYear=
    └── right: school year → /year/{YearId}        (operational hub)
            ├── /year/{YearId}/assistants       (סייעות)
            ├── /year/{YearId}/entitlements     (זכאויות)
            ├── /year/{YearId}/yearly-budget    (תקציב שנתי)
            ├── /salaries                       (שכר)
            └── /meitar-data                    (מיתר)

Side menu
    └── ניהול שנה → /year-elements     (Year Elements — shared year rates hub)
            ├── tab: class-assistant-hours
            └── tab: hour-value
```

- **Main dashboard** (`MainDashboard.razor`, `/maindashboard`): context buttons stay on the left — **בתי ספר וגנים**, **העלאת קובץ שכר**, **נתוני שכר**, **שליפת נתוני מיתר**, **נתוני מיתר**, **מיפוי מחלקות שכר**. Center of the page is a two-column year picker (visual left = **שנה לועזית** current/previous + בחר שנה; visual right = **שנת לימודים** current/previous + בחר שנה). Gregorian click → `/gregorian/{Year}`. School-year click → `/year/{YearId}`.
- **Gregorian year hub** (`GregorianYearManagement.razor`, `/gregorian/{Year}`): treasurer view for calendar year Jan–Dec. A short KPI line (net municipal, salary-vs-budget variance, Meitar income) plus five **clickable** cards mirroring the school-year hub. Budget/entitlements/assistants are **composed read-only** views; salaries/Meitar reuse existing screens with `?fromGregorianYear=`. Stitch: for each month of Y, take the last non-deleted yearly budget of the Hebrew year covering that month (typically Jan–Aug from the school year that started Sep Y−1, Sep–Dec from the school year that started Sep Y). Missing budget → zeros + warning; open (unlocked) last version is used and flagged **טרם ננעל**. Data: `GET api/gregorian-years/{year}/hub-summary`.
- **Gregorian budget** (`GregorianBudget.razor`, `/gregorian/{Year}/budget`): read-only tabs **תקציב** / **סיכום מול תקציב**, month buttons Jan–Dec, links to contributing school-year budgets. Vs-budget tab includes a trend chart (cumulative/monthly, כסף/משרות, comparison pair) built client-side from stitched `Comparisons`. `GET api/gregorian-years/{year}/budget`.
- **Gregorian entitlements / assistants**: overlap lists (`/gregorian/{Year}/entitlements`, `/gregorian/{Year}/assistants`) with a school-year column and `EntityFocusLink` into the operational screens. No create/edit/upload.
- **Year management hub** (`YearManagement.razor`, `/year/{YearId}`): displays the year name and five clickable summary cards (same security actions as the former nav cards) — **סייעות** (person count), **זכאויות** (count + allocated %), **תקציב** (last version hours/amount/version), **שכר** (YTD total + last uploaded month), **מיתר** (distinct imported months). Context buttons remain for entitlements upload, department map, and salary/Meitar summaries. Salary upload and Meitar retrieve live on `/salaries` and `/meitar-data` respectively. Data: `GET api/years/{id}/hub-summary`.
- **Year Elements hub** (`YearElements.razor`, `/year-elements`): shared admin multi-tab screen for year-scoped configuration (tabs: class assistant budget hours, hour monetary value). Menu item **ניהול שנה**.
- **Salaries view** (`Salaries.razor`, `/salaries`): read-only salary table with **העלאת קובץ שכר** context button (`SalaryUploadModal`; uses `yearmanagement_salary_upload` when opened via `?fromYear=`, else `maindashboard_salary_upload`). Period is a row of month buttons showing totals per month (`GET api/salaries/month-totals`). From the school-year hub (`?fromYear=`), months are that Hebrew year (typically Sept–Aug). From the gregorian hub (`?fromGregorianYear=`), months are Jan–Dec of that calendar year (no Hebrew-year dropdown; back returns to `/gregorian/{Year}`). From the main dashboard, a Hebrew-year dropdown appears above the buttons.
- **Salary month summary** (`SalaryMonthSummary.razor`, `/salaries/month-summary`): debug comparison of all salary payments vs last locked budget. Month buttons follow the same range as salaries (`GET api/salary-month-summaries/for-year?yearId=` or `for-gregorian-year?year=`). From `?fromGregorianYear=`, months are Jan–Dec.
- **Salary anomalies** (`SalaryAnomalies.razor`, `/salaries/anomalies`): debug investigation list with status/notes.
- **Salary department mappings** (`SalaryDepartmentMappings.razor`, `/salary-department-mappings`): tenant payroll department → assistant type.
- **Meitar data** (`MeitarData.razor`, `/meitar-data`): MUTAVIM / MUCARIM / SHARATIM tabs. **שליפת נתוני מיתר** context button (`MeitarRetrieveModal`; `yearmanagement_meitar_retrieve` when `?fromYear=`, else `maindashboard_meitar_retrieve`). Month buttons match salaries. From `?fromGregorianYear=`, months are Jan–Dec (no Hebrew-year dropdown; back to `/gregorian/{Year}`). From main dashboard, Hebrew-year dropdown + calc/effective date filter.
- **Meitar month summary** (`MeitarMonthSummary.razor`, `/meitar-data/month-summary`): debug Meitar income vs last locked budget.
- **Assistants** (`Assistants.razor`, `/year/{YearId}/assistants`): person CRUD for the logged-in authority. Table IDs elsewhere (salaries, salary anomalies, entitlement allocations) link here via `EntityFocusLink` (`?focusId={persons.id}`), which selects and scrolls to that row. Selecting a person opens a resizable bottom dock with tabs **הקצאות** (allocations for the year; view button navigates to `/year/{YearId}/entitlements?focusId={entitlementId}&allocationFocusId={allocationId}`) and **היסטוריית שכר** (matched salary rows for school-year months via `GET api/persons/{id}/salaries`).
- **Entitlements** (`Entitlements.razor`, `/year/{YearId}/entitlements`): combined personal + institutional entitlements for the year. Summary: total + allocated %, plus one wide card with all assistant-type counts and allocated %. Table includes **סוג מוסד** (גן / יסודי / תיכון); class name, pupil name, ID, and class classification appear on the **פרטי זכאות** dock tab (default). The dock is height-resizable. Supports deep links `?focusId={entitlementId}` and `?allocationFocusId={allocationId}` (opens allocations tab and scrolls to the row). Context button **העלאת זכאויות אישיות** (`PersonalEntitlementUploadModal`) imports personal (`student_help`) entitlements from PDF or Excel (PDF convert → optional download → upsert + orphan review).
- **Org units** (`OrgUnits.razor`, `/org-units`): manage tenant-owned institutions (schools and kindergartens in `assist_schema.institutions`). Opened from the main dashboard (and the main menu); also available at `/year/{YearId}/org-units` for bookmarks.
- **Yearly budget** (`YearlyBudget.razor`, `/year/{YearId}/yearly-budget`): versioned yearly budget by assistant type, with equal monthly split and **חשב תקציב** on open versions. Tabs: **תקציב** (year + month lines) and **סיכום מול תקציב** (that version’s `yearly_budget_comparisons`: budget amount first, then salary, Meitar income, then FTE; year totals plus per-month buttons; trend chart of budget vs income vs expenses over the Hebrew-year months, with כסף/משרות and מצטבר/חודשי toggles). Context button **חשב סיכומים מחדש** force-rebuilds salary and Meitar month summaries for the Hebrew year (current mappings) and refreshes the comparison tab.

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
| `GET api/gregorian-years/context` | Dashboard: current/previous/all Gregorian years derived from Hebrew year dates |
| `GET api/gregorian-years/{year}/hub-summary` | Treasurer hub KPIs + five card values for calendar year Jan–Dec |
| `GET api/gregorian-years/{year}/budget` | Stitched month details + comparisons from contributing school-year last budgets |
| `GET api/gregorian-years/{year}/entitlements` | Last-version entitlements overlapping Jan–Dec |
| `GET api/gregorian-years/{year}/assistants` | Persons with an active allocation overlapping Jan–Dec |
| `GET api/years/{id}` | Single year lookup (name + dates + flags) |
| `GET api/years/{id}/hub-summary` | Year hub cards: assistant count, entitlement count + allocated %, last budget totals/version, salary YTD + last uploaded month, Meitar imported-month count |
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
| `PUT api/entitlements/{id}/resolve-validity` | Fix/approve an invalid entitlement (required reason text) |
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
| `GET api/salaries/month-totals?fromYear=&fromMonth=&toYear=&toMonth=` | Grouped salary sums per calendar period (max 24 months) |
| `GET/POST/PUT api/salary-department-mappings` | Tenant salary department → assistant type |
| `GET api/salary-month-summaries?year=&month=` | Salary vs locked budget summary (latest process) |
| `GET api/salary-month-summaries/for-year?yearId=` | Latest-process salary summary lines for every month in a Hebrew year |
| `GET api/salary-month-summaries/for-gregorian-year?year=` | Same, for Jan–Dec of a calendar year |
| `GET api/salary-anomalies?year=&month=` | Salary anomaly details |
| `PUT api/salary-anomalies/{id}/status` | Update anomaly status + notes |
| `GET api/statuses?object=` | Shared statuses |
| `GET api/meitar-month-summaries?year=&month=` | Meitar vs locked budget summary (latest process) |
| `GET api/meitardata/mucarim?year=&month=&dateField=` | Stored MUCARIM rows for period (MUCARIM tab on `/meitar-data`) |
| `GET api/yearly-budgets?yearId=` | Last yearly budget for year (includes `Comparisons`), or empty shell if none |
| `GET api/yearly-budgets/{id}` | Specific budget version |
| `PUT api/yearly-budgets/{id}` | Save open budget (year lines; months re-split) |
| `POST api/yearly-budgets/{id}/calculate` | Calculate hours (class_help + personal) and amounts from entitlements + shared rates |
| `POST api/yearly-budgets/{id}/recalculate-summaries` | Force-rebuild salary + Meitar month summaries for the Hebrew year, then refresh version comparisons |
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
| Main dashboard | `maindashboard` | (page access not enforced) | `maindashboard_org_units`, `maindashboard_salary_upload`, `maindashboard_salaries_view`, `maindashboard_salary_dept_map` |
| Gregorian hub | `gregorian` | `gregorian_page_action` | `gregorian_back`, `gregorian_assistants`, `gregorian_entitlements`, `gregorian_budget`, `gregorian_salaries_view`, `gregorian_meitar_view`, `gregorian_salary_month_summary`, `gregorian_salary_anomalies`, `gregorian_meitar_month_summary` |
| Gregorian budget | `gregorian_budget` | `gregorian_budget_page_action` | `gregorian_budget_back`, `gregorian_budget_refresh` |
| Gregorian entitlements | `gregorian_entitlements` | `gregorian_entitlements_page_action` | `gregorian_entitlements_back`, `gregorian_entitlements_refresh` |
| Gregorian assistants | `gregorian_assistants` | `gregorian_assistants_page_action` | `gregorian_assistants_back`, `gregorian_assistants_refresh` |
| Year hub | `yearmanagement` | `yearmanagement_page_action` | `yearmanagement_back`, `yearmanagement_assistants`, `yearmanagement_entitlements`, `yearmanagement_yearly_budget`, `yearmanagement_salary_upload`, `yearmanagement_entitlements_upload`, `yearmanagement_salaries_view`, `yearmanagement_salary_dept_map`, `yearmanagement_salary_month_summary`, `yearmanagement_salary_anomalies`, `yearmanagement_meitar_month_summary` |
| Salaries view | `salaries` | `salaries_page_action` | `salaries_back`, `salaries_refresh`, `salaries_month_summary`, `salaries_anomalies` |
| Salary month summary | `salary_month_summary` | `salary_month_summary_page_action` | `salary_month_summary_back`, `salary_month_summary_refresh` |
| Salary anomalies | `salary_anomalies` | `salary_anomalies_page_action` | `salary_anomalies_back`, `salary_anomalies_refresh`, `salary_anomalies_status` |
| Salary department map | `salary_dept_map` | `salary_dept_map_page_action` | `salary_dept_map_back`, `salary_dept_map_refresh`, `salary_dept_map_save` |
| Meitar data | `meitardata` | `meitardata_page_action` | `meitardata_back`, `meitardata_refresh`, `meitardata_month_summary` |
| Meitar month summary | `meitar_month_summary` | `meitar_month_summary_page_action` | `meitar_month_summary_back`, `meitar_month_summary_refresh` |
| Assistants | `assistants` | `assistants_page_action` | `assistants_back`, `assistants_refresh`, `assistants_add`, `assistants_upload`, `assistants_view_details`, `assistants_edit`, `assistants_view_history` |
| Entitlements | `entitlements` | `entitlements_page_action` | back, refresh, add, edit, deactivate, allocations, `entitlements_personal_upload` |
| Yearly budget | `yearly_budget` | `yearly_budget_page_action` | `yearly_budget_back`, `yearly_budget_refresh`, `yearly_budget_calculate`, `yearly_budget_recalculate_summaries`, `yearly_budget_save`, `yearly_budget_lock`, `yearly_budget_new_version`, `yearly_budget_delete` |
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
8. `PetelAssistants/SQL/add-year-org-units-nav.sql` — legacy year hub card action (`yearmanagement_org_units`; UI entry moved to dashboard)
9. `PetelAssistants/SQL/add-salary-upload.sql` — salary tables + upload buttons
10. `PetelAssistants/SQL/add-salaries-view-actions.sql` — salary view screen + nav buttons
11. `PetelAssistants/SQL/add-yearly-budget.sql` — yearly budget tables
12. `PetelAssistants/SQL/add-yearly-budget-actions.sql` — yearly budget page + hub card actions
13. `PetelAssistants/SQL/add-class-assistant-budget-hours.sql` — shared rates table, Year Elements menu/actions, `yearly_budget_calculate`
14. `PetelAssistants/SQL/add-class-assistant-budget-hours-participation.sql` — `ministry_participation_pct` field on each year/school_level/classification record
15. `PetelAssistants/SQL/seed-class-assistant-budget-hours-tashpu.sql` — optional seed for תשפו
16. `PetelAssistants/SQL/add-budget-hour-value.sql` — shared `budget_hour_values` + `year_elements_hour_value_save`
17. `PetelAssistants/SQL/add-personal-approvals-pdf-action.sql` — PDF convert action (`entitlements_personal_approvals_pdf`)
18. `PetelAssistants/SQL/add-personal-entitlement-upload.sql` — personal entitlement upload (`entitlements_personal_upload` + mappings table)
19. `PetelAssistants/SQL/add-entitlement-validity.sql` — entitlement `is_valid` / source snapshot + `entitlements_resolve_invalid`
20. `PetelAssistants/SQL/add-maindashboard-org-units.sql` — dashboard button for org units (`maindashboard_org_units`)
21. `PetelAssistants/SQL/add-monthly-ops.sql` — statuses, department mapping, Meitar topic→type, month summaries, salary anomalies + debug screen actions
22. `PetelAssistants/SQL/add-meitar-mucarim-retrieve.sql` — `meitar_mucarim` table + best-effort tracking columns on `meitar_retrieve_processes` (MUCARIM pulled alongside MUTAVIM, viewable via a tab on `/meitar-data`)
23. `PetelAssistants/SQL/add-yearly-budget-comparisons.sql` — per-budget-version salary/Meitar comparison table (`yearly_budget_comparisons`)
24. `PetelAssistants/SQL/add-yearly-budget-recalculate-summaries-action.sql` — budget-screen button (`yearly_budget_recalculate_summaries`)
25. `PetelAssistants/SQL/add-gregorian-year-actions.sql` — calendar-year hub + child screens

## Files

| File | Route |
|------|-------|
| `MainDashboard.razor` | `/maindashboard` |
| `GregorianYearManagement.razor` | `/gregorian/{Year:int}` |
| `GregorianBudget.razor` | `/gregorian/{Year:int}/budget` |
| `GregorianEntitlements.razor` | `/gregorian/{Year:int}/entitlements` |
| `GregorianAssistants.razor` | `/gregorian/{Year:int}/assistants` |
| `YearManagement.razor` | `/year/{YearId:int}` |
| `YearElements.razor` | `/year-elements` |
| `YearElementsTabs/YearElementsClassAssistantHoursTab.razor` | (tab under year-elements) |
| `YearElementsTabs/YearElementsHourValueTab.razor` | (tab under year-elements) |
| `SalaryUploadModal.razor` | (modal from dashboard / year hub) |
| `Salaries.razor` | `/salaries` |
| `SalaryMonthSummary.razor` | `/salaries/month-summary` |
| `SalaryAnomalies.razor` | `/salaries/anomalies` |
| `SalaryDepartmentMappings.razor` | `/salary-department-mappings` |
| `MeitarData.razor` | `/meitar-data` |
| `MeitarMonthSummary.razor` | `/meitar-data/month-summary` |
| `Assistants.razor` | `/year/{YearId:int}/assistants` |
| `Entitlements.razor` | `/year/{YearId:int}/entitlements` |
| `PersonalEntitlementUploadModal.razor` | (modal from entitlements — personal PDF/Excel upload) |
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
