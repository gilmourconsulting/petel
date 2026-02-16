-- ============================================
-- Add JWT Configuration to System Attributes
-- ============================================
-- This migration adds JWT settings to system_attributes table
-- so JWT configuration becomes database-driven
-- Run in: DEV, TEST, PROD
-- ============================================

BEGIN;

-- Add JWT configuration attributes with explicit IDs
INSERT INTO petel_schema.system_attributes (id, name, description, value, value_type) VALUES 
    (10000, 'JWT_Issuer', 'JWT Token Issuer', 'Petel ATH', 'string'),
    (10001, 'JWT_Audience', 'JWT Token Audience', 'PetelAppUsers', 'string'),
    (10002, 'JWT_ExpirationHours', 'JWT Expiration (Hours)', '8', 'integer'),
    (10003, 'JWT_SecretKey', 'JWT Secret Key', 'LOADED_FROM_KEY_VAULT', 'string')
ON CONFLICT (name) DO UPDATE SET
    description = EXCLUDED.description,
    value = EXCLUDED.value,
    value_type = EXCLUDED.value_type,
    updated_at = CURRENT_TIMESTAMP;

-- Display created attributes
SELECT id, name, description, value, value_type
FROM petel_schema.system_attributes
WHERE name LIKE 'JWT_%'
ORDER BY name;

COMMIT;

-- ============================================
-- NOTES:
-- ============================================
-- 1. JWT_Issuer and JWT_Audience are now loaded from database
-- 2. Config file values serve as fallback defaults
-- 3. JWT_SecretKey is still loaded from Azure Key Vault (not from database)
-- 4. Changes take effect after API restart
-- ============================================
