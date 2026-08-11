-- =============================================================================
-- PetelAssistants — Shared budget hour monetary value (ערך שעה) per Hebrew year
-- 1. shared_schema.budget_hour_values (one row per hebrew_year_id)
-- 2. Security action year_elements_hour_value_save
-- Idempotent — safe to run multiple times.
-- =============================================================================

-- ─── 1. Hour value table ──────────────────────────────────────────────────────
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT FROM pg_tables
        WHERE schemaname = 'shared_schema' AND tablename = 'budget_hour_values'
    ) THEN
        CREATE TABLE shared_schema.budget_hour_values (
            id              SERIAL PRIMARY KEY,
            hebrew_year_id  INTEGER NOT NULL REFERENCES shared_schema.hebrew_years(id) ON DELETE CASCADE,
            hour_value      NUMERIC(12,4) NOT NULL DEFAULT 0,
            created_at      TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
            user_id         INTEGER NULL,
            updated_at      TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
            update_user     INTEGER NULL,
            CONSTRAINT budget_hour_values_nonneg CHECK (hour_value >= 0),
            CONSTRAINT budget_hour_values_year_unique UNIQUE (hebrew_year_id)
        );

        CREATE INDEX idx_budget_hour_values_year
            ON shared_schema.budget_hour_values(hebrew_year_id);

        RAISE NOTICE 'Table shared_schema.budget_hour_values created';
    ELSE
        RAISE NOTICE 'Table shared_schema.budget_hour_values already exists';
    END IF;
END $$;

-- ─── 2. Security action ───────────────────────────────────────────────────────
DO $$
DECLARE
    v_button_type_id INTEGER;
    v_action_id      INTEGER;
    v_role_rec       RECORD;
BEGIN
    SELECT id INTO v_button_type_id
    FROM shared_schema.action_types
    WHERE lower(name) IN ('button', 'onclick_button')
    LIMIT 1;

    IF v_button_type_id IS NULL THEN
        RAISE EXCEPTION 'action_types row "button" not found';
    END IF;

    INSERT INTO shared_schema.actions (name, display_name, reference, description, action_type_id)
    VALUES (
        'year_elements_hour_value_save',
        E'\u05e9\u05de\u05d9\u05e8\u05ea \u05e2\u05e8\u05da \u05e9\u05e2\u05d4',
        'year_elements',
        E'\u05e9\u05de\u05d9\u05e8\u05ea \u05e2\u05e8\u05da \u05e9\u05e2\u05d4 \u05ea\u05e7\u05e6\u05d9\u05d1\u05d9 \u05dc\u05e9\u05e0\u05ea \u05dc\u05d9\u05de\u05d5\u05d3\u05d9\u05dd',
        v_button_type_id
    )
    ON CONFLICT (name) DO NOTHING;

    SELECT id INTO v_action_id
    FROM shared_schema.actions
    WHERE name = 'year_elements_hour_value_save';

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

    RAISE NOTICE 'Budget hour value table and year_elements_hour_value_save action seeded';
END $$;
