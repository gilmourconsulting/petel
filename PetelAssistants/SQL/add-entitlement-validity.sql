-- =============================================================================
-- PetelAssistants — Entitlement validity flags (import invalid, fix later)
-- Adds:
--   assist_schema.entitlements validity + source snapshot columns
--   drops entitlements_institution_required (unmatched institution may be NULL)
--   entitlements_resolve_invalid security action
-- Idempotent — safe to run multiple times.
-- =============================================================================

-- ─── 1. Drop institution-required CHECK (unmatched upload rows store NULL) ────
ALTER TABLE assist_schema.entitlements
    DROP CONSTRAINT IF EXISTS entitlements_institution_required;

-- ─── 2. Validity + source snapshot columns ────────────────────────────────────
ALTER TABLE assist_schema.entitlements
    ADD COLUMN IF NOT EXISTS is_valid BOOLEAN NOT NULL DEFAULT true;

ALTER TABLE assist_schema.entitlements
    ADD COLUMN IF NOT EXISTS invalid_reasons VARCHAR(200) NULL;

ALTER TABLE assist_schema.entitlements
    ADD COLUMN IF NOT EXISTS source_institution_symbol VARCHAR(20) NULL;

ALTER TABLE assist_schema.entitlements
    ADD COLUMN IF NOT EXISTS source_support_code VARCHAR(10) NULL;

ALTER TABLE assist_schema.entitlements
    ADD COLUMN IF NOT EXISTS validity_resolved_at TIMESTAMP NULL;

ALTER TABLE assist_schema.entitlements
    ADD COLUMN IF NOT EXISTS validity_resolved_user INTEGER NULL
        REFERENCES assist_schema.users(id) ON DELETE SET NULL;

ALTER TABLE assist_schema.entitlements
    ADD COLUMN IF NOT EXISTS validity_resolved_reason VARCHAR(500) NULL;

CREATE INDEX IF NOT EXISTS idx_entitlements_is_valid
    ON assist_schema.entitlements(entity_id, hebrew_year_id, is_valid)
    WHERE is_last_version = true;

-- ─── 3. entitlements_resolve_invalid security action ──────────────────────────
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
    VALUES (
        'entitlements_resolve_invalid',
        E'\u05d8\u05d9\u05e4\u05d5\u05dc \u05d1\u05d6\u05db\u05d0\u05d5\u05ea \u05dc\u05d0 \u05ea\u05e7\u05d9\u05e0\u05d4',
        'entitlements',
        E'\u05ea\u05d9\u05e7\u05d5\u05df \u05d0\u05d5 \u05d0\u05d9\u05e9\u05d5\u05e8 \u05d6\u05db\u05d0\u05d5\u05ea \u05e9\u05d9\u05d5\u05d1\u05d0\u05d4 \u05db\u05dc\u05d0 \u05ea\u05e7\u05d9\u05e0\u05d4',
        v_button_type_id
    )
    ON CONFLICT (name) DO NOTHING;

    SELECT id INTO v_action_id
    FROM shared_schema.actions
    WHERE name = 'entitlements_resolve_invalid';

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

    RAISE NOTICE 'entitlements_resolve_invalid security action seeded';
END $$;

RAISE NOTICE 'add-entitlement-validity.sql completed';
