// public/session-timeout.js 
/**
 * Session Timeout Manager
 * Tracks user idle time and automatically logs out after configured timeout
 * Following Authentication & Session Management patterns
 */

(function() {
    'use strict';

    console.log('🕐 Initializing Session Timeout Manager...');

    const SessionTimeoutManager = {
        idleTimeoutMinutes: 10, // Default: 10 minutes (will be loaded from config)
        warningTimeMinutes: 2,  // Show warning 2 minutes before timeout
        idleTimer: null,
        warningTimer: null,
        lastActivityTime: Date.now(),
        warningShown: false,

        /**
         * Initialize timeout manager
         */
        async init() {
            try {
                // Load timeout configuration from backend
                await this.loadTimeoutConfig();

                // Set up activity listeners
                this.setupActivityListeners();

                // Start idle timer
                this.resetIdleTimer();

                console.log(`✅ Session timeout initialized: ${this.idleTimeoutMinutes} minutes`);
            } catch (error) {
                console.error('❌ Error initializing session timeout:', error);
                // Use default timeout if config fails to load
                this.setupActivityListeners();
                this.resetIdleTimer();
            }
        },

        /**
         * Load timeout configuration from backend
         */
        async loadTimeoutConfig() {
            try {
                const token = sessionStorage.getItem('authToken');
                if (!token) return;

                const response = await fetch(AppConfig.getApiUrl('session/timeout-config'), {
                    headers: { 'Authorization': `Bearer ${token}` }
                });

                if (response.ok) {
                    const config = await response.json();
                    this.idleTimeoutMinutes = config.timeoutMinutes || 10;
                    console.log(`✅ Loaded timeout config: ${this.idleTimeoutMinutes} minutes`);
                }
            } catch (error) {
                console.warn('⚠️ Could not load timeout config, using default:', error);
            }
        },

        /**
         * Set up activity event listeners
         */
        setupActivityListeners() {
            const events = ['mousedown', 'keydown', 'scroll', 'touchstart', 'click'];
            
            events.forEach(event => {
                document.addEventListener(event, () => this.onUserActivity(), true);
            });

            // Listen for API calls (successful responses indicate activity)
            window.addEventListener('apiCallSuccess', () => this.onUserActivity());
        },

        /**
         * Handle user activity
         */
        onUserActivity() {
            this.lastActivityTime = Date.now();
            this.resetIdleTimer();
            
            // Hide warning if shown
            if (this.warningShown) {
                this.hideWarning();
            }
        },

        /**
         * Reset idle timer
         */
        resetIdleTimer() {
            // Clear existing timers
            if (this.idleTimer) clearTimeout(this.idleTimer);
            if (this.warningTimer) clearTimeout(this.warningTimer);

            // Set warning timer (X minutes before logout)
            const warningDelay = (this.idleTimeoutMinutes - this.warningTimeMinutes) * 60 * 1000;
            this.warningTimer = setTimeout(() => this.showWarning(), warningDelay);

            // Set logout timer
            const logoutDelay = this.idleTimeoutMinutes * 60 * 1000;
            this.idleTimer = setTimeout(() => this.performAutoLogout(), logoutDelay);
        },

        /**
         * Show timeout warning
         */
        showWarning() {
            if (this.warningShown) return;
            
            this.warningShown = true;
            const remainingMinutes = this.warningTimeMinutes;

            // Create warning modal
            const modal = document.createElement('div');
            modal.id = 'sessionTimeoutWarning';
            modal.className = 'modal-overlay';
            modal.style.cssText = 'position: fixed; top: 0; left: 0; width: 100%; height: 100%; ' +
                'background: rgba(0,0,0,0.5); display: flex; align-items: center; justify-content: center; z-index: 10000;';
            
            modal.innerHTML = `
                <div class="modal-content" style="background: white; padding: 30px; border-radius: 8px; max-width: 400px; text-align: center; direction: rtl;">
                    <h2 style="margin-bottom: 15px; color: #d9534f;">⏰ אזהרת זמן קצוב</h2>
                    <p style="margin-bottom: 20px; font-size: 16px;">
                        השיחה שלך תסתיים בעוד <strong>${remainingMinutes} דקות</strong> עקב חוסר פעילות.
                    </p>
                    <p style="margin-bottom: 25px; color: #666;">
                        לחץ "המשך" כדי להישאר מחובר, או "התנתק" כדי לצאת כעת.
                    </p>
                    <div style="display: flex; gap: 10px; justify-content: center;">
                        <button onclick="window.SessionTimeoutManager.continueSession()" 
                                class="btn btn-primary" 
                                style="padding: 10px 20px; font-size: 16px;">
                            המשך פעילות
                        </button>
                        <button onclick="window.SessionTimeoutManager.logoutNow()" 
                                class="btn btn-secondary" 
                                style="padding: 10px 20px; font-size: 16px;">
                            התנתק כעת
                        </button>
                    </div>
                </div>
            `;

            document.body.appendChild(modal);
            console.log(`⚠️ Showing timeout warning: ${remainingMinutes} minutes remaining`);
        },

        /**
         * Hide warning modal
         */
        hideWarning() {
            const modal = document.getElementById('sessionTimeoutWarning');
            if (modal) {
                modal.remove();
                this.warningShown = false;
                console.log('✅ Timeout warning hidden');
            }
        },

        /**
         * Continue session (user clicked continue)
         */
        continueSession() {
            console.log('✅ User chose to continue session');
            this.hideWarning();
            this.onUserActivity(); // Reset timer
        },

        /**
         * Logout immediately (user clicked logout)
         */
        logoutNow() {
            console.log('🚪 User chose to logout now');
            this.hideWarning();
            this.performAutoLogout();
        },

        /**
         * Perform automatic logout
         */
        async performAutoLogout() {
            console.log('🚪 Performing automatic logout due to inactivity');

            try {
                const token = sessionStorage.getItem('authToken');
                if (token) {
                    // Call logout API
                    await fetch(AppConfig.getApiUrl('auth/logout'), {
                        method: 'POST',
                        headers: { 'Authorization': `Bearer ${token}` }
                    });
                }
            } catch (error) {
                console.error('Error during auto-logout API call:', error);
            }

            // Clear session storage
            sessionStorage.clear();

            // Show logout message
            alert('התנתקת אוטומטית עקב חוסר פעילות.');

            // Redirect to login
            window.location.href = 'login.html';
        },

        /**
         * Stop timeout manager (call on manual logout)
         */
        stop() {
            if (this.idleTimer) clearTimeout(this.idleTimer);
            if (this.warningTimer) clearTimeout(this.warningTimer);
            this.hideWarning();
            console.log('⏹️ Session timeout manager stopped');
        }
    };

    // Export to window
    window.SessionTimeoutManager = SessionTimeoutManager;

    // Auto-initialize on page load if authenticated
    document.addEventListener('DOMContentLoaded', function() {
        const token = sessionStorage.getItem('authToken');
        if (token && window.location.pathname !== '/login.html') {
            SessionTimeoutManager.init();
        }
    });

    console.log('✅ Session Timeout Manager loaded');
})();