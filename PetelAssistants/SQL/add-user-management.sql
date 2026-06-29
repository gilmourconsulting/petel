-- =============================================================================
-- PetelAssistants — User Management Schema
-- Run after bootstrap.sql and add-years-and-menu.sql.
-- Idempotent: safe to re-run (IF NOT EXISTS / ON CONFLICT DO NOTHING).
-- =============================================================================

-- =============================================================================
-- SHARED SCHEMA — global reference data, no entity_id
-- =============================================================================

-- ─── user_lock_reasons ────────────────────────────────────────────────────────
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT FROM pg_tables WHERE schemaname = 'shared_schema' AND tablename = 'user_lock_reasons'
    ) THEN
        CREATE TABLE shared_schema.user_lock_reasons (
            id                   SERIAL PRIMARY KEY,
            code                 VARCHAR(50)  NOT NULL UNIQUE,
            name                 VARCHAR(100) NOT NULL,
            description          VARCHAR(200) NULL,
            allow_forgot_password BOOLEAN NOT NULL DEFAULT true,
            is_active            BOOLEAN NOT NULL DEFAULT true,
            sort_order           INTEGER NOT NULL DEFAULT 0,
            created_at           TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
        );

        INSERT INTO shared_schema.user_lock_reasons (code, name, description, allow_forgot_password, sort_order)
        VALUES
            ('LOGIN_ATTEMPTS_EXCEEDED', 'נעילה אוטומטית - ניסיונות כושלים', 'נעילה עקב חריגת מספר ניסיונות כניסה',  true,  10),
            ('ADMIN_LOCKED',            'נעילה ידנית על ידי מנהל',            'נעילה שבוצעה ידנית על ידי מנהל מערכת', false, 20)
        ON CONFLICT (code) DO NOTHING;

        RAISE NOTICE 'Table user_lock_reasons created and seeded';
    ELSE
        RAISE NOTICE 'Table user_lock_reasons already exists';
    END IF;
END $$;

-- ─── action_types ─────────────────────────────────────────────────────────────
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT FROM pg_tables WHERE schemaname = 'shared_schema' AND tablename = 'action_types'
    ) THEN
        CREATE TABLE shared_schema.action_types (
            id          SERIAL PRIMARY KEY,
            name        VARCHAR(50)  NOT NULL UNIQUE,
            description VARCHAR(200) NULL,
            is_active   BOOLEAN NOT NULL DEFAULT true
        );

        INSERT INTO shared_schema.action_types (name, description)
        VALUES
            ('Button',   'כפתור על מסך'),
            ('Page',     'גישה לדף / מסך'),
            ('MenuItem', 'פריט תפריט ניווט')
        ON CONFLICT (name) DO NOTHING;

        RAISE NOTICE 'Table action_types created and seeded';
    ELSE
        RAISE NOTICE 'Table action_types already exists';
    END IF;
END $$;

-- ─── actions ──────────────────────────────────────────────────────────────────
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT FROM pg_tables WHERE schemaname = 'shared_schema' AND tablename = 'actions'
    ) THEN
        CREATE TABLE shared_schema.actions (
            id             SERIAL PRIMARY KEY,
            name           VARCHAR(200) NOT NULL UNIQUE,
            display_name   VARCHAR(200) NOT NULL,
            reference      VARCHAR(200) NULL,
            description    VARCHAR(500) NULL,
            action_type_id INTEGER NOT NULL REFERENCES shared_schema.action_types(id) ON DELETE RESTRICT,
            is_active      BOOLEAN NOT NULL DEFAULT true,
            created_at     TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
            updated_at     TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
        );

        CREATE INDEX idx_actions_action_type ON shared_schema.actions(action_type_id);
        CREATE INDEX idx_actions_name        ON shared_schema.actions(name);

        RAISE NOTICE 'Table actions created';
    ELSE
        RAISE NOTICE 'Table actions already exists';
    END IF;
END $$;

-- ─── Seed screen actions ───────────────────────────────────────────────────────
-- NOTE: 2 menu-level actions may already exist (seeded by the app team).
--       All INSERTs use ON CONFLICT DO NOTHING — safe to re-run.
DO $$
DECLARE
    v_button_type_id  INTEGER;
    v_page_type_id    INTEGER;
    v_menu_type_id    INTEGER;
BEGIN
    SELECT id INTO v_button_type_id FROM shared_schema.action_types WHERE name = 'Button';
    SELECT id INTO v_page_type_id   FROM shared_schema.action_types WHERE name = 'Page';
    SELECT id INTO v_menu_type_id   FROM shared_schema.action_types WHERE name = 'MenuItem';

    -- ── tenants screen ────────────────────────────────────────────────────────
    INSERT INTO shared_schema.actions (name, display_name, reference, description, action_type_id)
    VALUES
        ('tenants',             'גישה למסך ניהול רשויות',   'tenants', 'גישה לדף ניהול רשויות',       v_page_type_id),
        ('tenants_refresh',     'רענון נתוני רשויות',       'tenants', 'כפתור רענן נתוני רשויות',      v_button_type_id),
        ('tenants_create',      'יצירת רשות חדשה',          'tenants', 'כפתור הוסף רשות חדשה',         v_button_type_id),
        ('tenants_edit',        'עריכת רשות',               'tenants', 'כפתור עריכת פרטי רשות',        v_button_type_id),
        ('tenants_activate',    'הפעלת רשות',               'tenants', 'כפתור הפעל רשות',              v_button_type_id),
        ('tenants_deactivate',  'השהיית רשות',              'tenants', 'כפתור השהה רשות',              v_button_type_id),
        ('tenants_manageusers', 'ניהול משתמשי רשות',        'tenants', 'כפתור מעבר לניהול משתמשים',   v_button_type_id)
    ON CONFLICT (name) DO NOTHING;

    -- ── users screen ──────────────────────────────────────────────────────────
    INSERT INTO shared_schema.actions (name, display_name, reference, description, action_type_id)
    VALUES
        ('users',                      'גישה למסך ניהול משתמשים',    'users', 'גישה לדף ניהול משתמשים',           v_page_type_id),
        ('users_refresh',              'רענון נתוני משתמשים',        'users', 'כפתור רענן נתוני משתמשים',         v_button_type_id),
        ('users_create',               'יצירת משתמש חדש',            'users', 'כפתור הוסף משתמש חדש',             v_button_type_id),
        ('users_edit',                 'עריכת משתמש',                'users', 'כפתור עריכת פרטי משתמש',           v_button_type_id),
        ('users_deactivate',           'השבתת משתמש',                'users', 'כפתור השבת משתמש',                 v_button_type_id),
        ('users_lock',                 'נעילת משתמש',                'users', 'כפתור נעל משתמש',                  v_button_type_id),
        ('users_unlock',               'שחרור נעילת משתמש',          'users', 'כפתור שחרר נעילה',                 v_button_type_id),
        ('users_changepassword',       'שינוי סיסמה',                'users', 'כפתור שנה סיסמה',                  v_button_type_id),
        ('users_forcepasswordchange',  'אכיפת שינוי סיסמה',          'users', 'כפתור אכוף שינוי סיסמה',           v_button_type_id),
        ('users_resetfailedattempts',  'איפוס ניסיונות כושלים',      'users', 'כפתור אפס ניסיונות כושלים',        v_button_type_id),
        ('users_manageroles',          'ניהול תפקידי משתמש',         'users', 'כפתור ניהול תפקידים למשתמש',       v_button_type_id),
        ('users_roles',                'מעבר למסך תפקידים',          'users', 'כפתור מעבר לניהול תפקידים',        v_button_type_id),
        ('users_refreshcache',         'רענון מטמון אבטחה',          'users', 'כפתור רענן מטמון אבטחה',           v_button_type_id),
        ('users_maindashboard',        'חזרה ללוח בקרה מ-משתמשים',  'users', 'כפתור חזרה ללוח בקרה',            v_button_type_id),
        ('users_tenants',              'מעבר לניהול רשויות',         'users', 'כפתור מעבר לניהול רשויות',          v_button_type_id),
        ('users_resetotp',             'איפוס OTP',                  'users', 'כפתור איפוס אימות דו-שלבי',         v_button_type_id)
    ON CONFLICT (name) DO NOTHING;

    -- ── roles screen ──────────────────────────────────────────────────────────
    INSERT INTO shared_schema.actions (name, display_name, reference, description, action_type_id)
    VALUES
        ('roles',                  'גישה למסך ניהול תפקידים',   'roles', 'גישה לדף ניהול תפקידים',          v_page_type_id),
        ('roles_refresh',          'רענון נתוני תפקידים',       'roles', 'כפתור רענן נתוני תפקידים',         v_button_type_id),
        ('roles_create',           'יצירת תפקיד חדש',           'roles', 'כפתור הוסף תפקיד חדש',             v_button_type_id),
        ('roles_edit',             'עריכת תפקיד',               'roles', 'כפתור עריכת פרטי תפקיד',           v_button_type_id),
        ('roles_delete',           'מחיקת תפקיד',               'roles', 'כפתור מחיקת תפקיד',                v_button_type_id),
        ('roles_viewdetails',      'צפייה בפרטי תפקיד',         'roles', 'כפתור צפה בפרטי תפקיד',            v_button_type_id),
        ('roles_maindashboard',    'חזרה ללוח בקרה מ-תפקידים', 'roles', 'כפתור חזרה ללוח בקרה',            v_button_type_id),
        ('roles_users',            'מעבר למסך משתמשים',         'roles', 'כפתור מעבר לניהול משתמשים',        v_button_type_id)
    ON CONFLICT (name) DO NOTHING;

    -- ── roledetails screen ────────────────────────────────────────────────────
    INSERT INTO shared_schema.actions (name, display_name, reference, description, action_type_id)
    VALUES
        ('roledetails',                'גישה למסך פרטי תפקיד',      'roledetails', 'גישה לדף פרטי תפקיד',              v_page_type_id),
        ('roledetails_adduser',        'הוספת משתמש לתפקיד',        'roledetails', 'כפתור הוסף משתמש לתפקיד',           v_button_type_id),
        ('roledetails_removeuser',     'הסרת משתמש מתפקיד',         'roledetails', 'כפתור הסר משתמש מתפקיד',            v_button_type_id),
        ('roledetails_addaction',      'הוספת הרשאה לתפקיד',        'roledetails', 'כפתור הוסף הרשאה לתפקיד',           v_button_type_id),
        ('roledetails_removeaction',   'הסרת הרשאה מתפקיד',         'roledetails', 'כפתור הסר הרשאה מתפקיד',            v_button_type_id),
        ('roledetails_back',           'חזרה לרשימת תפקידים',       'roledetails', 'כפתור חזרה לרשימת תפקידים',         v_button_type_id)
    ON CONFLICT (name) DO NOTHING;

    RAISE NOTICE 'Screen actions seeded';
END $$;


-- =============================================================================
-- ASSIST SCHEMA — tenant-scoped operational data
-- =============================================================================

-- ─── Extend assist_schema.users ──────────────────────────────────────────────
DO $$
BEGIN
    -- phone
    IF NOT EXISTS (SELECT FROM information_schema.columns
        WHERE table_schema = 'assist_schema' AND table_name = 'users' AND column_name = 'phone') THEN
        ALTER TABLE assist_schema.users ADD COLUMN phone VARCHAR(20) NULL;
        RAISE NOTICE 'Added column phone to users';
    END IF;

    -- lock_reason_id
    IF NOT EXISTS (SELECT FROM information_schema.columns
        WHERE table_schema = 'assist_schema' AND table_name = 'users' AND column_name = 'lock_reason_id') THEN
        ALTER TABLE assist_schema.users
            ADD COLUMN lock_reason_id INTEGER NULL
                REFERENCES shared_schema.user_lock_reasons(id) ON DELETE SET NULL;
        RAISE NOTICE 'Added column lock_reason_id to users';
    END IF;

    -- locked_at
    IF NOT EXISTS (SELECT FROM information_schema.columns
        WHERE table_schema = 'assist_schema' AND table_name = 'users' AND column_name = 'locked_at') THEN
        ALTER TABLE assist_schema.users ADD COLUMN locked_at TIMESTAMP NULL;
        RAISE NOTICE 'Added column locked_at to users';
    END IF;

    -- locked_by
    IF NOT EXISTS (SELECT FROM information_schema.columns
        WHERE table_schema = 'assist_schema' AND table_name = 'users' AND column_name = 'locked_by') THEN
        ALTER TABLE assist_schema.users
            ADD COLUMN locked_by INTEGER NULL
                REFERENCES assist_schema.users(id) ON DELETE SET NULL;
        RAISE NOTICE 'Added column locked_by to users';
    END IF;

    -- failed_password_attempts
    IF NOT EXISTS (SELECT FROM information_schema.columns
        WHERE table_schema = 'assist_schema' AND table_name = 'users' AND column_name = 'failed_password_attempts') THEN
        ALTER TABLE assist_schema.users ADD COLUMN failed_password_attempts INTEGER NOT NULL DEFAULT 0;
        RAISE NOTICE 'Added column failed_password_attempts to users';
    END IF;

    -- failed_otp_attempts
    IF NOT EXISTS (SELECT FROM information_schema.columns
        WHERE table_schema = 'assist_schema' AND table_name = 'users' AND column_name = 'failed_otp_attempts') THEN
        ALTER TABLE assist_schema.users ADD COLUMN failed_otp_attempts INTEGER NOT NULL DEFAULT 0;
        RAISE NOTICE 'Added column failed_otp_attempts to users';
    END IF;

    -- password_changed_at
    IF NOT EXISTS (SELECT FROM information_schema.columns
        WHERE table_schema = 'assist_schema' AND table_name = 'users' AND column_name = 'password_changed_at') THEN
        ALTER TABLE assist_schema.users
            ADD COLUMN password_changed_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP;
        RAISE NOTICE 'Added column password_changed_at to users';
    END IF;

    -- password_change_required
    IF NOT EXISTS (SELECT FROM information_schema.columns
        WHERE table_schema = 'assist_schema' AND table_name = 'users' AND column_name = 'password_change_required') THEN
        ALTER TABLE assist_schema.users
            ADD COLUMN password_change_required BOOLEAN NOT NULL DEFAULT false;
        RAISE NOTICE 'Added column password_change_required to users';
    END IF;

    -- otp_secret
    IF NOT EXISTS (SELECT FROM information_schema.columns
        WHERE table_schema = 'assist_schema' AND table_name = 'users' AND column_name = 'otp_secret') THEN
        ALTER TABLE assist_schema.users ADD COLUMN otp_secret VARCHAR(255) NULL;
        RAISE NOTICE 'Added column otp_secret to users';
    END IF;

    -- otp_enabled
    IF NOT EXISTS (SELECT FROM information_schema.columns
        WHERE table_schema = 'assist_schema' AND table_name = 'users' AND column_name = 'otp_enabled') THEN
        ALTER TABLE assist_schema.users ADD COLUMN otp_enabled BOOLEAN NOT NULL DEFAULT false;
        RAISE NOTICE 'Added column otp_enabled to users';
    END IF;

    -- otp_verified
    IF NOT EXISTS (SELECT FROM information_schema.columns
        WHERE table_schema = 'assist_schema' AND table_name = 'users' AND column_name = 'otp_verified') THEN
        ALTER TABLE assist_schema.users ADD COLUMN otp_verified BOOLEAN NOT NULL DEFAULT false;
        RAISE NOTICE 'Added column otp_verified to users';
    END IF;
END $$;

-- ─── roles ────────────────────────────────────────────────────────────────────
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT FROM pg_tables WHERE schemaname = 'assist_schema' AND tablename = 'roles'
    ) THEN
        CREATE TABLE assist_schema.roles (
            id          SERIAL PRIMARY KEY,
            entity_id   INTEGER NOT NULL REFERENCES shared_schema.entities(id) ON DELETE RESTRICT,
            name        VARCHAR(100) NOT NULL,
            description VARCHAR(300) NULL,
            created_at  TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
            created_user INTEGER NULL REFERENCES assist_schema.users(id) ON DELETE SET NULL,
            updated_at  TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
            update_user INTEGER NULL REFERENCES assist_schema.users(id) ON DELETE SET NULL,
            CONSTRAINT uk_roles_entity_name UNIQUE (entity_id, name)
        );

        CREATE INDEX idx_roles_entity_id ON assist_schema.roles(entity_id);

        RAISE NOTICE 'Table roles created';
    ELSE
        RAISE NOTICE 'Table roles already exists';
    END IF;
END $$;

-- ─── user_roles ───────────────────────────────────────────────────────────────
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT FROM pg_tables WHERE schemaname = 'assist_schema' AND tablename = 'user_roles'
    ) THEN
        CREATE TABLE assist_schema.user_roles (
            id          SERIAL PRIMARY KEY,
            entity_id   INTEGER NOT NULL REFERENCES shared_schema.entities(id) ON DELETE RESTRICT,
            user_id     INTEGER NOT NULL REFERENCES assist_schema.users(id)    ON DELETE CASCADE,
            role_id     INTEGER NOT NULL REFERENCES assist_schema.roles(id)    ON DELETE CASCADE,
            is_active   BOOLEAN NOT NULL DEFAULT true,
            created_at  TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
            updated_at  TIMESTAMP NULL,
            update_user INTEGER NULL REFERENCES assist_schema.users(id) ON DELETE SET NULL,
            CONSTRAINT uk_user_roles_user_role UNIQUE (user_id, role_id)
        );

        CREATE INDEX idx_user_roles_entity_id ON assist_schema.user_roles(entity_id);
        CREATE INDEX idx_user_roles_user_id   ON assist_schema.user_roles(user_id);
        CREATE INDEX idx_user_roles_role_id   ON assist_schema.user_roles(role_id);

        RAISE NOTICE 'Table user_roles created';
    ELSE
        RAISE NOTICE 'Table user_roles already exists';
    END IF;
END $$;

-- ─── roles_actions ────────────────────────────────────────────────────────────
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT FROM pg_tables WHERE schemaname = 'assist_schema' AND tablename = 'roles_actions'
    ) THEN
        CREATE TABLE assist_schema.roles_actions (
            id          SERIAL PRIMARY KEY,
            entity_id   INTEGER NOT NULL REFERENCES shared_schema.entities(id)  ON DELETE RESTRICT,
            role_id     INTEGER NOT NULL REFERENCES assist_schema.roles(id)     ON DELETE CASCADE,
            action_id   INTEGER NOT NULL REFERENCES shared_schema.actions(id)   ON DELETE CASCADE,
            created_at  TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
            CONSTRAINT uk_roles_actions_role_action UNIQUE (role_id, action_id)
        );

        CREATE INDEX idx_roles_actions_entity_id ON assist_schema.roles_actions(entity_id);
        CREATE INDEX idx_roles_actions_role_id   ON assist_schema.roles_actions(role_id);
        CREATE INDEX idx_roles_actions_action_id ON assist_schema.roles_actions(action_id);

        RAISE NOTICE 'Table roles_actions created';
    ELSE
        RAISE NOTICE 'Table roles_actions already exists';
    END IF;
END $$;

-- ─── action_audit_logs ────────────────────────────────────────────────────────
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT FROM pg_tables WHERE schemaname = 'assist_schema' AND tablename = 'action_audit_logs'
    ) THEN
        CREATE TABLE assist_schema.action_audit_logs (
            id           SERIAL PRIMARY KEY,
            entity_id    INTEGER NOT NULL REFERENCES shared_schema.entities(id) ON DELETE RESTRICT,
            user_id      INTEGER NULL REFERENCES assist_schema.users(id) ON DELETE SET NULL,
            action_name  VARCHAR(200) NOT NULL,
            screen_name  VARCHAR(100) NULL,
            function_name VARCHAR(100) NULL,
            event_type   VARCHAR(50)  NOT NULL,
            result       VARCHAR(20)  NOT NULL,
            action_params VARCHAR(500) NULL,
            description  VARCHAR(500) NULL,
            ip_address   VARCHAR(45)  NULL,
            timestamp    TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
        );

        CREATE INDEX idx_audit_logs_entity_id  ON assist_schema.action_audit_logs(entity_id);
        CREATE INDEX idx_audit_logs_user_id    ON assist_schema.action_audit_logs(user_id);
        CREATE INDEX idx_audit_logs_timestamp  ON assist_schema.action_audit_logs(timestamp);

        RAISE NOTICE 'Table action_audit_logs created';
    ELSE
        RAISE NOTICE 'Table action_audit_logs already exists';
    END IF;
END $$;
