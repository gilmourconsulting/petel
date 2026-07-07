-- =============================================================================
-- PetelAssistants — Entitlements & org units security actions + menu items
-- Run after add-entitlements.sql. Idempotent.
-- =============================================================================

DO $$
DECLARE
    v_button_type_id      INTEGER;
    v_page_action_type_id INTEGER;
    v_action_id           INTEGER;
    v_role_rec            RECORD;
BEGIN
    INSERT INTO shared_schema.action_types (name, description)
    VALUES ('page_action', 'Page-level access control')
    ON CONFLICT (name) DO NOTHING;

    SELECT id INTO v_button_type_id
    FROM shared_schema.action_types
    WHERE lower(name) IN ('button', 'onclick_button')
    ORDER BY CASE WHEN lower(name) = 'button' THEN 0 ELSE 1 END
    LIMIT 1;

    SELECT id INTO v_page_action_type_id
    FROM shared_schema.action_types
    WHERE lower(name) IN ('page_action', 'page')
    ORDER BY CASE WHEN lower(name) = 'page_action' THEN 0 ELSE 1 END
    LIMIT 1;

    IF v_button_type_id IS NULL THEN
        RAISE EXCEPTION 'action_types row "button" not found';
    END IF;

    IF v_page_action_type_id IS NULL THEN
        RAISE EXCEPTION 'action_types row "page_action" not found';
    END IF;

    -- ── year hub: replace single entitlements nav with two cards ───────────────
    INSERT INTO shared_schema.actions (name, display_name, reference, description, action_type_id)
    VALUES
        ('yearmanagement_institutional_entitlements', 'מעבר לזכאויות מוסדיות', 'yearmanagement', 'כפתור מעבר לזכאויות מוסדיות', v_button_type_id),
        ('yearmanagement_personal_entitlements',      'מעבר לזכאויות אישיות',   'yearmanagement', 'כפתור מעבר לזכאויות אישיות',   v_button_type_id)
    ON CONFLICT (name) DO NOTHING;

    -- ── institutional entitlements ───────────────────────────────────────────
    INSERT INTO shared_schema.actions (name, display_name, reference, description, action_type_id)
    VALUES
        ('institutional_entitlements_page_action', 'גישה לזכאויות מוסדיות',       'institutional_entitlements', 'גישה לדף זכאויות מוסדיות',           v_page_action_type_id),
        ('institutional_entitlements_back',        'חזרה לניהול שנה',             'institutional_entitlements', 'חזרה לניהול שנה',                     v_button_type_id),
        ('institutional_entitlements_refresh',     'רענון זכאויות מוסדיות',       'institutional_entitlements', 'רענון רשימת זכאויות מוסדיות',         v_button_type_id),
        ('institutional_entitlements_add',         'הוספת זכאות מוסדית',          'institutional_entitlements', 'הוספת זכאות מוסדית',                  v_button_type_id),
        ('institutional_entitlements_edit',        'עריכת זכאות מוסדית',          'institutional_entitlements', 'עריכת זכאות מוסדית',                  v_button_type_id),
        ('institutional_entitlements_deactivate',  'השבתת זכאות מוסדית',         'institutional_entitlements', 'השבתת זכאות מוסדית',                  v_button_type_id)
    ON CONFLICT (name) DO NOTHING;

    -- ── personal entitlements ────────────────────────────────────────────────
    INSERT INTO shared_schema.actions (name, display_name, reference, description, action_type_id)
    VALUES
        ('personal_entitlements_page_action', 'גישה לזכאויות אישיות',       'personal_entitlements', 'גישה לדף זכאויות אישיות',           v_page_action_type_id),
        ('personal_entitlements_back',        'חזרה לניהול שנה',             'personal_entitlements', 'חזרה לניהול שנה',                     v_button_type_id),
        ('personal_entitlements_refresh',     'רענון זכאויות אישיות',       'personal_entitlements', 'רענון רשימת זכאויות אישיות',         v_button_type_id),
        ('personal_entitlements_add',         'הוספת זכאות אישית',          'personal_entitlements', 'הוספת זכאות אישית',                  v_button_type_id),
        ('personal_entitlements_edit',        'עריכת זכאות אישית',          'personal_entitlements', 'עריכת זכאות אישיות',                  v_button_type_id),
        ('personal_entitlements_deactivate',  'השבתת זכאות אישית',         'personal_entitlements', 'השבתת זכאות אישית',                  v_button_type_id)
    ON CONFLICT (name) DO NOTHING;

    -- ── org units (schools / kindergartens) ──────────────────────────────────
    INSERT INTO shared_schema.actions (name, display_name, reference, description, action_type_id)
    VALUES
        ('org_units_page_action',  'גישה לניהול מוסדות',     'org_units', 'גישה לדף בתי ספר וגנים',     v_page_action_type_id),
        ('org_units_back',         'חזרה לדף ראשי',          'org_units', 'חזרה לדף ראשי',               v_button_type_id),
        ('org_units_refresh',      'רענון מוסדות',           'org_units', 'רענון רשימת מוסדות',          v_button_type_id),
        ('org_units_add',          'הוספת מוסד',             'org_units', 'הוספת בית ספר או גן',         v_button_type_id),
        ('org_units_edit',         'עריכת מוסד',             'org_units', 'עריכת מוסד',                  v_button_type_id),
        ('org_units_activate',     'הפעלת מוסד',             'org_units', 'הפעלת מוסד',                  v_button_type_id),
        ('org_units_deactivate',   'השבתת מוסד',             'org_units', 'השבתת מוסד',                  v_button_type_id)
    ON CONFLICT (name) DO NOTHING;

    -- ── assistant types (system admin) ───────────────────────────────────────
    INSERT INTO shared_schema.actions (name, display_name, reference, description, action_type_id)
    VALUES
        ('assistant_types_page_action', 'גישה לסוגי סייעות',  'assistant_types', 'גישה לדף סוגי סייעות',  v_page_action_type_id),
        ('assistant_types_back',        'חזרה לדף ראשי',       'assistant_types', 'חזרה לדף ראשי',          v_button_type_id),
        ('assistant_types_refresh',     'רענון סוגי סייעות',   'assistant_types', 'רענון סוגי סייעות',      v_button_type_id),
        ('assistant_types_add',         'הוספת סוג סייעת',     'assistant_types', 'הוספת סוג סייעת',        v_button_type_id),
        ('assistant_types_edit',        'עריכת סוג סייעת',     'assistant_types', 'עריכת סוג סייעת',        v_button_type_id)
    ON CONFLICT (name) DO NOTHING;

    -- ── hebrew years admin ───────────────────────────────────────────────────
    INSERT INTO shared_schema.actions (name, display_name, reference, description, action_type_id)
    VALUES
        ('hebrew_years_page_action', 'גישה לניהול שנות לימודים', 'hebrew_years', 'גישה לדף שנות לימודים', v_page_action_type_id),
        ('hebrew_years_back',        'חזרה לדף ראשי',             'hebrew_years', 'חזרה לדף ראשי',          v_button_type_id),
        ('hebrew_years_refresh',     'רענון שנות לימודים',       'hebrew_years', 'רענון שנות לימודים',     v_button_type_id),
        ('hebrew_years_edit',        'עריכת שנת לימודים',        'hebrew_years', 'עריכת תאריכי שנה',       v_button_type_id)
    ON CONFLICT (name) DO NOTHING;

    -- ── menu items ───────────────────────────────────────────────────────────
    INSERT INTO shared_schema.menu_items (name, reference, text, sort_order, is_active)
    SELECT 'org_units', '#org-units', E'\u05d1\u05ea\u05d9 \u05e1\u05e4\u05e8 \u05d5\u05d2\u05e0\u05d9\u05dd', 20, true
    WHERE NOT EXISTS (SELECT 1 FROM shared_schema.menu_items WHERE name = 'org_units');

    INSERT INTO shared_schema.menu_items (name, reference, text, sort_order, is_active)
    SELECT 'assistant_types', '#assistant-types', E'\u05e1\u05d5\u05d2\u05d9 \u05e1\u05d9\u05d9\u05e2\u05d5\u05ea', 90, true
    WHERE NOT EXISTS (SELECT 1 FROM shared_schema.menu_items WHERE name = 'assistant_types');

    INSERT INTO shared_schema.menu_items (name, reference, text, sort_order, is_active)
    SELECT 'hebrew_years', '#hebrew-years', E'\u05e9\u05e0\u05d5\u05ea \u05dc\u05d9\u05de\u05d5\u05d3\u05d9\u05dd', 91, true
    WHERE NOT EXISTS (SELECT 1 FROM shared_schema.menu_items WHERE name = 'hebrew_years');

    -- ── assign all new actions to existing roles ─────────────────────────────
    FOR v_role_rec IN
        SELECT id AS role_id, entity_id
        FROM assist_schema.roles
    LOOP
        FOR v_action_id IN
            SELECT id FROM shared_schema.actions
            WHERE name IN (
                'yearmanagement_institutional_entitlements', 'yearmanagement_personal_entitlements',
                'institutional_entitlements_page_action', 'institutional_entitlements_back',
                'institutional_entitlements_refresh', 'institutional_entitlements_add',
                'institutional_entitlements_edit', 'institutional_entitlements_deactivate',
                'personal_entitlements_page_action', 'personal_entitlements_back',
                'personal_entitlements_refresh', 'personal_entitlements_add',
                'personal_entitlements_edit', 'personal_entitlements_deactivate',
                'org_units_page_action', 'org_units_back', 'org_units_refresh',
                'org_units_add', 'org_units_edit', 'org_units_activate', 'org_units_deactivate',
                'assistant_types_page_action', 'assistant_types_back', 'assistant_types_refresh',
                'assistant_types_add', 'assistant_types_edit',
                'hebrew_years_page_action', 'hebrew_years_back', 'hebrew_years_refresh', 'hebrew_years_edit'
            )
        LOOP
            INSERT INTO assist_schema.roles_actions (entity_id, role_id, action_id)
            SELECT v_role_rec.entity_id, v_role_rec.role_id, v_action_id
            WHERE NOT EXISTS (
                SELECT 1 FROM assist_schema.roles_actions
                WHERE role_id = v_role_rec.role_id AND action_id = v_action_id
            );
        END LOOP;
    END LOOP;

    RAISE NOTICE 'Entitlements security actions seeded';
END $$;
