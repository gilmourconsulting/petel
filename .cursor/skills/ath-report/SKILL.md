---
name: ath-report
description: >-
  Build or change PetelATH Excel import/export, report definitions, .xlsx
  templates, or Word/DOCX generation. Use when the user mentions an ATH report,
  Excel template, definition_json, AthExcelEntityRegistry, student file import,
  or a .docx placeholder.
---

# ATH reports and documents

Read `docs/agents/apps/petel-ath-reports-excel.md` first. Also read the Excel report and document sections of `docs/agents/apps/petel-ath.md`.

ATH only. Do not edit `PetelAssistants/**`.

## Choose the path

- On-screen table download: the table is already wrapped in `TableExcelExport`. Do not add a second Excel button for that grid.
- File import: EPPlus, `.xlsx` only, 10MB max, `GetCurrentSession()` before reading rows. Compare Hebrew cells with `GlobalFunctions.PureHebrewText()`.
- Template report: `definition_json` (parameters + dataSources), a row in `petel_schema.excel_report_definitions`, and the `.xlsx` uploaded from the Excel Reports page. Register new entities on `AthExcelEntityRegistry`.
- Word/DOCX: `DocumentTemplateEngine` and `{{placeholder}}` tokens via `DocumentTemplateService`. Do not invent a second document engine.

## Template report checklist

1. Token cells and `{{#ds}}` / `{{/ds}}` rows in the workbook.
2. `definition_json` for parameters and data sources.
3. Insert into `excel_report_definitions` (`report_type` `template` when it is a template).
4. Upload the `.xlsx` from the Excel Reports screen. Do not only commit the file and skip the upload step.
