-- Create view for council summary by year with ownership filtering
-- Each council-year-owner combination gets a row (for multi-owner councils)
-- Includes last-version students and historical split-council periods
-- (include_in_council_summary = true). Count is distinct per student identity.
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
