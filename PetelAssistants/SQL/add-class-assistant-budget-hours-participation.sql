-- =============================================================================
-- PetelAssistants — Add ministry_participation_pct to class assistant budget hours
-- Field on each pricing record keyed by year × school_level × classification.
-- Calculate multiplies configured hours by entitlement participation % / 100.
-- Idempotent — safe to run multiple times.
-- Run after add-class-assistant-budget-hours.sql
-- =============================================================================

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'shared_schema'
          AND table_name = 'class_assistant_budget_hours'
          AND column_name = 'ministry_participation_pct'
    ) THEN
        ALTER TABLE shared_schema.class_assistant_budget_hours
            ADD COLUMN ministry_participation_pct NUMERIC(5,2) NOT NULL DEFAULT 100;

        RAISE NOTICE 'Added ministry_participation_pct (default 100)';
    ELSE
        RAISE NOTICE 'ministry_participation_pct already exists';
    END IF;
END $$;

-- If a prior version added participation into the unique key, collapse duplicates
-- (keep highest participation, prefer 100) then restore unique on year/level/classification.
DO $$
BEGIN
    ALTER TABLE shared_schema.class_assistant_budget_hours
        DROP CONSTRAINT IF EXISTS class_assistant_budget_hours_unique;

    -- Remove duplicate rows for the same year/level/classification (keep one)
    DELETE FROM shared_schema.class_assistant_budget_hours a
    USING shared_schema.class_assistant_budget_hours b
    WHERE a.hebrew_year_id = b.hebrew_year_id
      AND a.school_level = b.school_level
      AND a.class_classification_id = b.class_classification_id
      AND a.id < b.id;

    ALTER TABLE shared_schema.class_assistant_budget_hours
        ADD CONSTRAINT class_assistant_budget_hours_unique
        UNIQUE (hebrew_year_id, school_level, class_classification_id);

    ALTER TABLE shared_schema.class_assistant_budget_hours
        DROP CONSTRAINT IF EXISTS class_assistant_budget_hours_participation_check;

    ALTER TABLE shared_schema.class_assistant_budget_hours
        ADD CONSTRAINT class_assistant_budget_hours_participation_check
        CHECK (ministry_participation_pct >= 0 AND ministry_participation_pct <= 100);

    ALTER TABLE shared_schema.class_assistant_budget_hours
        ALTER COLUMN ministry_participation_pct DROP DEFAULT;

    RAISE NOTICE 'Unique key is year/school_level/classification; participation is a record field';
END $$;
