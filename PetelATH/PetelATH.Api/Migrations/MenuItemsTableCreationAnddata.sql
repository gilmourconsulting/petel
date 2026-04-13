-- Create menu_items table
CREATE TABLE IF NOT EXISTS petel_schema.menu_items (
    id SERIAL PRIMARY KEY,
    name VARCHAR(50) NOT NULL,
    reference VARCHAR(100) NOT NULL,
    text VARCHAR(100) NOT NULL,
    action_id INTEGER NULL,
    sort_order INTEGER NOT NULL DEFAULT 0,
    is_active BOOLEAN NOT NULL DEFAULT true
);

-- Create index on sort_order for performance
CREATE INDEX IF NOT EXISTS idx_menu_items_sort_order 
ON petel_schema.menu_items(sort_order);

-- Create index on action_id for future permission filtering
CREATE INDEX IF NOT EXISTS idx_menu_items_action_id 
ON petel_schema.menu_items(action_id);

-- Insert initial menu items
INSERT INTO petel_schema.menu_items (name, reference, text, action_id, sort_order, is_active) VALUES
('maindashboard', '#maindashboard', 'עמוד ראשי', NULL, 10, true),
('users', '#users', 'ניהול משתמשים', NULL, 20, true),
('tasks', '#tasks', 'משימות', NULL, 30, true),
('reports', '#reports', 'דוחות', NULL, 40, true),
('analytics', '#analytics', 'ניתוח נתונים', NULL, 50, true),
('settings', '#settings', 'הגדרות מערכת', NULL, 60, true),
('help', '#help', 'עזרה ותמיכה', NULL, 70, true),
('systemattributes', '#systemattributes', 'מאפייני מערכת', NULL, 80, true),
('sessions', '#sessions', 'משתמשים פעילים', NULL, 90, true)
ON CONFLICT DO NOTHING;

-- Verify data
SELECT * FROM petel_schema.menu_items ORDER BY sort_order;