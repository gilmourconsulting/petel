-- =============================================================================
-- Align PetelAssistants roles security actions with PetelATH naming
-- Idempotent — safe to run multiple times
-- =============================================================================

DO $$
DECLARE
    v_button_type_id INTEGER;
    v_action_id      INTEGER;
    v_role_rec       RECORD;
BEGIN
    SELECT id INTO v_button_type_id
    FROM shared_schema.action_types
    WHERE name = 'button'
    LIMIT 1;

    IF v_button_type_id IS NULL THEN
        RAISE EXCEPTION 'action_types row "button" not found';
    END IF;

    -- ── Rename existing actions to ATH naming ────────────────────────────────
    UPDATE shared_schema.actions SET name = 'roles_view'              WHERE name = 'roles_viewdetails';
    UPDATE shared_schema.actions SET name = 'roledetails_addUsers'      WHERE name = 'roledetails_adduser';
    UPDATE shared_schema.actions SET name = 'roledetails_removeUser'    WHERE name = 'roledetails_removeuser';
    UPDATE shared_schema.actions SET name = 'roledetails_addActions'    WHERE name = 'roledetails_addaction';
    UPDATE shared_schema.actions SET name = 'roledetails_removeAction'  WHERE name = 'roledetails_removeaction';

    -- ── Insert missing roles screen actions ────────────────────────────────
    INSERT INTO shared_schema.actions (name, display_name, reference, description, action_type_id)
    VALUES
        ('roles_exportactions',      'ייצוא פעולות',              'roles', 'כפתור ייצוא פעולות',              v_button_type_id),
        ('roles_importactions',      'ייבוא פעולות',              'roles', 'כפתור ייבוא פעולות',              v_button_type_id),
        ('roles_exportroleactions',  'ייצוא תפקידים-פעולות',      'roles', 'כפתור ייצוא תפקידים-פעולות',      v_button_type_id),
        ('roles_importroleactions',  'ייבוא תפקידים-פעולות',      'roles', 'כפתור ייבוא תפקידים-פעולות',      v_button_type_id),
        ('roles_exportcomplete',     'ייצוא מלא',                 'roles', 'כפתור ייצוא מלא',                 v_button_type_id),
        ('roles_importcomplete',     'ייבוא מלא',                 'roles', 'כפתור ייבוא מלא',                 v_button_type_id),
        ('roles_refreshcache',       'רענון מטמון אבטחה',          'roles', 'כפתור רענון מטמון אבטחה',          v_button_type_id)
    ON CONFLICT (name) DO NOTHING;

    -- ── Insert missing roledetails screen actions ──────────────────────────
    INSERT INTO shared_schema.actions (name, display_name, reference, description, action_type_id)
    VALUES
        ('roledetails_refresh',   'רענון נתוני תפקיד',  'roledetails', 'כפתור רענון נתוני תפקיד',  v_button_type_id),
        ('roledetails_editName',  'עריכת שם תפקיד',     'roledetails', 'כפתור עריכת שם תפקיד',     v_button_type_id)
    ON CONFLICT (name) DO NOTHING;

    -- ── Assign new actions to roles that already have roles management ───────
    FOR v_role_rec IN
        SELECT DISTINCT r.id AS role_id, r.entity_id
        FROM assist_schema.roles r
        INNER JOIN assist_schema.roles_actions ra ON ra.role_id = r.id
        INNER JOIN shared_schema.actions a ON a.id = ra.action_id
        WHERE a.name IN ('roles', 'roles_create', 'roles_view', 'roles_viewdetails')
    LOOP
        FOR v_action_id IN
            SELECT id FROM shared_schema.actions
            WHERE name IN (
                'roles_exportactions', 'roles_importactions',
                'roles_exportroleactions', 'roles_importroleactions',
                'roles_exportcomplete', 'roles_importcomplete',
                'roles_refreshcache', 'roles_view',
                'roledetails_refresh', 'roledetails_editName',
                'roledetails_addUsers', 'roledetails_removeUser',
                'roledetails_addActions', 'roledetails_removeAction'
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

    RAISE NOTICE 'Roles actions aligned with PetelATH naming';
END $$;
