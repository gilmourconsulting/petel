-- Password policy system attribute (single regex)
-- Run this on all environments (dev, test, staging, production)
--
-- Security_PasswordPolicy  - ECMAScript-compatible regex the password must satisfy
--
-- The default below requires:
--   • at least one lowercase letter  (a-z)
--   • at least one uppercase letter  (A-Z)
--   • at least one digit             (0-9)
--   • at least one special character (@$!%*?&)
--   • total length 6-20 characters
--
-- To tighten/loosen the rules: UPDATE the value column and then call
--   POST /api/systemattributes/reload  to refresh the in-memory cache.

-- Widen value column first (safe even if column is already wider)
ALTER TABLE petel_schema.system_attributes
    ALTER COLUMN value TYPE varchar(200);

INSERT INTO petel_schema.system_attributes (name, description, value, value_type)
VALUES (
    'Security_PasswordPolicy',
    'תבנית סיסמה (regex)',
    '^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{6,20}$',
    'string'
)
ON CONFLICT (name) DO NOTHING;
