-- Migration: Add student delete and view-deleted actions
-- Run once on each environment (idempotent via ON CONFLICT DO NOTHING)

-- students_deleteStudent — allows a user to soft-delete a student (sets status to 8 = נמחק)
INSERT INTO petel_schema.actions (name, display_name, description, action_type_id, onclick_name, reference, sort_order, is_active)
VALUES ('students_deletestudent', 'מחק תלמיד', 'מחיקת תלמיד (שינוי סטטוס ל-נמחק)', 7, 'deleteStudent', 'students', 65, true)
ON CONFLICT (name) DO NOTHING;

-- students_viewDeleted — allows a user to see students with status 8 (נמחק)
INSERT INTO petel_schema.actions (name, display_name, description, action_type_id, onclick_name, reference, sort_order, is_active)
VALUES ('students_viewdeleted', 'צפייה בתלמידים מחוקים', 'הצגת תלמידים שסטטוסם הוא נמחק', 7, 'viewDeleted', 'students', 70, true)
ON CONFLICT (name) DO NOTHING;
