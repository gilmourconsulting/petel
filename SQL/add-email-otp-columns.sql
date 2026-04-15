-- Email OTP columns for users table
-- Replaces TOTP (authenticator-app) flow with server-sent email OTP codes.
-- Run this on all environments (dev, test, staging, production).
-- Safe to re-run: each ALTER is wrapped in an existence check.

-- DDL: add the three new columns to petel_schema.users
DO $$
BEGIN
    -- email_otp_code: BCrypt hash of the current one-time code (NULL = no code pending)
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'petel_schema'
          AND table_name   = 'users'
          AND column_name  = 'email_otp_code'
    ) THEN
        ALTER TABLE petel_schema.users
            ADD COLUMN email_otp_code VARCHAR(100) NULL;
        RAISE NOTICE 'Column email_otp_code added';
    ELSE
        RAISE NOTICE 'Column email_otp_code already exists – skipped';
    END IF;

    -- email_otp_expiry: UTC timestamp when the code expires (10 minutes after issue)
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'petel_schema'
          AND table_name   = 'users'
          AND column_name  = 'email_otp_expiry'
    ) THEN
        ALTER TABLE petel_schema.users
            ADD COLUMN email_otp_expiry TIMESTAMP WITH TIME ZONE NULL;
        RAISE NOTICE 'Column email_otp_expiry added';
    ELSE
        RAISE NOTICE 'Column email_otp_expiry already exists – skipped';
    END IF;

    -- email_otp_attempts: failed validation counter; resets to 0 on new code issue
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'petel_schema'
          AND table_name   = 'users'
          AND column_name  = 'email_otp_attempts'
    ) THEN
        ALTER TABLE petel_schema.users
            ADD COLUMN email_otp_attempts INTEGER NOT NULL DEFAULT 0;
        RAISE NOTICE 'Column email_otp_attempts added';
    ELSE
        RAISE NOTICE 'Column email_otp_attempts already exists – skipped';
    END IF;
END
$$;

-- DML: clear any residual TOTP state that might interfere with the new flow
-- (sets otp_enabled/otp_verified to false; leaves otp_secret in place for rollback)
UPDATE petel_schema.users
SET    otp_enabled  = false,
       otp_verified = false
WHERE  otp_enabled  = true
   OR  otp_verified = true;

-- Verify result
SELECT column_name, data_type, character_maximum_length, is_nullable
FROM   information_schema.columns
WHERE  table_schema = 'petel_schema'
  AND  table_name   = 'users'
  AND  column_name  IN ('email_otp_code', 'email_otp_expiry', 'email_otp_attempts')
ORDER BY column_name;
