-- Active: 1763630171565@@petel-test-db.postgres.database.azure.com@5432
-- Add master_student_id column to track students across encrypted id_number changes
-- This ensures we maintain history traceability even with encrypted identifiers

-- Step 1: Add the column (nullable initially for data migration)
ALTER TABLE petel_schema.school_students 
ADD COLUMN master_student_id INTEGER;

-- Step 2: Create index for performance
CREATE INDEX idx_school_students_master_id 
ON petel_schema.school_students(master_student_id);

-- Step 3: Populate master_student_id for existing records
-- Group by id_number and assign the FIRST id as the master_student_id for all versions
WITH student_groups AS (
    SELECT 
        id,
        first_name,
        last_name,
        school_year_id,
        MIN(id) OVER (PARTITION BY first_name, last_name, school_year_id ORDER BY version) as master_id
    FROM petel_schema.school_students
)
UPDATE petel_schema.school_students ss
SET master_student_id = sg.master_id
FROM student_groups sg
WHERE ss.id = sg.id;

-- Step 4: Make column NOT NULL after population
ALTER TABLE petel_schema.school_students 
ALTER COLUMN master_student_id SET NOT NULL;

-- Step 5: Add comment
COMMENT ON COLUMN petel_schema.school_students.master_student_id 
IS 'Master student ID - remains constant across all versions of a student record, used for history tracking with encrypted id_number';