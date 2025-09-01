const AppConfig = {
    // API base URL following Critical Development Workflows
    getApiUrl: function(endpoint) {
        const baseUrl = 'http://localhost:5082/api';
        
        // Handle endpoint with or without leading slash following Frontend Architecture Patterns
        const cleanEndpoint = endpoint.startsWith('/') ? endpoint.slice(1) : endpoint;
        
        return `${baseUrl}/${cleanEndpoint}`;
    },

    // Environment detection following Project-Specific Patterns
    isProduction: function() {
        return window.location.hostname !== 'localhost' && window.location.hostname !== '127.0.0.1';
    },

    // Get appropriate base URL based on environment
    getBaseUrl: function() {
        return this.isProduction() ? 'https://your-production-domain.com/api' : 'http://localhost:5082/api';
    }
};

// Make globally available following Cross-Component Communication
window.AppConfig = AppConfig;