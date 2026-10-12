-- Add Transaction Accounts "create accounts for councils" bulk button action
-- Run once on each environment (idempotent via ON CONFLICT DO NOTHING)

-- 1. Register the secure action for the new button
INSERT INTO petel_schema.actions (name, display_name, description, action_type_id, onclick_name, reference, sort_order, is_active)
VALUES ('accounts_bulkCreateCouncilAccounts', 'צור חשבונות למועצות',
        'יצירת חשבונות אגרות תלמידי חוץ לכל המועצות הקשורות לבעלות', 7, 'BulkCreateCouncilAccounts', 'transactionaccounts', 0, true)
ON CONFLICT (name) DO NOTHING;

-- 2. Grant the action to the roles that manage transaction accounts
INSERT INTO petel_schema.roles_actions (role_id, action_id, action_level)
SELECT r.id, a.id, 1
FROM petel_schema.roles r
JOIN petel_schema.actions a ON a.name = 'accounts_bulkCreateCouncilAccounts'
WHERE r.name IN ('Admin', 'School Net Manager')
ON CONFLICT (role_id, action_id) DO NOTHING;
