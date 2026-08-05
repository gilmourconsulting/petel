# PetelAssistants — Domain Context

> Canonical: `docs/agents/apps/petel-assistants-domain.md`. Technical patterns: [petel-assistants.md](petel-assistants.md).

## Project purpose

The aim of the system is to allow localities to manage school assistants. The school assistants are managed per school year — expressed in Jewish years.

There are different types of school assistants (class/school/student level).

In order to employ an assistant (סייעת) there needs to be an entitlement (זכאות) for that year. The entitlement includes school, number of hours, start and end date (default based on the school year), type of assistant and student (if relevant).

It should be possible to manage people in the system that are assistants of other roles. People may have multiple roles.

Assistants will be allocated to an entitlement with a start and end date that must be in the range of the entitlement.

## Architecture

**Multi-tenancy:** Each local authority's data must be strictly isolated — users of authority A must never see authority B's data. Local authorities are modelled as `entities` in `shared_schema`. Schools and kindergartens are tenant-owned `institutions` in `assist_schema` (not shared entities).

**Shared (global) configuration:** Some reference data is shared across all tenants — entity types, assistant types, cities, the list of authorities themselves, and similar lookup tables. Security actions appear here. System attributes as well. This data is managed centrally and read by all.

Key system attributes:

| Name | id | Type | Purpose |
|---|---|---|---|
| `validate_israeli_id_checksum` | 20 | bool | System-wide toggle: when `true`, all Israeli national ID entries (entitlements, persons, and any future features) are validated against the Luhn-like checksum algorithm. Read via `IAttributeCache`. |
| `Security_PasswordPolicy` | — | string | Regex for password validation. |
| `Security_OtpEnabled` | — | bool | Whether email OTP is required at login. |
| `Security_SessionTimeoutMinutes` | — | integer | Idle session timeout in minutes. |

**Tenant-specific configuration:** Some configuration belongs to a specific local authority — users, user roles, permission sets, local settings, and others to be defined.

**Cross-entity persons:** A person (e.g. an assistant) may exist in more than one local authority. There is no shared identity record — each local authority holds a fully isolated copy of the person's data, with no linkage enforced at the database level between authorities. If the same physical person works for two authorities, they appear as two independent records, each fully owned by their respective authority. Any deduplication or identity matching, if needed in future, is an application-layer concern, not a schema constraint.

**SaaS deployment:** The application will be deployed as a SaaS product. The multi-tenancy model must scale horizontally with tenant count at zero operational overhead per new tenant. Schema-per-tenant is explicitly excluded. The required approach is two fixed PostgreSQL schemas regardless of tenant count — `shared_schema` for global reference data with no tenant ownership, and `assist_schema` for all operational data with mandatory `entity_id` on every table, filtered at the EF Core query level via global query filters. Adding a new local authority is an `INSERT` into `shared_schema.entities` only, with no schema or infrastructure changes.

**Encryption:** National IDs and emails must be encrypted in the database (use `DataEncryptionService` from `Petel.Core`). See [reference/conventions.md](../reference/conventions.md) for EF conversion patterns.

## Persons (people)

**Identity:** Each person belongs to exactly one tenant (`entity_id`). National ID (`id_number`) is unique per tenant — encrypted with deterministic AES for DB lookup and uniqueness. The same physical person in two authorities appears as two independent rows.

**Not all persons are assistants.** Role linkage (assistant for a Hebrew year, pupil, etc.) is added in later features. The person domain is shared infrastructure.

**Versioned main data:** `person_details` stores historical versions with `version`, `is_last_version`, `start_date`, and `end_date`. Default reads use the last version (`is_last_version = true`). Updates create a new version and close the previous row's `end_date`.

**Address history:** `person_addresses` keeps all addresses; exactly one row is `is_active = true` per person. Changes deactivate the current row and insert a new active row.

**Phone history:** `person_phones` supports multiple phone types (from `shared_schema.phone_types`). Exactly one active phone per `(person_id, phone_type_id)`.

**Field alignment:** Main person fields follow PetelATH `persons` columns (`first_name`, `last_name`, `gender`, `date_of_birth`, `email`, `position`, etc.).

**Year linkage:** The Assistants screen (`/year/{YearId}/assistants`) lists persons for the authority and shows year-scoped allocation status. List/search accept optional `yearId` to populate `HasAllocation` (any active entitlement allocation for that Hebrew year). Filters: name/ID search + allocated / not allocated. **View details** (`assistants_view_details`) opens a read-only modal with the person snapshot, allocation history across all Hebrew years (`GET persons/{id}/allocations` without `yearId`), and salary history matched to the person (`GET persons/{id}/salaries` via `matched_person_id`).

**Excel import:** Assistants page supports bulk create via `POST api/personsfileupload/preview` then `/upload`. Flow: select file → map columns → process. Mapped fields today: `id_number` (required) plus either `name` (split on first space into first/last; single token → last name `-`) or both `first_name` and `last_name`. Existing national IDs for the tenant are **skipped** (not updated). SQL action: `assistants_upload` (`add-persons-upload-action.sql`).

## Salary file upload

Manual Excel/CSV salary import for the logged-in authority. Entry points: context buttons on Main Dashboard and Year Management (`SalaryUploadModal`).

**Tables (assist_schema):** `salaries`, `salary_upload_processes`, `salary_upload_warnings`, `salary_field_mappings`. SQL: `add-salary-upload.sql`.

**Period:** Separate integers `period_year` + `period_month` (1–12). UI defaults to the previous calendar month.

**Business key:** `(entity_id, period_year, period_month, national_id, department_id)`. Re-upload for an existing period requires user confirmation; on continue, prior salary rows (and their warnings) for that period are deleted, then new data is inserted.

**Process registration:** Each upload creates a `salary_upload_processes` row (`source = manual`). On completion: `row_count`, `total_salary_sum`, period, optional `file_name`.

**Column mapping:** Same preview → map → upload flow as persons. Mappable fields: `national_id`, `department_id`, `department_name`, `position_percentage`, `total_salary`. Entity-level saved map in `salary_field_mappings` (includes `id_includes_check_digit`); used as default when present.

**National ID:** Encrypted deterministic AES at rest. When the map/upload flag says the ID includes a check digit, verify Israeli checksum; on failure save the row with `has_id_warning = true` and a `salary_upload_warnings` row (`invalid_id_checksum`). When it does not include a check digit, left-pad to 8 digits and append the computed check digit. After rows are saved, each `national_id` is matched to `persons` for the same entity using a canonical 9-digit form (left-pad) so a leading zero on salary rows still matches a person stored without it; when found, `matched_person_id` is set to `persons.id`.

**Allocation matching:** Stored in `salaries.matched_allocation_id`. After person matching (at upload and on recheck), each matched row is linked to an active `entitlement_allocations` row for the person whose date range overlaps the row's salary month (earliest-starting allocation when several overlap; a still-valid stored match is kept). When the stored allocation is no longer active/overlapping it is replaced or cleared.

**API:** `GET api/salaryfileupload/period-exists`, `GET/PUT mapping`, `POST preview`, `POST upload`. Security actions: `maindashboard_salary_upload`, `yearmanagement_salary_upload`.

**View screen:** `/salaries` (`Salaries.razor`) — read-only table of uploaded salary rows. Entry points: context buttons on Main Dashboard and Year Management. Defaults to the previous calendar month. Summary dashboard above the filters (computed from the loaded period): record count, total salary, distinct national IDs, unmatched-to-person rows (count + sum, clickable drill-down), matched rows without an active allocation overlapping the salary month (count + sum, clickable drill-down), ID warning count. Filters above the table: period year/month (server reload), national ID, department, matched-to-person, allocation-for-period, ID warning. API: `GET api/salaries?year=&month=` — each row includes `HasAllocationForPeriod`, derived from the stored `matched_allocation_id`. Security actions: `salaries_page_action`, `salaries_back`, `salaries_refresh`, `salaries_recheck`, `maindashboard_salaries_view`, `yearmanagement_salaries_view`. SQL: `add-salaries-view-actions.sql`, `add-salaries-recheck-action.sql`.

**Recheck (view screen):** the "בדיקת התאמות מחדש" context button (`salaries_recheck`) calls `POST api/salaries/recheck?year=&month=`, which re-runs person matching for the period's still-unmatched rows (picks up person records created after upload; sets `matched_person_id`) and then allocation matching for all matched rows (updates the stored `matched_allocation_id` — added, removed, or re-pointed). Returns counts (`NewlyMatchedPersons`, `AllocationsAdded`, `AllocationsRemoved`); the page then reloads and shows a summary modal listing the affected rows, diffed against the previously displayed data.

## Hebrew years

**Global definition:** `shared_schema.hebrew_years` stores the Hebrew year label (`hebrew_year`), Gregorian `start_date` / `end_date`, and flags `is_current`, `is_previous`, `is_active`. System administrators set dates across the entire system via the Hebrew years admin screen.

**Validation:** Entitlement start/end dates must fall within the Hebrew year's date range. Defaults on create come from the year's dates.

## Institutions (schools and kindergartens)

Each local authority maintains its own list of schools and kindergartens as rows in `assist_schema.institutions` with mandatory `entity_id` (the owning authority). Institutions are **not** shared across tenants.

| Field | Values | Notes |
|---|---|---|
| `institution_type` | `school`, `kindergarten` | Required |
| `school_level` | `elementary` (יסודי), `high_school` (תיכון) | Required for schools; null for kindergartens |
| `is_special_education` | bool | חינוך מיוחד — applies to any institution |

CRUD UI remains at `/org-units` (`api/org-units`); storage is `institutions`.

## Assistant types

Managed globally in `shared_schema.assistant_types` by the system manager. Tenants read active types when creating entitlements.

## Entitlements (זכאויות)

**Scope:** Tenant-owned rows in `assist_schema.entitlements`, filtered by `entity_id` and Hebrew year.

**Single combined screen** at `/year/{YearId}/entitlements`. Personal and institutional entitlements are managed in the same table with unified filters (kind, institution, assistant type, active status, allocation status: none / partial / full).

**Kind is derived from assistant type level** — there is no stored `entitlement_kind` column. The `level` field on `shared_schema.assistant_types` (values: `personal`, `class`, `school`, `kindergarten`) determines the entitlement type:
- `personal` → personal entitlement (pupil fields required)
- `class` / `school` / `kindergarten` → institutional entitlement (pupil fields null)

**Personal entitlements:**
- `pupil_id_number` (VARCHAR 9, exactly 9 digits, leading zero allowed) — Israeli national ID stored **encrypted** via deterministic AES.
- `pupil_first_name`, `pupil_last_name` — required.
- `institution_id` — the pupil's school/kindergarten (must belong to the authority).
- `class_name` — optional free text.
- Israeli ID checksum validation is gated by `system_attribute` key `validate_israeli_id_checksum` (bool, id=20 in production). This is a **system-wide flag** — it applies to all Israeli national ID entries across the application (entitlements, persons, and any future features). Read via `IAttributeCache.GetAttributeValue("validate_israeli_id_checksum")`.

**Institutional entitlements:**
- `institution_id` — the institution receiving the entitlement (required, must belong to the authority).
- `class_name` — optional free text.
- Pupil fields (`pupil_id_number`, `pupil_first_name`, `pupil_last_name`) must be null.

**Ministry participation %** is selected from a dropdown backed by `shared_schema.ministry_participation_options` (seeded: 100%, 70%). Not free text.

**Business rules:**
- `institution_id` is required for all entitlements (both kinds).
- Pupil fields must be all-set or all-null (enforced by DB CHECK constraint).
- Dates must be within the Hebrew year bounds.
- The institution must belong to the logged-in authority (validated at service layer via tenant filter).
- Assistant allocation to entitlements (with dates within entitlement range) is a follow-on feature.

## Meitar data integration

Ministry budget data is queried from **PetelMeitar** via `MeitarDataService` (`IMeitarDataService`).

| Component | Location |
|---|---|
| Service | `PetelAssistants.Api/Services/MeitarDataService.cs` |
| API config | `MeitarApi:BaseUrl`, `MeitarApi:TimeoutSeconds` in appsettings |
| Symbol codes | `shared_schema.entities.symbol_code` on active `local_authority` rows |
| Filter config | `shared_schema.meitar_data_filter_values` (`file_name`, `filter_field`, `filter_value`) |

**Primary use case:** `QueryMutavimByTopicDescriptionsAsync()` loads symbol codes from local authorities and active filter rows from `meitar_data_filter_values` for `MUTAVIM` (typically `TopicCode`, per ApiReference), then calls PetelMeitar `POST /api/data/query` with that `filterField` + values.

**Generic queries:** `QueryAsync()` accepts any supported file suffix (see `MeitarDataFileNames.All`). `QueryAllFileTypesAsync()` iterates all 19 documented file types.

SQL migration: `PetelAssistants/SQL/add-meitar-data-integration.sql`. API reference: `PetelAssistants/docs/ApiReference.md`.

### MUTAVIM retrieve (period pull into Assistants)

Manual retrieve from Main Dashboard / Year Management context buttons (`MeitarRetrieveModal` → `MeitarDataController`). SQL: `PetelAssistants/SQL/add-meitar-mutavim-retrieve.sql`.

| Endpoint | Purpose |
|---|---|
| `GET api/meitardata/period-exists?year=&month=` | Whether MUTAVIM rows already exist for the current entity + period |
| `POST api/meitardata/retrieve` | Pull MUTAVIM for the authority’s `symbol_code` and period; body: `{ periodYear, periodMonth, replaceExisting }` |

**Period:** calendar `period_year` + `period_month` (1–12). UI defaults to the previous calendar month. Meitar filter uses `CalcDate` as `MM/yyyy`.

**Scope:** session entity only — never queries other authorities’ symbols. Requires `entities.symbol_code` on the logged-in authority.

**Topic filter:** same as `QueryMutavimByTopicDescriptionsAsync` — Meitar is queried using the active `filter_field` + `filter_value` rows for `MUTAVIM` in `meitar_data_filter_values` (e.g. `TopicCode` / `101`). Required; fails if none configured. Period is applied in Assistants by keeping rows whose `calcDate` matches the selected month/year.

**Override:** re-retrieve for an existing period requires confirmation; on continue, prior `meitar_mutavim` rows for that period are deleted, then a new `meitar_retrieve_processes` row and fresh fact rows are inserted.

**Tables (assist_schema):** `meitar_retrieve_processes`, `meitar_mutavim`. Rows store the full Meitar MUTAVIM payload except import metadata (`Id`, `SourceFile`, `ImportedAt`, `ImportRunId`): optional `effective_date`, `unit_count`, `cost`, `participation_percent`, `previous_calculated_amount`, `calculated_difference` were added by `add-meitar-data-view.sql` (rows retrieved before those columns existed have nulls until re-retrieved).

**Security actions:** `maindashboard_meitar_retrieve`, `yearmanagement_meitar_retrieve`.

### Meitar data view screen

**View screen:** `/meitar-data` (`MeitarData.razor`) — read-only table of stored `meitar_mutavim` rows. Entry points: context buttons on Main Dashboard (`/meitar-data`) and Year Management (`/meitar-data?fromYear={yearId}`). Period filter is calendar year + month (server reload) applied to a date column, not to `period_year`/`period_month`:

- **From Year Management:** defaults to September of the Gregorian year in which the Hebrew year starts (via `GET years/{id}` → `StartDate`); filters by **calc date** only (date-field selector hidden). Back returns to `/year/{yearId}`.
- **From Main Dashboard:** a selector chooses the filter column — **calc date (תאריך חישוב)** or **effective date (תאריך תחולה)** (rows with null `effective_date` are excluded when filtering by effective date). Defaults to the previous calendar month. Back returns to `/maindashboard`.

Table shows all stored MUTAVIM fields (beneficiary code, calc date, effective date, topic code/description, unit count, cost, participation percent, calculated amount, previous calculated amount, calculated difference) — import metadata is not shown. Client-side filters: beneficiary code, topic code/description. Summary cards: record count, total calculated sum, distinct topics.

**API:** `GET api/meitardata?year=&month=&dateField=calc|effective` — tenant-scoped list of `MeitarMutavimListItemDto`.

**Security actions:** `meitardata_page_action`, `meitardata_back`, `meitardata_refresh`, `maindashboard_meitar_view`, `yearmanagement_meitar_view`. SQL: `add-meitar-data-view.sql` (also adds the `effective_date` column).

## Related

- Year management UI flow: [PetelAssistants/docs/year-management-screens.md](../../PetelAssistants/docs/year-management-screens.md)
- Tenancy implementation: [petel-assistants.md](petel-assistants.md)
