/**
 * Session manager with tenant ID maintained but not enforced
 */
class SessionManager {
    constructor() {
        this.checkSession();
    }

    setSession(data) {
        sessionStorage.setItem('authToken', data.token);
        sessionStorage.setItem('userFullName', data.userFullName);
        
        // TenantId maintained but not required
        if (data.tenantId) {
            sessionStorage.setItem('tenantId', data.tenantId);
        }
        
        if (data.selectedSchoolId) {
            sessionStorage.setItem('selectedSchoolId', data.selectedSchoolId);
        }
        
        if (data.selectedSchoolName) {
            sessionStorage.setItem('selectedSchoolName', data.selectedSchoolName);
        }
    }

    getAuthToken() {
        return sessionStorage.getItem('authToken');
    }

    getUserFullName() {
        return sessionStorage.getItem('userFullName');
    }

    // Tenant ID getter maintained for backward compatibility
    getTenantId() {
        return sessionStorage.getItem('tenantId') || ''; // Return empty string if not present
    }

    getSelectedSchoolId() {
        return sessionStorage.getItem('selectedSchoolId') || '';
    }

    getSelectedSchoolName() {
        return sessionStorage.getItem('selectedSchoolName') || '';
    }

    clearSession() {
        sessionStorage.clear();
    }

    checkSession() {
        if (!this.getAuthToken() && 
            window.location.pathname !== '/login.html' && 
            !window.location.pathname.endsWith('/')) {
            window.location.href = 'login.html';
        }
    }

    isAuthenticated() {
        return !!this.getAuthToken();
    }
}

const sessionManager = new SessionManager();