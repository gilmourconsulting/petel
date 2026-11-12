-- Exclude soft-deleted students (status 8 / נמחק) from council_summary_vw.
-- Idempotent: safe to re-run on ATH test and production.

CREATE OR REPLACE VIEW petel_schema.council_summary_vw AS
SELECT
    c.id AS council_id,
    c.name AS council_name,
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
        AND (ss.status IS DISTINCT FROM 8)
    INNER JOIN petel_schema.school_years sy ON ss.school_year_id = sy.id
    INNER JOIN petel_schema.schools sch ON ss.school_year_id = sch.school_year_id AND sch.is_last_version = true
    LEFT JOIN petel_schema.entities e ON sch.owner = e.id
WHERE
    sy.year_id IS NOT NULL
GROUP BY
    c.id,
    c.name,
    sy.year_id,
    sch.owner,
    e.name;

ALTER VIEW petel_schema.council_summary_vw OWNER TO "PetelAdmin";
