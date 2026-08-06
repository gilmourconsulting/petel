-- =============================================================================
-- PetelAssistants — Yearly budget security actions
-- Page/button actions for /year/{id}/yearly-budget and year hub nav card.
-- Idempotent — safe to run multiple times.
-- Run after add-yearly-budget.sql
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
        ('yearly_budget_page_action',      'גישה למסך תקציב שנתי',   'yearly_budget',  'גישה לדף תקציב שנתי',                    v_page_action_type_id),
        ('yearly_budget_back',             'חזרה מתקציב שנתי',        'yearly_budget',  'כפתור חזרה לניהול שנה',                   v_button_type_id),
        ('yearly_budget_refresh',          'רענון תקציב שנתי',        'yearly_budget',  'כפתור רענון מסך תקציב שנתי',              v_button_type_id),
        ('yearly_budget_save',             'שמירת תקציב שנתי',        'yearly_budget',  'כפתור שמירת גרסת תקציב פתוחה',            v_button_type_id),
        ('yearly_budget_lock',             'נעילת תקציב שנתי',        'yearly_budget',  'כפתור נעילת גרסת תקציב',                  v_button_type_id),
        ('yearly_budget_new_version',      'גרסה חדשה לתקציב שנתי',   'yearly_budget',  'כפתור יצירת גרסת תקציב חדשה מגרסה נעולה', v_button_type_id),
        ('yearly_budget_delete',           'מחיקת גרסת תקציב שנתי',   'yearly_budget',  'כפתור מחיקה רכה של גרסת תקציב',           v_button_type_id),
        ('yearmanagement_yearly_budget',   'מעבר לתקציב שנתי',        'yearmanagement', 'כפתור מעבר לתקציב שנתי מניהול שנה',       v_button_type_id)
    ON CONFLICT (name) DO NOTHING;

    FOREACH v_action_name IN ARRAY ARRAY[
        'yearly_budget_page_action',
        'yearly_budget_back',
        'yearly_budget_refresh',
        'yearly_budget_save',
        'yearly_budget_lock',
        'yearly_budget_new_version',
        'yearly_budget_delete',
        'yearmanagement_yearly_budget'
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

    RAISE NOTICE 'Yearly budget security actions seeded';
END $$;
