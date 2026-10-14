-- Add School Year Config "copy from previous year" button action
-- Run once on each environment (idempotent via ON CONFLICT DO NOTHING)

INSERT INTO petel_schema.actions (name, display_name, description, action_type_id, onclick_name, reference, sort_order, is_active)
VALUES ('schoolyearconfig_copyFromPreviousYear', 'העתק הגדרות מהשנה הקודמת',
        'העתקת כל הגדרות שנת הלימודים מהשנה הקודמת', 7, 'copyFromPreviousYear', 'schoolyearconfig', 30, true)
ON CONFLICT (name) DO NOTHING;
