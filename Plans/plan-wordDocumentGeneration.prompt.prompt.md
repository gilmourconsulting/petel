# Plan: Word Document Generation via Template (MiniWord, DOCX)

## TL;DR
Extend the existing Excel report system to support Word document (DOCX) generation using **MiniWord** (Apache-2.0, free). Simultaneously rename all four `excel_report_*` database tables to `report_*` and rename the corresponding C# model classes and DbSet properties. Unify the Blazor Reports page. Add a bulk Word generation service mirroring `CouncilExcelGenerationService`.

## Decisions
- **Library**: MiniWord (Apache-2.0, free) — `{{token}}` syntax, RTL preserved from template, DOCX output
- **PDF**: Out of scope — users open DOCX in Word/Google Docs and print to PDF themselves
- **Template syntax**: `{{FieldName}}` (scalar), `{{listName}}` + `{{listName.Field}}` (collection in table row)
- **DB tables**: Rename `excel_report_*` → `report_*`; add `format` column to `report_definitions`
- **C# class names**: Full rename (`ExcelReportDefinition` → `ReportDefinition` etc.)
  - `Petel.Core.Excel.ReportDefinition` (JSON schema) renamed to `ReportTemplateSchema` to avoid collision
- **Controller routes**: `ReportsController` (`api/reports`), `ReportTemplatesController` (`api/reporttemplates`)
- **UI**: Unified Reports page (extend existing Excel Reports page)
- **Bulk generation**: Yes — new `WordDocumentGenerationService`

---

## Phase 1: Rename DB Tables + Shared Schema Class

1. **SQL migration** `SQL/rename-report-tables.sql`:
   - `ALTER TABLE petel_schema.excel_report_definitions RENAME TO report_definitions`
   - `ALTER TABLE petel_schema.excel_report_templates RENAME TO report_templates`
   - `ALTER TABLE petel_schema.excel_report_queries RENAME TO report_queries`
   - `ALTER TABLE petel_schema.excel_report_parameters RENAME TO report_parameters`
   - Rename FK constraints and indexes
   - Add `format VARCHAR(10) NOT NULL DEFAULT 'excel'` with CHECK `('excel', 'word')` to `report_definitions`
   - Idempotent `DO $$` blocks

2. **Rename JSON schema class** in `shared/Petel.Core/Excel/ReportDefinition.cs`:
   - `ReportDefinition` → `ReportTemplateSchema`
   - Update all refs: `ReportTemplateEngine.cs`, `ExcelReportsController.cs`, `AthExcelEntityRegistry.cs`, doc comment in `ExcelQueryConfig.cs`

3. **Rename EF model classes** (`PetelATH/PetelATH.Api/Models/`):
   - `ExcelReportDefinition` → `ReportDefinition` — `[Table("report_definitions")]` + add `Format` property
   - `ExcelReportTemplate` → `ReportTemplate` — `[Table("report_templates")]`
   - `ExcelReportQuery` → `ReportQuery` — `[Table("report_queries")]`
   - `ExcelReportParameter` → `ReportParameter` — `[Table("report_parameters")]`
   - Update navigation property types in each model

4. **Update `AppDbContext.cs`** (`PetelATH/PetelATH.Api/Data/AppDbContext.cs`):
   - Rename DbSet properties: `ExcelReportDefinitions` → `ReportDefinitions`, etc. (4 props + ~10 OnModelCreating lines)

5. **Update historical SQL seed files** — add comments noting table rename; files: `add-definition-json-column.sql`, `insert-council-students-report.sql`, `insert-entity-students-report.sql`, `update-*` files

## Phase 2: Rename Controllers + Update All References

6. **Rename controllers**:
   - `ExcelReportsController.cs` → `ReportsController.cs`; route → `api/reports`
   - `ExcelReportTemplatesController.cs` → `ReportTemplatesController.cs`; route → `api/reporttemplates`
   - Update all `_context.ExcelReport*` DbSet accesses and type refs throughout (~24 lines across both files)

7. **Single-line fixes**:
   - `DocumentsController.cs` line 1878: `ExcelReportDefinitions` → `ReportDefinitions`
   - `CouncilExcelGenerationService.cs` line 81: `ExcelReportDefinitions` → `ReportDefinitions`

8. **Update Blazor API route strings** (6 lines across 2 files):
   - `ExcelTemplateMapping.razor`: `"excelreporttemplates/..."` → `"reporttemplates/..."`
   - `ExcelReports.razor` line 525: `"excelreporttemplates/..."` → `"reporttemplates/..."`

9. **Rename Blazor pages** (file + `@page` directives + nav menu links):
   - `ExcelReports.razor` → `Reports.razor` (`/reports`)
   - `ExcelReportBuilder.razor` → `ReportBuilder.razor`
   - `ExcelTemplateMapping.razor` → `ReportTemplateMapping.razor`

10. **Rename Blazor DTOs**: `ExcelReportDto.cs` → `ReportDto.cs`, class `ExcelReportDto` → `ReportDto`; add `string Format` property; update all usages in the 3 Blazor pages

## Phase 3: MiniWord Integration + DocumentTemplateEngine

11. **Add NuGet** to `shared/Petel.Core/Petel.Core.csproj`:
    - `<PackageReference Include="MiniWord" Version="0.9.2" />`

12. **Create `shared/Petel.Core/Documents/DocumentTemplateEngine.cs`**:
    - Constructor: `(IExcelEntityRegistry registry, ILogger<DocumentTemplateEngine> logger)` — reuses `IExcelEntityRegistry`, `ExcelEntityContext`, `ReportTemplateSchema`
    - `Task<byte[]> GenerateAsync(byte[] templateBlob, string definitionJson, ExcelEntityContext context, Dictionary<string, string> runtimeParams, CancellationToken ct = default)`
    - Parse `definitionJson` → `ReportTemplateSchema`; resolve parameters same way as `ReportTemplateEngine`
    - Build `Dictionary<string, object>` for MiniWord:
      - **Scalar** sources: flatten to `"dsName_FieldName"` → value (template: `{{dsName_FieldName}}`)
      - **Collection** sources: key `"dsName"` → `List<ExpandoObject>` (convert from `List<Dictionary<string,object?>>` — Dictionary keys become dynamic properties via ExpandoObject)
    - Call `MiniWord.SaveAsByTemplate(outputStream, templateBytes, valueDict)`; return `outputStream.ToArray()`

13. **Create `shared/Petel.Core/Documents/DocumentTemplateService.cs`**:
    - `IReadOnlyList<string> ScanPlaceholders(byte[] templateBlob)` — use `DocumentFormat.OpenXml` (already available via MiniWord transitive dep) to scan all run text in body and table cells for `{{...}}` tokens via regex

## Phase 4: API — Word Report Generation

14. **Register** `DocumentTemplateEngine` and `DocumentTemplateService` as scoped in `PetelATH/PetelATH.Api/Program.cs`

15. **Update `ReportsController` `POST /{id}/generate`**:
    - Branch on `reportDef.Format`:
      - `"excel"` → existing path (no change)
      - `"word"` → call `DocumentTemplateEngine.GenerateAsync()`, return `File(bytes, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "report.docx")`

16. **Update `ReportTemplatesController`**:
    - `POST /{id}/upload`: validate `.xlsx` for excel, `.docx` for word
    - `GET /{id}/scan`: route to `DocumentTemplateService.ScanPlaceholders()` for word, existing `ExcelTemplateService` for excel

## Phase 5: Bulk Word Generation Service

17. **Create `PetelATH/PetelATH.Api/Services/WordDocumentGenerationService.cs`**:
    - Mirrors `CouncilExcelGenerationService` exactly
    - Uses `DocumentTemplateEngine` instead of `ReportTemplateEngine`
    - Configurable `ReportDefinitionName` and `DocumentTypeName` constants
    - Returns `CouncilExcelResult` (reused unchanged)
    - Output file extension `.docx`; same Document + DocumentLink upsert pattern

## Phase 6: Blazor UI Updates

18. **Update `Reports.razor`**:
    - Add `Format` badge column ("Excel" / "Word") to report list
    - Add filter tabs: "הכל" / "Excel" / "Word"
    - Run modal: DOCX download for word reports (no format toggle needed)

19. **Update `ReportTemplateMapping.razor`**:
    - `accept=".xlsx"` for excel, `accept=".docx"` for word (based on report format)
    - Placeholder section label: `{{...}}` syntax for both formats

---

## Relevant Files

| File | Change |
|---|---|
| `shared/Petel.Core/Excel/ReportDefinition.cs` | Rename class → `ReportTemplateSchema` |
| `shared/Petel.Core/Excel/ReportTemplateEngine.cs` | Update refs |
| `shared/Petel.Core/Excel/ExcelQueryConfig.cs` | Update doc comment |
| `shared/Petel.Core/Petel.Core.csproj` | Add MiniWord NuGet |
| NEW `shared/Petel.Core/Documents/DocumentTemplateEngine.cs` | Word template engine |
| NEW `shared/Petel.Core/Documents/DocumentTemplateService.cs` | `.docx` placeholder scanner |
| `PetelATH/PetelATH.Api/Models/ExcelReportDefinition.cs` | Rename + table + Format prop |
| `PetelATH/PetelATH.Api/Models/ExcelReportTemplate.cs` | Rename + table |
| `PetelATH/PetelATH.Api/Models/ExcelReportQuery.cs` | Rename + table |
| `PetelATH/PetelATH.Api/Models/ExcelReportParameter.cs` | Rename + table |
| `PetelATH/PetelATH.Api/Data/AppDbContext.cs` | 4 DbSets + OnModelCreating (~14 lines) |
| `PetelATH/PetelATH.Api/Controllers/ExcelReportsController.cs` | Rename → `ReportsController.cs` |
| `PetelATH/PetelATH.Api/Controllers/ExcelReportTemplatesController.cs` | Rename → `ReportTemplatesController.cs` |
| `PetelATH/PetelATH.Api/Controllers/DocumentsController.cs` | 1 line |
| `PetelATH/PetelATH.Api/Services/CouncilExcelGenerationService.cs` | 1 line |
| `PetelATH/PetelATH.Api/Program.cs` | Register 2 new services |
| NEW `PetelATH/PetelATH.Api/Services/WordDocumentGenerationService.cs` | Bulk Word generation |
| `PetelATH/PetelATH.BlazorServer/Components/Pages/ExcelReports.razor` | Rename + format badge |
| `PetelATH/PetelATH.BlazorServer/Components/Pages/ExcelReportBuilder.razor` | Rename |
| `PetelATH/PetelATH.BlazorServer/Components/Pages/ExcelTemplateMapping.razor` | Rename + .docx accept |
| `PetelATH/PetelATH.BlazorServer/DTOs/ExcelReportDto.cs` | Rename + Format field |
| NEW `SQL/rename-report-tables.sql` | Idempotent rename + format column |

---

## Verification

1. Run `SQL/rename-report-tables.sql` on dev DB; verify `report_definitions` (with `format` column), `report_templates`, `report_queries`, `report_parameters` exist; `excel_report_*` gone
2. `dotnet build` — no compile errors after renames and MiniWord added
3. Open Reports page — all existing reports load with "Excel" badge; generate correctly (no regression)
4. Create `format='word'` report; upload `.docx` template with `{{CouncilName}}` and table row `{{students.Name}}` / `{{students.ClassName}}`; run → verify DOCX downloads with Hebrew text and logos preserved, table rows expanded
5. Run `WordDocumentGenerationService` for a test council; verify `Document` + `DocumentLink` records created

---

## Further Considerations

1. **`ReportDefinition` name collision**: Renaming shared JSON schema class to `ReportTemplateSchema` resolves the collision. Ensure no serialization attributes use `nameof()` on the old class name.
2. **MiniWord ExpandoObject conversion**: `QueryEntityAsync` returns `List<Dictionary<string,object?>>`. MiniWord needs `IEnumerable` of objects with readable properties. Convert via ExpandoObject: `dict.Aggregate(new ExpandoObject() as IDictionary<string,object?>, (exp, kv) => { exp[kv.Key] = kv.Value; return exp; })`. This is the key implementation detail in `DocumentTemplateEngine`.
3. **PDF upgrade path**: When needed, deploy Gotenberg (MIT Docker, ~$5–10/mo ACI). `DocumentTemplateEngine` already returns DOCX bytes — a thin `GotenbergPdfConverter` service wraps the ACI HTTP POST. MiniWord is the same library regardless of which PDF path is chosen later.
