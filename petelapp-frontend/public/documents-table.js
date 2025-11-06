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
        * Load document types from system attributes
        */
        async loadDocumentTypes() {
            try {
                const url = AppConfig.getApiUrl(`systemAttributes/by-category/${this.options.documentTypeCategory}`);
                const token = sessionStorage.getItem('authToken');

                const response = await fetch(url, {
                    headers: { 'Authorization': `Bearer ${token}` }
                });

                if (!response.ok) {
                    console.warn('⚠ Failed to load document types');
                    return;
                }

                this.documentTypes = await response.json();

                if (this.options.showUploadForm) {
                    const selectElement = document.getElementById(`${this.containerId}_documentTypeSelect`);
                    if (selectElement) {
                        this.documentTypes.forEach(type => {
                            const option = document.createElement('option');
                            option.value = type.id;
                            option.textContent = type.description;
                            selectElement.appendChild(option);
                        });
                    }
                }

                console.log('✅ Document types loaded:', this.documentTypes.length);
            } catch (error) {
                console.error('❌ Error loading document types:', error);
            }
        }

        /**
         * Load and display documents table
         */
        async loadDocumentsTable() {
            try {
                console.log('📊 Loading documents table...');

                const token = sessionStorage.getItem('authToken');

                // ✅ Build URL with query parameters if entityId/yearId provided
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
                        key: 'description',
                        label: 'תיאור',
                        sortable: true,
                        readOnly: true,
                        hidden: true
                    },
                    {
                        key: 'documentType',
                        label: 'סוג מסמך',
                        sortable: true,
                        readOnly: true
                    },
                    {
                        key: 'version',
                        label: 'גרסה',
                        sortable: true,
                        readOnly: true
                    },
                    {
                        key: 'fileEncoding',
                        label: 'סוג קובץ',
                        sortable: true,
                        readOnly: true
                    },
                    {
                        key: 'fileSize',
                        label: 'גודל',
                        sortable: true,
                        readOnly: true,
                        render: (data) => this.formatFileSize(data.fileSize)
                    },
                    {
                        key: 'createdAt',
                        label: 'תאריך יצירה',
                        sortable: true,
                        readOnly: true,
                        hidden: true,
                        render: (data) => new Date(data.createdAt).toLocaleDateString('he-IL')
                    },
                    {
                        key: 'actions',
                        label: 'פעולות',
                        sortable: false,
                        readOnly: true,
                        render: (data) => {
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
                                ? `<button onclick="documentsTableInstance_${this.containerId}.uploadDocument(${data.id})" class="btn-icon" title="העלאה">
                                <img src="upload_icon.png" alt="העלאה" class="action-icon-natural">
                            </button>`
                                : '';

                            const deleteBtn = this.options.allowDelete
                                ? `<button onclick="documentsTableInstance_${this.containerId}.deleteDocument(${data.id})" class="btn-icon" title="מחיקה">
                                <img src="delete_icon.png" alt="מחיקה" class="action-icon-natural">
                            </button>`
                                : '';

                            return `${downloadBtn} ${uploadBtn} ${deleteBtn}`;
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
                const documentTypeSelect = document.getElementById(`${this.containerId}_documentTypeSelect`);
                const uploadBtn = document.getElementById(`${this.containerId}_uploadBtn`);

                if (!fileInput.files || fileInput.files.length === 0) {
                    alert('אנא בחר קובץ להעלאה');
                    return;
                }

                const file = fileInput.files[0];
                const formData = new FormData();
                formData.append('file', file);
                formData.append('description', descriptionInput.value);
                formData.append('documentTypeId', documentTypeSelect.value);

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
window.DocumentsTableComponent = DocumentsTableComponent;