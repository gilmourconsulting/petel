/**
 * Alerts/Events Modal Component
 * Handles creation of new alerts and events
 */
if (typeof window.AlertsModal === 'undefined') {
    window.AlertsModal = class AlertsModal {
    constructor() {
        this.modalId = 'alertsModalContainer';
        this.isEvent = false;
        this.alertLevel = null;
        this.entityTypeId = null;
        this.entityId = null;
    }

    /**
     * Show modal for creating alert or event
     * @param {boolean} isEvent - true for event, false for alert
     */
    async show(isEvent) {
        try {
            this.isEvent = isEvent;

            // Get session data
            const session = await window.SessionState.getSession();
            this.entityId = parseInt(session.entityId, 10);
            this.entityTypeId = parseInt(session.entityTypeId, 10);

            // Determine alert level based on entity type
            this.alertLevel = this.determineAlertLevel();

            console.log('📝 Opening modal:', {
                isEvent: this.isEvent,
                entityTypeId: this.entityTypeId,
                alertLevel: this.alertLevel
            });

            // Create and show modal
            this.render();
        } catch (error) {
            console.error('❌ Error showing modal:', error);
            alert('שגיאה בפתיחת חלון הוספה');
        }
    }

    /**
     * Determine alert level based on entity type
     */
    determineAlertLevel() {
        // On school dashboard, always level 7 (school)
        if (window.location.pathname.includes('schooldashboard') || 
            window.SessionState.getProperty('SelectedSchoolId')) {
            return 7;
        }

        // On main dashboard, based on entity type
        switch (this.entityTypeId) {
            case 1:  // Ministry
                return 7;
            case 5:  // School network
                return 3;
            case 6:  // Owner
                return 2;
            default:
                return 7;
        }
    }

    /**
     * Render modal HTML
     */
    render() {
        const modalTitle = this.isEvent ? 'הוספת אירוע חדש' : 'הוספת התראה חדשה';
        
        const modalHtml = `
            <div id="${this.modalId}" class="modal-overlay" onclick="window.alertsModal.handleOverlayClick(event)">
                <div class="modal-content">
                    <div class="modal-header">
                        <h2>${modalTitle}</h2>
                        <button class="modal-close" onclick="window.alertsModal.close()">×</button>
                    </div>
                    <div class="modal-body">
                        <form id="alertForm" onsubmit="window.alertsModal.handleSubmit(event)">
                            <!-- Description Field -->
                            <div class="form-group">
                                <label for="alertDescription">תיאור *</label>
                                <textarea 
                                    id="alertDescription" 
                                    name="description" 
                                    rows="4" 
                                    required 
                                    placeholder="הזן תיאור ${this.isEvent ? 'האירוע' : 'ההתראה'}"
                                    class="form-control"
                                ></textarea>
                            </div>

                            ${this.isEvent ? `
                                <!-- Event Date Field -->
                                <div class="form-group">
                                    <label for="eventDate">תאריך אירוע *</label>
                                    <input 
                                        type="date" 
                                        id="eventDate" 
                                        name="eventDate" 
                                        required 
                                        class="form-control"
                                    />
                                </div>

                                <!-- Event Time Field -->
                                <div class="form-group">
                                    <label for="eventTime">שעת אירוע *</label>
                                    <input 
                                        type="time" 
                                        id="eventTime" 
                                        name="eventTime" 
                                        required 
                                        class="form-control"
                                    />
                                </div>
                            ` : ''}

                            <div class="modal-actions">
                                <button type="submit" class="btn-primary">שמור</button>
                                <button type="button" class="btn-secondary" onclick="window.alertsModal.close()">ביטול</button>
                            </div>
                        </form>
                    </div>
                </div>
            </div>
        `;

        // Remove existing modal if present
        const existingModal = document.getElementById(this.modalId);
        if (existingModal) {
            existingModal.remove();
        }

        // Add modal to DOM
        document.body.insertAdjacentHTML('beforeend', modalHtml);
    }

/**
 * Handle form submission
 */
async handleSubmit(event) {
    event.preventDefault();

    try {
        const formData = new FormData(event.target);
        const description = formData.get('description');
        let eventDate = null;

        // Combine date and time for events with proper timezone
        if (this.isEvent) {
            const dateValue = formData.get('eventDate');
            const timeValue = formData.get('eventTime');
            
            // ✅ Create proper ISO 8601 datetime with timezone
            // Assumes local timezone of the user's browser
            const localDateTime = new Date(`${dateValue}T${timeValue}:00`);
            
            // Convert to ISO string (includes timezone)
            eventDate = localDateTime.toISOString();
            
            console.log('📅 Event datetime:', {
                date: dateValue,
                time: timeValue,
                combined: eventDate,
                timezone: Intl.DateTimeFormat().resolvedOptions().timeZone
            });
        }

        // Show loading state
        const submitBtn = event.target.querySelector('button[type="submit"]');
        const originalText = submitBtn.textContent;
        submitBtn.disabled = true;
        submitBtn.textContent = 'שומר...';

        // ✅ Ask distribution questions BEFORE API call
        const distributionFlags = await this.askDistributionQuestions();
        if (distributionFlags === null) {
            // User cancelled
            submitBtn.disabled = false;
            submitBtn.textContent = originalText;
            return;
        }

        console.log('📤 Saving alert:', {
            description,
            eventDate,
            isEvent: this.isEvent,
            alertLevel: this.alertLevel,
            distributeToOwned: distributionFlags.distributeToOwned,
            distributeToSchools: distributionFlags.distributeToSchools
        });

        // Create alert with distribution flags
        const result = await this.createAlert({
            description,
            eventDate,
            distributeToOwned: distributionFlags.distributeToOwned,
            distributeToSchools: distributionFlags.distributeToSchools
        });

        console.log('✅ Alert created:', result);

        // Close modal
        this.close();

        // Show success message
        alert(`${this.isEvent ? 'האירוע' : 'ההתראה'} נוסף בהצלחה`);

        // Refresh the appropriate card
        const cardId = this.isEvent ? 'eventsCard' : 'alertsCard';
        if (typeof window.loadDashboardCardData === 'function') {
            await window.loadDashboardCardData(cardId);
        }

    } catch (error) {
        console.error('❌ Error saving alert:', error);
        alert('שגיאה בשמירת ההתראה. אנא נסה שוב.');
        
        // Restore button state
        const submitBtn = event.target.querySelector('button[type="submit"]');
        if (submitBtn) {
            submitBtn.disabled = false;
            submitBtn.textContent = 'שמור';
        }
    }
}

    /**
     * Ask distribution questions based on alert level
     */
    async askDistributionQuestions() {
        const answers = {
            distributeToOwned: false,
            distributeToSchools: false
        };

        // Question 1: For owner level (2) - distribute to owned entities
        if (this.alertLevel === 2) {
            const result = window.confirm('להפיץ לכל הבעליות?');
            if (result === null) return null; // Cancelled
            answers.distributeToOwned = result;
        }

        // Question 2: For owner (2) or network (3) - distribute to schools
        if (this.alertLevel === 2 || this.alertLevel === 3) {
            const result = window.confirm('להפיץ לכל מוסדות החינוך?');
            if (result === null) return null; // Cancelled
            answers.distributeToSchools = result;
        }

        return answers;
    }

    /**
     * Create alert via API
     */
    async createAlert(data) {
        const requestBody = {
            alertType: 2,  // Manual
            alertLevel: this.alertLevel,
            description: data.description,
            status: 1,  // New
            isEvent: this.isEvent,
            eventDate: data.eventDate,
            distributeToOwned: data.distributeToOwned,
            distributeToSchools: data.distributeToSchools
        };

        console.log('📤 API Request:', requestBody);

        const response = await fetch(
            AppConfig.getApiUrl('alerts'),
            {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Authorization': `Bearer ${sessionStorage.getItem('authToken')}`
                },
                body: JSON.stringify(requestBody)
            }
        );

        if (!response.ok) {
            const errorText = await response.text();
            throw new Error(`Failed to create alert: ${response.status} - ${errorText}`);
        }

        const result = await response.json();
        return result;
    }

    /**
     * Handle click on overlay (close if clicking outside modal)
     */
    handleOverlayClick(event) {
        if (event.target.id === this.modalId) {
            this.close();
        }
    }

    /**
     * Close modal
     */
    close() {
        const modal = document.getElementById(this.modalId);
        if (modal) {
            modal.remove();
        }
    }
};
}
// ✅ Create or reuse global instance
if (!window.alertsModal) {
    window.alertsModal = new window.AlertsModal();
    console.log('✅ Alerts Modal instance created');
} else {
    console.log('✅ Alerts Modal instance already exists');
}