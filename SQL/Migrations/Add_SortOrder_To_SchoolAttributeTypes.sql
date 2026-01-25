-- =============================================
-- Add sort_order column to school_attributes_types table
-- 
-- Purpose: Fix missing sort_order column causing attributes 
-- not to display in test environment
-- =============================================

DO $$
BEGIN
    -- Check if sort_order column exists
    IF NOT EXISTS (
        SELECT 1 
        FROM information_schema.columns 
        WHERE table_schema = 'petel_schema' 
        AND table_name = 'school_attributes_types' 
        AND column_name = 'sort_order'
    ) THEN
        -- Add the sort_order column
        ALTER TABLE petel_schema.school_attributes_types 
        ADD COLUMN sort_order INTEGER NOT NULL DEFAULT 0;
        
        RAISE NOTICE '✅ Added sort_order column to school_attributes_types';
    ELSE
        RAISE NOTICE '✓ sort_order column already exists';
    END IF;

    -- Update existing rows with reasonable sort order values if needed
    UPDATE petel_schema.school_attributes_types
    SET sort_order = id * 10
    WHERE sort_order = 0;

    RAISE NOTICE '✅ Migration completed successfully';

EXCEPTION
    WHEN OTHERS THEN
        RAISE EXCEPTION 'Error adding sort_order column: %', SQLERRM;
END;
$$;

-- Verify the column exists
SELECT column_name, data_type, column_default
FROM information_schema.columns
WHERE table_schema = 'petel_schema'
AND table_name = 'school_attributes_types'
AND column_name = 'sort_order';
