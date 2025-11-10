/**
 * Reusable Documents Table Component
 * Can be used for school documents, student documents, or any entity documents
 */


if (typeof window.DocumentsTableComponent === 'undefined') {
    window.DocumentsTableComponent = class DocumentsTableComponent {
        constructor(containerId, options = {}) {
            this.containerId = containerId;
            this.options = {
                showUploadForm: options.showUploadForm ?? true,
                allowDelete: options.allowDelete ?? true,
                allowDownload: options.allowDownload ?? true,
                allowUpload: options.allowUpload ?? true,
                entityId: options.entityId,
                entityType: options.entityType || 'school', // 'school', 'student', 'entity'
                yearId: options.yearId,
                onUploadSuccess: options.onUploadSuccess,
                onDeleteSuccess: options.onDeleteSuccess
            };

            this.documentsTable = null;
            this.documentTypes = [];
        }

        /**
         * Initialize the documents table component
         */
        async init() {
            try {
                console.log('🔧 Initializing DocumentsTableComponent...', this.options);

                const container = document.getElementById(this.containerId);
                if (!container) {
                    throw new Error(`Container ${this.containerId} not found`);
                }

                // Render HTML structure
                this.render(container);

                // Load document types
                await this.loadDocumentTypes();

                // Load documents table
                await this.loadDocumentsTable();

                // Setup upload form if enabled
                if (this.options.showUploadForm) {
                    this.setupUploadForm();
                }

                console.log('✅ DocumentsTableComponent initialized');
            } catch (error) {
                console.error('❌ Error initializing DocumentsTableComponent:', error);
                throw error;
            }
        }

        /**
         * Render the component HTML structure
         */
        render(container) {
            const uploadSection = this.options.showUploadForm ? `
            <div class="upload-section">
                <h3>העלאת מסמך חדש</h3>
                <form class="upload-form" id="${this.containerId}_uploadForm" enctype="multipart/form-data">
                    <div class="form-group">
                        <label for="${this.containerId}_fileInput">בחר קובץ *</label>
                        <input type="file" id="${this.containerId}_fileInput" required>
                    </div>

                    <div class="form-group">
                        <label for="${this.containerId}_descriptionInput">תיאור המסמך *</label>
                        <input type="text" id="${this.containerId}_descriptionInput" required>
                    </div>

                    <div class="form-group">
                        <label for="${this.containerId}_documentTypeSelect">סוג מסמך *</label>
                        <select id="${this.containerId}_documentTypeSelect" required>
                            <option value="">-- בחר סוג מסמך --</option>
                        </select>
                    </div>

                    <div class="form-group">
                        <button type="submit" class="btn-upload" id="${this.containerId}_uploadBtn">
                            <img src="upload_icon.png" alt="העלאה" class="action-icon-natural">
                            העלה מסמך
                        </button>
                    </div>
                </form>
            </div>
        ` : '';

            container.innerHTML = `
            ${uploadSection}
            <div class="documents-table-wrapper" id="${this.containerId}_tableWrapper"></div>
        `;
        }

        /**
        * Load document types from backend
        */
        async loadDocumentTypes() {
            try {
                // ✅ FIXED: Use documents/types endpoint
                const url = AppConfig.getApiUrl('documents/types');
                const token = sessionStorage.getItem('authToken');

                const response = await fetch(url, {
                    headers: { 'Authorization': `Bearer ${token}` }
                });

                if (!response.ok) {
                    console.warn('⚠ Failed to load document types');
                    return;
                }

                this.documentTypes = await response.json();
                console.log('✅ Document types loaded:', this.documentTypes.length);
            } catch (error) {
                console.error('❌ Error loading document types:', error);
            }
        }


        /**
 * Load document status types
 */
        async loadDocumentStatusTypes() {
            try {
                const url = AppConfig.getApiUrl('documents/status-types');
                const token = sessionStorage.getItem('authToken');

                const response = await fetch(url, {
                    headers: { 'Authorization': `Bearer ${token}` }
                });

                if (!response.ok) {
                    console.warn('⚠ Failed to load document status types');
                    return [];
                }

                const statusTypes = await response.json();
                console.log('✅ Document status types loaded:', statusTypes.length);
                return statusTypes;
            } catch (error) {
                console.error('❌ Error loading document status types:', error);
                return [];
            }
        }



                /**
         * Load and display documents table
         */
        async loadDocumentsTable() {
            try {
                console.log('📊 Loading documents table...');
        
                const token = sessionStorage.getItem('authToken');
        
                // Build URL with query parameters if entityId/yearId provided
                let url = AppConfig.getApiUrl('documents/by-entity');
                const params = new URLSearchParams();
        
                if (this.options.entityId) {
                    params.append('entityId', this.options.entityId);
                }
                if (this.options.yearId) {
                    params.append('yearId', this.options.yearId);
                }
        
                if (params.toString()) {
                    url += '?' + params.toString();
                }
        
                console.log('📡 Fetching documents from:', url);
        
                const response = await fetch(url, {
                    headers: { 'Authorization': `Bearer ${token}` }
                });
        
                if (!response.ok) {
                    throw new Error('Failed to load documents');
                }
        
                const documents = await response.json();
                console.log(`✅ Loaded ${documents.length} documents`);
        
                // Initialize ReusableTable
                this.documentsTable = new ReusableTable(`${this.containerId}_tableWrapper`, {
                    tableName: 'documents',
                    isReadOnly: false,
                    allowAdd: false,
                    allowEdit: false,
                    allowDelete: this.options.allowDelete
                });
        
                const columns = [
                    {
                        key: 'documentType',
                        label: 'סוג מסמך',
                        sortable: true,
                        readOnly: true
                    },
                    {
                        key: 'description',
                        label: 'תיאור',
                        sortable: true,
                        readOnly: true
                    },
                    {
                        key: 'statusName',
                        label: 'סטטוס',
                        sortable: true,
                        readOnly: true
                    },
                    {
                        key: 'createdAt',
                        label: 'עדכון אחרון',
                        sortable: true,
                        readOnly: true,
                        render: (data) => new Date(data.createdAt).toLocaleDateString('he-IL')
                    },
                    {
                        key: 'actions',
                        label: 'פעולות',
                        sortable: false,
                        readOnly: true,
                        render: (data) => {
                            const escapedDocType = data.documentType.replace(/'/g, "\\'").replace(/"/g, '&quot;');
                            
                            const viewBtn = this.options.allowDownload && data.fileSize > 0
                                ? `<button onclick="documentsTableInstance_${this.containerId}.viewDocument(${data.id})" class="btn-icon" title="צפייה">
                                    <img src="view_icon.png" alt="צפייה" class="action-icon-natural">
                                </button>`
                                : `<button class="btn-icon" disabled title="אין קובץ לצפייה">
                                    <img src="view_icon.png" alt="צפייה" class="action-icon-natural" style="opacity: 0.3;">
                                </button>`;
                                
                            const downloadBtn = this.options.allowDownload && data.fileSize > 0
                                ? `<button onclick="documentsTableInstance_${this.containerId}.downloadDocument(${data.id})" class="btn-icon" title="הורדה">
                                    <img src="download_icon.png" alt="הורדה" class="action-icon-natural">
                                </button>`
                                : (this.options.allowDownload
                                ? `<button class="btn-icon" disabled title="אין קובץ להורדה">
                                    <img src="download_icon.png" alt="הורדה" class="action-icon-natural" style="opacity: 0.3;">
                                </button>`
                                    : '');
        
                            const uploadBtn = this.options.allowUpload
                                ? `<button 
                                    onclick="documentsTableInstance_${this.containerId}.showUploadModal(${data.id}, ${data.fileSize}, '${escapedDocType}', ${data.documentTypeId})" 
                                    data-doc-id="${data.id}"
                                    data-file-size="${data.fileSize}"
                                    data-doc-type="${escapedDocType}"
                                    data-doc-type-id="${data.documentTypeId}"
                                    class="btn-icon" 
                                    title="העלאת קובץ">
                                    <img src="upload_icon.png" alt="העלאה" class="action-icon-natural">
                                </button>`
                                : '';
        
                            const deleteBtn = this.options.allowDelete
                                ? `<button onclick="documentsTableInstance_${this.containerId}.deleteDocument(${data.id})" class="btn-icon" title="מחיקה">
                                    <img src="delete_icon.png" alt="מחיקה" class="action-icon-natural">
                                </button>`
                                : '';
        
                            return `${viewBtn} ${downloadBtn} ${uploadBtn} ${deleteBtn}`;
                        }
                    }
                ];
        
                this.documentsTable.init(documents, columns);
                console.log('✅ Documents table initialized');
            } catch (error) {
                console.error('❌ Error loading documents table:', error);
                alert('שגיאה בטעינת המסמכים');
            }
        }


        /**
 * View document in new browser tab
 */
        async viewDocument(documentId) {
            try {
                console.log('👁️ Viewing document:', documentId);

                const token = sessionStorage.getItem('authToken');
                const url = AppConfig.getApiUrl(`documents/${documentId}/download`);

                // ✅ FIXED: Fetch blob with auth header, then create object URL
                const response = await fetch(url, {
                    method: 'GET',
                    headers: {
                        'Authorization': `Bearer ${token}`,
                        'X-View-Mode': 'inline' // Signal backend this is for viewing
                    }
                });

                if (!response.ok) {
                    throw new Error('שגיאה בטעינת המסמך');
                }

                // Get blob and content type
                const blob = await response.blob();
                const contentType = response.headers.get('content-type');

                // Create object URL and open in new tab
                const objectUrl = URL.createObjectURL(blob);
                const viewWindow = window.open(objectUrl, '_blank');

                // Clean up object URL after window loads (or after 1 minute timeout)
                if (viewWindow) {
                    viewWindow.addEventListener('load', () => {
                        setTimeout(() => URL.revokeObjectURL(objectUrl), 1000);
                    });
                }

                // Fallback cleanup after 1 minute
                setTimeout(() => URL.revokeObjectURL(objectUrl), 60000);

                console.log('✅ Document opened in new tab');
            } catch (error) {
                console.error('❌ Error viewing document:', error);
                alert('שגיאה בפתיחת המסמך');
            }
        }

  /**
 * Show upload modal
 */
async showUploadModal(documentId, currentFileSize, documentTypeName, documentTypeId) {
    try {
        console.log('📤 Showing upload modal for document:', documentId);

        // Load status types only (document type is read-only)
        const statusTypes = await this.loadDocumentStatusTypes();

        // Create modal HTML
        const modalHtml = `
            <div class="modal-overlay" id="uploadModal_${this.containerId}">
                <div class="modal-content">
                    <div class="modal-header">
                        <h3>העלאת מסמך - ${documentTypeName}</h3>
                        <button class="modal-close" onclick="documentsTableInstance_${this.containerId}.closeUploadModal()">&times;</button>
                    </div>
                    <div class="modal-body">
                        <form id="uploadModalForm_${this.containerId}">
                            <div class="form-group">
                                <label for="uploadFile_${this.containerId}">בחר קובץ *</label>
                                <input type="file" id="uploadFile_${this.containerId}" required>
                            </div>

                            <div class="form-group">
                                <label for="uploadDescription_${this.containerId}">תיאור (אופציונלי)</label>
                                <input type="text" id="uploadDescription_${this.containerId}">
                            </div>

                            <div class="form-group">
                                <label>סוג מסמך</label>
                                <input type="text" value="${documentTypeName}" readonly class="readonly-field">
                            </div>

                            <div class="form-group">
                                <label for="uploadStatus_${this.containerId}">סטטוס *</label>
                                <select id="uploadStatus_${this.containerId}" required>
                                    ${statusTypes.map(status =>
                                        `<option value="${status.id}">${status.name}</option>`
                                    ).join('')}
                                </select>
                            </div>

                            <input type="hidden" id="uploadDocumentId_${this.containerId}" value="${documentId}">
                            <input type="hidden" id="uploadDocumentTypeId_${this.containerId}" value="${documentTypeId}">
                            <input type="hidden" id="uploadCurrentFileSize_${this.containerId}" value="${currentFileSize}">
                        </form>
                    </div>
                    <div class="modal-footer">
                        <button class="btn-secondary" onclick="documentsTableInstance_${this.containerId}.closeUploadModal()">ביטול</button>
                        <button class="btn-primary" onclick="documentsTableInstance_${this.containerId}.processUpload()">העלה</button>
                    </div>
                </div>
            </div>
        `;

        // Add modal to document body
        const modalContainer = document.createElement('div');
        modalContainer.innerHTML = modalHtml;
        document.body.appendChild(modalContainer.firstElementChild);

        console.log('✅ Upload modal displayed');
    } catch (error) {
        console.error('❌ Error showing upload modal:', error);
        alert('שגיאה בפתיחת חלון ההעלאה');
    }
}

        /**
         * Close upload modal
         */
        closeUploadModal() {
            const modal = document.getElementById(`uploadModal_${this.containerId}`);
            if (modal) {
                modal.remove();
            }
        }

        /**
         * Process upload from modal
         */
        async processUpload() {
            try {
                const fileInput = document.getElementById(`uploadFile_${this.containerId}`);
                const descriptionInput = document.getElementById(`uploadDescription_${this.containerId}`);
                const statusSelect = document.getElementById(`uploadStatus_${this.containerId}`);
                const documentIdInput = document.getElementById(`uploadDocumentId_${this.containerId}`);
                const documentTypeIdInput = document.getElementById(`uploadDocumentTypeId_${this.containerId}`);
                const currentFileSizeInput = document.getElementById(`uploadCurrentFileSize_${this.containerId}`);

                if (!fileInput.files || fileInput.files.length === 0) {
                    alert('אנא בחר קובץ להעלאה');
                    return;
                }

                const file = fileInput.files[0];
                const documentId = parseInt(documentIdInput.value);
                const documentTypeId = parseInt(documentTypeIdInput.value);
                const currentFileSize = parseInt(currentFileSizeInput.value);
                const hasExistingFile = currentFileSize > 0;

                // Check if replacing existing document
                let replaceExisting = false;
                if (hasExistingFile) {
                    replaceExisting = confirm('קיים כבר קובץ למסמך זה. האם ברצונך להחליף אותו?');
                    if (!replaceExisting) {
                        console.log('ℹ️ User cancelled replacement');
                        this.closeUploadModal();
                        return;
                    }
                }

                const formData = new FormData();
                formData.append('file', file);
                formData.append('description', descriptionInput.value || '');
                formData.append('statusId', statusSelect.value);
                formData.append('entityId', this.options.entityId);
                formData.append('documentTypeId', documentTypeId);

                if (this.options.yearId) {
                    formData.append('yearId', this.options.yearId);
                }

                
                    formData.append('existingDocumentId', documentId);
                    formData.append('replaceExisting', 'true');
               

                const token = sessionStorage.getItem('authToken');
                const url = AppConfig.getApiUrl('documents/upload');

                console.log('📤 Uploading document...');

                const response = await fetch(url, {
                    method: 'POST',
                    headers: { 'Authorization': `Bearer ${token}` },
                    body: formData
                });

                if (!response.ok) {
                    const error = await response.json();
                    throw new Error(error.error || 'שגיאה בהעלאת המסמך');
                }

                const result = await response.json();
                alert(result.message || 'המסמך הועלה בהצלחה');

                // Close modal
                this.closeUploadModal();

                // Reload table
                await this.loadDocumentsTable();

                // Call custom callback if provided
                if (this.options.onUploadSuccess) {
                    this.options.onUploadSuccess(result);
                }

                console.log('✅ Document uploaded successfully:', result);
            } catch (error) {
                console.error('❌ Error processing upload:', error);
                alert(error.message || 'שגיאה בהעלאת המסמך');
            }
        }


        /**
         * Setup upload form event handler
         */
        setupUploadForm() {
            const form = document.getElementById(`${this.containerId}_uploadForm`);
            if (form) {
                form.addEventListener('submit', async (e) => {
                    e.preventDefault();
                    await this.uploadDocument();
                });
            }
        }

        /**
         * Upload a new document
         */
        async uploadDocument() {
            try {
                const fileInput = document.getElementById(`${this.containerId}_fileInput`);
                const descriptionInput = document.getElementById(`${this.containerId}_descriptionInput`);
                const documentTypeIdInput = document.getElementById(`uploadDocumentTypeId_${this.containerId}`);

                if (!fileInput.files || fileInput.files.length === 0) {
                    alert('אנא בחר קובץ להעלאה');
                    return;
                }

                const file = fileInput.files[0];
                const formData = new FormData();
                formData.append('file', file);
                formData.append('description', descriptionInput.value);
                formData.append('documentTypeId', documentTypeIdInput.value);

                // Add entity context if provided
                if (this.options.entityId) {
                    formData.append('entityId', this.options.entityId);
                }
                if (this.options.yearId) {
                    formData.append('yearId', this.options.yearId);
                }

                uploadBtn.disabled = true;
                uploadBtn.innerHTML = '<img src="upload_icon.png" alt="מעלה..." class="action-icon-natural"> מעלה...';

                const token = sessionStorage.getItem('authToken');
                const url = AppConfig.getApiUrl('documents/upload');

                const response = await fetch(url, {
                    method: 'POST',
                    headers: { 'Authorization': `Bearer ${token}` },
                    body: formData
                });

                if (!response.ok) {
                    const error = await response.json();
                    throw new Error(error.error || 'שגיאה בהעלאת המסמך');
                }

                const result = await response.json();
                alert(result.message || 'המסמך הועלה בהצלחה');

                // Reset form
                document.getElementById(`${this.containerId}_uploadForm`).reset();

                // Reload table
                await this.loadDocumentsTable();

                // Call custom callback if provided
                if (this.options.onUploadSuccess) {
                    this.options.onUploadSuccess(result);
                }

                console.log('✅ Document uploaded successfully');
            } catch (error) {
                console.error('❌ Error uploading document:', error);
                alert(error.message || 'שגיאה בהעלאת המסמך');
            } finally {
                const uploadBtn = document.getElementById(`${this.containerId}_uploadBtn`);
                if (uploadBtn) {
                    uploadBtn.disabled = false;
                    uploadBtn.innerHTML = '<img src="upload_icon.png" alt="העלאה" class="action-icon-natural"> העלה מסמך';
                }
            }
        }

        /**
         * Download a document
         */
        async downloadDocument(documentId) {
            try {
                console.log('⬇ Downloading document:', documentId);

                const token = sessionStorage.getItem('authToken');
                const url = AppConfig.getApiUrl(`documents/${documentId}/download`);

                const response = await fetch(url, {
                    headers: { 'Authorization': `Bearer ${token}` }
                });

                if (!response.ok) {
                    throw new Error('שגיאה בהורדת המסמך');
                }

                const blob = await response.blob();
                const contentDisposition = response.headers.get('content-disposition');
                let filename = 'document';
                if (contentDisposition) {
                    const match = contentDisposition.match(/filename[^;=\n]*=((['"]).*?\2|[^;\n]*)/);
                    if (match && match[1]) {
                        filename = match[1].replace(/['"]/g, '');
                    }
                }

                const link = document.createElement('a');
                link.href = URL.createObjectURL(blob);
                link.download = filename;
                document.body.appendChild(link);
                link.click();
                document.body.removeChild(link);
                URL.revokeObjectURL(link.href);

                console.log('✅ Document downloaded');
            } catch (error) {
                console.error('❌ Error downloading document:', error);
                alert('שגיאה בהורדת המסמך');
            }
        }

        /**
         * Delete a document
         */
        async deleteDocument(documentId) {
            try {
                if (!confirm('האם אתה בטוח שברצונך למחוק את המסמך?')) {
                    return;
                }

                console.log('🗑 Deleting document:', documentId);

                const token = sessionStorage.getItem('authToken');
                const url = AppConfig.getApiUrl(`documents/${documentId}`);

                const response = await fetch(url, {
                    method: 'DELETE',
                    headers: { 'Authorization': `Bearer ${token}` }
                });

                if (!response.ok) {
                    throw new Error('שגיאה במחיקת המסמך');
                }

                const result = await response.json();
                alert(result.message || 'המסמך נמחק בהצלחה');

                // Reload table
                await this.loadDocumentsTable();

                // Call custom callback if provided
                if (this.options.onDeleteSuccess) {
                    this.options.onDeleteSuccess(documentId);
                }

                console.log('✅ Document deleted');
            } catch (error) {
                console.error('❌ Error deleting document:', error);
                alert('שגיאה במחיקת המסמך');
            }
        }

        /**
         * Reload the documents table
         */
        async reload() {
            await this.loadDocumentsTable();
        }

        /**
         * Update component options (e.g., change entityId)
         */
        updateOptions(newOptions) {
            this.options = { ...this.options, ...newOptions };
        }

        /**
         * Format file size helper
         */
        formatFileSize(bytes) {
            if (!bytes || bytes === 0) return '0 B';
            const k = 1024;
            const sizes = ['B', 'KB', 'MB', 'GB'];
            const i = Math.floor(Math.log(bytes) / Math.log(k));
            return Math.round(bytes / Math.pow(k, i) * 100) / 100 + ' ' + sizes[i];
        }
    }; console.log('✅ DocumentsTableComponent class registered');
}

// Make class globally available
//window.DocumentsTableComponent = DocumentsTableComponent;