-- Production Environment Configuration
-- STRICT rate limiting and security for production
-- Conservative settings prioritizing security and system stability

INSERT INTO petel_schema.system_attributes (name, description, value, value_type, created_user, sort_order) VALUES 

-- Rate Limiting - STRICT production security
('RateLimit_Enabled', 'Enable/disable rate limiting system-wide', 'true', 'boolean', 1, 300),
('RateLimit_LoginLimit', 'Maximum login attempts per 15 minutes', '5', 'integer', 1, 301),
('RateLimit_LoginPeriod', 'Login attempts rate limit period', '15m', 'string', 1, 302),
('RateLimit_OtpLimit', 'Maximum OTP validation attempts per 15 minutes', '3', 'integer', 1, 303),
('RateLimit_OtpPeriod', 'OTP validation rate limit period', '15m', 'string', 1, 304),
('RateLimit_ApiLimit', 'API requests limit per minute', '60', 'integer', 1, 305),
('RateLimit_ApiPeriod', 'API requests rate limit period', '1m', 'string', 1, 306),  
('RateLimit_HourlyLimit', 'API requests limit per hour', '2000', 'integer', 1, 307),
('RateLimit_PostLimit', 'POST requests limit per minute', '30', 'integer', 1, 308),
('RateLimit_PutLimit', 'PUT requests limit per minute', '20', 'integer', 1, 309),
('RateLimit_DeleteLimit', 'DELETE requests limit per minute', '10', 'integer', 1, 310),
('RateLimit_GetLimit', 'GET requests limit per minute', '60', 'integer', 1, 311),

-- System Configuration - Production Settings
('System_MaintenanceMode', 'Put system in maintenance mode', 'false', 'boolean', 1, 400),
('System_MaintenanceMessage', 'Message displayed during maintenance', 'המערכת נמצאת בתחזוקה מתוכננת. אנא נסו שוב בעוד מספר דקות. תודה על הסבלנות.', 'string', 1, 401),
('System_Environment', 'Current environment identifier', 'production', 'string', 1, 402),
('System_DetailedLogging', 'Enable detailed API request/response logging', 'false', 'boolean', 1, 403),

-- Security Settings - Production (strict)
('Security_OtpIssuer', 'OTP issuer name displayed in authenticator apps', 'מערכת פתל - סטודנטים חיצוניים', 'string', 1)

ON CONFLICT (name) DO UPDATE SET
    description = EXCLUDED.description,
    value = EXCLUDED.value,
    value_type = EXCLUDED.value_type,
    updated_at = CURRENT_TIMESTAMP;

-- Update existing security settings for STRICT production security
UPDATE petel_schema.system_attributes SET 
    value = '30',
    updated_at = CURRENT_TIMESTAMP
WHERE name = 'Security_SessionTimeoutMinutes';

UPDATE petel_schema.system_attributes SET 
    value = '3',
    updated_at = CURRENT_TIMESTAMP
WHERE name = 'Security_MaxPasswordAttempts';

UPDATE petel_schema.system_attributes SET 
    value = '3',
    updated_at = CURRENT_TIMESTAMP
WHERE name = 'Security_MaxOtpAttempts';

-- REQUIRE OTP in production
UPDATE petel_schema.system_attributes SET 
    value = 'true',
    updated_at = CURRENT_TIMESTAMP
WHERE name = 'Security_OtpEnabled';

-- Reduce password expiration for better security
UPDATE petel_schema.system_attributes SET 
    value = '3',
    updated_at = CURRENT_TIMESTAMP
WHERE name = 'Security_PasswordExpirationMonths';

COMMIT;