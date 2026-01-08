class ReusableTable {
    constructor(containerId, options = {}) {
        this.containerId = containerId;
        this.data = [];
        this.originalData = []; // Keep original data for comparison
        this.columns = [];
        this.isReadOnly = options.isReadOnly !== false; // ✅ Store as instance property
        this.isEditing = false;
        this.sortColumn = options.defaultSortColumn || null;  // ✅ Accept from options
        this.sortDirection = options.defaultSortDirection || 'asc';  // ✅ Accept from options
        this.multiSort = [];
        this.filters = {};
        this.filteredData = [];
        this.options = options;
        this.sessionToken = sessionStorage.getItem('authToken'); // Security token
        this.allowedUpdates = new Set(); // Track server-approved updatable fields
        this.columnVisibility = new Map(); // columnKey -> boolean (true = visible, false = hidden)

    }

    // Initialize the table with server validation

    async init(data, columns) {
        console.log('🔵 ReusableTable.init() called');
        console.log('Data received:', data.length, 'rows');
        console.log('First row:', data[0]);
        console.log('Columns received:', columns);

        this.data = data;
        this.originalData = JSON.parse(JSON.stringify(data)); // Deep copy
        this.columns = columns.map(col => ({
            key: col.key,
            label: col.label,
            readOnly: col.readOnly || false,
            filterAllowed: col.filterAllowed !== false,
            sortable: col.sortable !== false,
            hidden: col.hidden || false,
            render: col.render || null,
            ...col
        }));

        console.log('Processed columns:', this.columns);

        // Skip server validation for read-only tables or when no auth token
        if (this.isReadOnly || !this.sessionToken || this.sessionToken === 'undefined') {
            console.log('✅ Skipping server validation for read-only table');
            this.filteredData = [...this.data];

            // ✅ Apply default sort if sortColumn is set
            if (this.sortColumn) {
                console.log(`🔽 Applying default sort: ${this.sortColumn} ${this.sortDirection}`);
                this.applySort();
            }

            console.log('Filtered data set:', this.filteredData.length, 'rows');
            this.render();
            return;
        }

        // Get server-side column permissions only for editable tables
        await this.validateColumnPermissions();

        this.filteredData = [...this.data];

        // ✅ Apply default sort if sortColumn is set
        if (this.sortColumn) {
            console.log(`🔽 Applying default sort: ${this.sortColumn} ${this.sortDirection}`);
            this.applySort();
        }
        this.render();
    }

    // ✅  Toggle between read-only and edit modes
    toggleMode(isReadOnly) {
        console.log(`🔄 Toggling table mode from ${this.isReadOnly ? 'READ-ONLY' : 'EDIT'} to ${isReadOnly ? 'READ-ONLY' : 'EDIT'}`);

        this.isReadOnly = isReadOnly;
        this.options.isReadOnly = isReadOnly;

        // Re-render with current data
        this.render();

        console.log(`✅ Table mode toggled successfully`);
    }

    // Update data without full re-initialization
    updateData(newData, newColumns = null) {
        console.log(`🔄 Updating table data: ${newData?.length || 0} rows`);

        this.data = newData || [];
        this.originalData = JSON.parse(JSON.stringify(newData || []));
        this.filteredData = [...this.data];

        // ✅ Update columns if provided
        if (newColumns) {
            console.log('📋 Updating columns with new render functions');
            this.columns = newColumns;
        }

        // Re-render with updated data and columns
        this.render();

        console.log(`✅ Table data updated successfully`);
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

    applySort() {
    if (!this.sortColumn) return;
    
    this.filteredData.sort((a, b) => {
        const aVal = a[this.sortColumn] || '';
        const bVal = b[this.sortColumn] || '';
        
        if (this.sortDirection === 'asc') {
            return aVal.toString().localeCompare(bVal.toString(), 'he');
        } else {
            return bVal.toString().localeCompare(aVal.toString(), 'he');
        }
    });
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
        console.log('🟢 ReusableTable.render() called');
        const container = document.getElementById(this.containerId);
        if (!container) {
            console.error('❌ Container not found:', this.containerId);
            return;
        }

        console.log('Rendering', this.filteredData.length, 'rows with', this.columns.length, 'columns');

        // Ensure we have data to render
        if (!this.filteredData || this.filteredData.length === 0) {
            container.innerHTML = '<p style="text-align: center; padding: 20px; color: #666;">אין נתונים להצגה</p>';
            return;
        }

        //  BEFORE re-rendering, save current visibility states from DOM
        this.saveColumnVisibilityStates();

        // Filter out hidden columns for rendering
        let visibleColumns = this.columns.filter(col => !col.hidden);
        
        // ✅ Move actions column to the beginning (rightmost in RTL)
        const actionsIndex = visibleColumns.findIndex(col => col.key === 'actions');
        if (actionsIndex > 0) {
            const actionsColumn = visibleColumns.splice(actionsIndex, 1)[0];
            visibleColumns.unshift(actionsColumn);
        }
        
        console.log('Visible columns:', visibleColumns.length, 'out of', this.columns.length);

        // Helper function to determine column width style
        const getColumnWidthStyle = (col) => {
            if (col.key === 'actions') {
                return 'width: 100px; min-width: 100px; max-width: 100px;';
            }
            
            // Check for special text column cases by label
            if (col.label === 'כיתה') {
                return 'width: 50px; min-width: 50px; max-width: 50px;';
            }
            
            if (col.label === 'תז') {
                return 'width: 150px; min-width: 150px; max-width: 150px;';
            }
            
            // Check if column key contains "date" (case insensitive)
            if (col.key.toLowerCase().includes('date')) {
                return 'width: 120px; min-width: 120px; max-width: 120px;';
            }
                        // Check if column key contains "number" (case insensitive)
            if (col.key.toLowerCase().includes('number') || col.key.toLowerCase().includes('hour')) {
                return 'width: 80px; min-width: 80px; max-width: 80px;';
            }
                        // Check if column renders currency values
            if (col.render) {
                const firstRow = this.filteredData.find(row => row[col.key] != null);
                if (firstRow) {
                    const renderedValue = col.render(firstRow);
                    // Check for currency symbols (₪, $, €, £) or common currency patterns
                    if (typeof renderedValue === 'string' && (renderedValue.includes('₪') || renderedValue.includes('$') || renderedValue.includes('€') || renderedValue.includes('£') || /\d+[,.]?\d*\s*(ILS|USD|EUR|GBP)/.test(renderedValue))) {
                        return 'width: 120px; min-width: 120px; max-width: 120px;';
                    }
                }
            }
            
            // Check data type from first non-null value
            const sampleValue = this.filteredData.find(row => row[col.key] != null)?.[col.key];
            const dataType = typeof sampleValue;
            
            // Check if it's a date by value
            if (sampleValue instanceof Date || (typeof sampleValue === 'string' && /^\d{4}-\d{2}-\d{2}/.test(sampleValue))) {
                return 'width: 50px; min-width: 50px; max-width: 50px;';
            }
            
            if (dataType === 'number' || dataType === 'boolean') {
                return 'width: auto; padding: 5px;';
            }
            
            // Default to 150px for text columns
            return 'width: 150px; min-width: 150px; max-width: 150px;';
        };

        // Create table HTML following Hebrew/RTL Specific Patterns
        const tableHTML = `
        <div class="table-container" dir="rtl">
            <table class="data-table">
                <thead style="background: #5a4d7a; color: white;">
                    <tr>
                        ${visibleColumns.map(col => {
            console.log('Rendering header for column:', col.key, col.label);

            // Check if frontend has hidden this column
            const isHiddenByFrontend = this.columnVisibility.get(col.key) === false;
            const hideStyle = isHiddenByFrontend ? 'display: none;' : '';
            const widthStyle = getColumnWidthStyle(col);

            return `
                                <th data-column="${col.key}" 
                                    style="background: #5a4d7a; color: white; text-align: right; white-space: nowrap; ${widthStyle} ${hideStyle}${col.sortable ? 'cursor: pointer;' : ''} position: relative;"
                                    ${col.sortable ? '' : ''}>
                                    ${col.key === 'actions' ? '' : col.label}
                                    ${col.sortable ? '<span class="sort-indicator"></span>' : ''}
                                    <span class="resize-handle" style="position: absolute; left: 0; top: 0; bottom: 0; width: 5px; cursor: col-resize; background: transparent;"></span>
                                </th>
                            `;
        }).join('')}
                    </tr>
                </thead>
                <tbody>
                    ${this.filteredData.map((row, rowIndex) => {
            if (rowIndex === 0) {
                console.log('First row data:', row);
            }
            return `
                            <tr>
                                ${visibleColumns.map(col => {
                const cellValue = row[col.key];
                const renderedValue = col.render ? col.render(row) : (cellValue !== undefined && cellValue !== null ? cellValue : '');

                if (rowIndex === 0) {
                    console.log(`Column ${col.key}:`, cellValue, '→', renderedValue);
                }

                // Check if frontend has hidden this column
                const isHiddenByFrontend = this.columnVisibility.get(col.key) === false;
                const hideStyle = isHiddenByFrontend ? 'display: none;' : '';
                const widthStyle = getColumnWidthStyle(col);
                
                return `
                                        <td data-column="${col.key}" style="text-align: right; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; ${widthStyle} ${hideStyle}">
                                            ${renderedValue}
                                        </td>
                                    `;
            }).join('')}
                            </tr>
                        `;
        }).join('')}
                </tbody>
            </table>
        </div>
    `;

        container.innerHTML = tableHTML;

        // Add sort listeners for sortable AND visible columns
        visibleColumns.forEach(col => {
            if (col.sortable) {
                const header = container.querySelector(`th[data-column="${col.key}"]`);
                if (header) {
                    header.addEventListener('click', () => this.sort(col.key));
                }
            }
        });

        // Add resize functionality
        this.addResizeListeners(container);

        console.log('✅ Table rendered successfully');
    }

    // Add column resize functionality
    addResizeListeners(container) {
        const resizeHandles = container.querySelectorAll('.resize-handle');
        
        resizeHandles.forEach(handle => {
            handle.addEventListener('mousedown', (e) => {
                e.stopPropagation(); // Prevent sort from triggering
                const th = handle.parentElement;
                const columnKey = th.getAttribute('data-column');
                const startX = e.pageX;
                const startWidth = th.offsetWidth;

                const onMouseMove = (moveEvent) => {
                    const deltaX = startX - moveEvent.pageX; // Reversed for RTL
                    const newWidth = Math.max(50, startWidth + deltaX);
                    
                    // Update header
                    th.style.width = `${newWidth}px`;
                    th.style.minWidth = `${newWidth}px`;
                    th.style.maxWidth = `${newWidth}px`;
                    
                    // Update all cells in this column
                    const cells = container.querySelectorAll(`td[data-column="${columnKey}"]`);
                    cells.forEach(cell => {
                        cell.style.width = `${newWidth}px`;
                        cell.style.minWidth = `${newWidth}px`;
                        cell.style.maxWidth = `${newWidth}px`;
                    });
                };

                const onMouseUp = () => {
                    document.removeEventListener('mousemove', onMouseMove);
                    document.removeEventListener('mouseup', onMouseUp);
                };

                document.addEventListener('mousemove', onMouseMove);
                document.addEventListener('mouseup', onMouseUp);
            });

            // Visual feedback on hover
            handle.addEventListener('mouseenter', () => {
                handle.style.background = 'rgba(255, 255, 255, 0.3)';
            });

            handle.addEventListener('mouseleave', () => {
                handle.style.background = 'transparent';
            });
        });
    }
    // Add sort method
    sort(columnKey) {
        if (this.sortColumn === columnKey) {
            this.sortDirection = this.sortDirection === 'asc' ? 'desc' : 'asc';
        } else {
            this.sortColumn = columnKey;
            this.sortDirection = 'asc';
        }

   /*     this.filteredData.sort((a, b) => {
            const aVal = a[columnKey] || '';
            const bVal = b[columnKey] || '';

            if (this.sortDirection === 'asc') {
                return aVal.toString().localeCompare(bVal.toString(), 'he');
            } else {
                return bVal.toString().localeCompare(aVal.toString(), 'he');
            }
        });*/

        this.applySort();
        this.render();
    }

    //  Save current column visibility states from DOM before re-render
    saveColumnVisibilityStates() {
        const container = document.getElementById(this.containerId);
        if (!container) return;

        // Check all header cells for display style
        const headers = container.querySelectorAll('th[data-column]');
        headers.forEach(th => {
            const columnKey = th.getAttribute('data-column');
            const isVisible = th.style.display !== 'none';
            this.columnVisibility.set(columnKey, isVisible);
        });

        console.log('📊 Saved column visibility states:', Object.fromEntries(this.columnVisibility));
    }

    //  Allow frontend to explicitly set column visibility
    setColumnVisibility(columnKey, isVisible) {
        this.columnVisibility.set(columnKey, isVisible);

        const container = document.getElementById(this.containerId);
        if (!container) return;

        // Apply to DOM immediately
        const headers = container.querySelectorAll(`th[data-column="${columnKey}"]`);
        const cells = container.querySelectorAll(`td[data-column="${columnKey}"]`);

        headers.forEach(th => {
            th.style.display = isVisible ? '' : 'none';
        });

        cells.forEach(td => {
            td.style.display = isVisible ? '' : 'none';
        });

        console.log(`🔄 Column visibility changed: ${columnKey} = ${isVisible ? 'visible' : 'hidden'}`);
    }
}