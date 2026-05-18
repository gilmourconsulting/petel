-- SQL/update-entity-students-report-add-network-param.sql
-- Adds the network_entity_id parameter to the "דוח תלמידי ישות" report definition.
-- Run this if the report was already inserted via insert-entity-students-report.sql
-- (which uses ON CONFLICT DO NOTHING, so the new parameter was never saved).
-- Safe to re-run — uses jsonb_set which is idempotent.

UPDATE petel_schema.excel_report_definitions
SET definition_json = jsonb_set(
    definition_json::jsonb,
    '{parameters}',
    (
        -- Rebuild the parameters array with network_entity_id included.
        -- If it already exists (by name), skip adding it.
        CASE
            WHEN definition_json::jsonb -> 'parameters' @> '[{"name":"network_entity_id"}]'
            THEN definition_json::jsonb -> 'parameters'
            ELSE (definition_json::jsonb -> 'parameters') ||
                 '[{"name":"network_entity_id","type":"network_selector","label":"סנן לפי רשת (לרשת בעלויות בלבד)","required":false}]'::jsonb
        END
    ),
    true
)::text
WHERE name = 'דוח תלמידי ישות';
