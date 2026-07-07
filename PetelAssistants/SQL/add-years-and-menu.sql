-- =============================================================================
-- PetelAssistants — Hebrew Years & Menu Items
-- Run after bootstrap.sql. Idempotent (uses IF NOT EXISTS / ON CONFLICT DO NOTHING).
-- =============================================================================

-- ─── hebrew_years ─────────────────────────────────────────────────────────────
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT FROM pg_tables WHERE schemaname = 'shared_schema' AND tablename = 'hebrew_years'
    ) THEN
        CREATE TABLE shared_schema.hebrew_years (
            id          SERIAL PRIMARY KEY,
            hebrew_year VARCHAR(20)  NOT NULL UNIQUE,
            start_date  DATE         NULL,
            end_date    DATE         NULL,
            is_current  BOOLEAN NOT NULL DEFAULT false,
            is_previous BOOLEAN NOT NULL DEFAULT false,
            is_active   BOOLEAN NOT NULL DEFAULT true
        );

        -- Seed with common years — update is_current / is_previous to match your environment
        INSERT INTO shared_schema.hebrew_years (hebrew_year, is_previous, is_current, is_active)
        VALUES
            (E'\u05ea\u05e9\u05e4\u05d2', false, false, true),   -- תשפג 2022/23
            (E'\u05ea\u05e9\u05e4\u05d3', false, false, true),   -- תשפד 2023/24
            (E'\u05ea\u05e9\u05e4\u05d4', true,  false, true),   -- תשפה 2024/25 ← previous
            (E'\u05ea\u05e9\u05e4\u05d5', false, true,  true)    -- תשפו 2025/26 ← current
        ON CONFLICT (hebrew_year) DO NOTHING;

        RAISE NOTICE 'Table hebrew_years created and seeded';
    ELSE
        RAISE NOTICE 'Table hebrew_years already exists';
    END IF;
END $$;

-- ─── menu_items ───────────────────────────────────────────────────────────────
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT FROM pg_tables WHERE schemaname = 'shared_schema' AND tablename = 'menu_items'
    ) THEN
        CREATE TABLE shared_schema.menu_items (
            id          SERIAL PRIMARY KEY,
            name        VARCHAR(50)  NOT NULL UNIQUE,
            reference   VARCHAR(100) NOT NULL,
            text        VARCHAR(100) NOT NULL,
            action_id   INTEGER      NULL,
            sort_order  INTEGER      NOT NULL DEFAULT 0,
            is_active   BOOLEAN      NOT NULL DEFAULT true
        );

        -- Seed initial menu — add rows as new pages are built
        INSERT INTO shared_schema.menu_items (name, reference, text, sort_order, is_active)
        VALUES
            ('maindashboard', '#maindashboard', 'לוח בקרה',    10, true)
        ON CONFLICT (name) DO NOTHING;

        RAISE NOTICE 'Table menu_items created and seeded';
    ELSE
        RAISE NOTICE 'Table menu_items already exists';
    END IF;
END $$;
