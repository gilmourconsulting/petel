-- Migration: Add user_lock_reasons lookup table and lock_reason_id FK to users
-- Idempotent — safe to run multiple times

DO $$
BEGIN
    -- Create user_lock_reasons table
    IF NOT EXISTS (
        SELECT FROM pg_tables
        WHERE schemaname = 'petel_schema'
        AND tablename = 'user_lock_reasons'
    ) THEN
        CREATE TABLE petel_schema.user_lock_reasons (
            id SERIAL PRIMARY KEY,
            code VARCHAR(50) NOT NULL,
            name VARCHAR(100) NOT NULL,
            description VARCHAR(200) NULL,
            allow_forgot_password BOOLEAN NOT NULL DEFAULT true,
            is_active BOOLEAN NOT NULL DEFAULT true,
            sort_order INTEGER NOT NULL DEFAULT 0,
            created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
            CONSTRAINT uk_user_lock_reasons_code UNIQUE (code)
        );

        CREATE INDEX idx_user_lock_reasons_code ON petel_schema.user_lock_reasons(code);

        RAISE NOTICE 'Table user_lock_reasons created successfully';
    ELSE
        RAISE NOTICE 'Table user_lock_reasons already exists';
    END IF;

    -- Seed standard lock reasons
    INSERT INTO petel_schema.user_lock_reasons (code, name, description, allow_forgot_password, sort_order)
    VALUES
        ('LOGIN_ATTEMPTS_EXCEEDED', 'חריגה במספר ניסיונות כניסה', 'החשבון ננעל אוטומטית לאחר מספר ניסיונות כניסה כושלים', true, 1),
        ('ADMIN_LOCKED', 'נעילה על ידי מנהל', 'החשבון ננעל ידנית על ידי מנהל המערכת', false, 2)
    ON CONFLICT (code) DO NOTHING;

    RAISE NOTICE 'Lock reasons seeded successfully';

    -- Add lock_reason_id column to users table
    IF NOT EXISTS (
        SELECT FROM information_schema.columns
        WHERE table_schema = 'petel_schema'
        AND table_name = 'users'
        AND column_name = 'lock_reason_id'
    ) THEN
        ALTER TABLE petel_schema.users
            ADD COLUMN lock_reason_id INTEGER NULL
                REFERENCES petel_schema.user_lock_reasons(id) ON DELETE SET NULL;

        CREATE INDEX idx_users_lock_reason_id ON petel_schema.users(lock_reason_id);

        RAISE NOTICE 'Column lock_reason_id added to users table';
    ELSE
        RAISE NOTICE 'Column lock_reason_id already exists in users table';
    END IF;
END
$$;
