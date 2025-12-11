-- Create view for council summary by year
CREATE OR REPLACE VIEW petel_schema.council_summary_vw AS
SELECT 
    c.id AS council_id,
    c.council_short_name,
    c.council_long_name,
    sy.year_id,
    COUNT(DISTINCT ss.id) AS number_of_students,
    COALESCE(SUM(ss.cost), 0) AS total_requested_amount
FROM 
    petel_schema.councils c
    LEFT JOIN petel_schema.school_students ss ON c.id = ss.sending_council AND ss.is_last_version = true
    LEFT JOIN petel_schema.school_years sy ON ss.school_year_id = sy.id
WHERE 
    sy.year_id IS NOT NULL
GROUP BY 
    c.id, 
    c.council_short_name, 
    c.council_long_name, 
    sy.year_id;

    ALTER VIEW petel_schema.council_summary_vw OWNER TO "PetelAdmin";