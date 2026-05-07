-- SQL/insert-council-students-report.sql
-- Inserts the "דוח תלמידים לפי רשות שולחת" report definition.
-- Run AFTER add-excel-reports.sql and add-definition-json-column.sql.
-- Safe to re-run (uses ON CONFLICT DO NOTHING).
--
-- After running this script:
--   1. Navigate to דוחות Excel → the report appears in the list.
--   2. Click the edit (pencil) icon → open the builder.
--   3. Upload council-students-template.xlsx as the template.
--   4. Click "הפעל דוח" (stats icon) → choose year + council → download.

INSERT INTO petel_schema.excel_report_definitions
    (name, description, report_type, allow_cross_year, requires_entity_context,
     is_active, sort_order, definition_json)
VALUES (
    'דוח תלמידים לפי רשות שולחת',
    'רשימת תלמידים עם פרטי בית ספר, מסוננת לפי שנת לימודים ורשות שולחת',
    'template',
    false,   -- cross-year not allowed; hebrew_year_id is required
    true,
    true,
    10,
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
      "name": "sending_council_id",
      "type": "council_selector",
      "label": "רשות שולחת",
      "required": true
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
      "name": "council",
      "entity": "Council",
      "type": "scalar",
      "filters": [
        { "field": "Id", "operator": "eq", "paramName": "sending_council_id" }
      ],
      "sort": []
    },
    {
      "name": "students",
      "entity": "StudentsWithSchool",
      "type": "collection",
      "filters": [
        { "field": "SendingCouncil", "operator": "eq", "paramName": "sending_council_id" }
      ],
      "sort": [
        { "field": "SchoolName",  "direction": "asc" },
        { "field": "LastName",    "direction": "asc" },
        { "field": "FirstName",   "direction": "asc" }
      ]
    }
  ]
}
$definition$
)
ON CONFLICT DO NOTHING;
