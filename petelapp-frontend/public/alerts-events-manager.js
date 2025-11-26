/**
 * Alerts and Events Manager
 * Handles loading and displaying alerts/events from backend
 * Used by main dashboard and school dashboard
 */
class AlertsEventsManager {
    constructor() {
        this.alerts = [];
        this.events = [];
    }

    /**
     * Load alerts and events for any entity
     * @param {number|string} entityId - Entity ID to load alerts for
     * @returns {Promise<{alerts: Array, events: Array}>}
     */
    async loadAlertEventsData(entityId) {
        try {
            if (!entityId) {
                console.error('❌ No entity ID provided');
                return { alerts: [], events: [] };
            }

            console.log('📊 Loading alerts/events for entity:', entityId);

            // Load alerts (is_event=false)
            const alertsResponse = await fetch(
                AppConfig.getApiUrl(`alerts/entity/${entityId}?isEvent=false`),
                AppConfig.getDefaultFetchOptions()
            );

            if (!alertsResponse.ok) {
                throw new Error('Failed to load alerts');
            }

            this.alerts = await alertsResponse.json();
            console.log(`✅ Loaded ${this.alerts.length} alerts`);

            // Load events (is_event=true)
            const eventsResponse = await fetch(
                AppConfig.getApiUrl(`alerts/entity/${entityId}?isEvent=true`),
                AppConfig.getDefaultFetchOptions()
            );

            if (!eventsResponse.ok) {
                throw new Error('Failed to load events');
            }

            this.events = await eventsResponse.json();
            console.log(`✅ Loaded ${this.events.length} events`);

            return { alerts: this.alerts, events: this.events };

        } catch (error) {
            console.error('❌ Error loading alerts/events:', error);
            throw error;
        }
    }


    /**
  * Update alert status from 'new' to 'read'
  * @param {number} alertId - Alert ID
  * @param {number} entityId - Entity ID
  */
     async markAlertAsRead(alertId, entityId) {
        try {
            console.log(`📖 Marking alert as read: AlertId=${alertId}, EntityId=${entityId}`);

            const requestBody = {
                alertId: Number(alertId),
                entityId: Number(entityId),
                status: 2
            };

            console.log('📤 Sending update request:', requestBody);

            const response = await fetch(
                AppConfig.getApiUrl('alerts/status'),
                {
                    method: 'PUT',
                    headers: {
                        'Content-Type': 'application/json',
                        'Authorization': `Bearer ${sessionStorage.getItem('authToken')}`
                    },
                    body: JSON.stringify(requestBody)
                }
            );

            const responseText = await response.text();

            if (!response.ok) {
                console.error('❌ Server error response:', {
                    status: response.status,
                    statusText: response.statusText,
                    body: responseText
                });
                throw new Error(`Failed to update alert status: ${response.status} - ${responseText}`);
            }

            const result = responseText ? JSON.parse(responseText) : {};
            console.log('✅ Alert marked as read:', result);

            return result;

        } catch (error) {
            console.error('❌ Error marking alert as read:', error);
            throw error;
        }
    }

    /**
     * Render alerts in container
     * @param {Array} alerts - Alert data array
     * @param {string} containerId - DOM container ID
     * @param {number|string} entityId - Entity ID for status updates
     */
    renderAlerts(alerts, containerId, entityId) {
        const container = document.getElementById(containerId);
        if (!container) {
            console.error(`❌ Container not found: ${containerId}`);
            return;
        }

        // Clear existing content
        container.innerHTML = '';

        if (!alerts || alerts.length === 0) {
            container.innerHTML = '<div class="no-data-message">אין התראות להצגה</div>';
            return;
        }

        // Create internal cards for each alert
        alerts.forEach((alert, index) => {
  
            const internalCard = document.createElement('div');
            internalCard.className = 'internal-card';
            internalCard.setAttribute('data-alert-id', alert.id);

            // ✅ Add bold class if status is 'new' (status = 1)
            if (alert.status === 1) {
                internalCard.classList.add('alert-new');
            }

            const timestamp = this.formatTimestamp(alert.createdAt);

            internalCard.innerHTML = `
                <div class="internal-card-content">${alert.description}</div>
                <div class="internal-card-timestamp">${timestamp}</div>
            `;

            // Add click handler
            internalCard.addEventListener('click', async () => {
                console.log(`Alert clicked:`, {
                    id: alert.id,
                    description: alert.description,
                    status: alert.status,
                    entityId: entityId
                });

                // ✅ If status is 'new', mark as read
                if (alert.status === 1) {
                    try {
                        if (!entityId) {
                            console.error('❌ No entity ID provided for status update');
                            return;
                        }

                        await this.markAlertAsRead(alert.id, entityId);

                        // ✅ Remove bold styling
                        internalCard.classList.remove('alert-new');

                        // ✅ Update local data
                        alert.status = 2;

                        console.log('✅ Alert status updated to read');
                    } catch (error) {
                        console.error('❌ Error updating alert status:', error);
                        alert('שגיאה בעדכון סטטוס ההתראה. אנא נסה שוב.');
                    }
                }

                this.handleAlertClick(alert, 'alert', index);
            });

            container.appendChild(internalCard);
        });

        console.log(`✅ Rendered ${alerts.length} alerts in ${containerId}`);
    }

    /**
     * Render events in container
     * @param {Array} events - Event data array
     * @param {string} containerId - DOM container ID
     * @param {number|string} entityId - Entity ID for status updates
     */
    renderEvents(events, containerId, entityId) {
        const container = document.getElementById(containerId);
        if (!container) {
            console.error(`❌ Container not found: ${containerId}`);
            return;
        }

        // Clear existing content
        container.innerHTML = '';

        if (!events || events.length === 0) {
            container.innerHTML = '<div class="no-data-message">אין אירועים להצגה</div>';
            return;
        }

        // Create internal cards for each event
        events.forEach((event, index) => {
            const internalCard = document.createElement('div');
            internalCard.className = 'internal-card';
            internalCard.setAttribute('data-alert-id', event.id);

            // ✅ Add bold class if status is 'new' (status = 1)
            if (event.status === 1) {
                internalCard.classList.add('alert-new');
            }

            const timestamp = this.formatTimestamp(event.createdAt);
            
            const eventDate = event.eventDate
                ? this.formatEventDate(event.eventDate)
                : this.formatTimestamp(event.createdAt);

            const cardDescrption = `<span class="event-date">${eventDate}</span> - ${event.description}`;

            internalCard.innerHTML = `
                            
                <div class="internal-card-content">${cardDescrption}</div>
                <div class="internal-card-timestamp">${timestamp}</div>
            `;

            // Add click handler
            internalCard.addEventListener('click', async () => {
                console.log(`Event clicked:`, {
                    id: event.id,
                    description: event.description,
                    status: event.status,
                    entityId: entityId
                });

                // ✅ If status is 'new', mark as read
                if (event.status === 1) {
                    try {
                        if (!entityId) {
                            console.error('❌ No entity ID provided for status update');
                            return;
                        }

                        await this.markAlertAsRead(event.id, entityId);

                        // ✅ Remove bold styling
                        internalCard.classList.remove('alert-new');

                        // ✅ Update local data
                        event.status = 2;

                        console.log('✅ Event status updated to read');
                    } catch (error) {
                        console.error('❌ Error updating event status:', error);
                        alert('שגיאה בעדכון סטטוס האירוע. אנא נסה שוב.');
                    }
                }

                this.handleAlertClick(event, 'event', index);
            });

            container.appendChild(internalCard);
        });

        console.log(`✅ Rendered ${events.length} events in ${containerId}`);
    }

    /**
     * Format timestamp for display
     */
    formatTimestamp(dateString) {
        if (!dateString) return '';

        try {
            const date = new Date(dateString);
            const now = new Date();
            const diffMs = now - date;
            const diffMins = Math.floor(diffMs / 60000);
            const diffHours = Math.floor(diffMs / 3600000);
            const diffDays = Math.floor(diffMs / 86400000);

            if (diffMins < 1) {
                return 'עכשיו';
            } else if (diffMins < 60) {
                return `לפני ${diffMins} דקות`;
            } else if (diffHours < 24) {
                return `לפני ${diffHours} שעות`;
            } else if (diffDays < 7) {
                return `לפני ${diffDays} ימים`;
            } else {
                return date.toLocaleDateString('he-IL');
            }
        } catch (error) {
            console.error('Error formatting timestamp:', error);
            return dateString;
        }
    }

    /**
     * Format event date for display
     */
    formatEventDate(dateString) {
        if (!dateString) return '';

        try {
            const date = new Date(dateString);
            const now = new Date();
            const tomorrow = new Date(now);
            tomorrow.setDate(tomorrow.getDate() + 1);

            // Reset time parts for date comparison
            const dateOnly = new Date(date.getFullYear(), date.getMonth(), date.getDate());
            const todayOnly = new Date(now.getFullYear(), now.getMonth(), now.getDate());
            const tomorrowOnly = new Date(tomorrow.getFullYear(), tomorrow.getMonth(), tomorrow.getDate());

            if (dateOnly.getTime() === todayOnly.getTime()) {
                return `היום ${date.toLocaleTimeString('he-IL', { hour: '2-digit', minute: '2-digit' })}`;
            } else if (dateOnly.getTime() === tomorrowOnly.getTime()) {
                return `מחר ${date.toLocaleTimeString('he-IL', { hour: '2-digit', minute: '2-digit' })}`;
            } else {
                return date.toLocaleDateString('he-IL') + ' ' +
                    date.toLocaleTimeString('he-IL', { hour: '2-digit', minute: '2-digit' });
            }
        } catch (error) {
            console.error('Error formatting event date:', error);
            return dateString;
        }
    }

    /**
     * Handle alert/event click
     */
    handleAlertClick(item, type, index) {
        // Dispatch custom event for cross-component communication
        const event = new CustomEvent('alertEventClicked', {
            detail: {
                type: type, // 'alert' or 'event'
                item: item,
                index: index
            }
        });
        window.dispatchEvent(event);
    }
}

// Create global instance
window.AlertsEventsManager = new AlertsEventsManager();

console.log('✅ Alerts/Events Manager loaded');