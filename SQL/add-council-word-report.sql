-- Migration: Insert Word letter report definition for council letters
-- Idempotent: safe to re-run on all environments
-- Prereq: run add-council-word-doctype.sql first

DO $$
DECLARE
    v_definition_json TEXT := '{
  "parameters": [
    { "name": "hebrew_year_id",     "type": "year_selector",    "label": "שנת לימודים",  "required": true },
    { "name": "sending_council_id", "type": "council_selector", "label": "רשות שולחת",   "required": true }
  ],
  "dataSources": [
    {
      "name":    "header",
      "entity":  "OwnerEntity",
      "type":    "scalar",
      "filters": [],
      "sort":    []
    },
    {
      "name":    "council",
      "entity":  "Council",
      "type":    "scalar",
      "filters": [
        { "field": "Id", "operator": "eq", "paramName": "sending_council_id" }
      ],
      "sort": []
    },
    {
      "name":    "summary",
      "entity":  "CouncilStats",
      "type":    "scalar",
      "filters": [
        { "field": "CouncilId", "operator": "eq", "paramName": "sending_council_id" }
      ],
      "sort": []
    }
  ]
}';
BEGIN
    INSERT INTO petel_schema.report_definitions
        (name, description, report_type, format, definition_json, is_active, sort_order,
         allow_cross_year, requires_entity_context, created_at, updated_at)
    VALUES
        ('מכתב לרשות תשפו',
         'מכתב Word לרשות משלחת — כולל שם ארוך, מספר תלמידים וסכום בסיסית',
         'template',
         'word',
         v_definition_json,
         true,
         200,
         false,
         true,
         NOW(),
         NOW())
    ON CONFLICT DO NOTHING;

    RAISE NOTICE 'report_definition "מכתב לרשות תשפו" ready';
END
$$;
