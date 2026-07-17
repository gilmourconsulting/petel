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

**Year linkage:** Assistant registration per Hebrew year is **not** part of the person domain — it will be implemented via entitlement assignments (future).

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

**Single combined screen** at `/year/{YearId}/entitlements`. Personal and institutional entitlements are managed in the same table with unified filters.

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

**Primary use case:** `QueryMutavimByTopicDescriptionsAsync()` loads symbol codes from local authorities and TopicDescription filter values from the config table, then calls PetelMeitar `POST /api/data/query` with `fileName=MUTAVIM`.

**Generic queries:** `QueryAsync()` accepts any supported file suffix (see `MeitarDataFileNames.All`). `QueryAllFileTypesAsync()` iterates all 19 documented file types.

SQL migration: `PetelAssistants/SQL/add-meitar-data-integration.sql`. API reference: `PetelAssistants/docs/ApiReference.md`.

## Related

- Year management UI flow: [PetelAssistants/docs/year-management-screens.md](../../PetelAssistants/docs/year-management-screens.md)
- Tenancy implementation: [petel-assistants.md](petel-assistants.md)
