-- Add SystemVersion attribute to system_attributes table
-- This attribute stores the application version number displayed on the login page

INSERT INTO petel_schema.system_attributes (name, value, value_type, description, foreign_id)
VALUES ('SystemVersion', '1.0', 'string', 'גרסת המערכת המוצגת בדף ההתחברות', NULL)
ON CONFLICT (name) DO UPDATE 
SET value = EXCLUDED.value,
    description = EXCLUDED.description,
    updated_at = CURRENT_TIMESTAMP;

-- Verify the insert
SELECT * FROM petel_schema.system_attributes WHERE name = 'SystemVersion';
