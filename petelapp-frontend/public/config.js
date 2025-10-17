const AppConfig = {
    apiBaseUrl: 'http://localhost:5082/api',
    
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

// Make globally available following Cross-Component Communication
window.AppConfig = AppConfig;