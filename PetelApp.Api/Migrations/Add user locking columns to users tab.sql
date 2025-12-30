-- Add user locking columns to users table
ALTER TABLE petel_schema.users
ADD COLUMN IF NOT EXISTS is_locked BOOLEAN DEFAULT FALSE,
ADD COLUMN IF NOT EXISTS locked_at TIMESTAMP WITH TIME ZONE,
ADD COLUMN IF NOT EXISTS locked_by INTEGER REFERENCES petel_schema.users(id),
ADD COLUMN IF NOT EXISTS failed_password_attempts INTEGER DEFAULT 0,
ADD COLUMN IF NOT EXISTS failed_otp_attempts INTEGER DEFAULT 0,
ADD COLUMN IF NOT EXISTS last_failed_attempt TIMESTAMP WITH TIME ZONE;

-- Add indexes for performance
CREATE INDEX IF NOT EXISTS idx_users_is_locked ON petel_schema.users(is_locked);
CREATE INDEX IF NOT EXISTS idx_users_failed_attempts ON petel_schema.users(failed_password_attempts, failed_otp_attempts);

-- Add comments
COMMENT ON COLUMN petel_schema.users.is_locked IS 'User account is locked due to failed attempts or manual lock';
COMMENT ON COLUMN petel_schema.users.locked_at IS 'Timestamp when user was locked';
COMMENT ON COLUMN petel_schema.users.locked_by IS 'User ID who manually locked this account';
COMMENT ON COLUMN petel_schema.users.failed_password_attempts IS 'Count of consecutive failed password attempts';
COMMENT ON COLUMN petel_schema.users.failed_otp_attempts IS 'Count of consecutive failed OTP attempts';
COMMENT ON COLUMN petel_schema.users.last_failed_attempt IS 'Timestamp of last failed login attempt';

-- Add password expiration columns to users table
ALTER TABLE petel_schema.users
ADD COLUMN IF NOT EXISTS password_changed_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
ADD COLUMN IF NOT EXISTS password_change_required BOOLEAN DEFAULT FALSE;

-- Set initial password_changed_at for existing users
UPDATE petel_schema.users
SET password_changed_at = created_at
WHERE password_changed_at IS NULL;

-- Add index for password expiration checks
CREATE INDEX IF NOT EXISTS idx_users_password_changed_at ON petel_schema.users(password_changed_at);

-- Add comments
COMMENT ON COLUMN petel_schema.users.password_changed_at IS 'Timestamp when password was last changed';
COMMENT ON COLUMN petel_schema.users.password_change_required IS 'Admin forced password change flag';