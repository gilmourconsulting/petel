/**
 * Page Lifecycle Configuration
 * Defines cleanup, initialization, and navigation rules for each page
 * Following Single-Page Application with Module Loading pattern
 */

window.PageLifecycleConfig = {
    /**
     * Page definitions with cleanup and initialization hooks
     */
    pages: {
        'maindashboard': {
            file: 'maindashboard.html',
            title: 'לוח בקרה ראשי',
            cleanup: null, // No cleanup needed for main dashboard
            init: null
        },
        'schoollist': {
            file: 'schoollist.html',
            title: 'רשימת בתי ספר',
            cleanup: 'cleanupSchoolListPage',
            init: 'loadSchoolsData'
        },
        'schooldashboard': {
            file: 'schooldashboard.html',
            title: 'לוח בקרה בית ספר',
            cleanup: 'cleanupSchoolDashboardPage',
            init: null
        },
        'schooldetails': {
            file: 'schooldetails.html',
            title: 'פרטי בית ספר',
            cleanup: 'cleanupSchoolDetailsPage',
            init: 'initializeSchoolDetails'
        },
        'schooldocuments': {
            file: 'schooldocuments.html',
            title: 'מסמכי בית ספר',
            cleanup: 'cleanupSchoolDocumentsPage',
            init: null
        },
        'students': {
            file: 'students.html',
            title: 'רשימת תלמידים',
            cleanup: 'cleanupStudentsPage',
            init: 'loadStudentsData'
        },
        'student': {
            file: 'student.html',
            title: 'פרטי תלמיד',
            cleanup: 'cleanupStudentPage',
            init: 'loadStudentData'
        },
        'studentdetails': {
            file: 'student.html', // Same file as student
            title: 'פרטי תלמיד',
            cleanup: 'cleanupStudentPage',
            init: 'loadStudentData'
        },
        'systemattributes': {
            file: 'system-attributes.html',
            title: 'מאפייני מערכת',
            cleanup: null,
            init: 'loadSystemAttributes'
        },
        'schoolyear': {
            file: 'schoolyear.html',
            title: 'ניהול שנות לימוד',
            cleanup: null,
            init: 'initializeSchoolYear'
        },
        'entitydetails': {
            file: 'entitydetails.html',
            title: 'פרטי ישות',
            cleanup: 'cleanupEntityDetails',
            init: 'initializeEntityDetails'
        },
        'councilsummary': {
            file: 'councilsummary.html',
            title: 'סיכום רשויות',
            cleanup: 'cleanupCouncilSummary',
            init: null,
            selfInitializing: true
        },
        'councilstudents': {
            file: 'councilstudents.html',
            title: 'תלמידי רשות',
            cleanup: 'cleanupCouncilStudents',
            init: null,
            selfInitializing: true
        },
        'entities': {
            file: 'entities.html',
            title: 'ישויות',
            cleanup: 'cleanupEntitiesPage',
            init: 'initEntitiesPage'
        },
        'swagger': {
            file: 'swagger.html',
            title: 'Swagger API Documentation',
            cleanup: 'cleanupSwaggerPage',
            init: null,
            selfInitializing: true
        },
        'users': {
            file: 'users.html',
            title: 'משתמשים',
            cleanup: 'cleanupUsersPage',
            init: 'loadUsersData'
        },
        'roles': {
            file: 'roles.html',
            title: 'ניהול תפקידים',
            cleanup: 'cleanupRolesPage',
            init: 'loadRolesData'
        },
        'roledetails': {
            file: 'roledetails.html',
            title: 'פרטי תפקיד',
            cleanup: 'cleanupRoleDetailsPage',
            init: 'loadRoleDetails'
        },
        'sessions': {
            file: 'sessions.html',
            title: 'משתמשים פעילים',
            cleanup: null,
            init: null,
            selfInitializing: true
        },
        'schoolyearconfig': {
            file: 'schoolyearconfig.html',
            title: 'הגדרות שנת לימודים',
            cleanup: 'cleanupSchoolYearConfig',
            init: 'initializeSchoolYearConfig'
            //selfInitializing: true
        },
        'about': {
            file: 'about.html',
            title: 'אודות המערכת',
            cleanup: 'cleanupAboutPage',
            init: null,
            selfInitializing: true
        },
        'settings': {
            file: 'settings.html',
            title: 'הגדרות מערכת',
            cleanup: 'cleanupSettingsPage',
            init: null,
            selfInitializing: true
        },
    },

    /**
     * Navigation flow rules - defines what should be cleaned up when navigating
     * Format: { from: 'page', to: 'page', clearSession: ['key1', 'key2'] }
     */
    navigationRules: [
        // Student detail back to students list
        {
            from: 'student',
            to: 'students',
            clearSession: ['SelectedStudentId', 'SelectedStudentData']
        },
        {
            from: 'studentdetails',
            to: 'students',
            clearSession: ['SelectedStudentId', 'SelectedStudentData']
        },
        // Students back to school dashboard
        {
            from: 'students',
            to: 'schooldashboard',
            clearSession: []
        },
        // School dashboard back to school list
        {
            from: 'schooldashboard',
            to: 'schoollist',
            clearSession: []
        },
        // School details back to school dashboard
        {
            from: 'schooldetails',
            to: 'schooldashboard',
            clearSession: []
        },
        // School documents back to school dashboard
        {
            from: 'schooldocuments',
            to: 'schooldashboard',
            clearSession: []
        },
        // Any page to main dashboard - clear all school/student context
        {
            from: '*',
            to: 'maindashboard',
            clearSession: [
                'SelectedStudentId',
                'SelectedStudentData',
                'SelectedSchoolId',
                'SelectedSchoolName'
            ]
        },
        // School list to main dashboard - clear school context
        {
            from: 'schoollist',
            to: 'maindashboard',
            clearSession: ['SelectedSchoolId', 'SelectedSchoolName']
        },
        // ✅  Student to school dashboard
        {
            from: 'student',
            to: 'schooldashboard',
            clearSession: ['SelectedStudentId', 'SelectedStudentData']
        },

        // ✅  Student to school list
        {
            from: 'student',
            to: 'schoollist',
            clearSession: [
                'SelectedStudentId',
                'SelectedStudentData',
                'SelectedSchoolId',
                'SelectedSchoolName'
            ]
        },

        // ✅  School documents to school dashboard
        {
            from: 'schooldocuments',
            to: 'schooldashboard',
            clearSession: []
        }
    ],

    /**
     * Get page configuration
     */
    getPageConfig(pageName) {
        return this.pages[pageName?.toLowerCase()] || null;
    },

    /**
     * Get navigation rule for transition
     */
    getNavigationRule(fromPage, toPage) {
        // Check for specific rule
        const specificRule = this.navigationRules.find(
            rule => rule.from === fromPage && rule.to === toPage
        );
        if (specificRule) return specificRule;

        // Check for wildcard rule
        const wildcardRule = this.navigationRules.find(
            rule => rule.from === '*' && rule.to === toPage
        );
        return wildcardRule || null;
    }
};

console.log('✅ PageLifecycleConfig loaded');