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
     * Scroll a table row (or any element) into view by ID.
     * Used by EntityFocusLink landing pages (?focusId=).
     */
    scrollIntoView: function (elementId) {
        try {
            const element = document.getElementById(elementId);
            if (element) {
                element.scrollIntoView({ block: 'center', behavior: 'smooth' });
            }
        } catch (error) {
            console.error('Error scrolling into view:', error);
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
     * @param {string} fallbackFileName - Optional fallback filename when header is unavailable
     */
    downloadFileWithAuth: async function (url, token, fallbackFileName) {
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

            const contentType = response.headers.get('Content-Type');
            const disposition = response.headers.get('Content-Disposition');

            const blob = await response.blob();
            const blobUrl = window.URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = blobUrl;

            let filename = fallbackFileName || 'document';
            const contentTypeLower = (contentType || '').toLowerCase();

            // Try to extract filename from Content-Disposition header first
            if (disposition) {
                try {
                    // RFC 5987 format: filename*=UTF-8''...
                    const utfMatch = disposition.match(/filename\*=UTF-8''([^;]+)/i);
                    if (utfMatch && utfMatch[1]) {
                        filename = decodeURIComponent(utfMatch[1]);
                    } else {
                        // Basic format: filename="value"
                        const asciiMatch = disposition.match(/filename="?([^";,]+)"?/i);
                        if (asciiMatch && asciiMatch[1]) {
                            filename = asciiMatch[1];
                        }
                    }
                } catch (e) {
                    console.warn('Failed to parse Content-Disposition:', e);
                }
            } else {
                // No Content-Disposition header, use fallback + content-type hint
                if (contentTypeLower.includes('zip')) {
                    const base = (fallbackFileName || 'documents').replace(/\.zip$/i, '');
                    filename = base + '.zip';
                } else if (contentTypeLower.includes('pdf')) {
                    const base = (fallbackFileName || 'document').replace(/\.pdf$/i, '');
                    filename = base + '.pdf';
                }
            }

            // Ensure proper extension for ZIP files
            if (!filename.includes('.')) {
                if (contentTypeLower.includes('zip')) {
                    filename += '.zip';
                }
            } else if (contentTypeLower.includes('zip') && !filename.toLowerCase().endsWith('.zip')) {
                filename = filename.substring(0, filename.lastIndexOf('.')) + '.zip';
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
            console.log('👁️ [VIEW] Fetching:', url);
            const response = await fetch(url, {
                method: 'GET',
                headers: {
                    'Authorization': `Bearer ${token}`
                }
            });

            console.log('👁️ [VIEW] Response status:', response.status);
            if (!response.ok) {
                throw new Error(`HTTP error! status: ${response.status}`);
            }

            const contentType = response.headers.get('Content-Type');
            const disposition = response.headers.get('Content-Disposition');
            console.log('👁️ [VIEW] Content-Type:', contentType);
            console.log('👁️ [VIEW] Content-Disposition:', disposition);

            const contentTypeLower = (contentType || '').toLowerCase();
            const isViewable = contentTypeLower.includes('pdf') || contentTypeLower.includes('image/');
            console.log('👁️ [VIEW] isViewable:', isViewable);

            const blob = await response.blob();
            const blobUrl = window.URL.createObjectURL(blob);

            if (isViewable) {
                console.log('👁️ [VIEW] Opening inline in new tab');
                window.open(blobUrl, '_blank');
            } else {
                let filename = 'document';

                if (disposition) {
                    try {
                        const utfMatch = disposition.match(/filename\*=UTF-8''([^;]+)/i);
                        console.log('👁️ [VIEW] UTF-8 match:', utfMatch);
                        if (utfMatch && utfMatch[1]) {
                            filename = decodeURIComponent(utfMatch[1]);
                        } else {
                            const asciiMatch = disposition.match(/filename="?([^";,]+)"?/i);
                            console.log('👁️ [VIEW] ASCII match:', asciiMatch);
                            if (asciiMatch && asciiMatch[1]) {
                                filename = asciiMatch[1];
                            }
                        }
                    } catch (e) {
                        console.warn('👁️ [VIEW] Failed to parse Content-Disposition:', e);
                    }
                } else {
                    console.log('👁️ [VIEW] No Content-Disposition header — using fallback filename');
                }

                console.log('👁️ [VIEW] Downloading as:', filename);
                const a = document.createElement('a');
                a.href = blobUrl;
                a.download = filename;
                document.body.appendChild(a);
                a.click();
                document.body.removeChild(a);
                window.URL.revokeObjectURL(blobUrl);
            }
        } catch (error) {
            console.error('👁️ [VIEW] Error:', error);
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

window.BlazorHelpers.budgetTrendChart = {
    _charts: {},

    update: function (canvasId, payload, dotNetRef) {
        this.dispose(canvasId);
        const canvas = document.getElementById(canvasId);
        if (!canvas || typeof Chart === 'undefined' || !payload || !payload.labels) {
            return;
        }

        const isMoney = !!payload.isMoney;
        const chart = new Chart(canvas, {
            type: 'line',
            data: {
                labels: payload.labels,
                datasets: (payload.series || []).map(function (s) {
                    return {
                        label: s.label,
                        data: s.values,
                        borderColor: s.color,
                        backgroundColor: s.color,
                        spanGaps: false,
                        tension: 0,
                        pointRadius: 3,
                        pointHoverRadius: 5,
                        borderWidth: 2,
                        fill: false
                    };
                })
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                locale: 'he-IL',
                interaction: { mode: 'index', intersect: false },
                plugins: {
                    legend: {
                        position: 'top',
                        rtl: true,
                        textDirection: 'rtl',
                        align: 'end'
                    },
                    tooltip: {
                        rtl: true,
                        textDirection: 'rtl',
                        callbacks: {
                            label: function (ctx) {
                                const value = ctx.parsed.y;
                                if (value == null) {
                                    return ctx.dataset.label + ': —';
                                }
                                return ctx.dataset.label + ': ' + formatTrendValue(value, isMoney);
                            }
                        }
                    }
                },
                scales: {
                    x: {
                        ticks: { maxRotation: 45, minRotation: 0, autoSkip: true }
                    },
                    y: {
                        beginAtZero: true,
                        ticks: {
                            callback: function (value) {
                                return formatTrendValue(value, isMoney);
                            }
                        }
                    }
                },
                onClick: function (evt, elements, chartInstance) {
                    let index;
                    if (elements && elements.length) {
                        index = elements[0].index;
                    } else {
                        const points = chartInstance.getElementsAtEventForMode(
                            evt, 'index', { intersect: false }, true);
                        if (points.length) {
                            index = points[0].index;
                        }
                    }
                    if (index == null || !payload.months || !payload.months[index] || !dotNetRef) {
                        return;
                    }
                    const month = payload.months[index];
                    dotNetRef.invokeMethodAsync(
                        'OnChartMonthClicked', month.periodYear, month.periodMonth);
                }
            }
        });

        this._charts[canvasId] = chart;
    },

    dispose: function (canvasId) {
        const existing = this._charts[canvasId];
        if (existing) {
            existing.destroy();
            delete this._charts[canvasId];
        }
    }
};

function formatTrendValue(value, isMoney) {
    const numeric = Number(value);
    if (!isFinite(numeric)) {
        return '';
    }
    if (isMoney) {
        return '₪ ' + Math.round(numeric).toLocaleString('he-IL');
    }
    return numeric.toLocaleString('he-IL', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
}

// Legacy aliases for backward compatibility
window.FileUploadHelper = {
    triggerFileInput: function(elementId) {
        return window.BlazorHelpers.triggerFileInput(elementId);
    }
};

