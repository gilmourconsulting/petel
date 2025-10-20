const AppConfig = {
    // API base URL - configure per environment
    baseUrl: window.location.hostname === 'localhost' 
        ? 'http://localhost:5082' 
        : 'https://your-production-api.com',
    
    /**
     * Get full API URL for endpoint
     * @param {string} endpoint - API endpoint path
     * @returns {string} Full API URL
     * 
     * IMPORTANT:
     * - systemAttributes endpoint: NO authentication needed (global config)
     * - All other endpoints: Require Authorization header with auth token
     */
    getApiUrl(endpoint) {
        // Remove leading slash if present
        const cleanEndpoint = endpoint.startsWith('/') ? endpoint.slice(1) : endpoint;
        return `${this.baseUrl}/api/${cleanEndpoint}`;
    },
    
    getDefaultFetchOptions() {
        const authToken = sessionStorage.getItem('authToken');
        
        return {
            headers: {
                'Content-Type': 'application/json',
                'Authorization': authToken ? `Bearer ${authToken}` : ''
            }
        };
    },
    
    async fetchWithAuth(url, options = {}) {
        const defaultOptions = this.getDefaultFetchOptions();
        const mergedOptions = { ...defaultOptions, ...options };
        
        if (options.body && typeof options.body === 'object') {
            mergedOptions.body = JSON.stringify(options.body);
        }
        
        try {
            const response = await fetch(url, mergedOptions);
            
            if (response.status === 401) {
                // Handle unauthorized - clear session and redirect to login
                sessionStorage.clear();
                window.location.href = 'login.html';
                throw new Error('Unauthorized');
            }
            
            return response;
        } catch (error) {
            console.error('API Error:', error);
            throw error;
        }
    }
};

// Make globally accessible
window.AppConfig = AppConfig;