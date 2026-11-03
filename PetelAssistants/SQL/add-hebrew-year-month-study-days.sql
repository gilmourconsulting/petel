-- =============================================================================
-- PetelAssistants — Shared study days per Gregorian month for a Hebrew year
-- 1. shared_schema.hebrew_year_month_study_days
-- 2. Security action year_elements_study_days_save
-- Idempotent — safe to run multiple times.
-- =============================================================================

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT FROM pg_tables
        WHERE schemaname = 'shared_schema' AND tablename = 'hebrew_year_month_study_days'
    ) THEN
        CREATE TABLE shared_schema.hebrew_year_month_study_days (
            id              SERIAL PRIMARY KEY,
            hebrew_year_id  INTEGER NOT NULL REFERENCES shared_schema.hebrew_years(id) ON DELETE CASCADE,
            period_year     INTEGER NOT NULL,
            period_month    INTEGER NOT NULL,
            study_days      NUMERIC(5,2) NOT NULL DEFAULT 0,
            created_at      TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
            user_id         INTEGER NULL,
            updated_at      TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
            update_user     INTEGER NULL,
            CONSTRAINT hebrew_year_month_study_days_month_check
                CHECK (period_month >= 1 AND period_month <= 12),
            CONSTRAINT hebrew_year_month_study_days_nonneg
                CHECK (study_days >= 0 AND study_days <= 31),
            CONSTRAINT hebrew_year_month_study_days_unique
                UNIQUE (hebrew_year_id, period_year, period_month)
        );

        CREATE INDEX idx_hebrew_year_month_study_days_year
            ON shared_schema.hebrew_year_month_study_days(hebrew_year_id);

        RAISE NOTICE 'Table shared_schema.hebrew_year_month_study_days created';
    ELSE
        RAISE NOTICE 'Table shared_schema.hebrew_year_month_study_days already exists';
    END IF;
END $$;

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
        'year_elements_study_days_save',
        E'\u05e9\u05de\u05d9\u05e8\u05ea \u05d9\u05de\u05d9 \u05dc\u05d9\u05de\u05d5\u05d3 \u05d1\u05d7\u05d5\u05d3\u05e9',
        'year_elements',
        E'\u05e9\u05de\u05d9\u05e8\u05ea \u05de\u05e1\u05e4\u05e8 \u05d9\u05de\u05d9 \u05dc\u05d9\u05de\u05d5\u05d3 \u05dc\u05db\u05dc \u05d7\u05d5\u05d3\u05e9 \u05d1\u05e9\u05e0\u05ea \u05d4\u05dc\u05d9\u05de\u05d5\u05d3\u05d9\u05dd',
        v_button_type_id
    )
    ON CONFLICT (name) DO NOTHING;

    SELECT id INTO v_action_id
    FROM shared_schema.actions
    WHERE name = 'year_elements_study_days_save';

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

    RAISE NOTICE 'Hebrew year month study days table and year_elements_study_days_save action seeded';
END $$;
