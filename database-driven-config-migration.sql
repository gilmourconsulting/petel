-- Add runtime configuration attributes to system_attributes table
-- These can be modified at runtime without redeployment

INSERT INTO petel_schema.system_attributes (name, description, value, value_type) VALUES 
-- Feature Flags
('Features.RateLimitingEnabled', 'Enable/disable rate limiting system-wide', 'false', 'boolean'),
('Features.OtpEnabled', 'Enable/disable OTP two-factor authentication', 'true', 'boolean'),

-- Security Policy Settings  
('Security.SessionTimeoutMinutes', 'User session timeout in minutes', '60', 'integer'),
('Security.MaxPasswordAttempts', 'Maximum failed password attempts before lockout', '5', 'integer'),
('Security.MaxOtpAttempts', 'Maximum failed OTP attempts before lockout', '3', 'integer'), 
('Security.PasswordExpirationMonths', 'Password expiration period in months', '6', 'integer'),
('Security_OtpIssuer', 'OTP issuer name displayed in authenticator apps', 'Petel External Students System', 'string'),

-- Rate Limiting - Login Endpoints
('RateLimit.LoginAttemptsLimit', 'Maximum login attempts per period', '10', 'integer'),
('RateLimit.LoginAttemptsPeriod', 'Login attempts rate limit period', '15m', 'string'),
('RateLimit.OtpValidationLimit', 'Maximum OTP validation attempts per period', '5', 'integer'),
('RateLimit.OtpValidationPeriod', 'OTP validation rate limit period', '15m', 'string'),

-- Rate Limiting - General API
('RateLimit.ApiRequestsLimit', 'General API requests limit per minute', '120', 'integer'),
('RateLimit.ApiRequestsPeriod', 'General API requests rate limit period', '1m', 'string'), 
('RateLimit.ApiHourlyLimit', 'API requests limit per hour', '2000', 'integer'),

-- Rate Limiting - Method Specific
('RateLimit.PostRequestsLimit', 'POST requests limit per minute', '60', 'integer'),
('RateLimit.PutRequestsLimit', 'PUT requests limit per minute', '40', 'integer'),
('RateLimit.DeleteRequestsLimit', 'DELETE requests limit per minute', '20', 'integer'),
('RateLimit.GetRequestsLimit', 'GET requests limit per minute', '120', 'integer'),

-- Environment-Specific Overrides (can be updated per environment)
('Environment.Name', 'Current environment name (dev/test/prod)', 'production', 'string'),
('Environment.RateLimitMultiplier', 'Rate limit multiplier for this environment', '1.0', 'decimal'),

-- System Behavior
('System.EnableDetailedLogging', 'Enable detailed API request/response logging', 'false', 'boolean'),
('System_MaintenanceMode', 'Put system in maintenance mode', 'false', 'boolean'),
('System_MaintenanceMessage', 'Message displayed during maintenance', 'המערכת בתחזוקה. אנא נסו שוב מאוחר יותר.', 'string')

ON CONFLICT (name) DO UPDATE SET
    value = EXCLUDED.value,
    description = EXCLUDED.description,
    value_type = EXCLUDED.value_type,
    updated_at = CURRENT_TIMESTAMP;

-- Add indexes for performance
CREATE INDEX IF NOT EXISTS idx_system_attributes_name ON petel_schema.system_attributes(name);
CREATE INDEX IF NOT EXISTS idx_system_attributes_value_type ON petel_schema.system_attributes(value_type);