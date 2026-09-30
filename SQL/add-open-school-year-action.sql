-- Open-school-year wizard actions.
-- Run once on each environment. Idempotent.

DO $$
DECLARE
    page_type_id integer;
    button_type_id integer;
BEGIN
    SELECT id INTO page_type_id FROM petel_schema.action_types WHERE name = 'page_action';
    SELECT id INTO button_type_id FROM petel_schema.action_types WHERE name = 'button';

    IF page_type_id IS NULL THEN
        RAISE EXCEPTION 'action_types row page_action not found';
    END IF;

    IF button_type_id IS NULL THEN
        RAISE EXCEPTION 'action_types row button not found';
    END IF;

    INSERT INTO petel_schema.actions (name, display_name, description, action_type_id, onclick_name, reference, sort_order, is_active)
    SELECT 'openschoolyear', 'פתיחת שנת לימודים',
           'גישה לעמוד פתיחת שנת לימודים', page_type_id, 'accessPage', 'openschoolyear', 10, true
    WHERE NOT EXISTS (SELECT 1 FROM petel_schema.actions WHERE name = 'openschoolyear');

    INSERT INTO petel_schema.actions (name, display_name, description, action_type_id, onclick_name, reference, sort_order, is_active)
    SELECT 'schoollist_openSchoolYear', 'פתיחת שנת לימודים',
           'פתיחת אשף פתיחת שנת לימודים מרשימת בתי הספר', button_type_id, 'openSchoolYear', 'schoollist', 40, true
    WHERE NOT EXISTS (SELECT 1 FROM petel_schema.actions WHERE name = 'schoollist_openSchoolYear');

    INSERT INTO petel_schema.actions (name, display_name, description, action_type_id, onclick_name, reference, sort_order, is_active)
    SELECT 'openschoolyear_confirm', 'אישור פתיחת שנה',
           'יצירת שנת לימודים לבתי הספר שנבחרו', button_type_id, 'confirm', 'openschoolyear', 20, true
    WHERE NOT EXISTS (SELECT 1 FROM petel_schema.actions WHERE name = 'openschoolyear_confirm');
END $$;

INSERT INTO petel_schema.roles_actions (role_id, action_id, action_level, updated_at, update_user)
SELECT 1, a.id, 1, NOW(), 1
FROM petel_schema.actions a
WHERE a.name IN ('openschoolyear', 'schoollist_openSchoolYear', 'openschoolyear_confirm')
  AND NOT EXISTS (
      SELECT 1 FROM petel_schema.roles_actions ra
      WHERE ra.role_id = 1 AND ra.action_id = a.id
  );
