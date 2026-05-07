// wwwroot/js/blazorHelpers.js
// CSP-compliant helper functions for Blazor components

window.BlazorHelpers = {
    /**
     * Trigger a click event on a file input element
     * @param {string} elementId - The ID of the file input element
     */
    triggerFileInput: function (elementId) {
        try {
            const element = document.getElementById(elementId);
            if (element) {
                element.click();
            } else {
                console.error(`File input element with ID '${elementId}' not found`);
            }
        } catch (error) {
            console.error('Error triggering file input:', error);
        }
    },

    /**
     * Focus an element by ID
     * @param {string} elementId - The ID of the element to focus
     */
    focusElement: function (elementId) {
        try {
            const element = document.getElementById(elementId);
            if (element) {
                element.focus();
            }
        } catch (error) {
            console.error('Error focusing element:', error);
        }
    },

    /**
     * Show a loading overlay message
     * @param {string} elementId - The ID for the loading overlay
     * @param {string} title - The title text
     * @param {string} subtitle - The subtitle text (optional)
     */
    showLoadingOverlay: function (elementId, title, subtitle) {
        try {
            // Remove existing overlay if present
            this.removeElement(elementId);

            const loadingDiv = document.createElement('div');
            loadingDiv.id = elementId;
            loadingDiv.style.cssText = `
                position: fixed;
                top: 50%;
                left: 50%;
                transform: translate(-50%, -50%);
                background: white;
                padding: 30px;
                border-radius: 8px;
                box-shadow: 0 4px 6px rgba(0,0,0,0.1);
                z-index: 10000;
                text-align: center;
                direction: rtl;
            `;
            
            let html = `<div style='font-size: 18px; font-weight: bold; margin-bottom: 15px;'>${title}</div>`;
            if (subtitle) {
                html += `<div style='font-size: 14px; color: #666;'>${subtitle}</div>`;
            }
            loadingDiv.innerHTML = html;
            
            document.body.appendChild(loadingDiv);
        } catch (error) {
            console.error('Error showing loading overlay:', error);
        }
    },

    /**
     * Remove an element by ID
     * @param {string} elementId - The ID of the element to remove
     */
    removeElement: function (elementId) {
        try {
            const element = document.getElementById(elementId);
            if (element) {
                element.remove();
            }
        } catch (error) {
            console.error('Error removing element:', error);
        }
    },

    /**
     * Download a file from URL with authentication token
     * @param {string} url - The download URL
     * @param {string} token - The authentication token
     */
    downloadFileWithAuth: async function (url, token) {
        try {
            const response = await fetch(url, {
                method: 'GET',
                headers: {
                    'Authorization': `Bearer ${token}`
                }
            });

            if (!response.ok) {
                throw new Error(`HTTP error! status: ${response.status}`);
            }

            const blob = await response.blob();
            const blobUrl = window.URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = blobUrl;
            
            // Extract filename from Content-Disposition header or URL
            const disposition = response.headers.get('Content-Disposition');
            let filename = 'document.pdf';
            if (disposition && disposition.includes('filename=')) {
                filename = disposition
                    .split('filename=')[1]
                    .split(';')[0]
                    .replace(/['"]/g, '');
            }
            
            a.download = filename;
            document.body.appendChild(a);
            a.click();
            document.body.removeChild(a);
            window.URL.revokeObjectURL(blobUrl);
        } catch (error) {
            console.error('Error downloading file:', error);
            alert('שגיאה בהורדת המסמך: ' + error.message);
        }
    },

    /**
     * Open a file in a new window with authentication token
     * @param {string} url - The file URL
     * @param {string} token - The authentication token
     */
    viewFileWithAuth: async function (url, token) {
        try {
            const response = await fetch(url, {
                method: 'GET',
                headers: {
                    'Authorization': `Bearer ${token}`
                }
            });

            if (!response.ok) {
                throw new Error(`HTTP error! status: ${response.status}`);
            }

            const blob = await response.blob();
            const blobUrl = window.URL.createObjectURL(blob);
            window.open(blobUrl, '_blank');
        } catch (error) {
            console.error('Error viewing document:', error);
            alert('שגיאה בפתיחת המסמך: ' + error.message);
        }
    }
};

/**
 * Download a file from a base64 string.
 * Called from Blazor Excel report generation.
 */
window.downloadFileFromBase64 = function (base64, fileName, mimeType) {
    try {
        const byteChars = atob(base64);
        const byteNums = new Uint8Array(byteChars.length);
        for (let i = 0; i < byteChars.length; i++) {
            byteNums[i] = byteChars.charCodeAt(i);
        }
        const blob = new Blob([byteNums], { type: mimeType || 'application/octet-stream' });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = fileName || 'download';
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        URL.revokeObjectURL(url);
    } catch (error) {
        console.error('Error downloading file:', error);
    }
};

// Legacy aliases for backward compatibility
window.FileUploadHelper = {
    triggerFileInput: function(elementId) {
        return window.BlazorHelpers.triggerFileInput(elementId);
    }
};

