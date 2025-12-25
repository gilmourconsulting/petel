-- Add Swagger menu item to database
-- Run this SQL in your PostgreSQL database for test environment

INSERT INTO petel_schema.menu_items (name, reference, text, action_id, sort_order, is_active)
VALUES ('swagger', '#swagger', 'Swagger API', NULL, 110, true)
ON CONFLICT DO NOTHING;

-- Verify the insertion
SELECT * FROM petel_schema.menu_items WHERE name = 'swagger';
