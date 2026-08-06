-- =============================================================================
-- PetelAssistants — System Data Hub (הגדרות מערכת)
-- 1. assistant_types: position_type, position_hours
-- 2. shared_schema.meitar_topics (future use)
-- 3. Replace systemattributes menu with system_data (הגדרות מערכת)
-- 4. Security actions for /system-data
-- Idempotent — safe to run multiple times.
-- =============================================================================

-- ─── 1. assistant_types columns ───────────────────────────────────────────────
ALTER TABLE shared_schema.assistant_types
    ADD COLUMN IF NOT EXISTS position_type VARCHAR(20) NULL;

ALTER TABLE shared_schema.assistant_types
    ADD COLUMN IF NOT EXISTS position_hours NUMERIC(8,2) NULL;

-- ─── 2. meitar_topics ─────────────────────────────────────────────────────────
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT FROM pg_tables
        WHERE schemaname = 'shared_schema' AND tablename = 'meitar_topics'
    ) THEN
        CREATE TABLE shared_schema.meitar_topics (
            id            SERIAL PRIMARY KEY,
            code          VARCHAR(50) NOT NULL UNIQUE,
            name          VARCHAR(150) NOT NULL,
            description   VARCHAR(500) NULL,
            position_type VARCHAR(20) NULL,
            is_active     BOOLEAN NOT NULL DEFAULT true
        );
        RAISE NOTICE 'Table shared_schema.meitar_topics created';
    ELSE
        RAISE NOTICE 'Table shared_schema.meitar_topics already exists';
    END IF;
END $$;

-- ─── 3. Menu: deactivate systemattributes, insert system_data ─────────────────
UPDATE shared_schema.menu_items
SET is_active = false
WHERE name = 'systemattributes' AND is_active = true;

INSERT INTO shared_schema.menu_items (name, reference, text, sort_order, is_active)
SELECT 'system_data', '#system-data', E'\u05d4\u05d2\u05d3\u05e8\u05d5\u05ea \u05de\u05e2\u05e8\u05db\u05ea', 80, true
WHERE NOT EXISTS (SELECT 1 FROM shared_schema.menu_items WHERE name = 'system_data');

UPDATE shared_schema.menu_items
SET reference = '#system-data',
    text = E'\u05d4\u05d2\u05d3\u05e8\u05d5\u05ea \u05de\u05e2\u05e8\u05db\u05ea',
    sort_order = 80,
    is_active = true
WHERE name = 'system_data';

-- ─── 4. Security actions ──────────────────────────────────────────────────────
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
        ('system_data_page_action',              'גישה להגדרות מערכת',           'system_data', 'גישה לדף הגדרות מערכת',                    v_page_action_type_id),
        ('system_data_back',                     'חזרה לדף ראשי',                 'system_data', 'חזרה לדף ראשי',                              v_button_type_id),
        ('system_data_refresh',                  'רענון הגדרות מערכת',            'system_data', 'רענון נתוני הטאב הפעיל',                     v_button_type_id),
        ('system_data_attributes_add',           'הוספת מאפיין מערכת',            'system_data', 'הוספת מאפיין מערכת',                         v_button_type_id),
        ('system_data_attributes_edit',          'עריכת מאפיין מערכת',            'system_data', 'עריכת מאפיין מערכת',                         v_button_type_id),
        ('system_data_attributes_reload',        'טעינת מאפיינים למטמון',         'system_data', 'רענון מטמון מאפייני מערכת מהמסד',            v_button_type_id),
        ('system_data_assistant_types_add',      'הוספת סוג סייעת',               'system_data', 'הוספת סוג סייעת',                            v_button_type_id),
        ('system_data_assistant_types_edit',     'עריכת סוג סייעת',               'system_data', 'עריכת סוג סייעת',                            v_button_type_id),
        ('system_data_entity_types_add',         'הוספת סוג רשות',                'system_data', 'הוספת סוג רשות',                             v_button_type_id),
        ('system_data_entity_types_edit',        'עריכת סוג רשות',                'system_data', 'עריכת סוג רשות',                             v_button_type_id),
        ('system_data_hebrew_years_add',         'הוספת שנת לימודים',             'system_data', 'הוספת שנת לימודים',                          v_button_type_id),
        ('system_data_hebrew_years_edit',        'עריכת שנת לימודים',             'system_data', 'עריכת שנת לימודים',                          v_button_type_id),
        ('system_data_ministry_options_add',     'הוספת אחוז השתתפות',            'system_data', 'הוספת אחוז השתתפות משרד',                    v_button_type_id),
        ('system_data_ministry_options_edit',    'עריכת אחוז השתתפות',            'system_data', 'עריכת אחוז השתתפות משרד',                    v_button_type_id),
        ('system_data_meitar_filters_add',       'הוספת ערך סינון מיתר',          'system_data', 'הוספת ערך סינון מיתר',                       v_button_type_id),
        ('system_data_meitar_filters_edit',      'עריכת ערך סינון מיתר',          'system_data', 'עריכת ערך סינון מיתר',                       v_button_type_id),
        ('system_data_meitar_topics_add',        'הוספת נושא מיתר',               'system_data', 'הוספת נושא מיתר',                            v_button_type_id),
        ('system_data_meitar_topics_edit',       'עריכת נושא מיתר',               'system_data', 'עריכת נושא מיתר',                            v_button_type_id)
    ON CONFLICT (name) DO NOTHING;

    FOREACH v_action_name IN ARRAY ARRAY[
        'system_data_page_action',
        'system_data_back',
        'system_data_refresh',
        'system_data_attributes_add',
        'system_data_attributes_edit',
        'system_data_attributes_reload',
        'system_data_assistant_types_add',
        'system_data_assistant_types_edit',
        'system_data_entity_types_add',
        'system_data_entity_types_edit',
        'system_data_hebrew_years_add',
        'system_data_hebrew_years_edit',
        'system_data_ministry_options_add',
        'system_data_ministry_options_edit',
        'system_data_meitar_filters_add',
        'system_data_meitar_filters_edit',
        'system_data_meitar_topics_add',
        'system_data_meitar_topics_edit'
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

    RAISE NOTICE 'System data hub schema, menu, and security actions seeded';
END $$;
