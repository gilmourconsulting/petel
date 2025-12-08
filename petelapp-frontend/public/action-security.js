// petelapp-frontend/public/action-security.js
/**
 * Action-Based Security Framework - Frontend Implementation (UPDATED)
 * 
 * This utility intercepts onclick event handlers and verifies actions against 
 * the backend security function before allowing execution.
 * 
 * Architecture:
 * 1. Extracts onclick function name from button/element
 * 2. Constructs action identifier: {screenName}_{onclickFunctionName}
 * 3. Calls backend to verify user has permission
 * 4. Allows or prevents action execution
 * 
 * Usage:
 * - Add to index.html (loaded at application startup)
 * - All clickable elements with onclick automatically protected
 * - Actions table contains: name={screenName}_{functionName}, onclick_name={functionName}
 */

window.ActionSecurity = {
    // Cache for user actions to avoid repeated backend calls
    _userActionsCache: null,
    _cacheDuration: 5 * 60 * 1000, // 5 minute cache
    _cacheTimestamp: null,
    _currentScreenName: 'unknown',

    // Initialize the action security system
    async initialize() {
        console.log('🔐 Initializing Action Security Framework (onclick-based)...');

        // Get current screen name from page lifecycle
        this._updateCurrentScreenName();

        // Pre-load user actions on startup
        await this.preloadUserActions();

        // Setup global element click interceptor
        this.setupClickInterceptor();

        // Listen for page changes to update screen name
        window.addEventListener('pageChanged', (e) => {
            this._currentScreenName = e.detail?.pageName || 'unknown';
            console.log(`📄 Screen changed to: ${this._currentScreenName}`);
        });

        console.log('✅ Action Security Framework initialized');
    },

    // Update current screen name from PageLifecycleManager
    _updateCurrentScreenName() {
        if (window.PageLifecycleManager?.currentPage) {
            this._currentScreenName = window.PageLifecycleManager.currentPage;
            console.log(`📄 Initial screen: ${this._currentScreenName}`);
        }
    },

    // Pre-load all user actions from backend
    async preloadUserActions() {
        try {
            const response = await fetch(AppConfig.getApiUrl('security/user-actions'), {
                headers: {
                    'Authorization': `Bearer ${sessionStorage.getItem('authToken')}`
                }
            });

            if (response.ok) {
                this._userActionsCache = await response.json();
                this._cacheTimestamp = Date.now();
                console.log(`✅ Pre-loaded ${this._userActionsCache.length} user actions`);

                // Log action names for debugging
                const actionNames = this._userActionsCache.map(a => a.name).sort();
                console.log('📋 Available actions:', actionNames);
            } else {
                console.warn('⚠️ Could not pre-load user actions (HTTP ' + response.status + ')');
                this._userActionsCache = [];
            }
        } catch (error) {
            console.error('❌ Error pre-loading user actions:', error);
            this._userActionsCache = [];
        }
    },

    // Get all user actions from cache or backend
    async getUserActions(forceRefresh = false) {
        const now = Date.now();

        // Return cached if valid
        if (!forceRefresh && this._userActionsCache &&
            (now - this._cacheTimestamp) < this._cacheDuration) {
            return this._userActionsCache;
        }

        // Fetch fresh from backend
        try {
            const response = await fetch(AppConfig.getApiUrl('security/user-actions'), {
                headers: {
                    'Authorization': `Bearer ${sessionStorage.getItem('authToken')}`
                }
            });

            if (response.ok) {
                this._userActionsCache = await response.json();
                this._cacheTimestamp = now;
                return this._userActionsCache;
            }
        } catch (error) {
            console.error('❌ Error fetching user actions:', error);
        }

        return [];
    },

    // Extract onclick function name from element
    // Extract onclick function name from element
    _getOnclickFunctionName(element) {
        const onclick = element.getAttribute('onclick');
        if (!onclick) return null;

    // Remove event calls and return statements
    // Step 1: Start with original onclick
    let step1 = onclick;
    console.debug(`Step 1 - Original: ${step1}`);

    // Step 2: Remove event.X() calls
    let step2 = step1.replace(/event\.\w+\(\);\s*/g, '');
    console.debug(`Step 2 - After removing event calls: ${step2}`);

    // Step 3: Remove return false statements
    let step3 = step2.replace(/return\s+false;\s*/g, '');
    console.debug(`Step 3 - After removing return false: ${step3}`);

    // Step 4: Trim whitespace
    let step4 = step3.trim();
    console.debug(`Step 4 - After trim: ${step4}`);

    // Step 5: Extract function name - handle both window.func() and func()
    // Match: optional 'window.', then word characters (function name)
    const step5 = step4.replace(/window\./, '');
    //const step5 = step4.match(/^(?:window\.)?(\w+)/);
    console.debug(`Step 5 - Regex replace result:`, step5);

    const match = step5.match(/^(?:window\.)?(\w+)/);;

        if (match && match[1]) {
            console.debug(`📝 Extracted function name: ${match[1]} from onclick: ${onclick}`);
            return match[1];
        }

        console.debug(`⚠️ Could not extract function name from onclick: ${onclick}`);
        return null;
    },

    // Construct action identifier from screen name and function name
    _constructActionId(screenName, functionName) {
        if (!screenName || !functionName) return null;

        // Format: screenname_functionname (all lowercase)
        return `${screenName}_${functionName}`.toLowerCase();
    },

    // Check if action exists in user's allowed actions
    async hasPermission(actionName) {
        try {
            const userActions = await this.getUserActions();
            return userActions.some(a => a.name.toLowerCase() === actionName.toLowerCase());
        } catch (error) {
            console.error('❌ Error checking permission:', error);
            return false;
        }
    },

    // Update current screen name from PageLifecycleManager
    _updateCurrentScreenName() {
        if (window.PageLifecycleManager?.currentPage) {
            this._currentScreenName = window.PageLifecycleManager.currentPage;
            console.log(`📄 Initial screen: ${this._currentScreenName}`);
        } else {
            console.warn(`⚠️ PageLifecycleManager.currentPage not available, using 'unknown'`);
        }
    },

    // Setup global click interceptor for all elements with onclick
    setupClickInterceptor() {
        document.addEventListener('click', async (event) => {
            const element = event.target.closest('[onclick]');

            if (!element) return;

            // Skip certain elements
            if (this.shouldSkipSecurityCheck(element)) {
                return;
            }

            // Update screen name on each click (in case page changed)
            this._updateCurrentScreenName();

            // Extract onclick function name
            const functionName = this._getOnclickFunctionName(element);

            if (!functionName) {
                console.debug('ℹ️ Could not extract function name from onclick');
                return;
            }

            // Construct action identifier
            const actionId = this._constructActionId(this._currentScreenName, functionName);

            if (!actionId) {
                console.debug('ℹ️ Could not construct action ID');
                return;
            }
            const hasAccess = await this.hasPermission(actionId);

            if (!hasAccess) {
                // 🚫 DENY ACCESS
                event.preventDefault();
                event.stopPropagation();

                console.warn(`🚫 Access DENIED - Action: ${actionId}`);
                alert(`אין לך הרשאה לפעולה זו`);

                this._logEvent('ACCESS_DENIED', this._currentScreenName, functionName, actionId, 'DENIED');
                return;
            }

            // ✅ ALLOW ACCESS
            console.log(`✅ Access GRANTED - Action: ${actionId}`);
            this._logEvent('ACCESS_GRANTED', this._currentScreenName, functionName, actionId, 'ALLOWED');
            // Event continues normally
            // ... rest of the method
        }, true);
    },

    // Log event with context
    _logEvent(eventType, screenName, functionName, actionName, result) {
        const timestamp = new Date().toISOString();
        const logEntry = {
            timestamp,
            eventType,
            screenName,
            functionName,
            actionName,
            result,
            userId: sessionStorage.getItem('userId') || 'unknown'
        };

        console.log(`[${eventType}]`, logEntry);

        // Optional: Send to backend for audit logging
        // await fetch(AppConfig.getApiUrl('audit/log'), {
        //     method: 'POST',
        //     headers: {
        //         'Content-Type': 'application/json',
        //         'Authorization': `Bearer ${sessionStorage.getItem('authToken')}`
        //     },
        //     body: JSON.stringify(logEntry)
        // }).catch(e => console.error('Failed to send audit log:', e));
    },

    // Setup global click interceptor for all elements with onclick

    // Determine if element should skip security check
    shouldSkipSecurityCheck(element) {
        // Skip system buttons that shouldn't be secured
        const skipClasses = [
            'menu-toggle',
            'modal-close',
            'dialog-btn',
            'collapse-toggle',
            'logout-btn',
            'debug-btn'
        ];

        for (const className of skipClasses) {
            if (element.classList.contains(className)) {
                return true;
            }
        }

        // Skip elements with certain onclick patterns
        const onclick = element.getAttribute('onclick') || '';
        const skipPatterns = [
            'toggleMenu',
            'closeModal',
            'closeDialog',
            'toggleCard',
            'event.stopPropagation',
            'window.history',
            'window.location'
        ];

        for (const pattern of skipPatterns) {
            if (onclick.includes(pattern)) {
                return true;
            }
        }

        return false;
    },

    // Refresh user actions cache
    async refreshCache() {
        console.log('🔄 Refreshing user actions cache...');
        await this.preloadUserActions();
    },

    // Export action logging for external use
    async logAction(screenName, functionName) {
        const actionId = this._constructActionId(screenName, functionName);
        const hasAccess = await this.hasPermission(actionId);

        this._logEvent(
            'MANUAL_CHECK',
            screenName,
            functionName,
            actionId,
            hasAccess ? 'ALLOWED' : 'DENIED'
        );

        return hasAccess;
    }
};

// Initialize on page load
window.addEventListener('DOMContentLoaded', () => {
    if (sessionStorage.getItem('authToken')) {
        window.ActionSecurity.initialize();
    }
});

// Expose to window for external use
window.verifyActionAccess = async (screenName, functionName) =>
    window.ActionSecurity.logAction(screenName, functionName);