// Load environment configuration (defaults if not loaded)
const ENV_CONFIG = window.ENV_CONFIG || {
    API_BASE_URL: 'http://localhost:5082/api',
    ENVIRONMENT: 'development'
};

const AppConfig = {
    apiBaseUrl: ENV_CONFIG.API_BASE_URL, // ✅ Use from environment
    environment: ENV_CONFIG.ENVIRONMENT,
    otpEnabled: ENV_CONFIG.OTP_ENABLED,
    
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
        return `${this.apiBaseUrl}/${endpoint}`;
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

window.AppConfig = AppConfig;