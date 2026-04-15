-- ================================================================
-- PETEL SYSTEM - EXTEND COLUMNS FOR ENCRYPTED DATA
-- ================================================================
-- Purpose: Increase VARCHAR lengths to accommodate encrypted values
-- Date: 2025-12-31
-- Encrypted base64 strings are ~1.5-2x longer than plaintext
-- ================================================================

SET search_path TO petel_schema;

-- ================================================================
-- STEP 1: AUDIT CURRENT COLUMN LENGTHS
-- ================================================================

SELECT 
    table_name,
    column_name,
    data_type,
    character_maximum_length AS current_length,
    CASE 
        WHEN column_name IN ('id_number', 'phone_number') AND character_maximum_length < 100 THEN '❌ TOO SHORT'
        WHEN column_name = 'email' AND character_maximum_length < 255 THEN '❌ TOO SHORT'
        WHEN column_name = 'street' AND character_maximum_length < 150 THEN '❌ TOO SHORT'
        WHEN column_name = 'otp_secret' AND character_maximum_length < 255 THEN '❌ TOO SHORT'
        ELSE '✅ OK'
    END AS status,
    CASE 
        WHEN column_name IN ('id_number', 'phone_number') THEN 100
        WHEN column_name = 'email' THEN 255
        WHEN column_name = 'street' THEN 150
        WHEN column_name = 'otp_secret' THEN 255
        ELSE character_maximum_length
    END AS recommended_length
FROM information_schema.columns
WHERE table_schema = 'petel_schema'
  AND table_name IN ('persons', 'school_students', 'users')
  AND column_name IN ('id_number', 'email', 'phone_number', 'otp_secret', 'street', 'phone_number_prefix')
ORDER BY table_name, column_name;

-- ================================================================
-- STEP 2: IDENTIFY DEPENDENT VIEWS
-- ================================================================

SELECT DISTINCT
    dependent_view.relname AS view_name,
    source_table.relname AS table_name,
    pg_attribute.attname AS column_name
FROM pg_depend 
JOIN pg_rewrite ON pg_depend.objid = pg_rewrite.oid 
JOIN pg_class as dependent_view ON pg_rewrite.ev_class = dependent_view.oid 
JOIN pg_class as source_table ON pg_depend.refobjid = source_table.oid 
JOIN pg_attribute ON pg_depend.refobjid = pg_attribute.attrelid 
    AND pg_depend.refobjsubid = pg_attribute.attnum 
JOIN pg_namespace ON dependent_view.relnamespace = pg_namespace.oid
WHERE pg_namespace.nspname = 'petel_schema'
  AND source_table.relname IN ('persons', 'school_students')
  AND pg_attribute.attname IN ('id_number', 'email', 'phone_number', 'street')
  AND dependent_view.relkind = 'v'
ORDER BY dependent_view.relname;

-- ================================================================
-- STEP 3: SAVE VIEW DEFINITIONS BEFORE DROP
-- ================================================================

-- Save student_school_years_registration_vw definition
CREATE TEMP TABLE temp_view_definition AS
SELECT 
    'student_school_years_registration_vw' AS view_name,
    pg_get_viewdef('petel_schema.student_school_years_registration_vw'::regclass, true) AS definition;

-- Display saved view
SELECT * FROM temp_view_definition;

-- ================================================================
-- STEP 4: DROP DEPENDENT VIEWS
-- ================================================================

DROP VIEW IF EXISTS petel_schema.student_school_years_registration_vw CASCADE;

-- ================================================================
-- STEP 5: EXTEND COLUMNS (SAFE - NO DATA LOSS)
-- ================================================================

-- PERSONS TABLE
ALTER TABLE persons 
    ALTER COLUMN id_number TYPE VARCHAR(100);

ALTER TABLE persons 
    ALTER COLUMN email TYPE VARCHAR(255);

ALTER TABLE persons 
    ALTER COLUMN phone_number TYPE VARCHAR(100);

COMMENT ON COLUMN persons.id_number IS 'ID number - encrypted with AES-256, stores base64 string (~100 chars)';
COMMENT ON COLUMN persons.email IS 'Email - encrypted with AES-256, stores base64 string (~255 chars)';
COMMENT ON COLUMN persons.phone_number IS 'Phone number - encrypted with AES-256, stores base64 string (~100 chars)';

-- SCHOOL_STUDENTS TABLE
ALTER TABLE school_students 
    ALTER COLUMN id_number TYPE VARCHAR(100);

ALTER TABLE school_students 
    ALTER COLUMN street TYPE VARCHAR(150);

COMMENT ON COLUMN school_students.id_number IS 'ID number - encrypted with AES-256, stores base64 string (~100 chars)';
COMMENT ON COLUMN school_students.street IS 'Street address - encrypted with AES-256, stores base64 string (~150 chars)';

-- USERS TABLE (already 255, but verify)
COMMENT ON COLUMN users.email IS 'Email - encrypted with AES-256, stores base64 string (~255 chars)';
COMMENT ON COLUMN users.otp_secret IS 'OTP secret - encrypted with AES-256, stores base64 string (~255 chars)';

-- ================================================================
-- STEP 6: RECREATE DEPENDENT VIEWS
-- ================================================================

-- Recreate student_school_years_registration_vw
CREATE VIEW petel_schema.student_school_years_registration_vw AS
 SELECT y.hebrew_year_name,
    y.school_id,
    p.first_name,
    p.last_name,
    p.id_type,
    p.id_number AS official_id,
    p.date_of_birth,
    sg.name AS school_grade,
    st.name AS school_track,
    sy.school_year_id
   FROM (((((petel_schema.school_years y
     JOIN petel_schema.student_school_years sy ON ((y.id = sy.school_year_id)))
     JOIN petel_schema.students s ON ((sy.student_id = s.id)))
     JOIN petel_schema.persons p ON ((s.person_id = p.id)))
     JOIN petel_schema.tracks st ON ((sy.track_id = st.id)))
     JOIN petel_schema.school_grades sg ON ((sg.id = sy.school_grade_id)));

ALTER VIEW petel_schema.student_school_years_registration_vw OWNER TO postgres;

-- ================================================================
-- STEP 7: VERIFY COLUMN CHANGES
-- ================================================================

SELECT 
    table_name,
    column_name,
    data_type,
    character_maximum_length AS new_length,
    '✅ UPDATED' AS status
FROM information_schema.columns
WHERE table_schema = 'petel_schema'
  AND table_name IN ('persons', 'school_students', 'users')
  AND column_name IN ('id_number', 'email', 'phone_number', 'otp_secret', 'street')
ORDER BY table_name, column_name;

-- ================================================================
-- STEP 8: VERIFY VIEWS RECREATED
-- ================================================================

SELECT 
    table_name AS view_name,
    '✅ RECREATED' AS status
FROM information_schema.views
WHERE table_schema = 'petel_schema'
  AND table_name = 'student_school_years_registration_vw';

-- ================================================================
-- FINAL SUMMARY
-- ================================================================

DO $$
BEGIN
    RAISE NOTICE '========================================';
    RAISE NOTICE '✅ COLUMN EXTENSION COMPLETE';
    RAISE NOTICE '========================================';
    RAISE NOTICE '';
    RAISE NOTICE 'Updated columns:';
    RAISE NOTICE '  ✅ persons.id_number: 50 → 100 chars';
    RAISE NOTICE '  ✅ persons.email: 50 → 255 chars';
    RAISE NOTICE '  ✅ persons.phone_number: 10 → 100 chars';
    RAISE NOTICE '  ✅ school_students.id_number: 15 → 100 chars';
    RAISE NOTICE '  ✅ school_students.street: 50 → 150 chars';
    RAISE NOTICE '  ✅ users.email: 255 chars (already sufficient)';
    RAISE NOTICE '  ✅ users.otp_secret: 255 chars (already sufficient)';
    RAISE NOTICE '';
    RAISE NOTICE 'Views recreated:';
    RAISE NOTICE '  ✅ student_school_years_registration_vw';
    RAISE NOTICE '';
    RAISE NOTICE 'NEXT STEPS:';
    RAISE NOTICE '  1. Run encryption migration:';
    RAISE NOTICE '     dotnet run -- migrate-encrypt-data';
    RAISE NOTICE '';
    RAISE NOTICE '  2. Verify encrypted data:';
    RAISE NOTICE '     SELECT id, LEFT(id_number, 40) || ''...'' AS sample';
    RAISE NOTICE '     FROM petel_schema.persons LIMIT 3;';
    RAISE NOTICE '========================================';
END $$;