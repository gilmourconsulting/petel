// wwwroot/js/sessionTimeout.js
// Session timeout activity tracking for Blazor

window.SessionTimeout = {
    dotNetReference: null,

    initialize: function (dotNetRef) {
        console.log('🕐 Initializing session timeout activity tracking...');
        this.dotNetReference = dotNetRef;

        // Set up activity listeners
        const events = ['mousedown', 'keydown', 'scroll', 'touchstart', 'click'];

        events.forEach(event => {
            document.addEventListener(event, () => this.onUserActivity(), true);
        });

        console.log('✅ Session timeout activity tracking initialized');
    },

    onUserActivity: function () {
        if (this.dotNetReference) {
            try {
                this.dotNetReference.invokeMethodAsync('OnUserActivity');
            } catch (error) {
                console.error('Error invoking OnUserActivity:', error);
            }
        }
    }
};
