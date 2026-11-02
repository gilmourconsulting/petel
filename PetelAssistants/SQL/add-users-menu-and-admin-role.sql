-- =============================================================================
-- PetelAssistants — Users menu + system admin role grants
-- 1. Menu item ניהול משתמשים → /users
-- 2. Ensure page_action users_page_action exists
-- 3. Role "admin" on מערכת פטל (entity type admin) gets every action id 1..1000
-- Idempotent — safe to run multiple times.
-- =============================================================================

-- ─── 1. Menu item ─────────────────────────────────────────────────────────────
INSERT INTO shared_schema.menu_items (name, reference, text, sort_order, is_active)
SELECT 'users', '#users', E'\u05e0\u05d9\u05d4\u05d5\u05dc \u05de\u05e9\u05ea\u05de\u05e9\u05d9\u05dd', 30, true
WHERE NOT EXISTS (SELECT 1 FROM shared_schema.menu_items WHERE name = 'users');

UPDATE shared_schema.menu_items
SET reference  = '#users',
    text       = E'\u05e0\u05d9\u05d4\u05d5\u05dc \u05de\u05e9\u05ea\u05de\u05e9\u05d9\u05dd',
    sort_order = 30,
    is_active  = true
WHERE name = 'users';

-- ─── 2. Ensure users_page_action (SecurePageBase PAGE_ACCESS) ─────────────────
DO $$
DECLARE
    v_page_action_type_id INTEGER;
    v_menu_type_id        INTEGER;
BEGIN
    SELECT id INTO v_page_action_type_id
    FROM shared_schema.action_types
    WHERE lower(name) IN ('page_action', 'page')
    ORDER BY CASE WHEN lower(name) = 'page_action' THEN 0 ELSE 1 END
    LIMIT 1;

    SELECT id INTO v_menu_type_id
    FROM shared_schema.action_types
    WHERE lower(name) IN ('menu_item', 'menuitem')
    LIMIT 1;

    IF v_page_action_type_id IS NOT NULL THEN
        INSERT INTO shared_schema.actions (name, display_name, reference, description, action_type_id)
        VALUES (
            'users_page_action',
            E'\u05d2\u05d9\u05e9\u05d4 \u05dc\u05de\u05e1\u05da \u05e0\u05d9\u05d4\u05d5\u05dc \u05de\u05e9\u05ea\u05de\u05e9\u05d9\u05dd',
            'users',
            'PAGE_ACCESS for /users',
            v_page_action_type_id
        )
        ON CONFLICT (name) DO NOTHING;
    END IF;

    IF v_menu_type_id IS NOT NULL THEN
        INSERT INTO shared_schema.actions (name, display_name, reference, description, action_type_id)
        VALUES (
            'users_menu',
            E'\u05e0\u05d9\u05d4\u05d5\u05dc \u05de\u05e9\u05ea\u05de\u05e9\u05d9\u05dd',
            'users',
            'Menu item for /users',
            v_menu_type_id
        )
        ON CONFLICT (name) DO NOTHING;

        UPDATE shared_schema.menu_items mi
        SET action_id = a.id
        FROM shared_schema.actions a
        WHERE mi.name = 'users'
          AND a.name = 'users_menu'
          AND (mi.action_id IS DISTINCT FROM a.id);
    END IF;
END $$;

-- ─── 3. Placeholder actions for missing ids 1..1000 (FK-safe grants) ──────────
DO $$
DECLARE
    v_button_type_id INTEGER;
BEGIN
    SELECT id INTO v_button_type_id
    FROM shared_schema.action_types
    WHERE lower(name) IN ('button', 'onclick_button')
    LIMIT 1;

    IF v_button_type_id IS NULL THEN
        RAISE EXCEPTION 'action_types button row not found';
    END IF;

    INSERT INTO shared_schema.actions (id, name, display_name, reference, description, action_type_id, is_active)
    SELECT
        s.id,
        '_reserved_' || s.id,
        'Reserved action ' || s.id,
        '_reserved',
        'Placeholder so admin role can be pre-granted action ids 1..1000',
        v_button_type_id,
        false
    FROM generate_series(1, 1000) AS s(id)
    WHERE NOT EXISTS (
        SELECT 1 FROM shared_schema.actions a WHERE a.id = s.id
    );

    PERFORM setval(
        pg_get_serial_sequence('shared_schema.actions', 'id'),
        GREATEST((SELECT COALESCE(MAX(id), 1) FROM shared_schema.actions), 1000),
        true
    );
END $$;

-- ─── 4. Role "admin" on מערכת פטל + full grants 1..1000 ───────────────────────
DO $$
DECLARE
    v_entity_id INTEGER;
    v_role_id   INTEGER;
    v_user_id   INTEGER;
    v_granted   INTEGER;
BEGIN
    SELECT e.id INTO v_entity_id
    FROM shared_schema.entities e
    JOIN shared_schema.entity_types et ON et.id = e.entity_type_id
    WHERE e.name = E'\u05de\u05e2\u05e8\u05db\u05ea \u05e4\u05d8\u05dc'
       OR lower(et.name) = 'admin'
    ORDER BY CASE WHEN e.name = E'\u05de\u05e2\u05e8\u05db\u05ea \u05e4\u05d8\u05dc' THEN 0 ELSE 1 END
    LIMIT 1;

    IF v_entity_id IS NULL THEN
        RAISE EXCEPTION 'Admin entity מערכת פטל not found';
    END IF;

    -- Prefer renaming existing system-manager role; otherwise create "admin"
    SELECT id INTO v_role_id
    FROM assist_schema.roles
    WHERE entity_id = v_entity_id
      AND lower(name) = 'admin'
    LIMIT 1;

    IF v_role_id IS NULL THEN
        SELECT id INTO v_role_id
        FROM assist_schema.roles
        WHERE entity_id = v_entity_id
          AND name = E'\u05de\u05e0\u05d4\u05dc \u05de\u05e2\u05e8\u05db\u05ea'
        LIMIT 1;

        IF v_role_id IS NOT NULL THEN
            UPDATE assist_schema.roles
            SET name = 'admin',
                description = 'System admin — all actions'
            WHERE id = v_role_id;
        ELSE
            INSERT INTO assist_schema.roles (entity_id, name, description)
            VALUES (v_entity_id, 'admin', 'System admin — all actions')
            RETURNING id INTO v_role_id;
        END IF;
    END IF;

    -- Ensure Admin user is on this role
    SELECT id INTO v_user_id
    FROM assist_schema.users
    WHERE entity_id = v_entity_id
      AND lower(username) = 'admin'
    LIMIT 1;

    IF v_user_id IS NOT NULL THEN
        INSERT INTO assist_schema.user_roles (entity_id, user_id, role_id, is_active)
        SELECT v_entity_id, v_user_id, v_role_id, true
        WHERE NOT EXISTS (
            SELECT 1 FROM assist_schema.user_roles
            WHERE user_id = v_user_id AND role_id = v_role_id
        );
    END IF;

    INSERT INTO assist_schema.roles_actions (entity_id, role_id, action_id)
    SELECT v_entity_id, v_role_id, a.id
    FROM shared_schema.actions a
    WHERE a.id BETWEEN 1 AND 1000
      AND NOT EXISTS (
          SELECT 1 FROM assist_schema.roles_actions ra
          WHERE ra.role_id = v_role_id AND ra.action_id = a.id
      );

    SELECT count(*) INTO v_granted
    FROM assist_schema.roles_actions
    WHERE role_id = v_role_id;

    RAISE NOTICE 'admin role id=% on entity=% has % role_actions grants', v_role_id, v_entity_id, v_granted;
END $$;
