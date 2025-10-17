

// Show debug button only in development
window.addEventListener('DOMContentLoaded', function() {
    // Show debug button only when logged in and in development
    const authToken = sessionStorage.getItem('authToken');
    if (authToken && window.location.hostname === 'localhost') {
        const debugBtn = document.getElementById('debugSessionBtn');
        if (debugBtn) {
            debugBtn.style.display = 'block';
        }
    }
});

// Show session debug modal
async function showSessionDebug() {
    const modal = document.getElementById('debugModal');
    const jsonContainer = document.getElementById('debugJsonContainer');
    
    if (!modal || !jsonContainer) return;
    
    modal.style.display = 'block';
    jsonContainer.textContent = 'טוען נתוני סשן...';
    
    await loadSessionDebugData();
}

// Load session data from backend
async function loadSessionDebugData() {
    try {
        const authToken = sessionStorage.getItem('authToken');
        if (!authToken) {
            document.getElementById('debugJsonContainer').textContent = 'שגיאה: לא נמצא טוקן אימות';
            return;
        }

        const response = await fetch(AppConfig.getApiUrl('session/debug'), {
            method: 'GET',
            headers: {
                'Content-Type': 'application/json',
                'Authorization': `Bearer ${authToken}`
            }
        });

        if (response.ok) {
            const sessionData = await response.json();
            document.getElementById('debugJsonContainer').textContent = 
                JSON.stringify(sessionData, null, 2);
        } else {
            const errorText = await response.text();
            document.getElementById('debugJsonContainer').textContent = 
                `שגיאה ${response.status}: ${errorText}`;
        }
    } catch (error) {
        document.getElementById('debugJsonContainer').textContent = 
            `שגיאה בטעינת נתוני סשן: ${error.message}`;
    }
}

// Refresh session data
function refreshSessionDebug() {
    loadSessionDebugData();
}

// Close debug modal
function closeSessionDebug() {
    const modal = document.getElementById('debugModal');
    if (modal) {
        modal.style.display = 'none';
    }
}

// Close modal when clicking outside
window.onclick = function(event) {
    const modal = document.getElementById('debugModal');
    if (event.target === modal) {
        closeSessionDebug();
    }
}

// Make functions globally available
window.showSessionDebug = showSessionDebug;
window.closeSessionDebug = closeSessionDebug;
window.refreshSessionDebug = refreshSessionDebug;