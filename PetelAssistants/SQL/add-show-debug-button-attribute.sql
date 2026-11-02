-- Seed system attribute that toggles the session debug button in MainLayout.
-- Set value to 'true' (or '1') to show the button; default is off.
INSERT INTO shared_schema.system_attributes (name, value, value_type, description)
VALUES (
    'Show debug button',
    'false',
    'bool',
    'האם להציג את כפתור Debug Session בסרגל העליון'
)
ON CONFLICT (name) DO NOTHING;
