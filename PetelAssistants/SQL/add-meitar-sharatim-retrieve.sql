-- =============================================================================
-- PetelAssistants — Meitar SHARATIM retrieve
-- Adds assist_schema.meitar_sharatim (special-needs class counts per school per
-- month, pulled alongside MUTAVIM/MUCARIM by the existing Meitar retrieve
-- action) and best-effort tracking columns on meitar_retrieve_processes.
-- Seeds the TopicCode=107 filter for SHARATIM in meitar_data_filter_values.
-- Idempotent — safe to run multiple times.
-- Run after add-meitar-mutavim-retrieve.sql and add-meitar-mucarim-retrieve.sql
-- =============================================================================

-- ─── 1. meitar_retrieve_processes: SHARATIM best-effort tracking columns ─────
ALTER TABLE assist_schema.meitar_retrieve_processes
    ADD COLUMN IF NOT EXISTS sharatim_row_count        INTEGER NULL,
    ADD COLUMN IF NOT EXISTS sharatim_total_class_count INTEGER NULL,
    ADD COLUMN IF NOT EXISTS sharatim_error             TEXT    NULL;

-- ─── 2. meitar_sharatim ───────────────────────────────────────────────────────
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT FROM pg_tables
        WHERE schemaname = 'assist_schema'
          AND tablename  = 'meitar_sharatim'
    ) THEN
        CREATE TABLE assist_schema.meitar_sharatim (
            id                 SERIAL PRIMARY KEY,
            entity_id          INTEGER      NOT NULL REFERENCES shared_schema.entities(id) ON DELETE CASCADE,
            period_year        INTEGER      NOT NULL,
            period_month       INTEGER      NOT NULL,
            calc_date          DATE         NOT NULL,
            effective_date     DATE         NOT NULL,
            institution_code   VARCHAR(50)  NULL,
            institution_name   VARCHAR(300) NULL,
            topic_code         VARCHAR(50)  NULL,
            class_count        INTEGER      NOT NULL,
            institution_id     INTEGER      NULL REFERENCES assist_schema.institutions(id) ON DELETE SET NULL,
            hebrew_year_id     INTEGER      NULL,
            process_id         INTEGER      NOT NULL REFERENCES assist_schema.meitar_retrieve_processes(id) ON DELETE RESTRICT,
            created_at         TIMESTAMP    NOT NULL DEFAULT CURRENT_TIMESTAMP,
            user_id            INTEGER      NULL REFERENCES assist_schema.users(id) ON DELETE SET NULL,
            updated_at         TIMESTAMP    NOT NULL DEFAULT CURRENT_TIMESTAMP,
            update_user        INTEGER      NULL REFERENCES assist_schema.users(id) ON DELETE SET NULL,
            CONSTRAINT meitar_sharatim_month_check CHECK (period_month BETWEEN 1 AND 12)
        );

        CREATE INDEX idx_meitar_sharatim_entity_id
            ON assist_schema.meitar_sharatim(entity_id);

        CREATE INDEX idx_meitar_sharatim_period
            ON assist_schema.meitar_sharatim(entity_id, period_year, period_month);

        CREATE INDEX idx_meitar_sharatim_process_id
            ON assist_schema.meitar_sharatim(process_id);

        CREATE INDEX idx_meitar_sharatim_institution_id
            ON assist_schema.meitar_sharatim(institution_id);

        CREATE INDEX idx_meitar_sharatim_hebrew_year_id
            ON assist_schema.meitar_sharatim(hebrew_year_id);

        RAISE NOTICE 'Table assist_schema.meitar_sharatim created';
    ELSE
        RAISE NOTICE 'Table assist_schema.meitar_sharatim already exists';
    END IF;
END $$;

-- Note: hebrew_year_id intentionally has no FK constraint — hebrew_years lives
-- in shared_schema and assist_schema tables never reference shared_schema
-- lookup tables (other than entities) with a DB-level FK (see entitlements.hebrew_year_id
-- for the same convention).

-- ─── 3. Seed TopicCode=107 filter for SHARATIM ───────────────────────────────
INSERT INTO shared_schema.meitar_data_filter_values (file_name, filter_field, filter_value, is_active, display_order)
SELECT 'SHARATIM', 'TopicCode', '107', true, 1
WHERE NOT EXISTS (
    SELECT 1 FROM shared_schema.meitar_data_filter_values
    WHERE file_name = 'SHARATIM' AND filter_field = 'TopicCode' AND filter_value = '107'
);
