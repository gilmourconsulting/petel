# Excel Reports — User Guide

This guide explains how to use the Excel report generation system: how to browse and run existing reports, how to create a new report, and how to design a template report with custom formatting.

---

## Running a Report

1. Log in to the application.
2. Navigate to **דוחות אקסל** (Excel Reports) from the side menu.
3. The page shows a table of all available reports.
4. Click the **הפעל** (Run) button on any row.
5. A modal appears with the report's parameters (e.g. school year, council). Fill in the required fields.
6. Click **הפק דוח** — the filled `.xlsx` file downloads automatically.

> Parameters marked with * are required. Parameters of type "שנת לימודים" (school year) or "רשות שולחת" (council) show dropdowns populated from the database.

---

## Creating a New Report

1. Click **+ צור דוח חדש** at the top of the Excel Reports page.
2. Fill in the modal:
   - **שם** — report name (shown in the list)
   - **תיאור** — optional description
   - **סוג דוח** — choose one of:
     - `query_builder` — pick an entity, fields, and filters visually
     - `template` — upload a pre-designed `.xlsx` template (most powerful option)
3. Click **שמור** to create the report definition.

### After Creating — Configure the Report

**For `query_builder` reports:**

Click the **ערוך** (Edit) button. You are taken to the query builder page where you can select the entity, choose fields to include, add filters, and set sort order.

**For `template` reports:**

Click **ערוך** to open the edit modal. At the bottom of the modal you will see a **תבנית** (Template) section. Upload your designed `.xlsx` file there (see "Designing a Template" below).

---

## Designing a Template Report

A template report is a standard `.xlsx` file that you design in Excel with special token placeholders. When the report is generated, the engine replaces the tokens with live data.

### Token Syntax

**Scalar token** — outputs a single value from a data source named `header`:
```
{{header.Name}}
```

**Collection block** — outputs one row per data record:
```
Row:   {{#students}}
Row:   {{students.LastName}}  {{students.FirstName}}  {{students.SchoolName}}
Row:   {{/students}}
```

The `{{#students}}` and `{{/students}}` rows are **marker rows** — they are deleted at runtime. The row between them is the **template row** that is duplicated for each record, copying its cell styles, borders, and formatting.

Any SUM formula rows placed **below** the collection block automatically adjust when rows are inserted.

### Available Data Sources

Data sources are defined in the report's `definition_json`. Consult the system administrator for which entities are available, or see the table below for common ones:

| Source name | Hebrew description | Type |
|---|---|---|
| `header` | פרטי הגוף המפעיל | scalar |
| `council` | פרטי רשות | scalar |
| `students` | תלמידים | collection |
| `studentsWithSchool` | תלמידים + בית ספר | collection |

### Field Names

Field names are case-insensitive. Common fields for `StudentsWithSchool`:

| Token | Description |
|---|---|
| `{{students.LastName}}` | שם משפחה |
| `{{students.FirstName}}` | שם פרטי |
| `{{students.IdNumber}}` | תעודת זהות |
| `{{students.SchoolName}}` | שם בית הספר |
| `{{students.ClassName}}` | כיתה |
| `{{students.StartDate}}` | תאריך תחילה |
| `{{students.EndDate}}` | תאריך סיום |
| `{{students.Cost}}` | עלות |
| `{{students.DisabilityCategory}}` | קטגוריית מוגבלות |

For `OwnerEntity` (header data):

| Token | Description |
|---|---|
| `{{header.Name}}` | שם הגוף |
| `{{header.ContactPersonName}}` | שם איש קשר |
| `{{header.ContactPersonPhone}}` | טלפון |
| `{{header.ContactPersonEmail}}` | אימייל |

### Step-by-Step: Create a Template Report

1. **Design in Excel:**
   - Open Excel and create a new `.xlsx` file.
   - Add title rows, header rows, and any static content.
   - Place scalar tokens (e.g. `{{header.Name}}`) in the cells where you want fixed values.
   - For a list of records, add three consecutive rows:
     - Row N: a cell containing only `{{#students}}`
     - Row N+1: one cell per column, each containing a token like `{{students.LastName}}`
     - Row N+2: a cell containing only `{{/students}}`
   - Optionally add a SUM formula row below row N+2 — it will auto-adjust.
   - Apply any formatting (bold headers, borders, colours, RTL direction).
   - Save as `.xlsx`.

2. **Create the report definition in the UI:**
   - Go to Excel Reports → **+ צור דוח חדש**
   - Name the report, choose `template` as the type
   - Click Save

3. **Set the `definition_json`** (ask a developer if needed — this is a JSON string stored in the DB that declares which parameters and data sources the report uses).

4. **Upload the template:**
   - Click **ערוך** on the new report
   - In the Template section, click **בחר קובץ** and select your `.xlsx`
   - Click **העלה תבנית** — the system confirms how many placeholders were found

5. **Run the report:**
   - Click **הפעל**, fill in the required parameters, click **הפק דוח**

---

## Value Formatting

The engine formats values automatically:

| Data type | Output format |
|---|---|
| Date | `dd/MM/yyyy` |
| Number / decimal | `N2` (e.g. `1,234.56`) |
| Boolean | `כן` or `לא` |
| Null | (empty cell) |

---

## Troubleshooting

**Report generates but tokens are not replaced**

Check that the token spelling in the `.xlsx` file exactly matches the `name` field in the `definition_json` data source (e.g. `{{header.Name}}` requires a data source named `header`, not `Header`). Token matching is case-insensitive at runtime, but double-check for typos.

**Collection block not expanded**

The `{{#dsName}}` and `{{/dsName}}` marker cells must exist on separate rows. The template data row must be immediately after (one row below) the start marker.

**Download button does nothing / report fails**

Check that all required parameters are filled. If the error persists, ask a system administrator to check the API log for details.

**Upload returns an error**

Only `.xlsx` files up to 10 MB are accepted. Make sure you are saving in Excel format (not `.xls` or `.csv`).

---

## For Developers: Adding a New Report

See the developer instruction file at `.github/instructions/petelath.instructions.md`, section **Excel Report Generation System**, for:

- How to add a new entity to `AthExcelEntityRegistry`
- The `definition_json` schema reference
- DI registrations in `Program.cs`
- SQL scripts to run (`add-excel-reports.sql`, `add-definition-json-column.sql`)
- Common error fixes (`CellMappingsJson` must always be `"[]"`, never `null`)
