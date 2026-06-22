-- =============================================================================
-- PetelAssistants Database Bootstrap
-- Run once per environment to create both schemas and seed reference data.
-- Idempotent: safe to re-run (uses IF NOT EXISTS / ON CONFLICT DO NOTHING).
-- =============================================================================

-- ─── Create schemas ──────────────────────────────────────────────────────────
CREATE SCHEMA IF NOT EXISTS shared_schema;
CREATE SCHEMA IF NOT EXISTS assist_schema;

-- ─── EF Core migration history tables ────────────────────────────────────────
-- shared_schema uses its own migrations history (if EF migrations are added later).
CREATE TABLE IF NOT EXISTS shared_schema."__EFMigrationsHistory" (
    "MigrationId"    VARCHAR(150) NOT NULL,
    "ProductVersion" VARCHAR(32)  NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

-- assist_schema hosts the primary EF migrations history.
CREATE TABLE IF NOT EXISTS assist_schema."__EFMigrationsHistory" (
    "MigrationId"    VARCHAR(150) NOT NULL,
    "ProductVersion" VARCHAR(32)  NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory_assist" PRIMARY KEY ("MigrationId")
);


-- =============================================================================
-- SHARED SCHEMA — global reference data, no entity_id
-- =============================================================================

-- ─── entity_types ─────────────────────────────────────────────────────────────
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT FROM pg_tables WHERE schemaname = 'shared_schema' AND tablename = 'entity_types'
    ) THEN
        CREATE TABLE shared_schema.entity_types (
            id          SERIAL PRIMARY KEY,
            name        VARCHAR(100) NOT NULL,
            description VARCHAR(200) NULL,
            is_active   BOOLEAN NOT NULL DEFAULT true
        );

        INSERT INTO shared_schema.entity_types (name, description)
        VALUES
            ('local_authority', 'רשות מקומית'),
            ('school',          'בית ספר'),
            ('org_unit',        'יחידה ארגונית')
        ON CONFLICT DO NOTHING;

        RAISE NOTICE 'Table entity_types created and seeded';
    END IF;
END $$;

-- ─── entities (tenant registry + org units) ───────────────────────────────────
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT FROM pg_tables WHERE schemaname = 'shared_schema' AND tablename = 'entities'
    ) THEN
        CREATE TABLE shared_schema.entities (
            id             SERIAL PRIMARY KEY,
            name           VARCHAR(200) NOT NULL,
            entity_type_id INTEGER NULL REFERENCES shared_schema.entity_types(id) ON DELETE SET NULL,
            is_active      BOOLEAN NOT NULL DEFAULT true
        );

        CREATE INDEX idx_entities_entity_type ON shared_schema.entities(entity_type_id);

        RAISE NOTICE 'Table entities created';
    END IF;
END $$;

-- ─── system_attributes (global application config) ───────────────────────────
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT FROM pg_tables WHERE schemaname = 'shared_schema' AND tablename = 'system_attributes'
    ) THEN
        CREATE TABLE shared_schema.system_attributes (
            id          SERIAL PRIMARY KEY,
            name        VARCHAR(100) NOT NULL UNIQUE,
            value       VARCHAR(500) NOT NULL DEFAULT '',
            value_type  VARCHAR(50)  NOT NULL DEFAULT 'string',
            description VARCHAR(200) NULL
        );

        INSERT INTO shared_schema.system_attributes (name, value, value_type, description)
        VALUES
            ('Security_PasswordPolicy',        '^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{6,20}$', 'string', 'מדיניות סיסמה (ביטוי רגולרי)'),
            ('Security_OtpEnabled',            'false',  'bool',    'האם OTP דוא"ל מופעל'),
            ('Security_SessionTimeoutMinutes', '30',     'integer', 'זמן תפוגת סשן (דקות)')
        ON CONFLICT (name) DO NOTHING;

        RAISE NOTICE 'Table system_attributes created and seeded';
    END IF;
END $$;


-- =============================================================================
-- ASSIST SCHEMA — tenant-scoped operational data
-- Every table MUST have entity_id NOT NULL referencing shared_schema.entities.
-- =============================================================================

-- ─── users ────────────────────────────────────────────────────────────────────
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT FROM pg_tables WHERE schemaname = 'assist_schema' AND tablename = 'users'
    ) THEN
        CREATE TABLE assist_schema.users (
            id            SERIAL PRIMARY KEY,

            -- Tenant discriminator — FK to the owning local authority in shared_schema.
            entity_id     INTEGER NOT NULL REFERENCES shared_schema.entities(id) ON DELETE RESTRICT,

            -- Credentials
            username      VARCHAR(50)  NOT NULL,
            password_hash VARCHAR(255) NOT NULL,

            -- Profile
            first_name    VARCHAR(100) NULL,
            last_name     VARCHAR(100) NULL,
            email         VARCHAR(200) NULL,

            -- Status
            last_login    TIMESTAMP    NULL,
            is_active     BOOLEAN NOT NULL DEFAULT true,
            is_locked     BOOLEAN NOT NULL DEFAULT false,

            -- Audit fields (required on all assist_schema tables)
            created_at    TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
            created_user  INTEGER   NULL REFERENCES assist_schema.users(id) ON DELETE SET NULL,
            updated_at    TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
            update_user   INTEGER   NULL REFERENCES assist_schema.users(id) ON DELETE SET NULL,

            -- Username is unique per authority (two authorities may have the same username)
            CONSTRAINT uk_users_entity_username UNIQUE (entity_id, username)
        );

        CREATE INDEX idx_users_entity_id ON assist_schema.users(entity_id);
        CREATE INDEX idx_users_created_user ON assist_schema.users(created_user);
        CREATE INDEX idx_users_update_user  ON assist_schema.users(update_user);

        RAISE NOTICE 'Table users created';
    END IF;
END $$;
