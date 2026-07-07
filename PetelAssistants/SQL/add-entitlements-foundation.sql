-- =============================================================================
-- PetelAssistants — Entitlements foundation (Hebrew years, assistant types, org hierarchy)
-- Run after add-years-and-menu.sql. Idempotent.
-- =============================================================================

-- ─── hebrew_years: rename year_name → hebrew_year (ATH convention) ───────────
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'shared_schema'
          AND table_name = 'hebrew_years'
          AND column_name = 'year_name'
    ) AND NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'shared_schema'
          AND table_name = 'hebrew_years'
          AND column_name = 'hebrew_year'
    ) THEN
        ALTER TABLE shared_schema.hebrew_years RENAME COLUMN year_name TO hebrew_year;
        RAISE NOTICE 'Renamed hebrew_years.year_name to hebrew_year';
    END IF;
END $$;

-- Ensure date/flag columns exist
ALTER TABLE shared_schema.hebrew_years ADD COLUMN IF NOT EXISTS start_date  DATE NULL;
ALTER TABLE shared_schema.hebrew_years ADD COLUMN IF NOT EXISTS end_date    DATE NULL;
ALTER TABLE shared_schema.hebrew_years ADD COLUMN IF NOT EXISTS is_current  BOOLEAN NOT NULL DEFAULT false;
ALTER TABLE shared_schema.hebrew_years ADD COLUMN IF NOT EXISTS is_previous BOOLEAN NOT NULL DEFAULT false;
ALTER TABLE shared_schema.hebrew_years ADD COLUMN IF NOT EXISTS is_active   BOOLEAN NOT NULL DEFAULT true;

-- Backfill sample Gregorian ranges if missing (approximate school-year boundaries)
UPDATE shared_schema.hebrew_years SET start_date = '2022-09-01', end_date = '2023-08-31'
WHERE hebrew_year = E'\u05ea\u05e9\u05e4\u05d2' AND start_date IS NULL;

UPDATE shared_schema.hebrew_years SET start_date = '2023-09-01', end_date = '2024-08-31'
WHERE hebrew_year = E'\u05ea\u05e9\u05e4\u05d3' AND start_date IS NULL;

UPDATE shared_schema.hebrew_years SET start_date = '2024-09-01', end_date = '2025-08-31'
WHERE hebrew_year = E'\u05ea\u05e9\u05e4\u05d4' AND start_date IS NULL;

UPDATE shared_schema.hebrew_years SET start_date = '2025-09-01', end_date = '2026-08-31'
WHERE hebrew_year = E'\u05ea\u05e9\u05e4\u05d5' AND start_date IS NULL;

-- ─── entity_types: kindergarten ───────────────────────────────────────────────
INSERT INTO shared_schema.entity_types (name, description)
SELECT 'kindergarten', E'\u05d2\u05df \u05d9\u05dc\u05d3\u05d9\u05dd'
WHERE NOT EXISTS (
    SELECT 1 FROM shared_schema.entity_types WHERE name = 'kindergarten'
);

-- ─── entities: parent_entity_id for schools/kindergartens under authorities ───
ALTER TABLE shared_schema.entities
    ADD COLUMN IF NOT EXISTS parent_entity_id INTEGER NULL
    REFERENCES shared_schema.entities(id) ON DELETE RESTRICT;

CREATE INDEX IF NOT EXISTS idx_entities_parent_entity_id
    ON shared_schema.entities(parent_entity_id);

-- ─── assistant_types (shared lookup) ─────────────────────────────────────────
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT FROM pg_tables WHERE schemaname = 'shared_schema' AND tablename = 'assistant_types'
    ) THEN
        CREATE TABLE shared_schema.assistant_types (
            id           SERIAL PRIMARY KEY,
            name         VARCHAR(100) NOT NULL UNIQUE,
            display_name VARCHAR(150) NOT NULL,
            description  VARCHAR(255) NULL,
            sort_order   INTEGER NOT NULL DEFAULT 0,
            is_active    BOOLEAN NOT NULL DEFAULT true
        );

        INSERT INTO shared_schema.assistant_types (name, display_name, description, sort_order)
        VALUES
            ('class_help',   E'\u05e1\u05d9\u05d9\u05e2\u05ea \u05db\u05d9\u05ea\u05ea\u05d9\u05ea',     E'\u05e1\u05d9\u05d9\u05e2\u05ea \u05db\u05d9\u05ea\u05d4',           10),
            ('school_help',  E'\u05e1\u05d9\u05d9\u05e2\u05ea \u05ea\u05d2\u05d1\u05d5\u05e8 \u05de\u05d5\u05e1\u05d3\u05d9\u05ea', E'\u05e1\u05d9\u05d9\u05e2\u05ea \u05ea\u05d2\u05d1\u05d5\u05e8 \u05de\u05d5\u05e1\u05d3\u05d9\u05ea', 20),
            ('student_help', E'\u05e1\u05d9\u05d9\u05e2\u05ea \u05ea\u05dc\u05de\u05d9\u05d3',           E'\u05e1\u05d9\u05d9\u05e2\u05ea \u05ea\u05dc\u05de\u05d9\u05d3 \u05d0\u05d9\u05e9\u05d9', 30)
        ON CONFLICT (name) DO NOTHING;

        RAISE NOTICE 'Table assistant_types created and seeded';
    ELSE
        RAISE NOTICE 'Table assistant_types already exists';
    END IF;
END $$;
