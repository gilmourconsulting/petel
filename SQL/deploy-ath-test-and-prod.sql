-- ATH schema for test + production (petel_schema)
-- Run on each ATH database after the 2026-08-19 deploy. Idempotent.
-- Combines: add-student-created-user.sql + add-student-include-in-council-summary.sql
--
-- Order: add columns first, then replace council_summary_vw
--   (the view reads include_in_council_summary).

-- 1. created_user on school_students
-- created_at already exists (DB default now()). This adds the creator user FK
-- so student version history can show who created each version.

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'petel_schema'
          AND table_name   = 'school_students'
          AND column_name  = 'created_user'
    ) THEN
        ALTER TABLE petel_schema.school_students
            ADD COLUMN created_user INTEGER NULL REFERENCES petel_schema.users(id) ON DELETE SET NULL;
        RAISE NOTICE 'Added created_user to school_students';
    ELSE
        RAISE NOTICE 'created_user already exists on school_students';
    END IF;
END
$$;

-- 2. include_in_council_summary on school_students
-- Historical split-council periods stay off the students list (is_last_version = false)
-- but still count in council summary and council calculations.

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'petel_schema'
          AND table_name   = 'school_students'
          AND column_name  = 'include_in_council_summary'
    ) THEN
        ALTER TABLE petel_schema.school_students
            ADD COLUMN include_in_council_summary BOOLEAN NOT NULL DEFAULT false;
        RAISE NOTICE 'Added include_in_council_summary to school_students';
    ELSE
        RAISE NOTICE 'include_in_council_summary already exists on school_students';
    END IF;
END
$$;

-- 3. council_summary_vw — last-version students plus flagged historical split periods

CREATE OR REPLACE VIEW petel_schema.council_summary_vw AS
SELECT
    c.id AS council_id,
    c.council_short_name AS council_name,
    sy.year_id,
    sch.owner AS owner_id,
    e.name AS owner_name,
    COUNT(DISTINCT ss.master_student_id) AS number_of_students,
    COALESCE(SUM(ss.cost), 0) AS total_requested_amount
FROM
    petel_schema.councils c
    INNER JOIN petel_schema.school_students ss
        ON c.id = ss.sending_council
        AND (ss.is_last_version = true OR ss.include_in_council_summary = true)
    INNER JOIN petel_schema.school_years sy ON ss.school_year_id = sy.id
    INNER JOIN petel_schema.schools sch ON ss.school_year_id = sch.school_year_id AND sch.is_last_version = true
    LEFT JOIN petel_schema.entities e ON sch.owner = e.id
WHERE
    sy.year_id IS NOT NULL
GROUP BY
    c.id,
    c.council_short_name,
    sy.year_id,
    sch.owner,
    e.name;

ALTER VIEW petel_schema.council_summary_vw OWNER TO "PetelAdmin";
