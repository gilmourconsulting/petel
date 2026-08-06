-- =============================================================================
-- PetelAssistants — Yearly budget (תקציב שנתי)
-- Tenant-scoped versioned budget per Hebrew year with year + month detail lines.
-- Idempotent — safe to run multiple times.
-- =============================================================================

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT FROM pg_tables
        WHERE schemaname = 'assist_schema' AND tablename = 'yearly_budgets'
    ) THEN
        CREATE TABLE assist_schema.yearly_budgets (
            id                       SERIAL PRIMARY KEY,
            entity_id                INTEGER NOT NULL REFERENCES shared_schema.entities(id) ON DELETE CASCADE,
            hebrew_year_id           INTEGER NOT NULL REFERENCES shared_schema.hebrew_years(id) ON DELETE RESTRICT,
            master_yearly_budget_id  INTEGER NOT NULL DEFAULT 0,
            version                  INTEGER NOT NULL DEFAULT 0,
            is_last_version          BOOLEAN NOT NULL DEFAULT true,
            status                   VARCHAR(20) NOT NULL DEFAULT 'open',
            created_at               TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
            user_id                  INTEGER NULL REFERENCES assist_schema.users(id) ON DELETE SET NULL,
            updated_at               TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
            update_user              INTEGER NULL REFERENCES assist_schema.users(id) ON DELETE SET NULL,
            CONSTRAINT yearly_budgets_status_check CHECK (status IN ('open', 'locked', 'deleted')),
            CONSTRAINT yearly_budgets_version_nonneg CHECK (version >= 0)
        );

        CREATE INDEX idx_yearly_budgets_entity_year
            ON assist_schema.yearly_budgets(entity_id, hebrew_year_id);

        CREATE INDEX idx_yearly_budgets_master
            ON assist_schema.yearly_budgets(master_yearly_budget_id);

        CREATE UNIQUE INDEX uq_yearly_budgets_entity_year_last
            ON assist_schema.yearly_budgets(entity_id, hebrew_year_id)
            WHERE is_last_version = true;

        RAISE NOTICE 'Table yearly_budgets created';
    ELSE
        RAISE NOTICE 'Table yearly_budgets already exists';
    END IF;
END $$;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT FROM pg_tables
        WHERE schemaname = 'assist_schema' AND tablename = 'yearly_budget_details'
    ) THEN
        CREATE TABLE assist_schema.yearly_budget_details (
            id                 SERIAL PRIMARY KEY,
            entity_id          INTEGER NOT NULL REFERENCES shared_schema.entities(id) ON DELETE CASCADE,
            yearly_budget_id   INTEGER NOT NULL REFERENCES assist_schema.yearly_budgets(id) ON DELETE CASCADE,
            assistant_type_id  INTEGER NOT NULL REFERENCES shared_schema.assistant_types(id) ON DELETE RESTRICT,
            fte                NUMERIC(10,2) NOT NULL DEFAULT 0,
            hours              NUMERIC(10,2) NOT NULL DEFAULT 0,
            amount             NUMERIC(14,2) NOT NULL DEFAULT 0,
            remarks            TEXT NULL,
            created_at         TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
            user_id            INTEGER NULL REFERENCES assist_schema.users(id) ON DELETE SET NULL,
            updated_at         TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
            update_user        INTEGER NULL REFERENCES assist_schema.users(id) ON DELETE SET NULL,
            CONSTRAINT yearly_budget_details_unique UNIQUE (yearly_budget_id, assistant_type_id),
            CONSTRAINT yearly_budget_details_nonneg CHECK (fte >= 0 AND hours >= 0 AND amount >= 0)
        );

        CREATE INDEX idx_yearly_budget_details_budget
            ON assist_schema.yearly_budget_details(yearly_budget_id);

        RAISE NOTICE 'Table yearly_budget_details created';
    ELSE
        RAISE NOTICE 'Table yearly_budget_details already exists';
    END IF;
END $$;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT FROM pg_tables
        WHERE schemaname = 'assist_schema' AND tablename = 'yearly_budget_month_details'
    ) THEN
        CREATE TABLE assist_schema.yearly_budget_month_details (
            id                 SERIAL PRIMARY KEY,
            entity_id          INTEGER NOT NULL REFERENCES shared_schema.entities(id) ON DELETE CASCADE,
            yearly_budget_id   INTEGER NOT NULL REFERENCES assist_schema.yearly_budgets(id) ON DELETE CASCADE,
            assistant_type_id  INTEGER NOT NULL REFERENCES shared_schema.assistant_types(id) ON DELETE RESTRICT,
            period_year        INTEGER NOT NULL,
            period_month       INTEGER NOT NULL,
            fte                NUMERIC(10,2) NOT NULL DEFAULT 0,
            hours              NUMERIC(10,2) NOT NULL DEFAULT 0,
            amount             NUMERIC(14,2) NOT NULL DEFAULT 0,
            remarks            TEXT NULL,
            created_at         TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
            user_id            INTEGER NULL REFERENCES assist_schema.users(id) ON DELETE SET NULL,
            updated_at         TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
            update_user        INTEGER NULL REFERENCES assist_schema.users(id) ON DELETE SET NULL,
            CONSTRAINT yearly_budget_month_details_unique
                UNIQUE (yearly_budget_id, assistant_type_id, period_year, period_month),
            CONSTRAINT yearly_budget_month_details_month_check
                CHECK (period_month >= 1 AND period_month <= 12),
            CONSTRAINT yearly_budget_month_details_nonneg CHECK (fte >= 0 AND hours >= 0 AND amount >= 0)
        );

        CREATE INDEX idx_yearly_budget_month_details_budget
            ON assist_schema.yearly_budget_month_details(yearly_budget_id);

        CREATE INDEX idx_yearly_budget_month_details_period
            ON assist_schema.yearly_budget_month_details(yearly_budget_id, period_year, period_month);

        RAISE NOTICE 'Table yearly_budget_month_details created';
    ELSE
        RAISE NOTICE 'Table yearly_budget_month_details already exists';
    END IF;
END $$;

-- Align version numbering: first version is 0 (idempotent for already-created tables)
DO $$
BEGIN
    IF EXISTS (
        SELECT FROM pg_tables
        WHERE schemaname = 'assist_schema' AND tablename = 'yearly_budgets'
    ) THEN
        ALTER TABLE assist_schema.yearly_budgets
            DROP CONSTRAINT IF EXISTS yearly_budgets_version_positive;

        IF NOT EXISTS (
            SELECT 1 FROM pg_constraint
            WHERE conname = 'yearly_budgets_version_nonneg'
              AND conrelid = 'assist_schema.yearly_budgets'::regclass
        ) THEN
            ALTER TABLE assist_schema.yearly_budgets
                ADD CONSTRAINT yearly_budgets_version_nonneg CHECK (version >= 0);
        END IF;

        ALTER TABLE assist_schema.yearly_budgets
            ALTER COLUMN version SET DEFAULT 0;

        RAISE NOTICE 'yearly_budgets version constraint/default aligned to allow 0';
    END IF;
END $$;

-- Shift version series that started at 1 down by 1 so the first version is 0.
-- Safe to re-run: only touches masters whose minimum version is currently 1.
DO $$
DECLARE
    v_updated INTEGER;
BEGIN
    IF EXISTS (
        SELECT FROM pg_tables
        WHERE schemaname = 'assist_schema' AND tablename = 'yearly_budgets'
    ) THEN
        UPDATE assist_schema.yearly_budgets yb
        SET version = yb.version - 1
        WHERE EXISTS (
            SELECT 1
            FROM assist_schema.yearly_budgets m
            WHERE m.master_yearly_budget_id = yb.master_yearly_budget_id
            GROUP BY m.master_yearly_budget_id
            HAVING MIN(m.version) = 1
        );

        GET DIAGNOSTICS v_updated = ROW_COUNT;
        RAISE NOTICE 'yearly_budgets versions renumbered (started-at-1 → start-at-0): % row(s)', v_updated;
    END IF;
END $$;
