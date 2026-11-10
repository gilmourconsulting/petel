-- =============================================================================
-- PetelAssistants — MUCARIM ↔ entitlement matching
-- Tables: meitar_entitlement_match_processes, meitar_mucarim_entitlement_matches
-- Security actions for Meitar period recheck and entitlement-scoped match.
-- Idempotent — safe to run multiple times.
-- Run after add-meitar-mucarim-retrieve.sql
-- =============================================================================

-- ─── 1. meitar_entitlement_match_processes ───────────────────────────────────
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT FROM pg_tables
        WHERE schemaname = 'assist_schema'
          AND tablename  = 'meitar_entitlement_match_processes'
    ) THEN
        CREATE TABLE assist_schema.meitar_entitlement_match_processes (
            id                    SERIAL PRIMARY KEY,
            entity_id             INTEGER       NOT NULL REFERENCES shared_schema.entities(id) ON DELETE CASCADE,
            source                VARCHAR(30)   NOT NULL,
            match_trigger         VARCHAR(30)   NOT NULL,
            period_year           INTEGER       NULL,
            period_month          INTEGER       NULL,
            entitlement_id        INTEGER       NULL REFERENCES assist_schema.entitlements(id) ON DELETE SET NULL,
            disabled_count        INTEGER       NOT NULL DEFAULT 0,
            matched_count         INTEGER       NOT NULL DEFAULT 0,
            unmatched_line_count  INTEGER       NOT NULL DEFAULT 0,
            created_at            TIMESTAMP     NOT NULL DEFAULT CURRENT_TIMESTAMP,
            user_id               INTEGER       NULL REFERENCES assist_schema.users(id) ON DELETE SET NULL,
            updated_at            TIMESTAMP     NOT NULL DEFAULT CURRENT_TIMESTAMP,
            update_user           INTEGER       NULL REFERENCES assist_schema.users(id) ON DELETE SET NULL,
            CONSTRAINT meitar_entitlement_match_processes_source_check
                CHECK (source IN ('mucarim')),
            CONSTRAINT meitar_entitlement_match_processes_trigger_check
                CHECK (match_trigger IN ('retrieve', 'period_recheck', 'entitlement')),
            CONSTRAINT meitar_entitlement_match_processes_month_check
                CHECK (period_month IS NULL OR period_month BETWEEN 1 AND 12)
        );

        CREATE INDEX idx_meitar_entitlement_match_processes_entity
            ON assist_schema.meitar_entitlement_match_processes(entity_id);

        CREATE INDEX idx_meitar_entitlement_match_processes_period
            ON assist_schema.meitar_entitlement_match_processes(entity_id, period_year, period_month);

        RAISE NOTICE 'Table assist_schema.meitar_entitlement_match_processes created';
    ELSE
        RAISE NOTICE 'Table assist_schema.meitar_entitlement_match_processes already exists';
    END IF;
END $$;

-- ─── 2. meitar_mucarim_entitlement_matches ───────────────────────────────────
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT FROM pg_tables
        WHERE schemaname = 'assist_schema'
          AND tablename  = 'meitar_mucarim_entitlement_matches'
    ) THEN
        CREATE TABLE assist_schema.meitar_mucarim_entitlement_matches (
            id                  SERIAL PRIMARY KEY,
            entity_id           INTEGER       NOT NULL REFERENCES shared_schema.entities(id) ON DELETE CASCADE,
            meitar_mucarim_id   INTEGER       NOT NULL REFERENCES assist_schema.meitar_mucarim(id) ON DELETE CASCADE,
            entitlement_id      INTEGER       NOT NULL REFERENCES assist_schema.entitlements(id) ON DELETE CASCADE,
            match_process_id    INTEGER       NOT NULL REFERENCES assist_schema.meitar_entitlement_match_processes(id) ON DELETE RESTRICT,
            is_active           BOOLEAN       NOT NULL DEFAULT TRUE,
            disabled_at         TIMESTAMP     NULL,
            disable_user        INTEGER       NULL REFERENCES assist_schema.users(id) ON DELETE SET NULL,
            created_at          TIMESTAMP     NOT NULL DEFAULT CURRENT_TIMESTAMP,
            user_id             INTEGER       NULL REFERENCES assist_schema.users(id) ON DELETE SET NULL,
            updated_at          TIMESTAMP     NOT NULL DEFAULT CURRENT_TIMESTAMP,
            update_user         INTEGER       NULL REFERENCES assist_schema.users(id) ON DELETE SET NULL
        );

        CREATE UNIQUE INDEX uq_meitar_mucarim_entitlement_matches_active
            ON assist_schema.meitar_mucarim_entitlement_matches(meitar_mucarim_id, entitlement_id)
            WHERE is_active = TRUE;

        CREATE INDEX idx_meitar_mucarim_entitlement_matches_entity
            ON assist_schema.meitar_mucarim_entitlement_matches(entity_id);

        CREATE INDEX idx_meitar_mucarim_entitlement_matches_mucarim
            ON assist_schema.meitar_mucarim_entitlement_matches(meitar_mucarim_id);

        CREATE INDEX idx_meitar_mucarim_entitlement_matches_entitlement
            ON assist_schema.meitar_mucarim_entitlement_matches(entitlement_id);

        CREATE INDEX idx_meitar_mucarim_entitlement_matches_process
            ON assist_schema.meitar_mucarim_entitlement_matches(match_process_id);

        CREATE INDEX idx_meitar_mucarim_entitlement_matches_active
            ON assist_schema.meitar_mucarim_entitlement_matches(entity_id, is_active);

        RAISE NOTICE 'Table assist_schema.meitar_mucarim_entitlement_matches created';
    ELSE
        RAISE NOTICE 'Table assist_schema.meitar_mucarim_entitlement_matches already exists';
    END IF;
END $$;

-- ─── 3. Security actions ─────────────────────────────────────────────────────
DO $$
DECLARE
    v_button_type_id INTEGER;
    v_action_id      INTEGER;
    v_role_rec       RECORD;
BEGIN
    SELECT id INTO v_button_type_id
    FROM shared_schema.action_types
    WHERE lower(name) IN ('button', 'onclick_button')
    LIMIT 1;

    IF v_button_type_id IS NULL THEN
        RAISE EXCEPTION 'action_types row "button" not found';
    END IF;

    INSERT INTO shared_schema.actions (name, display_name, reference, description, action_type_id)
    VALUES
        ('meitardata_mucarim_entitlement_recheck',
         'בדיקת התאמות זכאות מוכרים מחדש',
         'meitardata',
         'כפתור בדיקה מחדש של התאמת שורות מוכרים (MUCARIM) לזכאויות לתקופה',
         v_button_type_id),
        ('entitlements_meitar_match_recheck',
         'התאמת מוכרים מיתר לזכאות',
         'entitlements',
         'כפתור הרצת התאמת שורות מוכרים (MUCARIM) לזכאות נבחרת',
         v_button_type_id)
    ON CONFLICT (name) DO NOTHING;

    FOR v_action_id IN
        SELECT id FROM shared_schema.actions
        WHERE name IN (
            'meitardata_mucarim_entitlement_recheck',
            'entitlements_meitar_match_recheck'
        )
    LOOP
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
    END LOOP;

    RAISE NOTICE 'MUCARIM entitlement match security actions seeded';
END $$;
