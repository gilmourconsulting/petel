# Excel Report Generation System — Implementation Plan

## Goal

Build a definition-driven Excel report engine that lets administrators design
templated `.xlsx` reports, upload them via the Blazor UI, and generate them
on-demand with live database data.

---

## Architecture

```
Blazor UI (ExcelReports.razor)
  └─ POST /api/excelreports/{id}/generate  { RuntimeParams }
       └─ ExcelReportsController
            ├─ query_builder / advanced_sql → ExcelGenerationService
            └─ template → ReportTemplateEngine
                 ├─ Parses definition_json → ReportDefinition
                 ├─ For each dataSource → AthExcelEntityRegistry.QueryEntityAsync()
                 └─ Fills template blob → returns filled .xlsx bytes
```

### Report Types

| Type | Description |
|---|---|
| `query_builder` | Entity + field/filter/sort config → simple auto-formatted table |
| `advanced_sql` | Raw SQL → simple table |
| `template` | Designer-uploaded .xlsx; engine fills `{{tokens}}` and expands collection rows |

---

## Components Built

### Shared Library (`shared/Petel.Core/Excel/`)

| File | Status | Purpose |
|---|---|---|
| `IExcelEntityRegistry.cs` | ✅ Done | Interface: `QueryEntityAsync`, `GetAvailableEntities`, `GetEntityDescriptor` |
| `ExcelEntityContext.cs` | ✅ Done | Scope holder: `EntityId`, `EntityTypeId`, `SchoolYearId` |
| `ExcelQueryConfig.cs` | ✅ Done | Field/filter/sort config POCO |
| `ExcelEntityDescriptor.cs` | ✅ Done | Entity + field metadata for builder UI |
| `ExcelGenerationService.cs` | ✅ Done | Simple tabular export (`query_builder`/`advanced_sql`) |
| `ExcelTemplateService.cs` | ✅ Done | `ScanPlaceholders()` — validates and lists tokens in uploaded file |
| `ReportDefinition.cs` | ✅ Done | `ReportDefinition`, `ParameterDefinition`, `DataSourceDefinition` POCOs |
| `ReportTemplateEngine.cs` | ✅ Done | Full template fill + collection row expansion engine |

### API (`PetelATH.Api/`)

| File | Status | Purpose |
|---|---|---|
| `Models/ExcelReportDefinition.cs` | ✅ Done | Entity + `DefinitionJson` column |
| `Models/ExcelReportTemplate.cs` | ✅ Done | Template blob store (`CellMappingsJson = "[]"` always) |
| `Models/ExcelReportQuery.cs` | ✅ Done | Query config for non-template reports |
| `Models/ExcelReportParameter.cs` | ✅ Done | DB-stored parameter schema |
| `Data/AppDbContext.cs` (config) | ✅ Done | EF relationships + `excel_report_*` DbSets |
| `Controllers/ExcelReportsController.cs` | ✅ Done | CRUD + `/params` + `/generate` + `/preview` |
| `Controllers/ExcelReportTemplatesController.cs` | ✅ Done | `/upload` + `/download` + `/scan` + `/mappings` |
| `Services/AthExcelEntityRegistry.cs` | ✅ Done | 9 entities: Students, Schools, SchoolClasses, AdditionalStudyPrograms, Transactions, TransactionAccounts, OwnerEntity, Council, StudentsWithSchool |
| `Program.cs` DI registrations | ✅ Done | `ExcelTemplateService`, `IExcelEntityRegistry`, `ExcelGenerationService`, `ReportTemplateEngine` — all Scoped |

### Blazor UI (`PetelATH.BlazorServer/`)

| File | Status | Purpose |
|---|---|---|
| `Components/Pages/ExcelReports.razor` | ✅ Done | List, create, edit, run reports; template upload |
| `DTOs/ExcelReportDto.cs` | ✅ Done | `TemplateFilename` field included |

### Database Scripts (`SQL/`)

| File | Status | Purpose |
|---|---|---|
| `add-excel-reports.sql` | ✅ Written | Creates 4 tables (idempotent) |
| `add-definition-json-column.sql` | ✅ Written | Adds `definition_json` to `excel_report_definitions` |
| `insert-council-students-report.sql` | ✅ Written | Sample report definition with full JSON |
| `Templates/council-students-report-definition.json` | ✅ Written | Definition JSON for the sample report |
| `Templates/council-students-template.xlsx` | ✅ Generated | Sample Excel template file |

---

## SQL Scripts — Run Order (Per Environment)

Run once on each environment DB before using the reports feature:

```sql
-- 1. Create tables
\i SQL/add-excel-reports.sql

-- 2. Add definition_json column
\i SQL/add-definition-json-column.sql

-- 3. (Optional) Insert sample council-students report
\i SQL/insert-council-students-report.sql
```

All scripts are **idempotent** — safe to re-run.

---

## Key Design Decisions

### Template Engine Flow

1. Parse `definition_json` → `ReportDefinition` object
2. For each `dataSource`, call `AthExcelEntityRegistry.QueryEntityAsync()` scoped to session entity
3. Apply in-memory filters (using `paramName` → `runtimeParams[paramName]`)
4. Apply in-memory sort
5. Open template `.xlsx` with EPPlus
6. For each worksheet:
   a. `ExpandCollection` for each collection data source (finds `{{#ds}}` / `{{/ds}}` rows, inserts rows)
   b. `FillScalars` replaces `{{ds.Field}}` tokens
7. Return `package.GetAsByteArray()`

### `CellMappingsJson` = `"[]"` Always

`excel_report_templates.cell_mappings_json` is `TEXT NOT NULL DEFAULT '[]'`.  
**Never assign `null`** — always `"[]"`. This was the root cause of the first 500 error on upload.

### `definition_json` vs Legacy Path

`ExcelReportsController.GenerateTemplateReportAsync`:
- If `DefinitionJson` is non-null → `ReportTemplateEngine.GenerateAsync()` (collection expansion supported)
- Else → legacy `ExcelTemplateService.FillTemplate()` (scalar replacement only)

### Year ID vs School Year ID

`school_year_id` runtime param = `hebrew_years.id` (NOT `school_years.id`).  
`AthExcelEntityRegistry` uses `context.SchoolYearId` which is set from `runtimeParams["school_year_id"]` in `BuildEntityContext`.

---

## Template Syntax Quick Reference

```
Scalar:              {{header.Name}}
Collection start:    {{#students}}        ← this row is deleted
Collection data:     {{students.LastName}} {{students.FirstName}}
Collection end:      {{/students}}        ← this row is deleted
```

SUM formula rows below a collection block automatically shift when rows are inserted.

---

## Entity Registry — Available Entities

| Name | Hebrew | Type | Scope |
|---|---|---|---|
| `Students` | תלמידים | collection | School/Council |
| `Schools` | בתי ספר | collection | Council/Admin |
| `SchoolClasses` | כיתות | collection | School |
| `AdditionalStudyPrograms` | תוכניות תל"ן | collection | School |
| `Transactions` | עסקאות | collection | School (cross-year) |
| `TransactionAccounts` | חשבונות | collection | School (cross-year) |
| `OwnerEntity` | הגוף שלי | scalar | Any |
| `Council` | רשויות | collection/scalar | Any |
| `StudentsWithSchool` | תלמידים + בית ספר | collection | Council/Admin |

---

## Status Summary

| Area | Status |
|---|---|
| Core engine (ReportTemplateEngine, entity registry) | ✅ Complete |
| API controllers + models + EF | ✅ Complete |
| Blazor UI (list, create, edit, run, upload) | ✅ Complete |
| SQL migration scripts | ✅ Written — need to be run on each environment |
| Sample council-students report artifacts | ✅ Complete |
| Upload 500 bug fix (CellMappingsJson = null) | ✅ Fixed |
| End-to-end test (run council-students report) | ⏳ Pending — after DB scripts run |

---

## Remaining Tasks

- [ ] Run `SQL/add-excel-reports.sql` on dev, test, production DBs
- [ ] Run `SQL/add-definition-json-column.sql` on dev, test, production DBs
- [ ] Upload `SQL/Templates/council-students-template.xlsx` via Blazor UI Edit → Upload Template
- [ ] Run the council-students report end-to-end to verify output
- [ ] (Optional) Add more template reports for other data types
