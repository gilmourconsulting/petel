-- Migration: Create school_year_attributes table
-- Purpose: Store attributes that vary per school year (e.g., required sessions for additional study programs)
-- Author: System
-- Date: 2026-01-13

-- Create table if not exists
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT FROM pg_tables
        WHERE schemaname = 'petel_schema'
        AND tablename = 'school_year_attributes'
    ) THEN
        CREATE TABLE petel_schema.school_year_attributes (
            id SERIAL PRIMARY KEY,
            year_id INTEGER NOT NULL REFERENCES petel_schema.hebrew_years(id) ON DELETE CASCADE,
            name VARCHAR(100) NOT NULL,
            description VARCHAR(200) NULL,
            value VARCHAR(500) NOT NULL,
            created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
            created_user INTEGER NULL REFERENCES petel_schema.users(id) ON DELETE SET NULL,
            updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
            update_user INTEGER NULL REFERENCES petel_schema.users(id) ON DELETE SET NULL,
            CONSTRAINT uk_year_attribute UNIQUE (year_id, name)
        );

        -- Add indexes for faster lookups
        CREATE INDEX idx_school_year_attributes_year_id ON petel_schema.school_year_attributes(year_id);
        CREATE INDEX idx_school_year_attributes_name ON petel_schema.school_year_attributes(name);
        CREATE INDEX idx_school_year_attributes_created_user ON petel_schema.school_year_attributes(created_user);
        CREATE INDEX idx_school_year_attributes_update_user ON petel_schema.school_year_attributes(update_user);

        RAISE NOTICE 'Table school_year_attributes created successfully';
    ELSE
        RAISE NOTICE 'Table school_year_attributes already exists';
    END IF;
END
$$;

-- Insert initial data for "additional study sessions required" attribute
-- מפגשי תל"ן נדרשים
INSERT INTO petel_schema.school_year_attributes (year_id, name, description, value)
VALUES 
    (102, 'additional_study_sessions_required', 'מפגשי תל"ן נדרשים', '32'),
    (101, 'additional_study_sessions_required', 'מפגשי תל"ן נדרשים', '35')
ON CONFLICT (year_id, name) DO NOTHING;

-- Verification query
SELECT 
    sya.id,
    hy.year_name,
    sya.name,
    sya.description,
    sya.value,
    sya.created_at,
    sya.created_user,
    sya.updated_at,
    sya.update_user
FROM petel_schema.school_year_attributes sya
INNER JOIN petel_schema.hebrew_years hy ON sya.year_id = hy.id
ORDER BY hy.year_name DESC, sya.name;
