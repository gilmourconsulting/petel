class ReusableTable {
    constructor(containerId, options = {}) {
        this.containerId = containerId;
        this.data = [];
        this.originalData = []; // Keep original data for comparison
        this.columns = [];
        this.isReadOnly = true;
        this.isEditing = false;
        this.sortColumn = null;
        this.sortDirection = 'asc';
        this.multiSort = [];
        this.filters = {};
        this.filteredData = [];
        this.options = options;
        this.sessionToken = sessionStorage.getItem('authToken'); // Security token
        this.allowedUpdates = new Set(); // Track server-approved updatable fields
    }

    // Initialize the table with server validation
    async init(data, columns) {
        this.data = data;
        this.originalData = JSON.parse(JSON.stringify(data)); // Deep copy
        this.columns = columns.map(col => ({
            key: col.key,
            label: col.label,
            readOnly: col.readOnly || false,
            filterAllowed: col.filterAllowed !== false,
            ...col
        }));
        
        // Skip server validation for read-only tables or when no auth token
        if (this.isReadOnly || !this.sessionToken || this.sessionToken === 'undefined') {
            console.log('Skipping server validation for read-only table');
            this.filteredData = [...this.data];
            this.render();
            return;
        }
        
        // Get server-side column permissions only for editable tables
        await this.validateColumnPermissions();
        
        this.filteredData = [...this.data];
        this.render();
    }

    // Validate column permissions with server
    async validateColumnPermissions() {
        try {
            const response = await fetch(AppConfig.getApiUrl('tablePermissions'), {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Authorization': `Bearer ${this.sessionToken}`
                },
                body: JSON.stringify({
                    tableName: this.options.tableName || 'default',
                    columns: this.columns.map(col => ({
                        key: col.key,
                        requestedPermission: col.readOnly ? 'readonly' : 'updatable'
                    }))
                })
            });

            if (response.ok) {
                const permissions = await response.json();
                
                // Update column permissions based on server response
                this.columns.forEach(col => {
                    const serverPermission = permissions.find(p => p.columnKey === col.key);
                    if (serverPermission) {
                        col.readOnly = !serverPermission.canUpdate;
                        if (serverPermission.canUpdate) {
                            this.allowedUpdates.add(col.key);
                        }
                    } else {
                        // If server doesn't return permission, default to read-only
                        col.readOnly = true;
                    }
                });
            } else {
                console.warn('Failed to validate permissions, defaulting to read-only');
                this.columns.forEach(col => {
                    col.readOnly = true;
                });
            }
        } catch (error) {
            console.error('Error validating permissions:', error);
            // Default to read-only if validation fails
            this.columns.forEach(col => {
                col.readOnly = true;
            });
        }
    }

    // Secure cell update with server validation
    async updateCell(rowIndex, columnKey, newValue) {
        // Client-side validation
        if (!this.allowedUpdates.has(columnKey)) {
            console.error('Unauthorized update attempt blocked');
            this.render(); // Reset the display
            return;
        }

        const originalRowIndex = this.data.findIndex(row => row === this.filteredData[rowIndex]);
        if (originalRowIndex === -1) return;

        const oldValue = this.data[originalRowIndex][columnKey];
        
        // Prepare update data for server validation
        const updateData = {
            rowId: this.data[originalRowIndex].id || originalRowIndex,
            columnKey: columnKey,
            oldValue: oldValue,
            newValue: newValue,
            tableName: this.options.tableName || 'default',
            sessionToken: this.sessionToken
        };

        try {
            // Send to server for validation and update
            const response = await fetch(AppConfig.getApiUrl('validateUpdate'), {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Authorization': `Bearer ${this.sessionToken}`
                },
                body: JSON.stringify(updateData)
            });

            if (response.ok) {
                const result = await response.json();
                if (result.success) {
                    // Server approved the update
                    this.data[originalRowIndex][columnKey] = newValue;
                    this.filteredData[rowIndex][columnKey] = newValue;
                    
                    // Show success indicator
                    this.showUpdateStatus('success', `עודכן בהצלחה: ${columnKey}`);
                } else {
                    // Server rejected the update
                    this.showUpdateStatus('error', result.message || 'עדכון נדחה על ידי השרת');
                    this.render(); // Reset to original values
                }
            } else {
                throw new Error('Server validation failed');
            }
        } catch (error) {
            console.error('Update validation failed:', error);
            this.showUpdateStatus('error', 'שגיאה בעדכון - השרת לא זמין');
            this.render(); // Reset to original values
        }
    }

    // Show update status to user
    showUpdateStatus(type, message) {
        const statusDiv = document.createElement('div');
        statusDiv.className = `update-status ${type}`;
        statusDiv.textContent = message;
        statusDiv.style.cssText = `
            position: fixed;
            top: 20px;
            right: 20px;
            padding: 12px 20px;
            border-radius: 6px;
            color: white;
            font-weight: bold;
            z-index: 10000;
            direction: rtl;
            background: ${type === 'success' ? '#4caf50' : '#f44336'};
        `;
        
        document.body.appendChild(statusDiv);
        
        setTimeout(() => {
            statusDiv.remove();
        }, 3000);
    }

    // Secure save function
    async saveChanges() {
        const changes = this.getChanges();
        if (changes.length === 0) {
            this.showUpdateStatus('info', 'אין שינויים לשמירה');
            return;
        }

        try {
            const response = await fetch(AppConfig.getApiUrl('saveTableChanges'), {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Authorization': `Bearer ${this.sessionToken}`
                },
                body: JSON.stringify({
                    tableName: this.options.tableName || 'default',
                    changes: changes,
                    originalData: this.originalData
                })
            });

            if (response.ok) {
                const result = await response.json();
                if (result.success) {
                    this.originalData = JSON.parse(JSON.stringify(this.data)); // Update baseline
                    this.showUpdateStatus('success', 'שינויים נשמרו בהצלחה');
                    this.toggleEditMode(); // Exit edit mode
                } else {
                    this.showUpdateStatus('error', result.message || 'שמירה נכשלה');
                }
            } else {
                throw new Error('Save failed');
            }
        } catch (error) {
            console.error('Save failed:', error);
            this.showUpdateStatus('error', 'שגיאה בשמירה');
        }
    }

    // Get changes for server validation
    getChanges() {
        const changes = [];
        
        this.data.forEach((row, index) => {
            const originalRow = this.originalData[index];
            if (!originalRow) return;
            
            this.columns.forEach(col => {
                if (this.allowedUpdates.has(col.key) && row[col.key] !== originalRow[col.key]) {
                    changes.push({
                        rowId: row.id || index,
                        columnKey: col.key,
                        oldValue: originalRow[col.key],
                        newValue: row[col.key]
                    });
                }
            });
        });
        
        return changes;
    }

    // Override the original toggleEditMode to include save validation
    toggleEditMode() {
        if (this.isEditing) {
            // Exiting edit mode - save changes
            this.saveChanges();
        } else {
            // Entering edit mode
            this.isEditing = true;
            const editBtn = document.getElementById('editToggleBtn');
            if (editBtn) {
                editBtn.innerHTML = '💾 שמור';
                editBtn.className = 'action-btn save-btn';
            }
            this.render();
        }
    }

    // Add validation for exported data
    async exportToXlsx() {
        if (!await this.validateExportPermission()) {
            this.showUpdateStatus('error', 'אין הרשאה לייצוא נתונים');
            return;
        }
        
        const csvData = this.generateCsvData();
        const blob = new Blob([csvData], { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });
        this.downloadFile(blob, 'table-export.xlsx');
    }

    async exportToCsv() {
        if (!await this.validateExportPermission()) {
            this.showUpdateStatus('error', 'אין הרשאה לייצוא נתונים');
            return;
        }
        
        const csvData = this.generateCsvData();
        const blob = new Blob([csvData], { type: 'text/csv;charset=utf-8;' });
        this.downloadFile(blob, 'table-export.csv');
    }

    // Validate export permissions
    async validateExportPermission() {
        try {
            const response = await fetch(AppConfig.getApiUrl('validateExport'), {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Authorization': `Bearer ${this.sessionToken}`
                },
                body: JSON.stringify({
                    tableName: this.options.tableName || 'default'
                })
            });

            if (response.ok) {
                const result = await response.json();
                return result.canExport;
            }
            return false;
        } catch (error) {
            console.error('Export validation failed:', error);
            return false;
        }
    }

    // Add this render method to table-component.js following Hebrew/RTL Specific Patterns:

    render() {
        const container = document.getElementById(this.containerId);
        if (!container) {
            console.error('Container not found:', this.containerId);
            return;
        }

        // Create table HTML following Hebrew/RTL Specific Patterns
        const tableHTML = `
            <div class="table-container" dir="rtl">
                <table class="data-table">
                    <thead>
                        <tr>
                            ${this.columns.map(col => `
                                <th data-column="${col.key}" ${col.sortable ? 'style="cursor: pointer;"' : ''}>
                                    ${col.label}
                                    ${col.sortable ? '<span class="sort-indicator"></span>' : ''}
                                </th>
                            `).join('')}
                        </tr>
                    </thead>
                    <tbody>
                        ${this.filteredData.map(row => `
                            <tr>
                                ${this.columns.map(col => `
                                    <td data-column="${col.key}">
                                        ${col.render ? col.render(row) : (row[col.key] || '')}
                                    </td>
                                `).join('')}
                            </tr>
                        `).join('')}
                    </tbody>
                </table>
            </div>
        `;

        container.innerHTML = tableHTML;

        // Add sort listeners for sortable columns
        this.columns.forEach(col => {
            if (col.sortable) {
                const header = container.querySelector(`th[data-column="${col.key}"]`);
                if (header) {
                    header.addEventListener('click', () => this.sort(col.key));
                }
            }
        });

        console.log('✅ Table rendered successfully');
    }

    // Add sort method
    sort(columnKey) {
        if (this.sortColumn === columnKey) {
            this.sortDirection = this.sortDirection === 'asc' ? 'desc' : 'asc';
        } else {
            this.sortColumn = columnKey;
            this.sortDirection = 'asc';
        }

        this.filteredData.sort((a, b) => {
            const aVal = a[columnKey] || '';
            const bVal = b[columnKey] || '';
            
            if (this.sortDirection === 'asc') {
                return aVal.toString().localeCompare(bVal.toString(), 'he');
            } else {
                return bVal.toString().localeCompare(aVal.toString(), 'he');
            }
        });

        this.render();
    }

    // ... (keep all other existing methods)
}