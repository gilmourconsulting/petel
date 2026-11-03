-- =============================================================================
-- PetelAssistants — Per-institution school calendar settings per Hebrew year
-- 1. assist_schema.institution_year_settings
-- 2. Security actions for /year/{id}/institution-year-settings + year hub card
-- Idempotent — safe to run multiple times.
-- =============================================================================

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT FROM pg_tables
        WHERE schemaname = 'assist_schema' AND tablename = 'institution_year_settings'
    ) THEN
        CREATE TABLE assist_schema.institution_year_settings (
            id                       SERIAL PRIMARY KEY,
            entity_id                INTEGER NOT NULL,
            institution_id           INTEGER NOT NULL REFERENCES assist_schema.institutions(id) ON DELETE CASCADE,
            hebrew_year_id           INTEGER NOT NULL,
            school_days_per_week     NUMERIC(4,2) NOT NULL DEFAULT 0,
            weekly_hours             NUMERIC(8,2) NOT NULL DEFAULT 0,
            has_extended_school_day  BOOLEAN NOT NULL DEFAULT FALSE,
            created_at               TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
            user_id                  INTEGER NULL REFERENCES assist_schema.users(id) ON DELETE SET NULL,
            updated_at               TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
            update_user              INTEGER NULL REFERENCES assist_schema.users(id) ON DELETE SET NULL,
            CONSTRAINT institution_year_settings_unique
                UNIQUE (entity_id, institution_id, hebrew_year_id),
            CONSTRAINT institution_year_settings_days_check
                CHECK (school_days_per_week >= 0 AND school_days_per_week <= 7),
            CONSTRAINT institution_year_settings_hours_check
                CHECK (weekly_hours >= 0)
        );

        CREATE INDEX idx_institution_year_settings_year
            ON assist_schema.institution_year_settings(entity_id, hebrew_year_id);

        CREATE INDEX idx_institution_year_settings_institution
            ON assist_schema.institution_year_settings(institution_id);

        RAISE NOTICE 'Table assist_schema.institution_year_settings created';
    ELSE
        RAISE NOTICE 'Table assist_schema.institution_year_settings already exists';
    END IF;
END $$;

DO $$
DECLARE
    v_button_type_id      INTEGER;
    v_page_action_type_id INTEGER;
    v_action_id           INTEGER;
    v_role_rec            RECORD;
    v_action_name         TEXT;
BEGIN
    SELECT id INTO v_button_type_id
    FROM shared_schema.action_types
    WHERE lower(name) IN ('button', 'onclick_button')
    LIMIT 1;

    SELECT id INTO v_page_action_type_id
    FROM shared_schema.action_types
    WHERE lower(name) IN ('page_action', 'page')
    LIMIT 1;

    IF v_button_type_id IS NULL THEN
        RAISE EXCEPTION 'action_types row "button" not found';
    END IF;

    IF v_page_action_type_id IS NULL THEN
        RAISE EXCEPTION 'action_types row "page_action" not found';
    END IF;

    INSERT INTO shared_schema.actions (name, display_name, reference, description, action_type_id)
    VALUES
        ('institution_year_settings_page_action', 'גישה להגדרות מוסד לשנה', 'institution_year_settings', 'גישה לדף הגדרות ימי לימוד ושעות למוסד לשנת לימודים', v_page_action_type_id),
        ('institution_year_settings_back',        'חזרה מהגדרות מוסד',       'institution_year_settings', 'כפתור חזרה לניהול שנה', v_button_type_id),
        ('institution_year_settings_refresh',     'רענון הגדרות מוסד',       'institution_year_settings', 'כפתור רענון מסך הגדרות מוסד לשנה', v_button_type_id),
        ('institution_year_settings_save',        'שמירת הגדרות מוסד',       'institution_year_settings', 'כפתור שמירת ימי לימוד ושעות למוסדות', v_button_type_id),
        ('yearmanagement_institution_year_settings', 'מעבר להגדרות מוסד לשנה', 'yearmanagement', 'כפתור מעבר להגדרות מוסד לשנה מניהול שנה', v_button_type_id)
    ON CONFLICT (name) DO NOTHING;

    FOREACH v_action_name IN ARRAY ARRAY[
        'institution_year_settings_page_action',
        'institution_year_settings_back',
        'institution_year_settings_refresh',
        'institution_year_settings_save',
        'yearmanagement_institution_year_settings'
    ]
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

    RAISE NOTICE 'Institution year settings security actions seeded';
END $$;
