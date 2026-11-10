-- =============================================================================
-- PetelAssistants — Meitar CHESHBONIT retrieve
-- Adds assist_schema.meitar_cheshbonit (invoice summary pulled alongside
-- MUTAVIM/MUCARIM/SHARATIM by the existing Meitar retrieve action) and
-- best-effort tracking columns on meitar_retrieve_processes.
-- TopicCode filters for CHESHBONIT are configured in meitar_data_filter_values
-- via System Data (not seeded here).
-- Idempotent — safe to run multiple times.
-- Run after add-meitar-mutavim-retrieve.sql / add-meitar-mucarim-retrieve.sql /
-- add-meitar-sharatim-retrieve.sql
-- =============================================================================

-- ─── 1. meitar_retrieve_processes: CHESHBONIT best-effort tracking columns ───
ALTER TABLE assist_schema.meitar_retrieve_processes
    ADD COLUMN IF NOT EXISTS cheshbonit_row_count       INTEGER       NULL,
    ADD COLUMN IF NOT EXISTS cheshbonit_total_paid_sum  NUMERIC(14,2) NULL,
    ADD COLUMN IF NOT EXISTS cheshbonit_error           TEXT          NULL;

-- ─── 2. meitar_cheshbonit ─────────────────────────────────────────────────────
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT FROM pg_tables
        WHERE schemaname = 'assist_schema'
          AND tablename  = 'meitar_cheshbonit'
    ) THEN
        CREATE TABLE assist_schema.meitar_cheshbonit (
            id                          SERIAL PRIMARY KEY,
            entity_id                   INTEGER       NOT NULL REFERENCES shared_schema.entities(id) ON DELETE CASCADE,
            period_year                 INTEGER       NOT NULL,
            period_month                INTEGER       NOT NULL,
            beneficiary_code            VARCHAR(50)   NOT NULL,
            calc_date                   DATE          NOT NULL,
            effective_date              DATE          NULL,
            accounting_unit             VARCHAR(100)  NULL,
            topic_code                  VARCHAR(50)   NULL,
            topic_description           VARCHAR(500)  NULL,
            total_due                   NUMERIC(14,2) NULL,
            paid_amount                 NUMERIC(14,2) NULL,
            payment_difference          NUMERIC(14,2) NULL,
            debt_balance                NUMERIC(14,2) NULL,
            monthly_execution_balance   NUMERIC(14,2) NULL,
            notes                       VARCHAR(1000) NULL,
            process_id                  INTEGER       NOT NULL REFERENCES assist_schema.meitar_retrieve_processes(id) ON DELETE RESTRICT,
            created_at                  TIMESTAMP     NOT NULL DEFAULT CURRENT_TIMESTAMP,
            user_id                     INTEGER       NULL REFERENCES assist_schema.users(id) ON DELETE SET NULL,
            updated_at                  TIMESTAMP     NOT NULL DEFAULT CURRENT_TIMESTAMP,
            update_user                 INTEGER       NULL REFERENCES assist_schema.users(id) ON DELETE SET NULL,
            CONSTRAINT meitar_cheshbonit_month_check CHECK (period_month BETWEEN 1 AND 12)
        );

        CREATE INDEX idx_meitar_cheshbonit_entity_id
            ON assist_schema.meitar_cheshbonit(entity_id);

        CREATE INDEX idx_meitar_cheshbonit_period
            ON assist_schema.meitar_cheshbonit(entity_id, period_year, period_month);

        CREATE INDEX idx_meitar_cheshbonit_process_id
            ON assist_schema.meitar_cheshbonit(process_id);

        RAISE NOTICE 'Table assist_schema.meitar_cheshbonit created';
    ELSE
        RAISE NOTICE 'Table assist_schema.meitar_cheshbonit already exists';
    END IF;
END $$;
