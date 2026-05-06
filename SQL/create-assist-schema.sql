-- ============================================================
-- assistants_schema â€” Base Schema for PetelAssistants
-- ============================================================
-- Covers:
--   1. Security   â€” entity_types, entities, users, roles, user_roles,
--                   action_types, actions, roles_actions, user_lock_reasons,
--                   action_audit_logs, views (vw_role_actions, vw_user_actions)
--   2. System Attributes â€” system_attributes, system_actions
--   3. Menu Items — menu_items
--
-- Idempotent: safe to run multiple times on the same database.
-- Prerequisite: the target database must already exist.
-- ============================================================

BEGIN;

-- ============================================================
-- SCHEMA
-- ============================================================

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_namespace WHERE nspname = 'assistants_schema') THEN
        CREATE SCHEMA assistants_schema;
        RAISE NOTICE 'Schema assistants_schema created';
    ELSE
        RAISE NOTICE 'Schema assistants_schema already exists â€” skipped';
    END IF;
END
$$;

-- ============================================================
-- UTILITY FUNCTION â€” auto-update updated_at timestamp
-- ============================================================

CREATE OR REPLACE FUNCTION assistants_schema.trigger_set_timestamp()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
  NEW.updated_at = NOW();
  RETURN NEW;
END;
$$;

-- ============================================================
-- 1. SECURITY
-- ============================================================

-- ------------------------------------------------------------
-- entity_types  â€” categories of tenant / organisation
-- ------------------------------------------------------------
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT FROM pg_tables
        WHERE schemaname = 'assistants_schema' AND tablename = 'entity_types'
    ) THEN
        CREATE TABLE assistants_schema.entity_types (
            id      SERIAL PRIMARY KEY,
            name    VARCHAR(255) NOT NULL,
            created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
            updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
        );

        CREATE TRIGGER set_timestamp_entity_types
            BEFORE UPDATE ON assistants_schema.entity_types
            FOR EACH ROW EXECUTE FUNCTION assistants_schema.trigger_set_timestamp();

        RAISE NOTICE 'Table entity_types created';
    ELSE
        RAISE NOTICE 'Table entity_types already exists â€” skipped';
    END IF;
END
$$;

-- Seed: default entity type
INSERT INTO assistants_schema.entity_types (id, name)
VALUES (1, 'System')
ON CONFLICT (id) DO NOTHING;

-- ------------------------------------------------------------
-- entities  â€” tenant / organisation records
-- ------------------------------------------------------------
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT FROM pg_tables
        WHERE schemaname = 'assistants_schema' AND tablename = 'entities'
    ) THEN
        CREATE TABLE assistants_schema.entities (
            id              SERIAL PRIMARY KEY,
            entity_type_id  INTEGER NOT NULL
                                REFERENCES assistants_schema.entity_types(id),
            name            VARCHAR(255) NOT NULL,
            address         TEXT,
            phone           VARCHAR(50),
            email           VARCHAR(255),
            is_active       BOOLEAN DEFAULT TRUE,
            entity_logo     BYTEA,
            created_at      TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
            updated_at      TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
            owner           INTEGER  -- self-reference added via ALTER below
        );

        CREATE TRIGGER set_timestamp_entities
            BEFORE UPDATE ON assistants_schema.entities
            FOR EACH ROW EXECUTE FUNCTION assistants_schema.trigger_set_timestamp();

        CREATE INDEX idx_entities_entity_type_id ON assistants_schema.entities(entity_type_id);
        CREATE INDEX idx_entities_is_active      ON assistants_schema.entities(is_active);

        RAISE NOTICE 'Table entities created';
    ELSE
        RAISE NOTICE 'Table entities already exists â€” skipped';
    END IF;
END
$$;

-- Self-reference FK (safe to run multiple times)
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.table_constraints
        WHERE constraint_schema = 'assistants_schema'
          AND table_name        = 'entities'
          AND constraint_name   = 'entities_owner_fkey'
    ) THEN
        ALTER TABLE assistants_schema.entities
            ADD CONSTRAINT entities_owner_fkey
                FOREIGN KEY (owner) REFERENCES assistants_schema.entities(id) NOT VALID;
        RAISE NOTICE 'FK entities_owner_fkey added';
    END IF;
END
$$;

-- Seed: default system entity
INSERT INTO assistants_schema.entities (id, entity_type_id, name)
VALUES (1, 1, 'System')
ON CONFLICT (id) DO NOTHING;

-- ------------------------------------------------------------
-- user_lock_reasons â€” lookup table for account-lock causes
-- ------------------------------------------------------------
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT FROM pg_tables
        WHERE schemaname = 'assistants_schema' AND tablename = 'user_lock_reasons'
    ) THEN
        CREATE TABLE assistants_schema.user_lock_reasons (
            id                    SERIAL PRIMARY KEY,
            code                  VARCHAR(50)  NOT NULL,
            name                  VARCHAR(100) NOT NULL,
            description           VARCHAR(200) NULL,
            allow_forgot_password BOOLEAN NOT NULL DEFAULT TRUE,
            is_active             BOOLEAN NOT NULL DEFAULT TRUE,
            sort_order            INTEGER NOT NULL DEFAULT 0,
            created_at            TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
            CONSTRAINT uk_user_lock_reasons_code UNIQUE (code)
        );

        CREATE INDEX idx_user_lock_reasons_code ON assistants_schema.user_lock_reasons(code);

        -- Seed standard lock reasons
        INSERT INTO assistants_schema.user_lock_reasons
            (code, name, description, allow_forgot_password, sort_order)
        VALUES
            ('LOGIN_ATTEMPTS_EXCEEDED',
             '×—×¨×™×’×” ×‘×ž×¡×¤×¨ × ×™×¡×™×•× ×•×ª ×›× ×™×¡×”',
             '×”×—×©×‘×•×Ÿ × × ×¢×œ ××•×˜×•×ž×˜×™×ª ×œ××—×¨ ×ž×¡×¤×¨ × ×™×¡×™×•× ×•×ª ×›× ×™×¡×” ×›×•×©×œ×™×',
             TRUE, 1),
            ('ADMIN_LOCKED',
             '× ×¢×™×œ×” ×¢×œ ×™×“×™ ×ž× ×”×œ',
             '×”×—×©×‘×•×Ÿ × × ×¢×œ ×™×“× ×™×ª ×¢×œ ×™×“×™ ×ž× ×”×œ ×”×ž×¢×¨×›×ª',
             FALSE, 2)
        ON CONFLICT (code) DO NOTHING;

        RAISE NOTICE 'Table user_lock_reasons created';
    ELSE
        RAISE NOTICE 'Table user_lock_reasons already exists â€” skipped';
    END IF;
END
$$;

-- ------------------------------------------------------------
-- users  â€” application user accounts (full security model)
-- ------------------------------------------------------------
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT FROM pg_tables
        WHERE schemaname = 'assistants_schema' AND tablename = 'users'
    ) THEN
        CREATE TABLE assistants_schema.users (
            id                       SERIAL PRIMARY KEY,
            entity_id                INTEGER NOT NULL
                                         REFERENCES assistants_schema.entities(id),
            username                 VARCHAR(50)  NOT NULL,
            password_hash            VARCHAR(255) NOT NULL,
            email                    VARCHAR(255),
            phone                    VARCHAR(50),
            first_name               VARCHAR(100),
            last_name                VARCHAR(100),
            last_login               TIMESTAMP WITH TIME ZONE,
            is_active                BOOLEAN DEFAULT TRUE,
            update_user              INTEGER,            -- FK added below (self-ref)
            created_at               TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
            updated_at               TIMESTAMP WITH TIME ZONE DEFAULT NOW(),

            -- Legacy TOTP columns (retained for potential rollback; unused by email-OTP flow)
            otp_secret               VARCHAR(255),
            otp_enabled              BOOLEAN DEFAULT FALSE,
            otp_verified             BOOLEAN DEFAULT FALSE,

            -- Account locking
            is_locked                BOOLEAN NOT NULL DEFAULT FALSE,
            locked_at                TIMESTAMP WITH TIME ZONE,
            locked_by                INTEGER,            -- FK added below (self-ref)
            lock_reason_id           INTEGER
                                         REFERENCES assistants_schema.user_lock_reasons(id)
                                         ON DELETE SET NULL,
            failed_password_attempts INTEGER NOT NULL DEFAULT 0,
            failed_otp_attempts      INTEGER NOT NULL DEFAULT 0,
            last_failed_attempt      TIMESTAMP WITH TIME ZONE,

            -- Password expiration
            password_changed_at      TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
            password_change_required BOOLEAN NOT NULL DEFAULT FALSE,

            -- Email OTP (server-sent 6-digit code â€” replaces TOTP)
            email_otp_code           VARCHAR(100),       -- BCrypt hash of pending code
            email_otp_expiry         TIMESTAMP WITH TIME ZONE,
            email_otp_attempts       INTEGER NOT NULL DEFAULT 0,

            CONSTRAINT unique_username_per_entity UNIQUE (username, entity_id)
        );

        CREATE INDEX idx_users_entity_id       ON assistants_schema.users(entity_id);
        CREATE INDEX idx_users_username        ON assistants_schema.users(username);
        CREATE INDEX idx_users_is_active       ON assistants_schema.users(is_active);
        CREATE INDEX idx_users_is_locked       ON assistants_schema.users(is_locked);
        CREATE INDEX idx_users_lock_reason_id  ON assistants_schema.users(lock_reason_id);
        CREATE INDEX idx_users_otp_enabled     ON assistants_schema.users(otp_enabled)
                                               WHERE otp_enabled = TRUE;

        CREATE TRIGGER set_timestamp_users
            BEFORE UPDATE ON assistants_schema.users
            FOR EACH ROW EXECUTE FUNCTION assistants_schema.trigger_set_timestamp();

        RAISE NOTICE 'Table users created';
    ELSE
        RAISE NOTICE 'Table users already exists â€” skipped';
    END IF;
END
$$;

-- Self-reference FKs on users (safe to run multiple times)
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.table_constraints
        WHERE constraint_schema = 'assistants_schema'
          AND table_name        = 'users'
          AND constraint_name   = 'users_update_user_fkey'
    ) THEN
        ALTER TABLE assistants_schema.users
            ADD CONSTRAINT users_update_user_fkey
                FOREIGN KEY (update_user) REFERENCES assistants_schema.users(id)
                ON UPDATE CASCADE ON DELETE SET NULL;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.table_constraints
        WHERE constraint_schema = 'assistants_schema'
          AND table_name        = 'users'
          AND constraint_name   = 'users_locked_by_fkey'
    ) THEN
        ALTER TABLE assistants_schema.users
            ADD CONSTRAINT users_locked_by_fkey
                FOREIGN KEY (locked_by) REFERENCES assistants_schema.users(id)
                ON UPDATE CASCADE ON DELETE SET NULL;
    END IF;
END
$$;

-- ------------------------------------------------------------
-- roles  â€” named permission groups
-- ------------------------------------------------------------
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT FROM pg_tables
        WHERE schemaname = 'assistants_schema' AND tablename = 'roles'
    ) THEN
        CREATE TABLE assistants_schema.roles (
            id          SERIAL PRIMARY KEY,
            name        VARCHAR(50) NOT NULL,
            description TEXT,
            created_at  TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
            updated_at  TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
            update_user INTEGER REFERENCES assistants_schema.users(id)
                                ON UPDATE CASCADE ON DELETE SET NULL
        );

        CREATE TRIGGER set_timestamp_roles
            BEFORE UPDATE ON assistants_schema.roles
            FOR EACH ROW EXECUTE FUNCTION assistants_schema.trigger_set_timestamp();

        RAISE NOTICE 'Table roles created';
    ELSE
        RAISE NOTICE 'Table roles already exists â€” skipped';
    END IF;
END
$$;

-- ------------------------------------------------------------
-- user_roles  â€” many-to-many: users â†” roles
-- ------------------------------------------------------------
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT FROM pg_tables
        WHERE schemaname = 'assistants_schema' AND tablename = 'user_roles'
    ) THEN
        CREATE TABLE assistants_schema.user_roles (
            id          SERIAL PRIMARY KEY,
            user_id     INTEGER NOT NULL REFERENCES assistants_schema.users(id)
                                ON UPDATE CASCADE ON DELETE CASCADE,
            role_id     INTEGER NOT NULL REFERENCES assistants_schema.roles(id)
                                ON UPDATE CASCADE ON DELETE CASCADE,
            is_active   BOOLEAN DEFAULT TRUE,
            created_at  TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
            updated_at  TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
            update_user INTEGER REFERENCES assistants_schema.users(id)
                                ON UPDATE CASCADE ON DELETE SET NULL
        );

        CREATE INDEX idx_user_roles_user_id ON assistants_schema.user_roles(user_id);
        CREATE INDEX idx_user_roles_role_id ON assistants_schema.user_roles(role_id);

        CREATE TRIGGER set_timestamp_user_roles
            BEFORE UPDATE ON assistants_schema.user_roles
            FOR EACH ROW EXECUTE FUNCTION assistants_schema.trigger_set_timestamp();

        RAISE NOTICE 'Table user_roles created';
    ELSE
        RAISE NOTICE 'Table user_roles already exists â€” skipped';
    END IF;
END
$$;

-- ------------------------------------------------------------
-- action_types  â€” categories of security actions
--                 e.g. ONCLICK_BUTTON, MENU_NAVIGATION, API_CALL
-- ------------------------------------------------------------
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT FROM pg_tables
        WHERE schemaname = 'assistants_schema' AND tablename = 'action_types'
    ) THEN
        CREATE TABLE assistants_schema.action_types (
            id          SMALLSERIAL PRIMARY KEY,
            name        VARCHAR(50)  NOT NULL UNIQUE,
            description VARCHAR(255),
            created_at  TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
            updated_at  TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
            user_id     INTEGER REFERENCES assistants_schema.users(id)
                                ON UPDATE CASCADE ON DELETE SET NULL
        );

        -- Seed standard action types (mirrors ATH)
        INSERT INTO assistants_schema.action_types (name, description)
        VALUES
            ('ONCLICK_BUTTON',   'Button click action'),
            ('MENU_NAVIGATION',  'Navigation menu action'),
            ('API_CALL',         'Direct API endpoint call'),
            ('FILE_UPLOAD',      'File upload operation'),
            ('FILE_DOWNLOAD',    'File download operation')
        ON CONFLICT (name) DO NOTHING;

        RAISE NOTICE 'Table action_types created';
    ELSE
        RAISE NOTICE 'Table action_types already exists â€” skipped';
    END IF;
END
$$;

-- ------------------------------------------------------------
-- actions  â€” individual permission nodes
-- ------------------------------------------------------------
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT FROM pg_tables
        WHERE schemaname = 'assistants_schema' AND tablename = 'actions'
    ) THEN
        CREATE TABLE assistants_schema.actions (
            id             SERIAL PRIMARY KEY,
            name           VARCHAR(100) NOT NULL UNIQUE,
            display_name   VARCHAR(150),
            description    VARCHAR(255),
            action_type_id SMALLINT NOT NULL
                               REFERENCES assistants_schema.action_types(id)
                               ON UPDATE CASCADE ON DELETE RESTRICT,
            reference      VARCHAR(200),
            onclick_name   VARCHAR(100),
            sort_order     INTEGER DEFAULT 0,
            is_active      BOOLEAN DEFAULT TRUE,
            created_at     TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
            updated_at     TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
            user_id        INTEGER REFERENCES assistants_schema.users(id)
                                   ON UPDATE CASCADE ON DELETE SET NULL
        );

        CREATE INDEX idx_actions_action_type_id ON assistants_schema.actions(action_type_id);
        CREATE INDEX idx_actions_is_active      ON assistants_schema.actions(is_active);
        CREATE INDEX idx_actions_reference      ON assistants_schema.actions(reference);

        RAISE NOTICE 'Table actions created';
    ELSE
        RAISE NOTICE 'Table actions already exists â€” skipped';
    END IF;
END
$$;

-- ------------------------------------------------------------
-- roles_actions  â€” many-to-many: roles â†” actions
-- ------------------------------------------------------------
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT FROM pg_tables
        WHERE schemaname = 'assistants_schema' AND tablename = 'roles_actions'
    ) THEN
        CREATE TABLE assistants_schema.roles_actions (
            id           SERIAL PRIMARY KEY,
            role_id      INTEGER NOT NULL
                             REFERENCES assistants_schema.roles(id)
                             ON UPDATE CASCADE ON DELETE CASCADE,
            action_id    INTEGER NOT NULL
                             REFERENCES assistants_schema.actions(id)
                             ON UPDATE CASCADE ON DELETE CASCADE,
            action_level INTEGER NOT NULL DEFAULT 0,
            updated_at   TIMESTAMP WITH TIME ZONE,
            update_user  INTEGER REFERENCES assistants_schema.users(id)
                                 ON UPDATE CASCADE ON DELETE SET NULL,
            CONSTRAINT uk_role_action UNIQUE (role_id, action_id)
        );

        CREATE INDEX idx_roles_actions_role_id   ON assistants_schema.roles_actions(role_id);
        CREATE INDEX idx_roles_actions_action_id ON assistants_schema.roles_actions(action_id);

        RAISE NOTICE 'Table roles_actions created';
    ELSE
        RAISE NOTICE 'Table roles_actions already exists â€” skipped';
    END IF;
END
$$;

-- ------------------------------------------------------------
-- action_audit_logs  â€” audit trail for all action auth attempts
-- ------------------------------------------------------------
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT FROM pg_tables
        WHERE schemaname = 'assistants_schema' AND tablename = 'action_audit_logs'
    ) THEN
        CREATE TABLE assistants_schema.action_audit_logs (
            id            BIGSERIAL PRIMARY KEY,
            user_id       INTEGER NOT NULL
                              REFERENCES assistants_schema.users(id)
                              ON DELETE RESTRICT,
            action_name   VARCHAR(200) NOT NULL,
            screen_name   VARCHAR(100) NOT NULL,
            function_name VARCHAR(100) NOT NULL,
            -- ONCLICK_BUTTON | MENU_NAVIGATION | API_CALL | FILE_UPLOAD | etc.
            event_type    VARCHAR(50)  NOT NULL,
            -- GRANTED | DENIED
            result        VARCHAR(20)  NOT NULL,
            "timestamp"   TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
            ip_address    VARCHAR(45),
            action_params VARCHAR(500),
            description   VARCHAR(1000)
        );

        COMMENT ON TABLE  assistants_schema.action_audit_logs
            IS 'Audit log for all action authorization attempts â€“ tracks GRANTED and DENIED access';
        COMMENT ON COLUMN assistants_schema.action_audit_logs.event_type
            IS 'Authorization type: ONCLICK_BUTTON, MENU_NAVIGATION, API_CALL, FILE_UPLOAD, etc.';
        COMMENT ON COLUMN assistants_schema.action_audit_logs.result
            IS 'Authorization result: GRANTED or DENIED';

        CREATE INDEX idx_audit_user_id   ON assistants_schema.action_audit_logs(user_id);
        CREATE INDEX idx_audit_action    ON assistants_schema.action_audit_logs(action_name);
        CREATE INDEX idx_audit_result    ON assistants_schema.action_audit_logs(result);
        CREATE INDEX idx_audit_timestamp ON assistants_schema.action_audit_logs("timestamp");
        CREATE INDEX idx_audit_event     ON assistants_schema.action_audit_logs(event_type);
        CREATE INDEX idx_audit_user_ts   ON assistants_schema.action_audit_logs(user_id, "timestamp" DESC);
        CREATE INDEX idx_audit_user_res  ON assistants_schema.action_audit_logs(user_id, result, "timestamp" DESC);

        RAISE NOTICE 'Table action_audit_logs created';
    ELSE
        RAISE NOTICE 'Table action_audit_logs already exists â€” skipped';
    END IF;
END
$$;

-- ------------------------------------------------------------
-- Views: vw_role_actions, vw_user_actions
-- (CREATE OR REPLACE is inherently idempotent)
-- ------------------------------------------------------------

CREATE OR REPLACE VIEW assistants_schema.vw_role_actions AS
SELECT
    ra.id,
    ra.role_id,
    r.name        AS role_name,
    ra.action_id,
    a.name        AS action_name,
    a.display_name,
    a.description,
    at.name       AS action_type,
    a.reference,
    ra.action_level,
    ra.updated_at
FROM assistants_schema.roles_actions ra
JOIN assistants_schema.roles        r  ON ra.role_id      = r.id
JOIN assistants_schema.actions      a  ON ra.action_id    = a.id
JOIN assistants_schema.action_types at ON a.action_type_id = at.id
WHERE a.is_active = TRUE;

CREATE OR REPLACE VIEW assistants_schema.vw_user_actions AS
SELECT DISTINCT
    ur.user_id,
    u.username,
    ur.role_id,
    r.name  AS role_name,
    ra.action_id,
    a.name  AS action_name,
    a.display_name,
    at.name AS action_type,
    a.reference
FROM assistants_schema.user_roles    ur
JOIN assistants_schema.users         u  ON ur.user_id      = u.id
JOIN assistants_schema.roles         r  ON ur.role_id      = r.id
JOIN assistants_schema.roles_actions ra ON r.id             = ra.role_id
JOIN assistants_schema.actions       a  ON ra.action_id    = a.id
JOIN assistants_schema.action_types  at ON a.action_type_id = at.id
WHERE ur.is_active = TRUE
  AND a.is_active  = TRUE;

-- ============================================================
-- 2. SYSTEM ATTRIBUTES
-- ============================================================

-- ------------------------------------------------------------
-- system_actions  â€” named system operations (reload cache, etc.)
-- ------------------------------------------------------------
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT FROM pg_tables
        WHERE schemaname = 'assistants_schema' AND tablename = 'system_actions'
    ) THEN
        CREATE TABLE assistants_schema.system_actions (
            id          SERIAL PRIMARY KEY,
            name        VARCHAR(50) NOT NULL,
            action_type VARCHAR(50) NOT NULL,
            description TEXT,
            created_at  TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
            updated_at  TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
            update_user INTEGER REFERENCES assistants_schema.users(id)
                                ON DELETE SET NULL
        );

        CREATE TRIGGER set_timestamp_system_actions
            BEFORE UPDATE ON assistants_schema.system_actions
            FOR EACH ROW EXECUTE FUNCTION assistants_schema.trigger_set_timestamp();

        RAISE NOTICE 'Table system_actions created';
    ELSE
        RAISE NOTICE 'Table system_actions already exists â€” skipped';
    END IF;
END
$$;

-- ------------------------------------------------------------
-- system_attributes  â€” key/value configuration store
--                      value column is VARCHAR(200) to hold regex
-- ------------------------------------------------------------
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT FROM pg_tables
        WHERE schemaname = 'assistants_schema' AND tablename = 'system_attributes'
    ) THEN
        CREATE TABLE assistants_schema.system_attributes (
            id          SERIAL PRIMARY KEY,
            name        VARCHAR(50)  NOT NULL UNIQUE,
            description VARCHAR(50)  NOT NULL,
            value       VARCHAR(200) NOT NULL,
            value_type  VARCHAR(25),
            foreign_id  INTEGER,
            update_user INTEGER REFERENCES assistants_schema.users(id)
                                ON DELETE SET NULL,
            created_at  TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
            updated_at  TIMESTAMP WITH TIME ZONE DEFAULT NOW()
        );

        -- Seed core attributes
        INSERT INTO assistants_schema.system_attributes
            (name, description, value, value_type)
        VALUES
            -- Application version displayed on login page
            ('SystemVersion',
             '×’×¨×¡×ª ×”×ž×¢×¨×›×ª',
             '1.0',
             'string'),

            -- JWT configuration (actual secret is loaded from Key Vault)
            ('JWT_Issuer',
             'JWT Token Issuer',
             'PetelAssistants',
             'string'),
            ('JWT_Audience',
             'JWT Token Audience',
             'PetelAssistantsUsers',
             'string'),
            ('JWT_ExpirationHours',
             'JWT Expiration (Hours)',
             '8',
             'integer'),
            ('JWT_SecretKey',
             'JWT Secret Key',
             'LOADED_FROM_KEY_VAULT',
             'string'),

            -- Email OTP feature flag (false = skip OTP in development)
            ('Security_OtpEnabled',
             'OTP ×ž×•×¤×¢×œ',
             'false',
             'boolean'),

            -- Password policy â€” single ECMAScript-compatible regex
            -- Requires: lowercase, uppercase, digit, special char, length 6-20
            ('Security_PasswordPolicy',
             '×ª×‘× ×™×ª ×¡×™×¡×ž×” (regex)',
             '^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{6,20}$',
             'string'),

            -- Max failed login attempts before account lock
            ('Security_MaxFailedLoginAttempts',
             '×ž×§×¡×™×ž×•× × ×™×¡×™×•× ×•×ª ×›× ×™×¡×”',
             '5',
             'integer'),

            -- Password expiration in months (0 = disabled)
            ('Security_PasswordExpirationMonths',
             '×ª×§×•×¤×ª ×ª×¤×•×’×” ×œ×¡×™×¡×ž×” (×—×•×“×©×™×)',
             '0',
             'integer')
        ON CONFLICT (name) DO NOTHING;

        RAISE NOTICE 'Table system_attributes created and seeded';
    ELSE
        RAISE NOTICE 'Table system_attributes already exists â€” skipped';
    END IF;
END
$$;


-- ============================================================
-- MENU ITEMS  â€” database-driven navigation
-- (Required by PageLifecycleManager / MenuController)
-- ============================================================

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT FROM pg_tables
        WHERE schemaname = 'assistants_schema' AND tablename = 'menu_items'
    ) THEN
        CREATE TABLE assistants_schema.menu_items (
            id         SERIAL PRIMARY KEY,
            name       VARCHAR(50)  NOT NULL,   -- used in navigateTo()
            reference  VARCHAR(100) NOT NULL,   -- HTML href
            text       VARCHAR(100) NOT NULL,   -- display text (Hebrew)
            action_id  INTEGER                  -- NULL = visible to all
                           REFERENCES assistants_schema.actions(id)
                           ON DELETE SET NULL,
            sort_order INTEGER NOT NULL DEFAULT 0,
            is_active  BOOLEAN NOT NULL DEFAULT TRUE
        );

        CREATE INDEX idx_menu_items_sort_order ON assistants_schema.menu_items(sort_order);
        CREATE INDEX idx_menu_items_action_id  ON assistants_schema.menu_items(action_id);

        RAISE NOTICE 'Table menu_items created';
    ELSE
        RAISE NOTICE 'Table menu_items already exists â€” skipped';
    END IF;
END
$$;

-- ============================================================
-- GRANT PERMISSIONS (adjust role name to match your environment)
-- ============================================================

-- GRANT USAGE ON SCHEMA assistants_schema TO "AssistantsAdmin";
-- GRANT ALL PRIVILEGES ON ALL TABLES    IN SCHEMA assistants_schema TO "AssistantsAdmin";
-- GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA assistants_schema TO "AssistantsAdmin";
-- ALTER DEFAULT PRIVILEGES IN SCHEMA assistants_schema
--     GRANT ALL ON TABLES    TO "AssistantsAdmin";
-- ALTER DEFAULT PRIVILEGES IN SCHEMA assistants_schema
--     GRANT ALL ON SEQUENCES TO "AssistantsAdmin";

COMMIT;

-- ============================================================
-- VERIFICATION QUERY â€” uncomment after running to confirm
-- ============================================================
-- SELECT
--     table_name,
--     (SELECT COUNT(*) FROM information_schema.columns
--      WHERE table_schema = 'assistants_schema'
--        AND columns.table_name = t.table_name) AS column_count
-- FROM (
--     SELECT table_name
--     FROM information_schema.tables
--     WHERE table_schema = 'assistants_schema'
--       AND table_type   = 'BASE TABLE'
--     ORDER BY table_name
-- ) t;
