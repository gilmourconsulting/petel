-- Development Environment Configuration
-- Rate limiting DISABLED for development ease
-- Liberal timeouts and attempt limits for development workflow

INSERT INTO petel_schema.system_attributes (name, description, value, value_type, update_user) VALUES 

-- Rate Limiting - DISABLED in Development
('RateLimit_Enabled', 'Enable/disable rate limiting system-wide', 'false', 'boolean', 1),
('RateLimit_LoginLimit', 'Maximum login attempts per 15 minutes', '50', 'integer', 1),
('RateLimit_LoginPeriod', 'Login attempts rate limit period', '15m', 'string', 1),
('RateLimit_OtpLimit', 'Maximum OTP validation attempts per 15 minutes', '20', 'integer', 1),
('RateLimit_OtpPeriod', 'OTP validation rate limit period', '15m', 'string', 1),
('RateLimit_ApiLimit', 'API requests limit per minute', '500', 'integer', 1),
('RateLimit_ApiPeriod', 'API requests rate limit period', '1m', 'string', 1),
('RateLimit_HourlyLimit', 'API requests limit per hour', '10000', 'integer', 1),
('RateLimit_PostLimit', 'POST requests limit per minute', '200', 'integer', 1),
('RateLimit_PutLimit', 'PUT requests limit per minute', '150', 'integer', 1),
('RateLimit_DeleteLimit', 'DELETE requests limit per minute', '100', 'integer', 1),
('RateLimit_GetLimit', 'GET requests limit per minute', '500', 'integer', 1),

-- System Configuration - Development Settings
('System_MaintenanceMode', 'Put system in maintenance mode', 'false', 'boolean', 1),
('System_MaintenanceMessage', 'Message displayed during maintenance', 'המערכת בתחזוקה. אנא נסו שוב מאוחר יותר.', 'string', 1),
('System_Environment', 'Current environment identifier', 'development', 'string', 1),
('System_DetailedLogging', 'Enable detailed API request/response logging', 'true', 'boolean', 1),

-- Security Settings Updates (align with existing but more liberal for dev)
('Security_OtpIssuer', 'OTP issuer name displayed in authenticator apps', 'Petel Dev System', 'string', 1)

ON CONFLICT (name) DO UPDATE SET
    description = EXCLUDED.description,
    value = EXCLUDED.value,
    value_type = EXCLUDED.value_type,
    updated_at = CURRENT_TIMESTAMP;

-- Update existing security settings for dev environment
UPDATE petel_schema.system_attributes SET 
    value = '30',
    updated_at = CURRENT_TIMESTAMP
WHERE name = 'Security_SessionTimeoutMinutes';

UPDATE petel_schema.system_attributes SET 
    value = '10',
    updated_at = CURRENT_TIMESTAMP  
WHERE name = 'Security_MaxPasswordAttempts';

UPDATE petel_schema.system_attributes SET 
    value = '10',
    updated_at = CURRENT_TIMESTAMP
WHERE name = 'Security_MaxOtpAttempts';

COMMIT;