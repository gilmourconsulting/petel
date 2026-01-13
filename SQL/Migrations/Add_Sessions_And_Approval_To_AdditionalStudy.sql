-- ============================================================================
-- Migration: Add Number of Sessions, Approval Status, and Calculation Mode
-- to school_additional_study_programs table
-- Date: 2026-01-13
-- ============================================================================

BEGIN;

DO $$
BEGIN
    RAISE NOTICE '🚀 Starting migration: Add sessions and approval fields to additional study programs';

    -- ========================================================================
    -- STEP 1: Add number_of_sessions column
    -- ========================================================================
    
    IF NOT EXISTS (
        SELECT 1 
        FROM information_schema.columns 
        WHERE table_schema = 'petel_schema' 
        AND table_name = 'school_additional_study_programs' 
        AND column_name = 'number_of_sessions'
    ) THEN
        RAISE NOTICE '📝 Adding number_of_sessions column...';
        
        ALTER TABLE petel_schema.school_additional_study_programs 
        ADD COLUMN number_of_sessions INTEGER NULL;
        
        -- Set default value for existing records (assume 30 sessions if not specified)
        UPDATE petel_schema.school_additional_study_programs 
        SET number_of_sessions = 30 
        WHERE number_of_sessions IS NULL;
        
        -- Make column NOT NULL after setting defaults
        ALTER TABLE petel_schema.school_additional_study_programs 
        ALTER COLUMN number_of_sessions SET NOT NULL;
        
        ALTER TABLE petel_schema.school_additional_study_programs 
        ALTER COLUMN number_of_sessions SET DEFAULT 30;
        
        COMMENT ON COLUMN petel_schema.school_additional_study_programs.number_of_sessions 
        IS 'Number of sessions/meetings for this program (מספר מפגשים)';
        
        RAISE NOTICE '✅ number_of_sessions column added successfully';
    ELSE
        RAISE NOTICE '⏭️ number_of_sessions column already exists, skipping';
    END IF;

    -- ========================================================================
    -- STEP 2: Add approval_status column
    -- ========================================================================
    
    IF NOT EXISTS (
        SELECT 1 
        FROM information_schema.columns 
        WHERE table_schema = 'petel_schema' 
        AND table_name = 'school_additional_study_programs' 
        AND column_name = 'approval_status'
    ) THEN
        RAISE NOTICE '📝 Adding approval_status column...';
        
        ALTER TABLE petel_schema.school_additional_study_programs 
        ADD COLUMN approval_status INTEGER NULL;
        
        -- Set default value for existing records (0 = not approved)
        UPDATE petel_schema.school_additional_study_programs 
        SET approval_status = 0 
        WHERE approval_status IS NULL;
        
        -- Make column NOT NULL after setting defaults
        ALTER TABLE petel_schema.school_additional_study_programs 
        ALTER COLUMN approval_status SET NOT NULL;
        
        ALTER TABLE petel_schema.school_additional_study_programs 
        ALTER COLUMN approval_status SET DEFAULT 0;
        
        -- Add check constraint for valid values (0=not approved, 1=approved, 2=exception)
        ALTER TABLE petel_schema.school_additional_study_programs 
        ADD CONSTRAINT chk_approval_status 
        CHECK (approval_status IN (0, 1, 2));
        
        COMMENT ON COLUMN petel_schema.school_additional_study_programs.approval_status 
        IS 'Approval status: 0=לא מאושר (not approved), 1=מאושר (approved), 2=אישור חריג (exception)';
        
        RAISE NOTICE '✅ approval_status column added successfully';
    ELSE
        RAISE NOTICE '⏭️ approval_status column already exists, skipping';
    END IF;

    -- ========================================================================
    -- STEP 3: Add calculate_by_hourly_cost column
    -- ========================================================================
    
    IF NOT EXISTS (
        SELECT 1 
        FROM information_schema.columns 
        WHERE table_schema = 'petel_schema' 
        AND table_name = 'school_additional_study_programs' 
        AND column_name = 'calculate_by_hourly_cost'
    ) THEN
        RAISE NOTICE '📝 Adding calculate_by_hourly_cost column...';
        
        ALTER TABLE petel_schema.school_additional_study_programs 
        ADD COLUMN calculate_by_hourly_cost BOOLEAN NULL;
        
        -- Set default value for existing records (false = total cost mode, current behavior)
        UPDATE petel_schema.school_additional_study_programs 
        SET calculate_by_hourly_cost = false 
        WHERE calculate_by_hourly_cost IS NULL;
        
        -- Make column NOT NULL after setting defaults
        ALTER TABLE petel_schema.school_additional_study_programs 
        ALTER COLUMN calculate_by_hourly_cost SET NOT NULL;
        
        ALTER TABLE petel_schema.school_additional_study_programs 
        ALTER COLUMN calculate_by_hourly_cost SET DEFAULT false;
        
        COMMENT ON COLUMN petel_schema.school_additional_study_programs.calculate_by_hourly_cost 
        IS 'Calculation mode: false=calculate hourly from total, true=calculate total from hourly (עלות שעתית או עלות כוללת)';
        
        RAISE NOTICE '✅ calculate_by_hourly_cost column added successfully';
    ELSE
        RAISE NOTICE '⏭️ calculate_by_hourly_cost column already exists, skipping';
    END IF;

    -- ========================================================================
    -- STEP 4: Create index for approval_status filtering
    -- ========================================================================
    
    IF NOT EXISTS (
        SELECT 1 
        FROM pg_indexes 
        WHERE schemaname = 'petel_schema' 
        AND tablename = 'school_additional_study_programs' 
        AND indexname = 'idx_school_additional_study_programs_approval_status'
    ) THEN
        RAISE NOTICE '📝 Creating index on approval_status...';
        
        CREATE INDEX idx_school_additional_study_programs_approval_status 
        ON petel_schema.school_additional_study_programs(approval_status);
        
        RAISE NOTICE '✅ Index created successfully';
    ELSE
        RAISE NOTICE '⏭️ Index already exists, skipping';
    END IF;

    RAISE NOTICE '🎉 Migration completed successfully!';
    
END $$;

COMMIT;

-- ============================================================================
-- Verification Queries
-- ============================================================================

-- Verify new columns exist
SELECT 
    column_name,
    data_type,
    column_default,
    is_nullable
FROM information_schema.columns
WHERE table_schema = 'petel_schema'
AND table_name = 'school_additional_study_programs'
AND column_name IN ('number_of_sessions', 'approval_status', 'calculate_by_hourly_cost')
ORDER BY column_name;

-- Show sample data with new columns
SELECT 
    id,
    name,
    number_of_sessions,
    approval_status,
    calculate_by_hourly_cost,
    cost,
    hourly_cost
FROM petel_schema.school_additional_study_programs
WHERE is_last_version = true
LIMIT 5;
