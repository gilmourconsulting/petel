// Show debug button only in development
window.addEventListener('DOMContentLoaded', function() {
    const authToken = sessionStorage.getItem('authToken');
    if (authToken && window.location.hostname === 'localhost') {
        const debugBtn = document.getElementById('debugSessionBtn');
        if (debugBtn) {
            debugBtn.style.display = 'block';
        }
    }
});

async function showSessionDebug() {
    const modal = document.getElementById('debugModal');
    const jsonContainer = document.getElementById('debugJsonContainer');
    
    if (!modal || !jsonContainer) return;
    
    modal.style.display = 'block';
    jsonContainer.textContent = 'טוען נתוני סשן...';
    
    await loadSessionDebugData();
}

async function loadSessionDebugData() {
    try {
        const authToken = sessionStorage.getItem('authToken');
        if (!authToken) {
            document.getElementById('debugJsonContainer').textContent = 'שגיאה: לא נמצא טוקן אימות';
            return;
        }

        // Fetch session info
        const sessionResponse = await fetch(AppConfig.getApiUrl('session'), {
            headers: {
                'Authorization': `Bearer ${authToken}`
            }
        });

        if (!sessionResponse.ok) {
            const errorText = await sessionResponse.text();
            document.getElementById('debugJsonContainer').textContent = 
                `שגיאה ${sessionResponse.status}: ${errorText}`;
            return;
        }

        const sessionInfo = await sessionResponse.json();

        // Fetch all properties
        const propertiesResponse = await fetch(AppConfig.getApiUrl('session/properties'), {
            headers: {
                'Authorization': `Bearer ${authToken}`
            }
        });

        let allProperties = {};
        if (propertiesResponse.ok) {
            allProperties = await propertiesResponse.json();
        }

        // ✅ Fetch alert definitions from memory cache
        const alertDefsResponse = await fetch(AppConfig.getApiUrl('alerts/definitions'), {
            headers: {
                'Authorization': `Bearer ${authToken}`
            }
        });

        let alertDefinitions = {
            note: "Alert definitions loaded at startup into memory cache (AlertDefinitionsCache)"
        };

        if (alertDefsResponse.ok) {
            alertDefinitions = await alertDefsResponse.json();
        } else {
            alertDefinitions.error = `Failed to load: ${alertDefsResponse.status}`;
        }

        // Combine all data
        const debugData = {
            sessionInfo: sessionInfo,
            allProperties: allProperties,
            alertDefinitions: alertDefinitions,
            frontendStorage: {
                authToken: authToken ? `${authToken.substring(0, 20)}...` : null,
                note: "Only auth token should be stored in frontend"
            }
        };

        document.getElementById('debugJsonContainer').textContent = 
            JSON.stringify(debugData, null, 2);

    } catch (error) {
        document.getElementById('debugJsonContainer').textContent = 
            `שגיאה בטעינת נתוני סשן: ${error.message}`;
    }
}

function refreshSessionDebug() {
    loadSessionDebugData();
}

function closeSessionDebug() {
    const modal = document.getElementById('debugModal');
    if (modal) {
        modal.style.display = 'none';
    }
}

window.onclick = function(event) {
    const modal = document.getElementById('debugModal');
    if (event.target === modal) {
        closeSessionDebug();
    }
}

window.showSessionDebug = showSessionDebug;
window.closeSessionDebug = closeSessionDebug;
window.refreshSessionDebug = refreshSessionDebug;