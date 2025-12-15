// petelapp-frontend/public/action-security.js
/**
 * Action-Based Security Framework - Frontend Implementation
 * 
 * SECURE DESIGN PRINCIPLES:
 * 1. Fail-secure: Any error = DENY access (never allow by default)
 * 2. Server-side logging: Audit trails written by backend only
 * 3. Initialization check: System locks down if security fails to load
 * 4. Backend verification: All authorization decisions made server-side
 * 
 * Architecture:
 * 1. Frontend intercepts onclick events
 * 2. Sends request to backend: /api/security/verify-action-secure
 * 3. Backend: verifies permission + logs audit trail
 * 4. Frontend: allows or denies action based on backend response
 */

window.ActionSecurity = {
    _initialized: false,
    _initializationError: false,
    _currentScreenName: 'unknown',

    // Initialize the action security system
    async initialize() {
        console.log('🔐 Initializing Action Security Framework...');

        try {
            // Get current screen name
            this._updateCurrentScreenName();

            // Setup global click interceptor
            this.setupClickInterceptor();

            // Listen for page changes
            window.addEventListener('pageChanged', (e) => {
                this._currentScreenName = e.detail?.pageName || 'unknown';
                console.log(`📄 Screen changed to: ${this._currentScreenName}`);
            });

            this._initialized = true;
            this._initializationError = false;
            console.log('✅ Action Security Framework initialized');

        } catch (error) {
            console.error('❌ CRITICAL: Action Security Framework failed to initialize:', error);
            this._initialized = false;
            this._initializationError = true;

            // ✅ FAIL-SECURE: Block all actions if security fails to load
            this.blockAllActions();
        }
    },

    // Update current screen name from PageLifecycleManager
    _updateCurrentScreenName() {
        if (window.PageLifecycleManager?.currentPage) {
            this._currentScreenName = window.PageLifecycleManager.currentPage;
            console.log(`📄 Current screen: ${this._currentScreenName}`);
        } else {
            console.warn(`⚠️ PageLifecycleManager not available, using 'unknown'`);
        }
    },



    // Setup global click interceptor for all elements with onclick
    setupClickInterceptor() {
        document.addEventListener('click', async (event) => {
            const element = event.target.closest('[onclick]');
            if (!element) return;

            // ✅ FAIL-SECURE: Block if security system not initialized
            if (!this._initialized || this._initializationError) {
                event.preventDefault();
                event.stopPropagation();
                event.stopImmediatePropagation(); // ✅ Prevent ALL handlers
                console.error('🚫 SECURITY SYSTEM NOT INITIALIZED - ALL ACTIONS BLOCKED');
                alert('מערכת האבטחה לא פעילה. אנא רענן את הדף.');
                return;
            }

            // Skip system buttons
            if (this.shouldSkipSecurityCheck(element)) {
                return;
            }

            // ✅ CRITICAL: Prevent onclick from executing immediately
            event.preventDefault();
            event.stopPropagation();
            event.stopImmediatePropagation();

            // Update screen name
            this._updateCurrentScreenName();

            // Extract function name and parameters
            const functionName = this._getOnclickFunctionName(element);
            const actionParams = this._extractActionParams(element);

            if (!functionName) {
                console.debug('ℹ️ Could not extract function name');
                return;
            }

            // Construct action identifier
            const actionId = this._constructActionId(this._currentScreenName, functionName);
            if (!actionId) {
                console.debug('ℹ️ Could not construct action ID');
                return;
            }

            // ✅ VERIFY WITH BACKEND (includes audit logging server-side)
            const hasAccess = await this._verifyActionSecure(
                actionId,
                this._currentScreenName,
                functionName,
                'ONCLICK_BUTTON',
                actionParams
            );

            if (!hasAccess) {
                // 🚫 DENY ACCESS
      //          console.warn(`🚫 Access DENIED - Action: ${actionId}`);
                alert(`אין לך הרשאה לפעולה זו`);
                return;
            }

            // ✅ ALLOW ACCESS - Execute the onclick function
      //      console.log(`✅ Access GRANTED - Action: ${actionId}`);

            try {
                // Get the onclick attribute and execute it
                const onclickCode = element.getAttribute('onclick');
                if (onclickCode) {
                    // Create a function from the onclick code and execute it in the element's context
                    const func = new Function(onclickCode);
                    func.call(element);
                }
            } catch (error) {
                console.error('❌ Error executing onclick function:', error);
            }
        }, true); // ✅ Capture phase - runs BEFORE onclick
    },

    // ✅ SECURE: Verify action with backend (backend handles audit logging)
    async _verifyActionSecure(actionName, screenName, functionName, eventType, actionParams = null) {
        try {
            const authToken = sessionStorage.getItem('authToken');
            if (!authToken) {
                console.error('❌ No auth token');
                return false;
            }

            const response = await fetch(AppConfig.getApiUrl('security/verify-action-secure'), {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Authorization': `Bearer ${authToken}`
                },
                body: JSON.stringify({
                    actionName,
                    screenName,
                    functionName,
                    eventType,
                    actionParams
                })
            });

            if (!response.ok) {
                console.error(`❌ Authorization check failed: HTTP ${response.status}`);
                return false; // ✅ FAIL-SECURE: Deny on error
            }

            const result = await response.json();
            return result.allowed === true;

        } catch (error) {
            console.error('❌ Error verifying action:', error);
            return false; // ✅ FAIL-SECURE: Deny on error
        }
    },

    // Extract onclick function name
    _getOnclickFunctionName(element) {
        const onclick = element.getAttribute('onclick');
        if (!onclick) return null;

        try {
            let cleaned = onclick
                .replace(/event\.\w+\(\);\s*/g, '')
                .replace(/return\s+false;\s*/g, '')
                .trim()
                .replace(/window\./, '');

          //  const match = cleaned.match(/^(\w+)/);
            const match = cleaned.match(/^(?:\w+\.)?(\w+)/);
            return match ? match[1] : null;
        } catch (error) {
            console.error('Error extracting function name:', error);
            return null;
        }
    },

    // Extract action parameters
    _extractActionParams(element) {
        const onclick = element.getAttribute('onclick');
        if (!onclick) return null;

        try {
            const paramsMatch = onclick.match(/\(([^)]+)\)/);
            return paramsMatch && paramsMatch[1] ? paramsMatch[1].trim() : null;
        } catch (error) {
            return null;
        }
    },

    // Construct action identifier
    _constructActionId(screenName, functionName) {
        if (!screenName || !functionName) return null;
        return `${screenName}_${functionName}`.toLowerCase();
    },

    // Determine if element should skip security check
    shouldSkipSecurityCheck(element) {
        const skipClasses = [
            'menu-toggle', 'modal-close', 'dialog-btn',
            'collapse-toggle', 'logout-btn', 'debug-btn'
        ];

        for (const className of skipClasses) {
            if (element.classList.contains(className)) {
                return true;
            }
        }

        const onclick = element.getAttribute('onclick') || '';
        const skipPatterns = [
            'toggleMenu', 'closeModal', 'closeDialog',
            'toggleCard', 'event.stopPropagation',
            'window.history', 'window.location'
        ];

        for (const pattern of skipPatterns) {
            if (onclick.includes(pattern)) {
                return true;
            }
        }

        return false;
    },

    // ✅ FAIL-SECURE: Block all actions if security system fails
    blockAllActions() {
        console.error('🚨 BLOCKING ALL ACTIONS - SECURITY SYSTEM FAILURE');

        document.addEventListener('click', (event) => {
            const element = event.target.closest('[onclick]');
            if (element && !this.shouldSkipSecurityCheck(element)) {
                event.preventDefault();
                event.stopPropagation();
                alert('מערכת האבטחה לא פעילה. אנא רענן את הדף.');
            }
        }, true);
    },

    // Public method for menu navigation
    async verifyMenuNavigation(menuName, menuReference) {
        if (!this._initialized || this._initializationError) {
            console.error('🚫 Security system not initialized');
            return false;
        }

        return await this._verifyActionSecure(
            menuName,
            'menu',
            'navigateTo',
            'MENU_NAVIGATION',
            menuReference
        );
    }
};

// ✅ Initialize on page load with error handling
window.addEventListener('DOMContentLoaded', () => {
    if (sessionStorage.getItem('authToken')) {
        window.ActionSecurity.initialize().catch(error => {
            console.error('❌ CRITICAL: Security initialization failed:', error);
            alert('שגיאה קריטית: מערכת האבטחה לא נטענה. אנא רענן את הדף.');
        });
    }
});

// Expose for external use
window.verifyActionAccess = async (screenName, functionName, eventType = 'MANUAL_CHECK') => {
    if (!window.ActionSecurity._initialized) {
        console.error('Security system not initialized');
        return false;
    }
    const actionId = window.ActionSecurity._constructActionId(screenName, functionName);
    return await window.ActionSecurity._verifyActionSecure(
        actionId, screenName, functionName, eventType
    );
};