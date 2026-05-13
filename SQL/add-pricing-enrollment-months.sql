-- Migration: Add enrollment_months to school_students
--             Add full_price to school_student_pricing_elements
-- Run once on each environment. Idempotent.

DO $$
BEGIN
    -- 1. enrollment_months on school_students
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'petel_schema'
          AND table_name   = 'school_students'
          AND column_name  = 'enrollment_months'
    ) THEN
        ALTER TABLE petel_schema.school_students
            ADD COLUMN enrollment_months INTEGER NULL;
        RAISE NOTICE 'Added enrollment_months to school_students';
    ELSE
        RAISE NOTICE 'enrollment_months already exists on school_students';
    END IF;

    -- 2. full_price on school_student_pricing_elements
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'petel_schema'
          AND table_name   = 'school_student_pricing_elements'
          AND column_name  = 'full_price'
    ) THEN
        ALTER TABLE petel_schema.school_student_pricing_elements
            ADD COLUMN full_price NUMERIC(18,4) NOT NULL DEFAULT 0;
        RAISE NOTICE 'Added full_price to school_student_pricing_elements';
    ELSE
        RAISE NOTICE 'full_price already exists on school_student_pricing_elements';
    END IF;
END
$$;
