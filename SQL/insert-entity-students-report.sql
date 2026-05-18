-- SQL/insert-entity-students-report.sql
-- Inserts the "דוח תלמידי ישות" report definition.
-- Run AFTER add-excel-reports.sql and add-definition-json-column.sql.
-- Safe to re-run (uses ON CONFLICT DO NOTHING).
--
-- After running this script:
--   1. Navigate to דוחות Excel → the report appears in the list.
--   2. Click the edit (pencil) icon.
--   3. Upload entity-students-template.xlsx as the template
--      (generate it first: cd SQL/Templates/GenerateEntityStudentsTemplate && dotnet run).
--   4. Click "הפעל דוח" → choose year → download.
--
-- IMPORTANT: Replace "בסיסית" in the template with the exact value from the
--   'name' column of the basic pricing element for your school year.

INSERT INTO petel_schema.excel_report_definitions
    (name, description, report_type, allow_cross_year, requires_entity_context,
     is_active, sort_order, definition_json)
VALUES (
    'דוח תלמידי ישות',
    'רשימת כל תלמידי הישות עם פרטי בית ספר, כיתה, קטגוריה, תאריכי קליטה/סיום, חודשים מחושבים ועלויות מרכיב בסיסי',
    'template',
    false,
    true,
    true,
    20,
    $definition$

{
  "parameters": [
    {
      "name": "hebrew_year_id",
      "type": "year_selector",
      "label": "שנת לימודים",
      "required": true
    },
    {
      "name": "network_entity_id",
      "type": "network_selector",
      "label": "סנן לפי רשת (לרשת בעלויות בלבד)",
      "required": false
    }
  ],
  "dataSources": [
    {
      "name": "header",
      "entity": "OwnerEntity",
      "type": "scalar",
      "filters": [],
      "sort": []
    },
    {
      "name": "students",
      "entity": "StudentsWithPricingElements",
      "type": "collection",
      "filters": [],
      "sort": [
        { "field": "SchoolName", "direction": "asc" },
        { "field": "LastName",   "direction": "asc" },
        { "field": "FirstName",  "direction": "asc" }
      ]
    }
  ]
}

$definition$
)
ON CONFLICT DO NOTHING;
