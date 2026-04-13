/**
 * Bootstrap Configuration
 * This file contains ONLY the backend URL needed to load all other configuration.
 * 
 * DEPLOYMENT INSTRUCTIONS:
 * - Development: Keep as localhost:5082
 * - Production: Update this ONE file with production URL before deployment
 * - This is the ONLY hardcoded URL in the entire frontend
 */

// ✅ SINGLE SOURCE OF TRUTH for backend URL
const BACKEND_URL = (function() {
    // Auto-detect development environment
    if (window.location.hostname === 'localhost' || 
        window.location.hostname === '127.0.0.1') {
        return 'http://localhost:5082';
    }
    
    // Production: use same domain (frontend and backend on same server)
    return window.location.origin;
})();

// ✅ SECURE: Only expose bootstrap URL getter, not the URL itself
window.BootstrapConfig = {
    getBackendUrl() {
        return BACKEND_URL;
    },
    
    getConfigUrl() {
        return `${BACKEND_URL}/api/config/client`;
    }
};

console.log('✅ Bootstrap config loaded for:', window.location.hostname);