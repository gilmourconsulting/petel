-- SQL/update-council-report-entity.sql
-- Migrates the "דוח תלמידים לפי רשות שולחת" report definition to use
-- StudentsWithPricingElements instead of StudentsWithSchool.
-- Safe to re-run (REPLACE on an already-updated row is a no-op).
--
-- Run on all environments after deploying the API build that includes
-- the StudentsWithPricingElements registry entity.

UPDATE petel_schema.excel_report_definitions
SET definition_json = REPLACE(
    definition_json,
    '"entity": "StudentsWithSchool"',
    '"entity": "StudentsWithPricingElements"'
)
WHERE name = 'דוח תלמידים לפי רשות שולחת';
