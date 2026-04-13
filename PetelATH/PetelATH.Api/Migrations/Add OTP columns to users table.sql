-- Add OTP columns to users table
ALTER TABLE petel_schema.users
ADD COLUMN otp_secret VARCHAR(255),
ADD COLUMN otp_enabled BOOLEAN DEFAULT FALSE,
ADD COLUMN otp_verified BOOLEAN DEFAULT FALSE;

-- Index for performance
CREATE INDEX idx_users_otp_enabled ON petel_schema.users(otp_enabled) WHERE otp_enabled = TRUE;