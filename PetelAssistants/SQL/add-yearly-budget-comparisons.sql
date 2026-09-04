-- =============================================================================
-- PetelAssistants — Yearly budget comparisons (סיכום מול תקציב per version)
-- One row per budget version × calendar month × assistant type (nullable = unmapped).
-- Salary and Meitar actuals come from the latest import process for that month.
-- Idempotent — safe to run multiple times.
-- =============================================================================

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT FROM pg_tables
        WHERE schemaname = 'assist_schema' AND tablename = 'yearly_budget_comparisons'
    ) THEN
        CREATE TABLE assist_schema.yearly_budget_comparisons (
            id                  SERIAL PRIMARY KEY,
            entity_id           INTEGER       NOT NULL REFERENCES shared_schema.entities(id) ON DELETE CASCADE,
            yearly_budget_id    INTEGER       NOT NULL REFERENCES assist_schema.yearly_budgets(id) ON DELETE CASCADE,
            period_year         INTEGER       NOT NULL,
            period_month        INTEGER       NOT NULL,
            assistant_type_id   INTEGER       NULL REFERENCES shared_schema.assistant_types(id) ON DELETE SET NULL,
            budget_fte          NUMERIC(10,2) NOT NULL DEFAULT 0,
            budget_hours        NUMERIC(10,2) NOT NULL DEFAULT 0,
            budget_amount       NUMERIC(14,2) NOT NULL DEFAULT 0,
            salary_row_count    INTEGER       NOT NULL DEFAULT 0,
            salary_fte          NUMERIC(10,2) NOT NULL DEFAULT 0,
            salary_hours        NUMERIC(10,2) NOT NULL DEFAULT 0,
            salary_amount       NUMERIC(14,2) NOT NULL DEFAULT 0,
            salary_process_id   INTEGER       NULL REFERENCES assist_schema.salary_upload_processes(id) ON DELETE SET NULL,
            meitar_row_count    INTEGER       NOT NULL DEFAULT 0,
            meitar_hours        NUMERIC(14,4) NOT NULL DEFAULT 0,
            meitar_amount       NUMERIC(14,2) NOT NULL DEFAULT 0,
            meitar_process_id   INTEGER       NULL REFERENCES assist_schema.meitar_retrieve_processes(id) ON DELETE SET NULL,
            created_at          TIMESTAMP     NOT NULL DEFAULT CURRENT_TIMESTAMP,
            user_id             INTEGER       NULL REFERENCES assist_schema.users(id) ON DELETE SET NULL,
            updated_at          TIMESTAMP     NOT NULL DEFAULT CURRENT_TIMESTAMP,
            update_user         INTEGER       NULL REFERENCES assist_schema.users(id) ON DELETE SET NULL,
            CONSTRAINT yearly_budget_comparisons_month_check
                CHECK (period_month BETWEEN 1 AND 12),
            CONSTRAINT yearly_budget_comparisons_nonneg CHECK (
                budget_fte >= 0 AND budget_hours >= 0 AND budget_amount >= 0
                AND salary_row_count >= 0 AND salary_fte >= 0 AND salary_hours >= 0 AND salary_amount >= 0
                AND meitar_row_count >= 0 AND meitar_hours >= 0 AND meitar_amount >= 0
            )
        );

        CREATE INDEX idx_yearly_budget_comparisons_budget
            ON assist_schema.yearly_budget_comparisons(yearly_budget_id);

        CREATE INDEX idx_yearly_budget_comparisons_period
            ON assist_schema.yearly_budget_comparisons(yearly_budget_id, period_year, period_month);

        CREATE UNIQUE INDEX idx_yearly_budget_comparisons_budget_type
            ON assist_schema.yearly_budget_comparisons(yearly_budget_id, period_year, period_month, assistant_type_id)
            WHERE assistant_type_id IS NOT NULL;

        CREATE UNIQUE INDEX idx_yearly_budget_comparisons_budget_unmapped
            ON assist_schema.yearly_budget_comparisons(yearly_budget_id, period_year, period_month)
            WHERE assistant_type_id IS NULL;

        RAISE NOTICE 'Table assist_schema.yearly_budget_comparisons created';
    ELSE
        RAISE NOTICE 'Table assist_schema.yearly_budget_comparisons already exists';
    END IF;
END $$;
