-- =============================================================================
-- PetelAssistants — Class assistant budget hours + Year Elements hub
-- 1. shared_schema.class_assistant_budget_hours (year × school_level × classification)
-- 2. Menu item ניהול שנה → /year-elements
-- 3. Security actions for year_elements + yearly_budget_calculate
-- Idempotent — safe to run multiple times.
-- =============================================================================

-- ─── 1. Rate table ────────────────────────────────────────────────────────────
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT FROM pg_tables
        WHERE schemaname = 'shared_schema' AND tablename = 'class_assistant_budget_hours'
    ) THEN
        CREATE TABLE shared_schema.class_assistant_budget_hours (
            id                       SERIAL PRIMARY KEY,
            hebrew_year_id           INTEGER NOT NULL REFERENCES shared_schema.hebrew_years(id) ON DELETE CASCADE,
            school_level             VARCHAR(20) NOT NULL,
            class_classification_id  INTEGER NOT NULL REFERENCES shared_schema.class_classifications(id) ON DELETE RESTRICT,
            ministry_participation_pct NUMERIC(5,2) NOT NULL,
            hours                    NUMERIC(10,2) NOT NULL DEFAULT 0,
            created_at               TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
            user_id                  INTEGER NULL,
            updated_at               TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
            update_user              INTEGER NULL,
            CONSTRAINT class_assistant_budget_hours_school_level_check
                CHECK (school_level IN ('elementary', 'high_school')),
            CONSTRAINT class_assistant_budget_hours_nonneg CHECK (hours >= 0),
            CONSTRAINT class_assistant_budget_hours_participation_check
                CHECK (ministry_participation_pct >= 0 AND ministry_participation_pct <= 100),
            CONSTRAINT class_assistant_budget_hours_unique
                UNIQUE (hebrew_year_id, school_level, class_classification_id)
        );

        CREATE INDEX idx_class_assistant_budget_hours_year
            ON shared_schema.class_assistant_budget_hours(hebrew_year_id);

        RAISE NOTICE 'Table shared_schema.class_assistant_budget_hours created';
    ELSE
        RAISE NOTICE 'Table shared_schema.class_assistant_budget_hours already exists';
    END IF;
END $$;

-- ─── 2. Menu: ניהול שנה (shared year elements hub) ───────────────────────────
INSERT INTO shared_schema.menu_items (name, reference, text, sort_order, is_active)
SELECT 'year_elements', '#year-elements', E'\u05e0\u05d9\u05d4\u05d5\u05dc \u05e9\u05e0\u05d4', 85, true
WHERE NOT EXISTS (SELECT 1 FROM shared_schema.menu_items WHERE name = 'year_elements');

UPDATE shared_schema.menu_items
SET reference = '#year-elements',
    text = E'\u05e0\u05d9\u05d4\u05d5\u05dc \u05e9\u05e0\u05d4',
    sort_order = 85,
    is_active = true
WHERE name = 'year_elements';

-- ─── 3. Security actions ──────────────────────────────────────────────────────
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
        ('year_elements_page_action',       'גישה לניהול שנה',              'year_elements',  'גישה לדף ניהול אלמנטי שנה משותפים',     v_page_action_type_id),
        ('year_elements_back',              'חזרה מניהול שנה',              'year_elements',  'כפתור חזרה לדף ראשי',                   v_button_type_id),
        ('year_elements_refresh',           'רענון ניהול שנה',              'year_elements',  'כפתור רענון הטאב הפעיל',                v_button_type_id),
        ('year_elements_class_hours_save',  'שמירת שעות סייעת כיתתית',      'year_elements',  'שמירת מטריצת שעות תקציב סייעת כיתתית', v_button_type_id),
        ('yearly_budget_calculate',         'חישוב תקציב שנתי',             'yearly_budget',  'כפתור חישוב שעות תקציב מזכאויות',       v_button_type_id)
    ON CONFLICT (name) DO NOTHING;

    FOREACH v_action_name IN ARRAY ARRAY[
        'year_elements_page_action',
        'year_elements_back',
        'year_elements_refresh',
        'year_elements_class_hours_save',
        'yearly_budget_calculate'
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

    RAISE NOTICE 'Class assistant budget hours, year elements menu, and security actions seeded';
END $$;
