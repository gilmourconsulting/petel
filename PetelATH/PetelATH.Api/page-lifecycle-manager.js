/**
 * Page Lifecycle Manager
 * Centralized cleanup and initialization for all pages
 * Following Cross-Component Communication + Authentication & Session Management patterns
 */

window.PageLifecycleManager = {
    currentPage: null,
    previousPage: null,

    /**
     * Navigate to a new page with proper cleanup
     */
    async navigateTo(targetPage, fromPopstate = false) {
        console.log(`🔄 PageLifecycleManager: Navigating from ${this.currentPage} to ${targetPage}`);

        if (!window.checkAuthentication()) {
            console.log('❌ Authentication failed, aborting navigation');
            return;
        }

        const pageConfig = window.PageLifecycleConfig.getPageConfig(targetPage);
        if (!pageConfig) {
            console.error(`❌ No configuration found for page: ${targetPage}`);
            alert('שגיאה: עמוד לא נמצא');
            return;
        }

        try {
            // Step 1: Cleanup current page
            if (this.currentPage) {
                await this.cleanupPage(this.currentPage);
            }

            // Step 2: Apply navigation rules (clear session data)
            if (this.currentPage) {
                await this.applyNavigationRules(this.currentPage, targetPage);
            }

            // Step 3: Load new page HTML
            console.log(`📄 Loading ${pageConfig.file}...`);
            const response = await fetch(pageConfig.file);
            if (!response.ok) {
                throw new Error(`HTTP ${response.status} loading ${pageConfig.file}`);
            }

            const html = await response.text();
            const dynamicContent = document.getElementById('dynamicContent');
            if (!dynamicContent) {
                throw new Error('dynamicContent container not found');
            }

            dynamicContent.innerHTML = html;

            // Step 4: Re-execute scripts in loaded content
            window.executeScriptsInContainer(dynamicContent);

            // Step 5: Update navigation state
            this.previousPage = this.currentPage;
            this.currentPage = targetPage;

            // Step 6: Update browser history
            if (!fromPopstate) {
                history.pushState(
                    { section: targetPage },
                    pageConfig.title,
                    `#${targetPage}`
                );
            }

            // Step 7: Initialize new page (after a brief delay for scripts to load)
            setTimeout(() => {
                this.initializePage(targetPage, pageConfig);
            }, 100);

            console.log(`✅ Successfully navigated to ${targetPage}`);

        } catch (error) {
            console.error(`❌ Error navigating to ${targetPage}:`, error);
            alert('שגיאה בטעינת העמוד');
        }
    },

    /**
     * Cleanup a page before leaving it
     */
    async cleanupPage(pageName) {
        console.log(`🧹 Cleaning up page: ${pageName}`);

        const pageConfig = window.PageLifecycleConfig.getPageConfig(pageName);
        if (!pageConfig || !pageConfig.cleanup) {
            console.log(`ℹ️ No cleanup needed for ${pageName}`);
            return;
        }

        const cleanupFn = window[pageConfig.cleanup];
        if (typeof cleanupFn === 'function') {
            try {
                await cleanupFn();
                console.log(`✅ ${pageConfig.cleanup}() executed successfully`);
            } catch (error) {
                console.error(`❌ Error in ${pageConfig.cleanup}():`, error);
            }
        } else {
            console.warn(`⚠️ Cleanup function not found: ${pageConfig.cleanup}`);
        }

        // Clear any table component instances
        this.clearTableComponents();
    },

    /**
     * Initialize a page after loading it
     */
    async initializePage(pageName, pageConfig) {
        console.log(`🚀 Initializing page: ${pageName}`);

        if (!pageConfig.init) {
            console.log(`ℹ️ No initialization needed for ${pageName}`);
            return;
        }

        const initFn = window[pageConfig.init];
        if (typeof initFn === 'function') {
            try {
                await initFn();
                console.log(`✅ ${pageConfig.init}() executed successfully`);
            } catch (error) {
                console.error(`❌ Error in ${pageConfig.init}():`, error);
            }
        } else {
            console.warn(`⚠️ Init function not found: ${pageConfig.init}`);
        }
    },

    /**
     * Apply navigation rules (clear session data)
     */
    async applyNavigationRules(fromPage, toPage) {
        const rule = window.PageLifecycleConfig.getNavigationRule(fromPage, toPage);
        if (!rule || !rule.clearSession || rule.clearSession.length === 0) {
            console.log(`ℹ️ No session clearing needed for ${fromPage} → ${toPage}`);
            return;
        }

        console.log(`🗑️ Clearing session data for navigation ${fromPage} → ${toPage}:`, rule.clearSession);

        for (const key of rule.clearSession) {
            try {
                await window.SessionState.setProperty(key, '');
                console.log(`✅ Cleared session key: ${key}`);
            } catch (error) {
                console.error(`❌ Error clearing session key ${key}:`, error);
            }
        }
    },

    /**
     * Clear all ReusableTable component instances
     */
    clearTableComponents() {
        console.log('🧹 Clearing table component instances...');

        // Clear all window properties that look like table instances
        const tableInstances = Object.keys(window).filter(key =>
            key.startsWith('documentsTableInstance_') ||
            key.startsWith('tableInstance_') ||
            key.includes('Table') && window[key] instanceof Object
        );

        tableInstances.forEach(key => {
            try {
                delete window[key];
                console.log(`✅ Cleared table instance: ${key}`);
            } catch (error) {
                console.warn(`⚠️ Could not clear ${key}:`, error);
            }
        });

        // Clear any orphaned table containers
        document.querySelectorAll('[id*="TableContainer"]').forEach(container => {
            if (container && !document.getElementById('dynamicContent')?.contains(container)) {
                container.innerHTML = '';
            }
        });
    },

    /**
     * Handle browser back/forward navigation
     */
    handlePopstate(event) {
        if (!event.state || !event.state.section) {
            console.log('⚠️ Popstate with no section, loading main dashboard');
            this.navigateTo('maindashboard', true);
            return;
        }

        console.log(`◀️ Popstate navigation to: ${event.state.section}`);
        this.navigateTo(event.state.section, true);
    }
};

// Initialize on load
console.log('✅ PageLifecycleManager loaded');