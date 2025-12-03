-- =====================================================
-- Migration: Fix time with time zone to timestamp with time zone
-- Purpose: Convert created_at and updated_at fields from time to timestamp
-- Database: PostgreSQL
-- Date: 2024-12-03
-- =====================================================

BEGIN;

-- =====================================================
-- STEP 1: alert_levels table
-- =====================================================
DO $$
BEGIN
    -- Check if column exists and is of type time with time zone
    IF EXISTS (
        SELECT 1 
        FROM information_schema.columns 
        WHERE table_schema = 'petel_schema' 
        AND table_name = 'alert_levels' 
        AND column_name = 'created_at'
        AND data_type = 'time with time zone'
    ) THEN
        RAISE NOTICE 'Fixing alert_levels.created_at...';
        
        -- Add temporary column
        ALTER TABLE petel_schema.alert_levels 
        ADD COLUMN created_at_temp timestamp with time zone;
        
        -- Migrate data: Combine current date with existing time
        UPDATE petel_schema.alert_levels 
        SET created_at_temp = CURRENT_DATE + created_at::time;
        
        -- Drop old column and rename new one
        ALTER TABLE petel_schema.alert_levels DROP COLUMN created_at;
        ALTER TABLE petel_schema.alert_levels RENAME COLUMN created_at_temp TO created_at;
        
        -- Set default
        ALTER TABLE petel_schema.alert_levels 
        ALTER COLUMN created_at SET DEFAULT now();
        
        RAISE NOTICE '✅ alert_levels.created_at fixed';
    ELSE
        RAISE NOTICE '⏭️ alert_levels.created_at already correct or does not exist';
    END IF;
END $$;

-- =====================================================
-- STEP 2: alert_statuses table
-- =====================================================
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 
        FROM information_schema.columns 
        WHERE table_schema = 'petel_schema' 
        AND table_name = 'alert_statuses' 
        AND column_name = 'created_at'
        AND data_type = 'time with time zone'
    ) THEN
        RAISE NOTICE 'Fixing alert_statuses.created_at...';
        
        ALTER TABLE petel_schema.alert_statuses 
        ADD COLUMN created_at_temp timestamp with time zone;
        
        UPDATE petel_schema.alert_statuses 
        SET created_at_temp = CURRENT_DATE + created_at::time;
        
        ALTER TABLE petel_schema.alert_statuses DROP COLUMN created_at;
        ALTER TABLE petel_schema.alert_statuses RENAME COLUMN created_at_temp TO created_at;
        
        ALTER TABLE petel_schema.alert_statuses 
        ALTER COLUMN created_at SET DEFAULT now();
        
        RAISE NOTICE '✅ alert_statuses.created_at fixed';
    ELSE
        RAISE NOTICE '⏭️ alert_statuses.created_at already correct';
    END IF;
END $$;

-- =====================================================
-- STEP 3: alert_types table
-- =====================================================
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 
        FROM information_schema.columns 
        WHERE table_schema = 'petel_schema' 
        AND table_name = 'alert_types' 
        AND column_name = 'created_at'
        AND data_type = 'time with time zone'
    ) THEN
        RAISE NOTICE 'Fixing alert_types.created_at...';
        
        ALTER TABLE petel_schema.alert_types 
        ADD COLUMN created_at_temp timestamp with time zone;
        
        UPDATE petel_schema.alert_types 
        SET created_at_temp = CURRENT_DATE + created_at::time;
        
        ALTER TABLE petel_schema.alert_types DROP COLUMN created_at;
        ALTER TABLE petel_schema.alert_types RENAME COLUMN created_at_temp TO created_at;
        
        ALTER TABLE petel_schema.alert_types 
        ALTER COLUMN created_at SET DEFAULT now();
        
        RAISE NOTICE '✅ alert_types.created_at fixed';
    ELSE
        RAISE NOTICE '⏭️ alert_types.created_at already correct';
    END IF;
END $$;

-- =====================================================
-- STEP 4: document_status_types table
-- =====================================================
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 
        FROM information_schema.columns 
        WHERE table_schema = 'petel_schema' 
        AND table_name = 'document_status_types' 
        AND column_name = 'created_at'
        AND data_type = 'time with time zone'
    ) THEN
        RAISE NOTICE 'Fixing document_status_types.created_at...';
        
        ALTER TABLE petel_schema.document_status_types 
        ADD COLUMN created_at_temp timestamp with time zone;
        
        UPDATE petel_schema.document_status_types 
        SET created_at_temp = CURRENT_DATE + created_at::time;
        
        ALTER TABLE petel_schema.document_status_types DROP COLUMN created_at;
        ALTER TABLE petel_schema.document_status_types RENAME COLUMN created_at_temp TO created_at;
        
        ALTER TABLE petel_schema.document_status_types 
        ALTER COLUMN created_at SET DEFAULT now();
        
        RAISE NOTICE '✅ document_status_types.created_at fixed';
    ELSE
        RAISE NOTICE '⏭️ document_status_types.created_at already correct';
    END IF;
END $$;

-- =====================================================
-- STEP 5: school_additional_study_programs table
-- =====================================================
-- Fix created_at
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 
        FROM information_schema.columns 
        WHERE table_schema = 'petel_schema' 
        AND table_name = 'school_additional_study_programs' 
        AND column_name = 'created_at'
        AND data_type = 'time with time zone'
    ) THEN
        RAISE NOTICE 'Fixing school_additional_study_programs.created_at...';
        
        ALTER TABLE petel_schema.school_additional_study_programs 
        ADD COLUMN created_at_temp timestamp with time zone;
        
        UPDATE petel_schema.school_additional_study_programs 
        SET created_at_temp = CURRENT_DATE + created_at::time;
        
        ALTER TABLE petel_schema.school_additional_study_programs DROP COLUMN created_at;
        ALTER TABLE petel_schema.school_additional_study_programs RENAME COLUMN created_at_temp TO created_at;
        
        ALTER TABLE petel_schema.school_additional_study_programs 
        ALTER COLUMN created_at SET DEFAULT now();
        ALTER TABLE petel_schema.school_additional_study_programs 
        ALTER COLUMN created_at SET NOT NULL;
        
        RAISE NOTICE '✅ school_additional_study_programs.created_at fixed';
    ELSE
        RAISE NOTICE '⏭️ school_additional_study_programs.created_at already correct';
    END IF;
END $$;

-- Fix updated_at
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 
        FROM information_schema.columns 
        WHERE table_schema = 'petel_schema' 
        AND table_name = 'school_additional_study_programs' 
        AND column_name = 'updated_at'
        AND data_type = 'time with time zone'
    ) THEN
        RAISE NOTICE 'Fixing school_additional_study_programs.updated_at...';
        
        ALTER TABLE petel_schema.school_additional_study_programs 
        ADD COLUMN updated_at_temp timestamp with time zone;
        
        UPDATE petel_schema.school_additional_study_programs 
        SET updated_at_temp = CURRENT_DATE + updated_at::time;
        
        ALTER TABLE petel_schema.school_additional_study_programs DROP COLUMN updated_at;
        ALTER TABLE petel_schema.school_additional_study_programs RENAME COLUMN updated_at_temp TO updated_at;
        
        ALTER TABLE petel_schema.school_additional_study_programs 
        ALTER COLUMN updated_at SET DEFAULT now();
        ALTER TABLE petel_schema.school_additional_study_programs 
        ALTER COLUMN updated_at SET NOT NULL;
        
        RAISE NOTICE '✅ school_additional_study_programs.updated_at fixed';
    ELSE
        RAISE NOTICE '⏭️ school_additional_study_programs.updated_at already correct';
    END IF;
END $$;

-- =====================================================
-- STEP 6: school_attribute_types_values table
-- =====================================================
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 
        FROM information_schema.columns 
        WHERE table_schema = 'petel_schema' 
        AND table_name = 'school_attribute_types_values' 
        AND column_name = 'created_at'
        AND data_type = 'time with time zone'
    ) THEN
        RAISE NOTICE 'Fixing school_attribute_types_values.created_at...';
        
        ALTER TABLE petel_schema.school_attribute_types_values 
        ADD COLUMN created_at_temp timestamp with time zone;
        
        UPDATE petel_schema.school_attribute_types_values 
        SET created_at_temp = CASE 
            WHEN created_at IS NOT NULL THEN CURRENT_DATE + created_at::time
            ELSE NULL
        END;
        
        ALTER TABLE petel_schema.school_attribute_types_values DROP COLUMN created_at;
        ALTER TABLE petel_schema.school_attribute_types_values RENAME COLUMN created_at_temp TO created_at;
        
        RAISE NOTICE '✅ school_attribute_types_values.created_at fixed';
    ELSE
        RAISE NOTICE '⏭️ school_attribute_types_values.created_at already correct';
    END IF;
END $$;

-- =====================================================
-- STEP 7: school_attributes table
-- =====================================================
-- Fix created_at
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 
        FROM information_schema.columns 
        WHERE table_schema = 'petel_schema' 
        AND table_name = 'school_attributes' 
        AND column_name = 'created_at'
        AND data_type = 'time with time zone'
    ) THEN
        RAISE NOTICE 'Fixing school_attributes.created_at...';
        
        ALTER TABLE petel_schema.school_attributes 
        ADD COLUMN created_at_temp timestamp with time zone;
        
        UPDATE petel_schema.school_attributes 
        SET created_at_temp = CURRENT_DATE + created_at::time;
        
        ALTER TABLE petel_schema.school_attributes DROP COLUMN created_at;
        ALTER TABLE petel_schema.school_attributes RENAME COLUMN created_at_temp TO created_at;
        
        ALTER TABLE petel_schema.school_attributes 
        ALTER COLUMN created_at SET DEFAULT now();
        
        RAISE NOTICE '✅ school_attributes.created_at fixed';
    ELSE
        RAISE NOTICE '⏭️ school_attributes.created_at already correct';
    END IF;
END $$;

-- Fix updated_at
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 
        FROM information_schema.columns 
        WHERE table_schema = 'petel_schema' 
        AND table_name = 'school_attributes' 
        AND column_name = 'updated_at'
        AND data_type = 'time with time zone'
    ) THEN
        RAISE NOTICE 'Fixing school_attributes.updated_at...';
        
        ALTER TABLE petel_schema.school_attributes 
        ADD COLUMN updated_at_temp timestamp with time zone;
        
        UPDATE petel_schema.school_attributes 
        SET updated_at_temp = CURRENT_DATE + updated_at::time;
        
        ALTER TABLE petel_schema.school_attributes DROP COLUMN updated_at;
        ALTER TABLE petel_schema.school_attributes RENAME COLUMN updated_at_temp TO updated_at;
        
        ALTER TABLE petel_schema.school_attributes 
        ALTER COLUMN updated_at SET DEFAULT now();
        
        RAISE NOTICE '✅ school_attributes.updated_at fixed';
    ELSE
        RAISE NOTICE '⏭️ school_attributes.updated_at already correct';
    END IF;
END $$;

-- =====================================================
-- STEP 8: school_attributes_types table
-- =====================================================
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 
        FROM information_schema.columns 
        WHERE table_schema = 'petel_schema' 
        AND table_name = 'school_attributes_types' 
        AND column_name = 'created_at'
        AND data_type = 'time with time zone'
    ) THEN
        RAISE NOTICE 'Fixing school_attributes_types.created_at...';
        
        ALTER TABLE petel_schema.school_attributes_types 
        ADD COLUMN created_at_temp timestamp with time zone;
        
        UPDATE petel_schema.school_attributes_types 
        SET created_at_temp = CASE 
            WHEN created_at IS NOT NULL THEN CURRENT_DATE + created_at::time
            ELSE NULL
        END;
        
        ALTER TABLE petel_schema.school_attributes_types DROP COLUMN created_at;
        ALTER TABLE petel_schema.school_attributes_types RENAME COLUMN created_at_temp TO created_at;
        
        RAISE NOTICE '✅ school_attributes_types.created_at fixed';
    ELSE
        RAISE NOTICE '⏭️ school_attributes_types.created_at already correct';
    END IF;
END $$;

-- =====================================================
-- STEP 9: school_tracks table
-- =====================================================
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 
        FROM information_schema.columns 
        WHERE table_schema = 'petel_schema' 
        AND table_name = 'school_tracks' 
        AND column_name = 'created_at'
        AND data_type = 'time with time zone'
    ) THEN
        RAISE NOTICE 'Fixing school_tracks.created_at...';
        
        ALTER TABLE petel_schema.school_tracks 
        ADD COLUMN created_at_temp timestamp with time zone;
        
        UPDATE petel_schema.school_tracks 
        SET created_at_temp = CURRENT_DATE + created_at::time;
        
        ALTER TABLE petel_schema.school_tracks DROP COLUMN created_at;
        ALTER TABLE petel_schema.school_tracks RENAME COLUMN created_at_temp TO created_at;
        
        ALTER TABLE petel_schema.school_tracks 
        ALTER COLUMN created_at SET DEFAULT now();
        ALTER TABLE petel_schema.school_tracks 
        ALTER COLUMN created_at SET NOT NULL;
        
        RAISE NOTICE '✅ school_tracks.created_at fixed';
    ELSE
        RAISE NOTICE '⏭️ school_tracks.created_at already correct';
    END IF;
END $$;

-- =====================================================
-- VERIFICATION: Check all fixed columns
-- =====================================================
DO $$
DECLARE
    incorrect_count INTEGER;
BEGIN
    RAISE NOTICE '';
    RAISE NOTICE '==============================================';
    RAISE NOTICE 'VERIFICATION: Checking for remaining time with time zone columns';
    RAISE NOTICE '==============================================';
    
    SELECT COUNT(*) INTO incorrect_count
    FROM information_schema.columns 
    WHERE table_schema = 'petel_schema' 
    AND column_name IN ('created_at', 'updated_at')
    AND data_type = 'time with time zone';
    
    IF incorrect_count = 0 THEN
        RAISE NOTICE '✅ SUCCESS: All created_at and updated_at columns are now timestamp with time zone';
    ELSE
        RAISE WARNING '⚠️ WARNING: Still found % columns with incorrect type', incorrect_count;
        
        -- List remaining incorrect columns
        RAISE NOTICE 'Remaining incorrect columns:';
        FOR rec IN 
            SELECT table_name, column_name, data_type
            FROM information_schema.columns 
            WHERE table_schema = 'petel_schema' 
            AND column_name IN ('created_at', 'updated_at')
            AND data_type = 'time with time zone'
        LOOP
            RAISE NOTICE '  - %.% (%)', rec.table_name, rec.column_name, rec.data_type;
        END LOOP;
    END IF;
END $$;

-- =====================================================
-- SUMMARY: Show all created_at and updated_at columns
-- =====================================================
DO $$
BEGIN
    RAISE NOTICE '';
    RAISE NOTICE '==============================================';
    RAISE NOTICE 'SUMMARY: All created_at and updated_at columns';
    RAISE NOTICE '==============================================';
END $$;

SELECT 
    table_name,
    column_name,
    data_type,
    is_nullable,
    column_default
FROM information_schema.columns 
WHERE table_schema = 'petel_schema' 
AND column_name IN ('created_at', 'updated_at')
ORDER BY table_name, column_name;

COMMIT;

-- =====================================================
-- ROLLBACK INSTRUCTIONS
-- =====================================================
-- If you need to rollback this migration:
-- 1. Restore from backup
-- OR
-- 2. Manually convert timestamp back to time (DATA LOSS WARNING)
-- Example rollback command (NOT RECOMMENDED - loses date information):
-- ALTER TABLE petel_schema.alert_levels ALTER COLUMN created_at TYPE time with time zone;
-- =====================================================