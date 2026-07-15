-- =============================================================================
-- PetelAssistants — Entitlement allocations table and security actions
-- Creates assist_schema.entitlement_allocations.
-- Seeds button actions for add/deactivate allocation.
-- Idempotent — safe to run multiple times.
-- =============================================================================

-- ─── 1. entitlement_allocations table ────────────────────────────────────────
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT FROM pg_tables
        WHERE schemaname = 'assist_schema'
          AND tablename  = 'entitlement_allocations'
    ) THEN
        CREATE TABLE assist_schema.entitlement_allocations (
            id              SERIAL PRIMARY KEY,
            entity_id       INTEGER      NOT NULL REFERENCES shared_schema.entities(id) ON DELETE CASCADE,
            entitlement_id  INTEGER      NOT NULL REFERENCES assist_schema.entitlements(id) ON DELETE CASCADE,
            person_id       INTEGER      NOT NULL REFERENCES assist_schema.persons(id) ON DELETE CASCADE,
            start_date      DATE         NOT NULL,
            end_date        DATE         NOT NULL,
            hours           NUMERIC(8,2) NOT NULL,
            hours_unit      VARCHAR(10)  NOT NULL DEFAULT 'weekly',
            is_active       BOOLEAN      NOT NULL DEFAULT true,
            created_at      TIMESTAMP    NOT NULL DEFAULT CURRENT_TIMESTAMP,
            user_id         INTEGER      NULL REFERENCES assist_schema.users(id) ON DELETE SET NULL,
            updated_at      TIMESTAMP    NOT NULL DEFAULT CURRENT_TIMESTAMP,
            update_user     INTEGER      NULL REFERENCES assist_schema.users(id) ON DELETE SET NULL,
            CONSTRAINT entitlement_allocations_hours_check   CHECK (hours > 0),
            CONSTRAINT entitlement_allocations_dates_check   CHECK (start_date <= end_date),
            CONSTRAINT entitlement_allocations_unit_check    CHECK (hours_unit IN ('weekly', 'monthly'))
        );

        CREATE INDEX idx_entitlement_allocations_entity_id
            ON assist_schema.entitlement_allocations(entity_id);

        CREATE INDEX idx_entitlement_allocations_entitlement_id
            ON assist_schema.entitlement_allocations(entitlement_id);

        CREATE INDEX idx_entitlement_allocations_person_id
            ON assist_schema.entitlement_allocations(person_id);

        RAISE NOTICE 'Table assist_schema.entitlement_allocations created';
    ELSE
        RAISE NOTICE 'Table assist_schema.entitlement_allocations already exists';
    END IF;
END $$;

-- ─── 2. Security actions ──────────────────────────────────────────────────────
DO $$
DECLARE
    v_button_type_id INTEGER;
    v_action_id      INTEGER;
    v_role_rec       RECORD;
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
        ('entitlements_add_allocation',
         'הוספת הקצאה',
         'entitlements',
         'הוספת הקצאת סייעת לזכאות',
         v_button_type_id),
        ('entitlements_deactivate_allocation',
         'השבתת הקצאה',
         'entitlements',
         'השבתת הקצאת סייעת קיימת',
         v_button_type_id)
    ON CONFLICT (name) DO NOTHING;

    -- Assign to all existing roles
    FOR v_role_rec IN
        SELECT id AS role_id, entity_id
        FROM assist_schema.roles
    LOOP
        FOR v_action_id IN
            SELECT id FROM shared_schema.actions
            WHERE name IN (
                'entitlements_add_allocation',
                'entitlements_deactivate_allocation'
            )
        LOOP
            INSERT INTO assist_schema.roles_actions (entity_id, role_id, action_id)
            SELECT v_role_rec.entity_id, v_role_rec.role_id, v_action_id
            WHERE NOT EXISTS (
                SELECT 1 FROM assist_schema.roles_actions
                WHERE role_id  = v_role_rec.role_id
                  AND action_id = v_action_id
            );
        END LOOP;
    END LOOP;

    RAISE NOTICE 'Entitlement allocation security actions seeded';
END $$;

RAISE NOTICE 'add-entitlement-allocations.sql completed';
