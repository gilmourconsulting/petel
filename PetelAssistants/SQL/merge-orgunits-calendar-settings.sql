-- =============================================================================
-- PetelAssistants — Merge institution calendar settings into OrgUnits screen
-- 1. Seed org_units_save_calendar (bulk save ימים/שעות/יוח״א on OrgUnits)
-- 2. Ensure maindashboard_org_units is granted (dashboard entry restored)
-- 3. Deactivate unused year-hub card action yearmanagement_institution_year_settings
-- Idempotent — safe to run multiple times.
-- =============================================================================

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
    ORDER BY CASE WHEN lower(name) = 'button' THEN 0 ELSE 1 END
    LIMIT 1;

    IF v_button_type_id IS NULL THEN
        RAISE EXCEPTION 'action_types row "button" not found';
    END IF;

    INSERT INTO shared_schema.actions (name, display_name, reference, description, action_type_id)
    VALUES
        ('org_units_save_calendar', 'שמירת ימים ושעות למוסדות', 'org_units',
         'שמירת ימי לימוד, שעות שבועיות ויוח״א ברשימת מוסדות לשנה', v_button_type_id),
        ('maindashboard_org_units', 'מעבר לבתי ספר וגנים', 'maindashboard',
         'כפתור מעבר לניהול מוסדות מדף הבית', v_button_type_id)
    ON CONFLICT (name) DO NOTHING;

    FOREACH v_action_name IN ARRAY ARRAY[
        'org_units_save_calendar',
        'maindashboard_org_units'
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

    -- Hub card removed from UI; deactivate so it no longer appears in role grants lists as active UX
    UPDATE shared_schema.actions
    SET description = COALESCE(description, '') || CASE
            WHEN description LIKE '%[deprecated]%' THEN ''
            ELSE ' [deprecated: calendar fields live on org_units]'
        END
    WHERE name = 'yearmanagement_institution_year_settings'
      AND description NOT LIKE '%[deprecated]%';

    RAISE NOTICE 'OrgUnits calendar merge actions seeded';
END $$;
