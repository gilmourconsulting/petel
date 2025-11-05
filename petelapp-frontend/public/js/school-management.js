/**
 * School Management Module
 * Handles school creation, editing, and related operations
 * Following Authentication & Session Management patterns
 */

class SchoolManagement {
    constructor() {
        this.modal = null;
    }

    /**
     * Show add new school modal
     */
    async showAddSchoolModal() {
        console.log('🚀 showAddSchoolModal called');
        
        try {
            // Check auth token (only thing stored in frontend)
            const authToken = sessionStorage.getItem('authToken');
            console.log('🔑 Auth token check:', { exists: !!authToken });
            
            if (!authToken) {
                console.error('❌ No auth token found');
                alert('נדרשת התחברות למערכת');
                return;
            }

            // Get entity types for dropdown
            const entityTypesUrl = AppConfig.getApiUrl('entities/entity-types');
            console.log('🌐 Fetching entity types from:', entityTypesUrl);
            
            const entityTypesResponse = await fetch(entityTypesUrl, {
                headers: {
                    'Authorization': `Bearer ${authToken}`
                }
            });

            console.log('📥 Entity types response:', {
                status: entityTypesResponse.status,
                ok: entityTypesResponse.ok
            });

            if (!entityTypesResponse.ok) {
                const errorText = await entityTypesResponse.text();
                console.error('❌ Entity types error:', errorText);
                
                if (entityTypesResponse.status === 401) {
                    alert('פג תוקף ההתחברות. נא להתחבר מחדש.');
                    if (typeof window.sessionManager !== 'undefined') {
                        window.sessionManager.logout();
                    }
                    return;
                }
                
                throw new Error(`Failed to load entity types: ${entityTypesResponse.status}`);
            }

            const entityTypes = await entityTypesResponse.json();
            console.log('📋 Entity types loaded:', entityTypes);

            if (!Array.isArray(entityTypes) || entityTypes.length === 0) {
                console.warn('⚠️ No entity types returned');
                alert('לא נמצאו סוגי גופים במערכת');
                return;
            }

            // Get all entities for owner dropdown
            console.log('🏢 Loading entities...');
            const entitiesUrl = AppConfig.getApiUrl('entities/login');
            const entitiesResponse = await fetch(entitiesUrl);
            
            console.log('📥 Entities response:', {
                status: entitiesResponse.status,
                ok: entitiesResponse.ok
            });

            if (!entitiesResponse.ok) {
                const errorText = await entitiesResponse.text();
                console.error('❌ Entities error:', errorText);
                throw new Error(`Failed to load entities: ${entitiesResponse.status}`);
            }

            const entities = await entitiesResponse.json();
            console.log('🏢 Entities loaded:', entities);

            if (!Array.isArray(entities) || entities.length === 0) {
                console.warn('⚠️ No entities returned');
                alert('לא נמצאו גופים במערכת');
                return;
            }

            // ✅ CORRECT: Get current entity ID from backend session via SessionState
            console.log('👤 Getting session info via SessionState...');
            
            if (typeof window.SessionState === 'undefined') {
                console.error('❌ SessionState not available');
                alert('שגיאה: מערכת הסשן לא זמינה');
                return;
            }

            // ✅ CORRECT: Get identity data from backend session
         //   const sessionInfo = await window.SessionState.getSessionInfo();
        //    console.log('✅ Session info retrieved:', sessionInfo);

            const sessionInfo = await window.SessionState.getSession();
            console.log('✅ Session info retrieved:', sessionInfo);

            const currentEntityId = parseInt(sessionInfo.entityId);
            console.log('👤 Current entity ID:', currentEntityId);

            // Build modal HTML
            const modalHtml = `
                <div class="modal-overlay" id="addSchoolModal">
                    <div class="modal-content" style="max-width: 500px;">
                        <div class="modal-header">
                            <h3>הוספת בית ספר חדש</h3>
                            <button class="close-btn" onclick="schoolManagement.closeModal()">&times;</button>
                        </div>
                        <div class="modal-body">
                            <form id="addSchoolForm">
                                <div class="form-group">
                                    <label for="schoolName">שם בית הספר *</label>
                                    <input type="text" 
                                           id="schoolName" 
                                           name="schoolName" 
                                           class="form-control" 
                                           required 
                                           maxlength="255"
                                           placeholder="הזן שם בית ספר">
                                </div>

                                <div class="form-group">
                                    <label for="entityType">סוג גוף *</label>
                                    <select id="entityType" 
                                            name="entityType" 
                                            class="form-control" 
                                            disabled 
                                            required>
                                        ${entityTypes.map(et => `
                                            <option value="${et.id}" ${et.id === 4 ? 'selected' : ''}>
                                                ${et.name}
                                            </option>
                                        `).join('')}
                                    </select>
                                    <input type="hidden" id="entityTypeValue" value="4">
                                </div>

                                <div class="form-group">
                                    <label for="owner">בעלים *</label>
                                    <select id="owner" 
                                            name="owner" 
                                            class="form-control" 
                                            required>
                                        ${entities.map(e => `
                                            <option value="${e.id}" ${e.id === currentEntityId ? 'selected' : ''}>
                                                ${e.name}
                                            </option>
                                        `).join('')}
                                    </select>
                                </div>

                                <div class="form-actions" style="margin-top: 20px; display: flex; gap: 10px; justify-content: flex-end;">
                                    <button type="button" 
                                            class="btn btn-secondary" 
                                            onclick="schoolManagement.closeModal()">
                                        ביטול
                                    </button>
                                    <button type="submit" 
                                            class="btn btn-primary">
                                        אישור
                                    </button>
                                </div>
                            </form>
                        </div>
                    </div>
                </div>
            `;

            // Add modal to page
            const modalContainer = document.createElement('div');
            modalContainer.innerHTML = modalHtml;
            document.body.appendChild(modalContainer.firstElementChild);

            this.modal = document.getElementById('addSchoolModal');

            // Add form submit handler
            document.getElementById('addSchoolForm').addEventListener('submit', (e) => {
                e.preventDefault();
                this.handleAddSchool();
            });

            // Focus on school name input
            setTimeout(() => {
                document.getElementById('schoolName').focus();
            }, 100);

            console.log('✅ Modal displayed successfully');

        } catch (error) {
            console.error('❌ Error showing add school modal:', error);
            console.error('Error stack:', error.stack);
            alert('שגיאה בטעינת טופס הוספת בית ספר:\n' + error.message);
        }
    }

    /**
     * Handle add school form submission
     */
    async handleAddSchool() {
        console.log('📝 handleAddSchool called');
        
        try {
            const form = document.getElementById('addSchoolForm');
            const submitBtn = form.querySelector('button[type="submit"]');
            
            // Disable submit button
            submitBtn.disabled = true;
            submitBtn.textContent = 'שומר...';

            // Get form values
            const schoolName = document.getElementById('schoolName').value.trim();
            const entityTypeId = parseInt(document.getElementById('entityTypeValue').value);
            const ownerId = parseInt(document.getElementById('owner').value);

            if (!schoolName) {
                alert('נא להזין שם בית ספר');
                submitBtn.disabled = false;
                submitBtn.textContent = 'אישור';
                return;
            }

            console.log('📝 Creating school:', { schoolName, entityTypeId, ownerId });

            // Get auth token (only thing stored in frontend sessionStorage)
            const authToken = sessionStorage.getItem('authToken');
            console.log('🔑 Using auth token:', authToken ? 'present' : 'missing');

            if (!authToken) {
                alert('נדרשת התחברות למערכת');
                submitBtn.disabled = false;
                submitBtn.textContent = 'אישור';
                return;
            }

            const createUrl = AppConfig.getApiUrl('entities/create-school');
            console.log('🌐 Calling:', createUrl);

            // Call API to create school
            const response = await fetch(createUrl, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Authorization': `Bearer ${authToken}`
                },
                body: JSON.stringify({
                    name: schoolName,
                    entityTypeId: entityTypeId,
                    ownerId: ownerId
                })
            });

            console.log('📥 Create school response:', {
                status: response.status,
                ok: response.ok
            });

            const result = await response.json();
            console.log('📋 Create school result:', result);

            if (response.ok && result.success) {
                console.log('✅ School created successfully:', result.data);

                // Close modal first
                this.closeModal();

                // Show success message
                alert('בית הספר נוצר בהצלחה!');

                // ✅ CORRECT: Clear session school properties using SessionState
                console.log('🧹 Clearing session school properties...');
                await window.SessionState.setProperty('SelectedSchoolId', '');
                await window.SessionState.setProperty('SelectedSchoolName', '');
                await window.SessionState.setProperty('SelectedSchoolType', '');
                await window.SessionState.setProperty('SelectedSchoolOwner', '');
                await window.SessionState.setProperty('SelectedSchoolYearId', '');

                console.log('✅ Session school properties cleared');

                // Reload school list
                if (typeof window.loadSchoolsData === 'function') {
                    console.log('🔄 Reloading school list...');
                    await window.loadSchoolsData();
                } else {
                    console.warn('⚠️ loadSchoolsData function not found');
                }

            } else {
                console.error('❌ Failed to create school:', result);
                
                // Check for auth error
                if (response.status === 401) {
                    alert('פג תוקף ההתחברות. נא להתחבר מחדש.');
                    if (typeof window.sessionManager !== 'undefined') {
                        window.sessionManager.logout();
                    }
                    return;
                }
                
                alert(result.message || 'שגיאה ביצירת בית הספר');
                submitBtn.disabled = false;
                submitBtn.textContent = 'אישור';
            }

        } catch (error) {
            console.error('❌ Error creating school:', error);
            console.error('Error stack:', error.stack);
            alert('שגיאה ביצירת בית הספר:\n' + error.message);
            const submitBtn = document.getElementById('addSchoolForm')?.querySelector('button[type="submit"]');
            if (submitBtn) {
                submitBtn.disabled = false;
                submitBtn.textContent = 'אישור';
            }
        }
    }

    /**
     * Close and remove modal
     */
    closeModal() {
        console.log('🔒 Closing modal');
        if (this.modal) {
            this.modal.remove();
            this.modal = null;
        }
    }
}

// Create global instance
window.schoolManagement = new SchoolManagement();
console.log('✅ SchoolManagement module loaded and instance created');