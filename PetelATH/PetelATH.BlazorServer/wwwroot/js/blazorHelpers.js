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

/**
 * Read a rendered data table for Excel export.
 * Drops action columns, buttons, icons, and checkboxes.
 * Anchor text is kept; the URL is not.
 */
window.tableExcelExport = {
    extract: function (host) {
        if (!host) return null;

        const table = findOwnTable(host);
        if (!table || !table.tHead || !table.tHead.rows.length) return null;

        const headerEntries = columnEntries(table.tHead.rows[0]);
        const bodyRows = [];
        for (const tbody of table.tBodies) {
            for (const row of tbody.rows) {
                if (row.querySelector('table')) continue;
                if (row.cells.length === 1 && row.cells[0].colSpan > 1) continue;
                bodyRows.push(row);
            }
        }

        const included = [];
        for (let col = 0; col < headerEntries.length; col++) {
            const entry = headerEntries[col];
            if (!entry || !entry.isFirst) continue;
            if (entry.cell.classList.contains('actions-column')) continue;

            const headerText = stripSortMarks(cellText(entry.cell));
            if (!headerText || headerText === 'פעולות') continue;

            const onlyControls = bodyRows.length > 0 && bodyRows.every(row => {
                const bodyEntry = columnEntries(row)[col];
                return bodyEntry && isControlOnly(bodyEntry.cell);
            });
            if (onlyControls) continue;

            included.push({ col: col, header: headerText });
        }

        if (!included.length) return null;

        const rows = bodyRows.map(row => {
            const entries = columnEntries(row);
            return included.map(item => {
                const entry = entries[item.col];
                if (!entry || !entry.isFirst) return '';
                return cellText(entry.cell);
            });
        });

        const title = nearestTitle(host);
        return {
            headers: included.map(item => item.header),
            rows: rows,
            title: title,
            sheetName: toSheetName(title)
        };
    }
};

function findOwnTable(host) {
    const tables = host.querySelectorAll('table.data-table');
    for (const table of tables) {
        const ancestor = table.parentElement ? table.parentElement.closest('table') : null;
        if (ancestor && host.contains(ancestor)) continue;
        return table;
    }
    return null;
}

function columnEntries(row) {
    const entries = [];
    let col = 0;
    for (const cell of row.cells) {
        const span = cell.colSpan || 1;
        for (let offset = 0; offset < span; offset++) {
            entries[col] = { cell: cell, isFirst: offset === 0 };
            col++;
        }
    }
    return entries;
}

function cellText(cell) {
    const parts = [];
    const walk = node => {
        if (node.nodeType === Node.TEXT_NODE) {
            parts.push(node.textContent || '');
            return;
        }
        if (node.nodeType !== Node.ELEMENT_NODE) return;

        const tag = node.tagName;
        if (tag === 'TABLE' || tag === 'BUTTON' || tag === 'INPUT' || tag === 'IMG' || tag === 'SVG' || tag === 'SELECT') {
            if (tag === 'SELECT') {
                const option = node.selectedIndex >= 0 ? node.options[node.selectedIndex] : null;
                parts.push(option ? option.text : '');
            }
            return;
        }

        for (const child of node.childNodes) walk(child);
    };

    walk(cell);
    return parts.join('').replace(/\s+/g, ' ').trim();
}

function isControlOnly(cell) {
    if (!cell.querySelector('button, input, img, svg')) return false;
    return cellText(cell) === '';
}

function stripSortMarks(value) {
    return (value || '').replace(/[▲▼↑↓]/g, '').replace(/\s+/g, ' ').trim();
}

function nearestTitle(host) {
    let node = host.parentElement;
    while (node) {
        const title = node.querySelector(':scope > .detail-card-header .detail-card-title, :scope > .detail-card-header h2, :scope > .detail-card-header strong, :scope > .page-title, :scope > h1.page-title');
        const text = title ? stripSortMarks(title.textContent) : '';
        if (text) return text;
        node = node.parentElement;
    }

    const page = document.querySelector('.page-title');
    const pageText = page ? stripSortMarks(page.textContent) : '';
    return pageText || 'נתונים';
}

function toSheetName(title) {
    let name = (title || '').replace(/[\\/?*[\]:]/g, ' ').replace(/\s+/g, ' ').trim();
    if (!name) name = 'נתונים';
    if (name.length > 31) name = name.slice(0, 31).trim();
    return name || 'נתונים';
}

// Legacy aliases for backward compatibility
window.FileUploadHelper = {
    triggerFileInput: function(elementId) {
        return window.BlazorHelpers.triggerFileInput(elementId);
    }
};

