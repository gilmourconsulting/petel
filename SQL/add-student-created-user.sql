-- Migration: created_user on school_students
-- created_at already exists (DB default now()). This adds the creator user FK
-- so student version history can show who created each version.
-- Run once on each environment. Idempotent.

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'petel_schema'
          AND table_name   = 'school_students'
          AND column_name  = 'created_user'
    ) THEN
        ALTER TABLE petel_schema.school_students
            ADD COLUMN created_user INTEGER NULL REFERENCES petel_schema.users(id) ON DELETE SET NULL;
        RAISE NOTICE 'Added created_user to school_students';
    ELSE
        RAISE NOTICE 'created_user already exists on school_students';
    END IF;
END
$$;
