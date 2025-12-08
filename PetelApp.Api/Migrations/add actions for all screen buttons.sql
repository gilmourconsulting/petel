-- ============================================================================
-- Action-Based Security Framework - Actions Migration (ONCLICK-BASED)
-- ============================================================================
-- This migration adds all onclick-based actions discovered from HTML files
-- Format: action_name = {screen_name}_{onclick_function_name}
-- Action Type: 7 (button)
-- ============================================================================

-- Step 1: Add onclick_name column if it doesn't exist
ALTER TABLE petel_schema.actions 
ADD COLUMN IF NOT EXISTS onclick_name VARCHAR(100);

-- Step 2: Remove all previous button actions (action_type_id = 7)
DELETE FROM petel_schema.roles_actions 
WHERE action_id IN (
    SELECT id FROM petel_schema.actions 
    WHERE action_type_id = 7
);

DELETE FROM petel_schema.actions 
WHERE action_type_id = 7;

-- Step 3: Insert all onclick-based actions from HTML files
-- Format: INSERT INTO petel_schema.actions (name, display_name, description, action_type_id, onclick_name, reference, sort_order, is_active) 
-- VALUES (...)

-- index.html actions
INSERT INTO petel_schema.actions (name, display_name, description, action_type_id, onclick_name, reference, sort_order, is_active) 
VALUES ('index_navigatetomain', 'Navigate to Main Dashboard', 'Click on system logo', 7, 'navigateToMainDashboard', 'index', 10, true);

INSERT INTO petel_schema.actions (name, display_name, description, action_type_id, onclick_name, reference, sort_order, is_active) 
VALUES ('index_showsessiondebug', 'Show Session Debug', 'Show debug session info', 7, 'showSessionDebug', 'index', 20, true);

INSERT INTO petel_schema.actions (name, display_name, description, action_type_id, onclick_name, reference, sort_order, is_active) 
VALUES ('index_logout', 'Logout', 'User logout action', 7, 'logout', 'index', 30, true);

-- maindashboard.html actions
INSERT INTO petel_schema.actions (name, display_name, description, action_type_id, onclick_name, reference, sort_order, is_active) 
VALUES ('maindashboard_showschoolyear', 'Show School Year', 'Display school year context', 7, 'showSchoolYear', 'maindashboard', 10, true);

INSERT INTO petel_schema.actions (name, display_name, description, action_type_id, onclick_name, reference, sort_order, is_active) 
VALUES ('maindashboard_showaddmodal', 'Show Add Modal', 'Show add modal for alerts/events', 7, 'showAddModal', 'maindashboard', 20, true);

-- schooldashboard.html actions
INSERT INTO petel_schema.actions (name, display_name, description, action_type_id, onclick_name, reference, sort_order, is_active) 
VALUES ('schooldashboard_navigatetostudents', 'Navigate to Students', 'Go to students page', 7, 'navigateToStudents', 'schooldashboard', 10, true);

INSERT INTO petel_schema.actions (name, display_name, description, action_type_id, onclick_name, reference, sort_order, is_active) 
VALUES ('schooldashboard_navigatetoschooldetails', 'Navigate to School Details', 'Go to school details page', 7, 'navigateToSchoolDetails', 'schooldashboard', 20, true);

INSERT INTO petel_schema.actions (name, display_name, description, action_type_id, onclick_name, reference, sort_order, is_active) 
VALUES ('schooldashboard_navigatetoschookdocuments', 'Navigate to School Documents', 'Go to school documents page', 7, 'navigateToSchoolDocuments', 'schooldashboard', 30, true);

INSERT INTO petel_schema.actions (name, display_name, description, action_type_id, onclick_name, reference, sort_order, is_active) 
VALUES ('schooldashboard_navigatebacktoschoollist', 'Navigate Back to School List', 'Return to school list', 7, 'navigateBackToSchoolList', 'schooldashboard', 40, true);

INSERT INTO petel_schema.actions (name, display_name, description, action_type_id, onclick_name, reference, sort_order, is_active) 
VALUES ('schooldashboard_showaddmodal', 'Show Add Modal', 'Show add modal for alerts/events', 7, 'showAddModal', 'schooldashboard', 50, true);

-- schooldetails.html actions
INSERT INTO petel_schema.actions (name, display_name, description, action_type_id, onclick_name, reference, sort_order, is_active) 
VALUES ('schooldetails_navigatebacktoschooldashboard', 'Navigate Back to School Dashboard', 'Return to school dashboard', 7, 'navigateBackToSchoolDashboard', 'schooldetails', 10, true);

INSERT INTO petel_schema.actions (name, display_name, description, action_type_id, onclick_name, reference, sort_order, is_active) 
VALUES ('schooldetails_navigatebacktodashboard', 'Navigate Back to Dashboard', 'Return to main dashboard', 7, 'navigateBackToDashboard', 'schooldetails', 20, true);

INSERT INTO petel_schema.actions (name, display_name, description, action_type_id, onclick_name, reference, sort_order, is_active) 
VALUES ('schooldetails_showeditschooldetailsmodal', 'Edit School Details', 'Show edit school details modal', 7, 'event.stopPropagation', 'schooldetails', 30, true);

INSERT INTO petel_schema.actions (name, display_name, description, action_type_id, onclick_name, reference, sort_order, is_active) 
VALUES ('schooldetails_showeditclassesmodal', 'Edit Classes', 'Show edit classes modal', 7, 'event.stopPropagation', 'schooldetails', 40, true);

INSERT INTO petel_schema.actions (name, display_name, description, action_type_id, onclick_name, reference, sort_order, is_active) 
VALUES ('schooldetails_showaddclassmodal', 'Show Add Class Modal', 'Add new class', 7, 'showAddClassModal', 'schooldetails', 50, true);

INSERT INTO petel_schema.actions (name, display_name, description, action_type_id, onclick_name, reference, sort_order, is_active) 
VALUES ('schooldetails_showaddtrackmodal', 'Show Add Track Modal', 'Add new track', 7, 'showAddTrackModal', 'schooldetails', 60, true);

INSERT INTO petel_schema.actions (name, display_name, description, action_type_id, onclick_name, reference, sort_order, is_active) 
VALUES ('schooldetails_showaddadditionalstudymodal', 'Show Add Additional Study Modal', 'Add new additional study program', 7, 'showAddAdditionalStudyModal', 'schooldetails', 70, true);

INSERT INTO petel_schema.actions (name, display_name, description, action_type_id, onclick_name, reference, sort_order, is_active) 
VALUES ('schooldetails_adjusttime', 'Adjust Time', 'Adjust school time settings', 7, 'adjustTime', 'schooldetails', 80, true);

INSERT INTO petel_schema.actions (name, display_name, description, action_type_id, onclick_name, reference, sort_order, is_active) 
VALUES ('schooldetails_clearendtime', 'Clear End Time', 'Clear school end time', 7, 'clearEndTime', 'schooldetails', 90, true);

INSERT INTO petel_schema.actions (name, display_name, description, action_type_id, onclick_name, reference, sort_order, is_active) 
VALUES ('schooldetails_showeditclassmodal', 'Show Edit Class Modal', 'Edit existing class', 7, 'showEditClassModal', 'schooldetails', 100, true);

INSERT INTO petel_schema.actions (name, display_name, description, action_type_id, onclick_name, reference, sort_order, is_active) 
VALUES ('schooldetails_deleteclasswithconfirmation', 'Delete Class', 'Delete class with confirmation', 7, 'deleteClassWithConfirmation', 'schooldetails', 110, true);

INSERT INTO petel_schema.actions (name, display_name, description, action_type_id, onclick_name, reference, sort_order, is_active) 
VALUES ('schooldetails_showedittrackmodal', 'Show Edit Track Modal', 'Edit existing track', 7, 'showEditTrackModal', 'schooldetails', 120, true);

INSERT INTO petel_schema.actions (name, display_name, description, action_type_id, onclick_name, reference, sort_order, is_active) 
VALUES ('schooldetails_deletetrack', 'Delete Track', 'Delete track', 7, 'deleteTrack', 'schooldetails', 130, true);

INSERT INTO petel_schema.actions (name, display_name, description, action_type_id, onclick_name, reference, sort_order, is_active) 
VALUES ('schooldetails_editaddress', 'Edit Address', 'Edit school address', 7, 'editAddress', 'schooldetails', 140, true);

INSERT INTO petel_schema.actions (name, display_name, description, action_type_id, onclick_name, reference, sort_order, is_active) 
VALUES ('schooldetails_editprincipal', 'Edit Principal', 'Edit school principal info', 7, 'editPrincipal', 'schooldetails', 150, true);

INSERT INTO petel_schema.actions (name, display_name, description, action_type_id, onclick_name, reference, sort_order, is_active) 
VALUES ('schooldetails_editinspector', 'Edit Inspector', 'Edit school inspector info', 7, 'editInspector', 'schooldetails', 160, true);

INSERT INTO petel_schema.actions (name, display_name, description, action_type_id, onclick_name, reference, sort_order, is_active) 
VALUES ('schooldetails_editcontactperson', 'Edit Contact Person', 'Edit school contact person info', 7, 'editContactPerson', 'schooldetails', 170, true);

INSERT INTO petel_schema.actions (name, display_name, description, action_type_id, onclick_name, reference, sort_order, is_active) 
VALUES ('schooldetails_showprogramversionhistory', 'Show Program Version History', 'View program version history', 7, 'showProgramVersionHistory', 'schooldetails', 180, true);

INSERT INTO petel_schema.actions (name, display_name, description, action_type_id, onclick_name, reference, sort_order, is_active) 
VALUES ('schooldetails_showeditadditionalstudymodal', 'Show Edit Additional Study Modal', 'Edit existing additional study program', 7, 'showEditAdditionalStudyModal', 'schooldetails', 190, true);

INSERT INTO petel_schema.actions (name, display_name, description, action_type_id, onclick_name, reference, sort_order, is_active) 
VALUES ('schooldetails_deleteadditionalstudyprogram', 'Delete Additional Study Program', 'Delete additional study program', 7, 'deleteAdditionalStudyProgram', 'schooldetails', 200, true);

-- schoollist.html actions
INSERT INTO petel_schema.actions (name, display_name, description, action_type_id, onclick_name, reference, sort_order, is_active) 
VALUES ('schoollist_navigatetocouncilslist', 'Navigate to Councils List', 'Go to councils list', 7, 'navigateToCouncilsList', 'schoollist', 10, true);

INSERT INTO petel_schema.actions (name, display_name, description, action_type_id, onclick_name, reference, sort_order, is_active) 
VALUES ('schoollist_navigatebacktodashboard', 'Navigate Back to Dashboard', 'Return to main dashboard', 7, 'navigateBackToDashboard', 'schoollist', 20, true);

INSERT INTO petel_schema.actions (name, display_name, description, action_type_id, onclick_name, reference, sort_order, is_active) 
VALUES ('schoollist_showaddschoolmodal', 'Show Add School Modal', 'Add new school', 7, 'showAddSchoolModal', 'schoollist', 30, true);

INSERT INTO petel_schema.actions (name, display_name, description, action_type_id, onclick_name, reference, sort_order, is_active) 
VALUES ('schoollist_viewschool', 'View School', 'View school details', 7, 'viewSchool', 'schoollist', 40, true);

-- schoolyear.html actions
INSERT INTO petel_schema.actions (name, display_name, description, action_type_id, onclick_name, reference, sort_order, is_active) 
VALUES ('schoolyear_showschoolyearsection', 'Show School Year Section', 'Show school year section', 7, 'showSchoolYearSection', 'schoolyear', 10, true);

INSERT INTO petel_schema.actions (name, display_name, description, action_type_id, onclick_name, reference, sort_order, is_active) 
VALUES ('schoolyear_selectschoolyear', 'Select School Year', 'Select a school year', 7, 'selectSchoolYear', 'schoolyear', 20, true);

-- schooldocuments.html actions
INSERT INTO petel_schema.actions (name, display_name, description, action_type_id, onclick_name, reference, sort_order, is_active) 
VALUES ('schooldocuments_navigatetoschooldashboard', 'Navigate to School Dashboard', 'Return to school dashboard', 7, 'navigateToSchoolDashboard', 'schooldocuments', 10, true);

INSERT INTO petel_schema.actions (name, display_name, description, action_type_id, onclick_name, reference, sort_order, is_active) 
VALUES ('schooldocuments_navigatebacktoschoollist', 'Navigate Back to School List', 'Return to school list', 7, 'navigateBackToSchoolList', 'schooldocuments', 20, true);

-- students.html actions
INSERT INTO petel_schema.actions (name, display_name, description, action_type_id, onclick_name, reference, sort_order, is_active) 
VALUES ('students_refreshstudentsdata', 'Refresh Students Data', 'Refresh student list', 7, 'refreshStudentsData', 'students', 10, true);

INSERT INTO petel_schema.actions (name, display_name, description, action_type_id, onclick_name, reference, sort_order, is_active) 
VALUES ('students_navigatetoschooldashboard', 'Navigate to School Dashboard', 'Return to school dashboard', 7, 'navigateToSchoolDashboard', 'students', 20, true);

INSERT INTO petel_schema.actions (name, display_name, description, action_type_id, onclick_name, reference, sort_order, is_active) 
VALUES ('students_showuploaddialog', 'Show Upload Dialog', 'Show student import dialog', 7, 'showUploadDialog', 'students', 30, true);

INSERT INTO petel_schema.actions (name, display_name, description, action_type_id, onclick_name, reference, sort_order, is_active) 
VALUES ('students_closeuploaddialog', 'Close Upload Dialog', 'Close student import dialog', 7, 'closeUploadDialog', 'students', 40, true);

INSERT INTO petel_schema.actions (name, display_name, description, action_type_id, onclick_name, reference, sort_order, is_active) 
VALUES ('students_closeuploaddialoganrefresh', 'Close Upload Dialog and Refresh', 'Close import dialog and refresh list', 7, 'closeUploadDialogAndRefresh', 'students', 50, true);

INSERT INTO petel_schema.actions (name, display_name, description, action_type_id, onclick_name, reference, sort_order, is_active) 
VALUES ('students_viewstudentdetails', 'View Student Details', 'View individual student details', 7, 'viewStudentDetails', 'students', 60, true);

-- student.html actions
INSERT INTO petel_schema.actions (name, display_name, description, action_type_id, onclick_name, reference, sort_order, is_active) 
VALUES ('student_viewstudentpricingfromsection', 'View Student Pricing', 'View student pricing components', 7, 'viewStudentPricingFromSection', 'student', 10, true);

INSERT INTO petel_schema.actions (name, display_name, description, action_type_id, onclick_name, reference, sort_order, is_active) 
VALUES ('student_closepricingmodal', 'Close Pricing Modal', 'Close pricing modal', 7, 'closePricingModal', 'student', 20, true);

INSERT INTO petel_schema.actions (name, display_name, description, action_type_id, onclick_name, reference, sort_order, is_active) 
VALUES ('student_navigatetostudents', 'Navigate to Students', 'Return to students list', 7, 'navigateToStudents', 'student', 30, true);

INSERT INTO petel_schema.actions (name, display_name, description, action_type_id, onclick_name, reference, sort_order, is_active) 
VALUES ('student_navigatetoschooldashboard', 'Navigate to School Dashboard', 'Return to school dashboard', 7, 'navigateToSchoolDashboard', 'student', 40, true);

INSERT INTO petel_schema.actions (name, display_name, description, action_type_id, onclick_name, reference, sort_order, is_active) 
VALUES ('student_navigatetoschoollist', 'Navigate to School List', 'Return to school list', 7, 'navigateToSchoolList', 'student', 50, true);

INSERT INTO petel_schema.actions (name, display_name, description, action_type_id, onclick_name, reference, sort_order, is_active) 
VALUES ('student_initializestudentdocuments', 'Initialize Student Documents', 'Initialize student documents section', 7, 'initializeStudentDocuments', 'student', 60, true);

-- entities.html actions
INSERT INTO petel_schema.actions (name, display_name, description, action_type_id, onclick_name, reference, sort_order, is_active) 
VALUES ('entities_navigatetomain', 'Navigate to Main Dashboard', 'Return to main dashboard', 7, 'navigateTo', 'entities', 10, true);

INSERT INTO petel_schema.actions (name, display_name, description, action_type_id, onclick_name, reference, sort_order, is_active) 
VALUES ('entities_editentity', 'Edit Entity', 'Edit entity details', 7, 'editEntity', 'entities', 20, true);

INSERT INTO petel_schema.actions (name, display_name, description, action_type_id, onclick_name, reference, sort_order, is_active) 
VALUES ('entities_deleteentity', 'Delete Entity', 'Delete entity', 7, 'deleteEntity', 'entities', 30, true);

-- entitydetails.html actions
INSERT INTO petel_schema.actions (name, display_name, description, action_type_id, onclick_name, reference, sort_order, is_active) 
VALUES ('entitydetails_navigatetomain', 'Navigate to Main Dashboard', 'Return to main dashboard', 7, 'navigateTo', 'entitydetails', 10, true);

INSERT INTO petel_schema.actions (name, display_name, description, action_type_id, onclick_name, reference, sort_order, is_active) 
VALUES ('entitydetails_editentitydetails', 'Edit Entity Details', 'Edit entity details', 7, 'event.stopPropagation', 'entitydetails', 20, true);

INSERT INTO petel_schema.actions (name, display_name, description, action_type_id, onclick_name, reference, sort_order, is_active) 
VALUES ('entitydetails_editentityaddress', 'Edit Entity Address', 'Edit entity address', 7, 'editEntityAddress', 'entitydetails', 30, true);

INSERT INTO petel_schema.actions (name, display_name, description, action_type_id, onclick_name, reference, sort_order, is_active) 
VALUES ('entitydetails_editentitycontactperson', 'Edit Entity Contact Person', 'Edit entity contact person', 7, 'editEntityContactPerson', 'entitydetails', 40, true);

-- users.html actions
INSERT INTO petel_schema.actions (name, display_name, description, action_type_id, onclick_name, reference, sort_order, is_active) 
VALUES ('users_refreshusersdata', 'Refresh Users Data', 'Refresh user list', 7, 'refreshUsersData', 'users', 10, true);

INSERT INTO petel_schema.actions (name, display_name, description, action_type_id, onclick_name, reference, sort_order, is_active) 
VALUES ('users_showcreateuseridialog', 'Show Create User Dialog', 'Open create user dialog', 7, 'showCreateUserDialog', 'users', 20, true);

INSERT INTO petel_schema.actions (name, display_name, description, action_type_id, onclick_name, reference, sort_order, is_active) 
VALUES ('users_navigatetomain', 'Navigate to Main Dashboard', 'Return to main dashboard', 7, 'navigateToMainDashboard', 'users', 30, true);

INSERT INTO petel_schema.actions (name, display_name, description, action_type_id, onclick_name, reference, sort_order, is_active) 
VALUES ('users_closeuseridialog', 'Close User Dialog', 'Close user dialog', 7, 'closeUserDialog', 'users', 40, true);

INSERT INTO petel_schema.actions (name, display_name, description, action_type_id, onclick_name, reference, sort_order, is_active) 
VALUES ('users_edituser', 'Edit User', 'Edit user details', 7, 'editUser', 'users', 50, true);

INSERT INTO petel_schema.actions (name, display_name, description, action_type_id, onclick_name, reference, sort_order, is_active) 
VALUES ('users_deleteuser', 'Delete User', 'Delete user', 7, 'deleteUser', 'users', 60, true);

-- councilsummary.html actions
INSERT INTO petel_schema.actions (name, display_name, description, action_type_id, onclick_name, reference, sort_order, is_active) 
VALUES ('councilsummary_navigatebacktoschoolslist', 'Navigate Back to Schools List', 'Return to schools list', 7, 'navigateBackToSchoolsList', 'councilsummary', 10, true);

INSERT INTO petel_schema.actions (name, display_name, description, action_type_id, onclick_name, reference, sort_order, is_active) 
VALUES ('councilsummary_navigatebacktodashboard', 'Navigate Back to Dashboard', 'Return to main dashboard', 7, 'navigateBackToDashboard', 'councilsummary', 20, true);

INSERT INTO petel_schema.actions (name, display_name, description, action_type_id, onclick_name, reference, sort_order, is_active) 
VALUES ('councilsummary_viewcouncil', 'View Council', 'View council details', 7, 'viewCouncil', 'councilsummary', 30, true);

-- sessions.html actions
INSERT INTO petel_schema.actions (name, display_name, description, action_type_id, onclick_name, reference, sort_order, is_active) 
VALUES ('sessions_refreshsessiondata', 'Refresh Session Data', 'Refresh session data', 7, 'refreshSessionData', 'sessions', 10, true);

INSERT INTO petel_schema.actions (name, display_name, description, action_type_id, onclick_name, reference, sort_order, is_active) 
VALUES ('sessions_addproperty', 'Add Property', 'Add session property', 7, 'addProperty', 'sessions', 20, true);

INSERT INTO petel_schema.actions (name, display_name, description, action_type_id, onclick_name, reference, sort_order, is_active) 
VALUES ('sessions_deleteproperty', 'Delete Property', 'Delete session property', 7, 'deleteProperty', 'sessions', 30, true);

-- system-attributes.html actions
INSERT INTO petel_schema.actions (name, display_name, description, action_type_id, onclick_name, reference, sort_order, is_active) 
VALUES ('systemattributes_loadsystemattributesfromcache', 'Load from Cache', 'Load system attributes from cache', 7, 'loadSystemAttributesFromCache', 'systemattributes', 10, true);

INSERT INTO petel_schema.actions (name, display_name, description, action_type_id, onclick_name, reference, sort_order, is_active) 
VALUES ('systemattributes_reloadsystemattributesfromdb', 'Reload from DB', 'Reload system attributes from database', 7, 'reloadSystemAttributesFromDB', 'systemattributes', 20, true);

-- menu.html actions
INSERT INTO petel_schema.actions (name, display_name, description, action_type_id, onclick_name, reference, sort_order, is_active) 
VALUES ('menu_togglemenu', 'Toggle Menu', 'Toggle side menu', 7, 'toggleMenu', 'menu', 10, true);

INSERT INTO petel_schema.actions (name, display_name, description, action_type_id, onclick_name, reference, sort_order, is_active) 
VALUES ('menu_navigateto', 'Navigate to Menu Item', 'Navigate via menu item', 7, 'navigateTo', 'menu', 20, true);

-- ============================================================================
-- Step 4: Assign all actions to Admin role (role_id = 1)
-- ============================================================================
INSERT INTO petel_schema.roles_actions (role_id, action_id, action_level, updated_at)
SELECT 1, a.id, 1, NOW()
FROM petel_schema.actions a
WHERE a.action_type_id = 7
AND NOT EXISTS (
    SELECT 1 FROM petel_schema.roles_actions ra 
    WHERE ra.role_id = 1 AND ra.action_id = a.id
);

-- ============================================================================
-- Step 5: Verification Queries
-- ============================================================================

-- Count total onclick actions
SELECT 
    COUNT(*) as total_onclick_actions,
    COUNT(CASE WHEN is_active THEN 1 END) as active_actions
FROM petel_schema.actions 
WHERE action_type_id = 7;

-- List all onclick actions
SELECT 
    id,
    name,
    onclick_name,
    display_name,
    reference,
    is_active
FROM petel_schema.actions 
WHERE action_type_id = 7
ORDER BY reference, name;

-- Count admin permissions
SELECT 
    COUNT(*) as admin_action_permissions
FROM petel_schema.roles_actions ra
JOIN petel_schema.actions a ON ra.action_id = a.id
WHERE ra.role_id = 1 AND a.action_type_id = 7;