-- =============================================================================
-- PetelAssistants — Meitar MUCARIM retrieve
-- Adds assist_schema.meitar_mucarim (recognised institutions data pulled
-- alongside MUTAVIM by the existing Meitar retrieve action) and best-effort
-- tracking columns on meitar_retrieve_processes.
-- Idempotent — safe to run multiple times.
-- Run after add-meitar-mutavim-retrieve.sql and add-meitar-data-view.sql
-- =============================================================================

-- ─── 1. meitar_retrieve_processes: MUCARIM best-effort tracking columns ───────
ALTER TABLE assist_schema.meitar_retrieve_processes
    ADD COLUMN IF NOT EXISTS mucarim_row_count            INTEGER       NULL,
    ADD COLUMN IF NOT EXISTS mucarim_total_calculated_sum NUMERIC(14,2) NULL,
    ADD COLUMN IF NOT EXISTS mucarim_error                TEXT          NULL;

-- ─── 2. meitar_mucarim ────────────────────────────────────────────────────────
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT FROM pg_tables
        WHERE schemaname = 'assist_schema'
          AND tablename  = 'meitar_mucarim'
    ) THEN
        CREATE TABLE assist_schema.meitar_mucarim (
            id                          SERIAL PRIMARY KEY,
            entity_id                   INTEGER       NOT NULL REFERENCES shared_schema.entities(id) ON DELETE CASCADE,
            period_year                 INTEGER       NOT NULL,
            period_month                INTEGER       NOT NULL,
            beneficiary_code            VARCHAR(50)   NOT NULL,
            calc_date                   DATE          NOT NULL,
            effective_date              DATE          NULL,
            institution_code            VARCHAR(50)   NULL,
            institution_name            VARCHAR(300)  NULL,
            topic_code                  VARCHAR(50)   NULL,
            topic_description           VARCHAR(500)  NULL,
            status                      VARCHAR(100)  NULL,
            unit_count                  NUMERIC(14,4) NULL,
            percent                     NUMERIC(9,4)  NULL,
            cost                        NUMERIC(14,2) NULL,
            calculated_amount           NUMERIC(14,2) NOT NULL DEFAULT 0,
            previous_calculated_amount  NUMERIC(14,2) NULL,
            calculated_difference       NUMERIC(14,2) NULL,
            unit_description            VARCHAR(500)  NULL,
            process_id                  INTEGER       NOT NULL REFERENCES assist_schema.meitar_retrieve_processes(id) ON DELETE RESTRICT,
            created_at                  TIMESTAMP     NOT NULL DEFAULT CURRENT_TIMESTAMP,
            user_id                     INTEGER       NULL REFERENCES assist_schema.users(id) ON DELETE SET NULL,
            updated_at                  TIMESTAMP     NOT NULL DEFAULT CURRENT_TIMESTAMP,
            update_user                 INTEGER       NULL REFERENCES assist_schema.users(id) ON DELETE SET NULL,
            CONSTRAINT meitar_mucarim_month_check CHECK (period_month BETWEEN 1 AND 12)
        );

        CREATE INDEX idx_meitar_mucarim_entity_id
            ON assist_schema.meitar_mucarim(entity_id);

        CREATE INDEX idx_meitar_mucarim_period
            ON assist_schema.meitar_mucarim(entity_id, period_year, period_month);

        CREATE INDEX idx_meitar_mucarim_process_id
            ON assist_schema.meitar_mucarim(process_id);

        RAISE NOTICE 'Table assist_schema.meitar_mucarim created';
    ELSE
        RAISE NOTICE 'Table assist_schema.meitar_mucarim already exists';
    END IF;
END $$;
