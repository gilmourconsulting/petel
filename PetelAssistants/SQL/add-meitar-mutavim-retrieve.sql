-- =============================================================================
-- PetelAssistants — Meitar MUTAVIM retrieve
-- Stores period-scoped MUTAVIM rows pulled from PetelMeitar for the current
-- authority, plus security actions for the retrieve context buttons.
-- Idempotent — safe to run multiple times.
-- =============================================================================

-- ─── 1. meitar_retrieve_processes ────────────────────────────────────────────
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT FROM pg_tables
        WHERE schemaname = 'assist_schema'
          AND tablename  = 'meitar_retrieve_processes'
    ) THEN
        CREATE TABLE assist_schema.meitar_retrieve_processes (
            id                    SERIAL PRIMARY KEY,
            entity_id             INTEGER       NOT NULL REFERENCES shared_schema.entities(id) ON DELETE CASCADE,
            period_year           INTEGER       NOT NULL,
            period_month          INTEGER       NOT NULL,
            row_count             INTEGER       NULL,
            total_calculated_sum  NUMERIC(14,2) NULL,
            source                VARCHAR(20)   NOT NULL DEFAULT 'meitar',
            created_at            TIMESTAMP     NOT NULL DEFAULT CURRENT_TIMESTAMP,
            user_id               INTEGER       NULL REFERENCES assist_schema.users(id) ON DELETE SET NULL,
            updated_at            TIMESTAMP     NOT NULL DEFAULT CURRENT_TIMESTAMP,
            update_user           INTEGER       NULL REFERENCES assist_schema.users(id) ON DELETE SET NULL,
            CONSTRAINT meitar_retrieve_processes_month_check CHECK (period_month BETWEEN 1 AND 12)
        );

        CREATE INDEX idx_meitar_retrieve_processes_entity_id
            ON assist_schema.meitar_retrieve_processes(entity_id);

        CREATE INDEX idx_meitar_retrieve_processes_period
            ON assist_schema.meitar_retrieve_processes(entity_id, period_year, period_month);

        RAISE NOTICE 'Table assist_schema.meitar_retrieve_processes created';
    ELSE
        RAISE NOTICE 'Table assist_schema.meitar_retrieve_processes already exists';
    END IF;
END $$;

-- ─── 2. meitar_mutavim ───────────────────────────────────────────────────────
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT FROM pg_tables
        WHERE schemaname = 'assist_schema'
          AND tablename  = 'meitar_mutavim'
    ) THEN
        CREATE TABLE assist_schema.meitar_mutavim (
            id                 SERIAL PRIMARY KEY,
            entity_id          INTEGER       NOT NULL REFERENCES shared_schema.entities(id) ON DELETE CASCADE,
            period_year        INTEGER       NOT NULL,
            period_month       INTEGER       NOT NULL,
            beneficiary_code   VARCHAR(50)   NOT NULL,
            calc_date          DATE          NOT NULL,
            topic_code         VARCHAR(50)   NULL,
            topic_description  VARCHAR(500)  NULL,
            calculated_amount  NUMERIC(14,2) NOT NULL DEFAULT 0,
            process_id         INTEGER       NOT NULL REFERENCES assist_schema.meitar_retrieve_processes(id) ON DELETE RESTRICT,
            created_at         TIMESTAMP     NOT NULL DEFAULT CURRENT_TIMESTAMP,
            user_id            INTEGER       NULL REFERENCES assist_schema.users(id) ON DELETE SET NULL,
            updated_at         TIMESTAMP     NOT NULL DEFAULT CURRENT_TIMESTAMP,
            update_user        INTEGER       NULL REFERENCES assist_schema.users(id) ON DELETE SET NULL,
            CONSTRAINT meitar_mutavim_month_check CHECK (period_month BETWEEN 1 AND 12)
        );

        CREATE INDEX idx_meitar_mutavim_entity_id
            ON assist_schema.meitar_mutavim(entity_id);

        CREATE INDEX idx_meitar_mutavim_period
            ON assist_schema.meitar_mutavim(entity_id, period_year, period_month);

        CREATE INDEX idx_meitar_mutavim_process_id
            ON assist_schema.meitar_mutavim(process_id);

        RAISE NOTICE 'Table assist_schema.meitar_mutavim created';
    ELSE
        RAISE NOTICE 'Table assist_schema.meitar_mutavim already exists';
    END IF;
END $$;

-- ─── 3. Security actions ─────────────────────────────────────────────────────
DO $$
DECLARE
    v_button_type_id INTEGER;
    v_action_id      INTEGER;
    v_role_rec       RECORD;
    v_action_name    TEXT;
BEGIN
    SELECT id INTO v_button_type_id
    FROM shared_schema.action_types
    WHERE lower(name) IN ('button', 'onclick_button')
    LIMIT 1;

    IF v_button_type_id IS NULL THEN
        RAISE EXCEPTION 'action_types row "button" not found';
    END IF;

    INSERT INTO shared_schema.actions (name, display_name, reference, description, action_type_id)
    VALUES
        ('maindashboard_meitar_retrieve',  'שליפת נתוני מייתר', 'maindashboard',  'כפתור שליפת נתוני מייתר מדף הבית', v_button_type_id),
        ('yearmanagement_meitar_retrieve', 'שליפת נתוני מייתר', 'yearmanagement', 'כפתור שליפת נתוני מייתר ממסך ניהול שנה', v_button_type_id)
    ON CONFLICT (name) DO NOTHING;

    FOREACH v_action_name IN ARRAY ARRAY['maindashboard_meitar_retrieve', 'yearmanagement_meitar_retrieve']
    LOOP
        SELECT id INTO v_action_id
        FROM shared_schema.actions
        WHERE name = v_action_name;

        IF v_action_id IS NOT NULL THEN
            FOR v_role_rec IN
                SELECT id AS role_id, entity_id
                FROM assist_schema.roles
            LOOP
                INSERT INTO assist_schema.roles_actions (entity_id, role_id, action_id)
                SELECT v_role_rec.entity_id, v_role_rec.role_id, v_action_id
                WHERE NOT EXISTS (
                    SELECT 1 FROM assist_schema.roles_actions
                    WHERE role_id = v_role_rec.role_id AND action_id = v_action_id
                );
            END LOOP;
        END IF;
    END LOOP;

    RAISE NOTICE 'Meitar retrieve security actions seeded';
END $$;
