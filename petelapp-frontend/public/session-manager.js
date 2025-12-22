/**
 * Session manager with tenant ID maintained but not enforced
 */
class SessionManager {
    constructor() {
        this.baseUrl = window.AppConfig?.apiBaseUrl || 'http://localhost:5082/api';
    }

    /**
     * Get auth token from sessionStorage
     */
    getToken() {
        return sessionStorage.getItem('authToken');
    }

    /**
     * Set auth token in sessionStorage
     */
    setToken(token) {
        sessionStorage.setItem('authToken', token);
    }

    /**
     * Clear auth token from sessionStorage
     */
    clearToken() {
        sessionStorage.removeItem('authToken');
    }

    /**
     * Check if user is authenticated (has valid token)
     */
    async isAuthenticated() {
        const token = this.getToken();
        if (!token) {
            return false;
        }

        try {
            const response = await fetch(`${this.baseUrl}/auth/check`, {
                headers: {
                    'Authorization': `Bearer ${token}`
                }
            });
            
            if (response.ok) {
                const data = await response.json();
                return data.isAuthenticated === true;
            }
            return false;
        } catch (error) {
            console.error('Auth check failed:', error);
            return false;
        }
    }

    /**
     * Get current session info (identity + properties)
     */
    async getSessionInfo() {
        const token = this.getToken();
        if (!token) {
            return null;
        }

        try {
            const response = await fetch(`${this.baseUrl}/session`, {
                headers: {
                    'Authorization': `Bearer ${token}`
                }
            });

            if (!response.ok) {
                if (response.status === 401) {
                    this.logout();
                    return null;
                }
                throw new Error('Failed to get session info');
            }

            return await response.json();
        } catch (error) {
            console.error('Error getting session info:', error);
            return null;
        }
    }

    /**
     * Set a session property (generic storage)
     */
    async setSessionProperty(key, value) {
        const token = this.getToken();
        if (!token) {
            throw new Error('No auth token');
        }

        try {
            const response = await fetch(`${this.baseUrl}/session/property`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Authorization': `Bearer ${token}`
                },
                body: JSON.stringify({ key, value })
            });

            if (!response.ok) {
                throw new Error('Failed to set session property');
            }

            return await response.json();
        } catch (error) {
            console.error('Error setting session property:', error);
            return null;
        }
    }

    /**
     * Get a specific session property
     */
    async getSessionProperty(key) {
        const token = this.getToken();
        if (!token) {
            return null;
        }

        try {
            const response = await fetch(`${this.baseUrl}/session/property/${key}`, {
                headers: {
                    'Authorization': `Bearer ${token}`
                }
            });

            if (response.status === 404) {
                return null;
            }

            if (!response.ok) {
                throw new Error('Failed to get session property');
            }

            const data = await response.json();
            return data.value;
        } catch (error) {
            console.error('Error getting session property:', error);
            return null;
        }
    }

    /**
     * Get all session properties
     */
    async getAllSessionProperties() {
        const token = this.getToken();
        if (!token) {
            return {};
        }

        try {
            const response = await fetch(`${this.baseUrl}/session/properties`, {
                headers: {
                    'Authorization': `Bearer ${token}`
                }
            });

            if (!response.ok) {
                throw new Error('Failed to get session properties');
            }

            return await response.json();
        } catch (error) {
            console.error('Error getting session properties:', error);
            return {};
        }
    }

    /**
     * Logout - clear token and redirect
     */
    async logout() {
        const token = this.getToken();
        
        if (token) {
            try {
                await fetch(`${this.baseUrl}/api/auth/logout`, {
                    method: 'POST',
                    headers: {
                        'Authorization': `Bearer ${token}`
                    }
                });
            } catch (error) {
                console.error('Logout error:', error);
            }
        }
        
        // Clear session storage
        sessionStorage.clear();
        window.location.href = '/login.html';
    }

    /**
     * Helper: Make authenticated API call
     */
    async apiCall(endpoint, options = {}) {
        const token = this.getToken();
        if (!token) {
            throw new Error('No auth token');
        }

        const defaultOptions = {
            headers: {
                'Content-Type': 'application/json',
                'Authorization': `Bearer ${token}`,
                ...options.headers
            }
        };

        const response = await fetch(`${this.baseUrl}/api/${endpoint}`, {
            ...options,
            ...defaultOptions,
            headers: { ...defaultOptions.headers, ...options.headers }
        });

        if (response.status === 401) {
            this.logout();
            throw new Error('Session expired');
        }

        return response;
    }
}

// Create global instance
window.sessionManager = new SessionManager();