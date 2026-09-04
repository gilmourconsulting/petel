-- =============================================================================
-- PetelAssistants — Gregorian year (calendar year) hub security actions
-- Page/button actions for /gregorian/{year} and child read-only screens.
-- Idempotent — safe to run multiple times.
-- =============================================================================

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
        ('gregorian_page_action',            'גישה לניהול שנה לועזית',     'gregorian',               'גישה לדף ניהול שנה לועזית',                    v_page_action_type_id),
        ('gregorian_back',                   'חזרה מניהול שנה לועזית',       'gregorian',               'כפתור חזרה לדף הבית',                         v_button_type_id),
        ('gregorian_assistants',             'מעבר לסייעות שנה לועזית',      'gregorian',               'כרטיס מעבר לסייעות בשנה לועזית',              v_button_type_id),
        ('gregorian_entitlements',           'מעבר לזכאויות שנה לועזית',     'gregorian',               'כרטיס מעבר לזכאויות בשנה לועזית',             v_button_type_id),
        ('gregorian_budget',                 'מעבר לתקציב שנה לועזית',       'gregorian',               'כרטיס מעבר לתקציב שנה לועזית',                v_button_type_id),
        ('gregorian_salaries_view',          'מעבר לשכר שנה לועזית',         'gregorian',               'כרטיס מעבר לנתוני שכר בשנה לועזית',           v_button_type_id),
        ('gregorian_meitar_view',            'מעבר למיתר שנה לועזית',        'gregorian',               'כרטיס מעבר לנתוני מיתר בשנה לועזית',          v_button_type_id),
        ('gregorian_salary_month_summary',   'סיכום שכר מול תקציב לועזי',    'gregorian',               'כפתור סיכום שכר מול תקציב משנה לועזית',       v_button_type_id),
        ('gregorian_salary_anomalies',       'חריגות שכר שנה לועזית',        'gregorian',               'כפתור חריגות שכר משנה לועזית',                v_button_type_id),
        ('gregorian_meitar_month_summary',   'סיכום מיתר מול תקציב לועזי',   'gregorian',               'כפתור סיכום מיתר מול תקציב משנה לועזית',     v_button_type_id),
        ('gregorian_budget_page_action',     'גישה לתקציב שנה לועזית',       'gregorian_budget',        'גישה לדף תקציב שנה לועזית',                   v_page_action_type_id),
        ('gregorian_budget_back',            'חזרה מתקציב שנה לועזית',       'gregorian_budget',        'כפתור חזרה לניהול שנה לועזית',                v_button_type_id),
        ('gregorian_budget_refresh',         'רענון תקציב שנה לועזית',       'gregorian_budget',        'כפתור רענון תקציב שנה לועזית',                v_button_type_id),
        ('gregorian_entitlements_page_action','גישה לזכאויות שנה לועזית',    'gregorian_entitlements',  'גישה לדף זכאויות שנה לועזית',                 v_page_action_type_id),
        ('gregorian_entitlements_back',      'חזרה מזכאויות שנה לועזית',     'gregorian_entitlements',  'כפתור חזרה לניהול שנה לועזית',                v_button_type_id),
        ('gregorian_entitlements_refresh',   'רענון זכאויות שנה לועזית',     'gregorian_entitlements',  'כפתור רענון זכאויות שנה לועזית',              v_button_type_id),
        ('gregorian_assistants_page_action', 'גישה לסייעות שנה לועזית',      'gregorian_assistants',    'גישה לדף סייעות שנה לועזית',                  v_page_action_type_id),
        ('gregorian_assistants_back',        'חזרה מסייעות שנה לועזית',      'gregorian_assistants',    'כפתור חזרה לניהול שנה לועזית',                v_button_type_id),
        ('gregorian_assistants_refresh',     'רענון סייעות שנה לועזית',      'gregorian_assistants',    'כפתור רענון סייעות שנה לועזית',               v_button_type_id)
    ON CONFLICT (name) DO NOTHING;

    FOREACH v_action_name IN ARRAY ARRAY[
        'gregorian_page_action',
        'gregorian_back',
        'gregorian_assistants',
        'gregorian_entitlements',
        'gregorian_budget',
        'gregorian_salaries_view',
        'gregorian_meitar_view',
        'gregorian_salary_month_summary',
        'gregorian_salary_anomalies',
        'gregorian_meitar_month_summary',
        'gregorian_budget_page_action',
        'gregorian_budget_back',
        'gregorian_budget_refresh',
        'gregorian_entitlements_page_action',
        'gregorian_entitlements_back',
        'gregorian_entitlements_refresh',
        'gregorian_assistants_page_action',
        'gregorian_assistants_back',
        'gregorian_assistants_refresh'
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

    RAISE NOTICE 'Gregorian year security actions seeded';
END $$;
