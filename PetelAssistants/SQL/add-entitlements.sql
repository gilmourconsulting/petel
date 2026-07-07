-- =============================================================================
-- PetelAssistants — Entitlements (זכאויות)
-- Run after add-entitlements-foundation.sql. Idempotent.
-- =============================================================================

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT FROM pg_tables WHERE schemaname = 'assist_schema' AND tablename = 'entitlements'
    ) THEN
        CREATE TABLE assist_schema.entitlements (
            id                         SERIAL PRIMARY KEY,
            entity_id                  INTEGER NOT NULL REFERENCES shared_schema.entities(id) ON DELETE CASCADE,
            hebrew_year_id             INTEGER NOT NULL REFERENCES shared_schema.hebrew_years(id) ON DELETE RESTRICT,
            assistant_type_id          INTEGER NOT NULL REFERENCES shared_schema.assistant_types(id) ON DELETE RESTRICT,
            entitlement_kind           VARCHAR(20) NOT NULL,
            start_date                 DATE NOT NULL,
            end_date                   DATE NOT NULL,
            hours                      NUMERIC(10,2) NOT NULL,
            hours_unit                 VARCHAR(10) NOT NULL,
            ministry_participation_pct NUMERIC(5,2) NOT NULL,
            school_entity_id           INTEGER NULL REFERENCES shared_schema.entities(id) ON DELETE RESTRICT,
            class_name                 VARCHAR(100) NULL,
            pupil_external_id          VARCHAR(100) NULL,
            is_active                  BOOLEAN NOT NULL DEFAULT true,
            created_at                 TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
            user_id                    INTEGER NULL REFERENCES assist_schema.users(id) ON DELETE SET NULL,
            updated_at                 TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
            update_user                INTEGER NULL REFERENCES assist_schema.users(id) ON DELETE SET NULL,
            CONSTRAINT entitlements_date_check CHECK (end_date >= start_date),
            CONSTRAINT entitlements_hours_unit_check CHECK (hours_unit IN ('weekly', 'monthly')),
            CONSTRAINT entitlements_kind_check CHECK (entitlement_kind IN ('institutional', 'personal')),
            CONSTRAINT entitlements_ministry_pct_check CHECK (
                ministry_participation_pct >= 0 AND ministry_participation_pct <= 100
            ),
            CONSTRAINT entitlements_hours_positive CHECK (hours > 0),
            CONSTRAINT entitlements_institutional_check CHECK (
                entitlement_kind <> 'institutional'
                OR (school_entity_id IS NOT NULL AND pupil_external_id IS NULL)
            ),
            CONSTRAINT entitlements_personal_check CHECK (
                entitlement_kind <> 'personal'
                OR (pupil_external_id IS NOT NULL AND school_entity_id IS NULL)
            )
        );

        CREATE INDEX idx_entitlements_entity_year_kind
            ON assist_schema.entitlements(entity_id, hebrew_year_id, entitlement_kind);

        CREATE INDEX idx_entitlements_school_entity
            ON assist_schema.entitlements(school_entity_id)
            WHERE school_entity_id IS NOT NULL;

        RAISE NOTICE 'Table entitlements created';
    ELSE
        RAISE NOTICE 'Table entitlements already exists';
    END IF;
END $$;
