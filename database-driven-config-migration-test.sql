-- Test Environment Configuration  
-- Moderate rate limiting for realistic testing
-- Balanced settings that allow thorough testing while preventing abuse

INSERT INTO petel_schema.system_attributes (name, description, value, value_type, update_user) VALUES 

-- Rate Limiting - ENABLED with generous limits for testing
('RateLimit_Enabled', 'Enable/disable rate limiting system-wide', 'true', 'boolean', 1),
('RateLimit_LoginLimit', 'Maximum login attempts per 15 minutes', '20', 'integer', 1),
('RateLimit_LoginPeriod', 'Login attempts rate limit period', '15m', 'string', 1),
('RateLimit_OtpLimit', 'Maximum OTP validation attempts per 15 minutes', '10', 'integer', 1),
('RateLimit_OtpPeriod', 'OTP validation rate limit period', '15m', 'string', 1),
('RateLimit_ApiLimit', 'API requests limit per minute', '200', 'integer', 1),
('RateLimit_ApiPeriod', 'API requests rate limit period', '1m', 'string', 1),
('RateLimit_HourlyLimit', 'API requests limit per hour', '5000', 'integer', 1),
('RateLimit_PostLimit', 'POST requests limit per minute', '100', 'integer', 1),
('RateLimit_PutLimit', 'PUT requests limit per minute', '80', 'integer', 1),
('RateLimit_DeleteLimit', 'DELETE requests limit per minute', '40', 'integer', 1),
('RateLimit_GetLimit', 'GET requests limit per minute', '200', 'integer', 1),

-- System Configuration - Test Environment Settings
('System_MaintenanceMode', 'Put system in maintenance mode', 'false', 'boolean', 1),
('System_MaintenanceMessage', 'Message displayed during maintenance', 'מערכת הבדיקות בתחזוקה. אנא נסו שוב מאוחר יותר.', 'string', 1),
('System_Environment', 'Current environment identifier', 'test', 'string', 1),
('System_DetailedLogging', 'Enable detailed API request/response logging', 'true', 'boolean', 1),

-- Security Settings Updates for test environment
('Security_OtpIssuer', 'OTP issuer name displayed in authenticator apps', 'Petel Test System', 'string', 1)

ON CONFLICT (name) DO UPDATE SET
    description = EXCLUDED.description,
    value = EXCLUDED.value,
    value_type = EXCLUDED.value_type,
    updated_at = CURRENT_TIMESTAMP;

-- Update existing security settings for test environment  
UPDATE petel_schema.system_attributes SET 
    value = '60',
    updated_at = CURRENT_TIMESTAMP
WHERE name = 'Security_SessionTimeoutMinutes';

UPDATE petel_schema.system_attributes SET 
    value = '5',
    updated_at = CURRENT_TIMESTAMP
WHERE name = 'Security_MaxPasswordAttempts';

UPDATE petel_schema.system_attributes SET 
    value = '5',
    updated_at = CURRENT_TIMESTAMP
WHERE name = 'Security_MaxOtpAttempts';

-- Enable OTP by default in test
UPDATE petel_schema.system_attributes SET 
    value = 'true',
    updated_at = CURRENT_TIMESTAMP
WHERE name = 'Security_OtpEnabled';

COMMIT;