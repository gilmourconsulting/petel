-- =============================================================================
-- PetelAssistants — Meitar data view screen
-- 1. Adds the remaining Meitar MUTAVIM fields to assist_schema.meitar_mutavim
--    (effective_date, unit_count, cost, participation_percent,
--     previous_calculated_amount, calculated_difference).
-- 2. Creates page/button actions for /meitar-data and navigation from
--    dashboard / year hub.
-- Idempotent — safe to run multiple times.
-- Run after add-meitar-mutavim-retrieve.sql
-- =============================================================================

ALTER TABLE assist_schema.meitar_mutavim
    ADD COLUMN IF NOT EXISTS effective_date date,
    ADD COLUMN IF NOT EXISTS unit_count NUMERIC(14,4),
    ADD COLUMN IF NOT EXISTS cost NUMERIC(14,2),
    ADD COLUMN IF NOT EXISTS participation_percent NUMERIC(9,4),
    ADD COLUMN IF NOT EXISTS previous_calculated_amount NUMERIC(14,2),
    ADD COLUMN IF NOT EXISTS calculated_difference NUMERIC(14,2);

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
        ('meitardata_page_action',        'גישה למסך נתוני מיתר',   'meitardata',     'גישה לדף צפייה בנתוני מיתר',               v_page_action_type_id),
        ('meitardata_back',               'חזרה ממסך נתוני מיתר',   'meitardata',     'כפתור חזרה ממסך נתוני מיתר',              v_button_type_id),
        ('meitardata_refresh',            'רענון נתוני מיתר',       'meitardata',     'כפתור רענון רשימת נתוני מיתר',            v_button_type_id),
        ('maindashboard_meitar_view',     'צפייה בנתוני מיתר',      'maindashboard',  'כפתור מעבר לצפייה בנתוני מיתר מדף הבית',  v_button_type_id),
        ('yearmanagement_meitar_view',    'צפייה בנתוני מיתר',      'yearmanagement', 'כפתור מעבר לצפייה בנתוני מיתר מניהול שנה', v_button_type_id)
    ON CONFLICT (name) DO NOTHING;

    FOREACH v_action_name IN ARRAY ARRAY[
        'meitardata_page_action',
        'meitardata_back',
        'meitardata_refresh',
        'maindashboard_meitar_view',
        'yearmanagement_meitar_view'
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

    RAISE NOTICE 'Meitar data view security actions seeded';
END $$;
