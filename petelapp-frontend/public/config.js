const AppConfig = {
    // API Configuration
    api: {
        baseUrl: window.location.hostname === 'localhost' 
            ? 'http://localhost:5082'  // Development
            : 'https://your-production-api.com',  // Production
        endpoints: {
            systemAttributes: '/api/systemattributes',
            systemAttributesRefresh: '/api/systemattributes/refresh',
            auth: '/api/auth',
            schoolBudget: '/api/schoolbudget',
            hoursBudgets: '/api/hoursbudget',
            studentRegistrationSummary: '/api/students/registration-summary'
        }
    },
    
    // Environment detection
    isDevelopment: window.location.hostname === 'localhost',
    
    // Helper function to build full API URLs
    getApiUrl: function(endpoint) {
        return this.api.baseUrl + this.api.endpoints[endpoint];
    }
};