/**
 * Person Management Module
 * Handles person-related operations including search, selection, and updates
 */

console.log('📦 Person Management Module Loading...');

if (typeof PersonManagement === 'undefined') {
    class PersonManagement {
        constructor() {
            this.modal = null;
            this._currentCallback = null;
        }

        //console.log('📦 Person Management Module Loading...');

        async showPersonEditModal(personType, currentPersonData, callback) {
            console.log('👤 Opening person edit choice modal for:', personType);

            // ✅ Store callback for later use
            this._currentCallback = callback;

            const modalTitle = this.getModalTitle(personType);
            const position = this.getPositionByType(personType);

            const overlay = document.createElement('div');
            overlay.className = 'modal-overlay';

            const dialog = document.createElement('div');
            dialog.className = 'modal-content modal-small';

            const hasCurrentPerson = currentPersonData && currentPersonData.id;
            const personName = hasCurrentPerson
                ? `${currentPersonData.firstName} ${currentPersonData.lastName}`
                : 'אין איש קשר נוכחי';

            dialog.innerHTML = `
            <div class="modal-header">
                <h3>${modalTitle}</h3>
            </div>
            <div class="modal-body">
                ${hasCurrentPerson ? `
                    <div class="alert alert-info mb-3">
                        <div class="font-weight-bold mb-1">איש קשר נוכחי:</div>
                        <div>${personName}</div>
                        ${currentPersonData.position ? `<div class="text-muted mt-1">תפקיד: ${currentPersonData.position}</div>` : ''}
                    </div>
                ` : `
                    <div class="alert alert-warning mb-3">
                        <div>לא הוגדר איש קשר</div>
                    </div>
                `}
            </div>
            <div class="modal-footer" style="display: flex; gap: 10px; justify-content: flex-end;">
                <button id="cancelChoiceBtn" class="dialog-btn cancel">ביטול</button>
                ${hasCurrentPerson ? `
                    <button id="updateContactBtn" class="btn-primary">עדכון פרטי התקשרות</button>
                ` : ''}
                <button id="changePersonBtn" class="dialog-btn save">
                    ${hasCurrentPerson ? 'החלפת איש קשר' : 'בחירת איש קשר'}
                </button>
            </div>
        `;

            overlay.appendChild(dialog);
            document.body.appendChild(overlay);

            // Update contact details button
            const updateContactBtn = document.getElementById('updateContactBtn');
            if (updateContactBtn) {
                updateContactBtn.onclick = () => {
                    document.body.removeChild(overlay);
                    this.showUpdateContactModal(personType, currentPersonData);
                };
            }

            // Change person button
            document.getElementById('changePersonBtn').onclick = () => {
                document.body.removeChild(overlay);
                this.showPersonSearchModal(personType, position);
            };

            // Cancel button - call callback with null
            document.getElementById('cancelChoiceBtn').onclick = () => {
                document.body.removeChild(overlay);
                if (this._currentCallback) {
                    this._currentCallback(null);
                    this._currentCallback = null;
                }
            };

            // ESC key handler
            const escHandler = (e) => {
                if (e.key === 'Escape' && document.body.contains(overlay)) {
                    document.body.removeChild(overlay);
                    if (this._currentCallback) {
                        this._currentCallback(null);
                        this._currentCallback = null;
                    }
                    document.removeEventListener('keydown', escHandler);
                }
            };
            document.addEventListener('keydown', escHandler);
        }

        /**
         * Show modal to update contact details only (phone/email)
         */
        showUpdateContactModal(personType, personData) {
            console.log('📞 Opening update contact modal');

            const modalTitle = this.getModalTitle(personType);

            const overlay = document.createElement('div');
            overlay.className = 'modal-overlay';

            const dialog = document.createElement('div');
            dialog.className = 'modal-content';

            dialog.innerHTML = `
            <div class="modal-header">
                <h3>${modalTitle} - עדכון פרטי התקשרות</h3>
            </div>
            <div class="modal-body">
                <div class="form-group">
                    <label>שם מלא:</label>
                    <div class="d-flex gap-2">
                        <div style="flex: 1;">
                            <input type="text" id="personFirstName" class="form-control" value="${personData.firstName || ''}" placeholder="שם פרטי" readonly disabled>
                        </div>
                        <div style="flex: 1;">
                            <input type="text" id="personLastName" class="form-control" value="${personData.lastName || ''}" placeholder="שם משפחה" readonly disabled>
                        </div>
                    </div>
                </div>
                
                <div class="form-group">
                    <label>טלפון:</label>
                    <div class="d-flex gap-2" style="direction: ltr;">
                        <input type="text" id="personPhonePrefix" class="form-control" value="${personData.phoneNumberPrefix || ''}" placeholder="קידומת" maxlength="7" style="width: 80px; text-align: left;">
                        <input type="text" id="personPhone" class="form-control" value="${personData.phoneNumber || ''}" placeholder="מספר טלפון" maxlength="10" style="flex: 1; text-align: left;">
                    </div>
                </div>
                
                <div class="form-group">
                    <label>דוא"ל:</label>
                    <input type="email" id="personEmail" class="form-control" value="${personData.email || ''}" placeholder="example@domain.com" style="direction: ltr; text-align: left;">
                </div>
                
                <input type="hidden" id="personRecordId" value="${personData.id}">
            </div>
            <div class="modal-footer" style="display: flex; gap: 10px; justify-content: flex-end;">
                <button id="updateContactCancelBtn" class="dialog-btn cancel">ביטול</button>
                <button id="updateContactOkBtn" class="dialog-btn save">אישור</button>
            </div>
        `;

            overlay.appendChild(dialog);
            document.body.appendChild(overlay);

            // OK button handler
            document.getElementById('updateContactOkBtn').onclick = async () => {
                await this.saveContactUpdate(personType, overlay);
            };

            // Cancel button
            document.getElementById('updateContactCancelBtn').onclick = () => {
                document.body.removeChild(overlay);
            };

            // ESC key handler
            const escHandler = (e) => {
                if (e.key === 'Escape' && document.body.contains(overlay)) {
                    document.body.removeChild(overlay);
                    document.removeEventListener('keydown', escHandler);
                }
            };
            document.addEventListener('keydown', escHandler);

            // Focus phone field
            setTimeout(() => document.getElementById('personPhonePrefix')?.focus(), 100);
        }

        /**
         * Show person search modal
         */
        showPersonSearchModal(personType, position) {
            console.log('🔍 Opening person search modal');

            const modalTitle = this.getModalTitle(personType);

            const overlay = document.createElement('div');
            overlay.className = 'modal-overlay';

            const dialog = document.createElement('div');
            dialog.className = 'modal-content modal-large';

            dialog.innerHTML = `
            <div class="modal-header">
                <h3>${modalTitle} - חיפוש/בחירה</h3>
            </div>
            <div class="modal-body">
                <div class="form-group">
                    <label>חיפוש לפי שם:</label>
                    <div class="d-flex gap-2">
                        <input type="text" id="searchFirstName" class="form-input" placeholder="שם פרטי" >
                        <input type="text" id="searchLastName" class="form-input" placeholder="שם משפחה" >
                        <button id="searchBtn" class="btn-primary">
                            חפש
                        </button>
                    </div>
                </div>
                
                <div id="searchResults" class="mb-3" style="max-height: 300px; overflow-y: auto; border: 1px solid var(--border-color); border-radius: var(--border-radius); display: none;"></div>
                
                <input type="hidden" id="selectedPersonId" value="">
                <input type="hidden" id="personPosition" value="${position}">
            </div>
            <div class="modal-footer" style="display: flex; gap: 10px; justify-content: flex-end;">
                <button id="searchCancelBtn" class="dialog-btn cancel">ביטול</button>
                <button id="createNewPersonBtn" class="dialog-btn save">
                    + איש קשר חדש
                </button>
            </div>
        `;

            overlay.appendChild(dialog);
            document.body.appendChild(overlay);

            // Search button handler
            document.getElementById('searchBtn').onclick = async () => {
                await this.performPersonSearch();
            };

            // Create new person button
            document.getElementById('createNewPersonBtn').onclick = () => {
                document.body.removeChild(overlay);
                this.showNewPersonModal(personType, position);
            };

            // Cancel button
            document.getElementById('searchCancelBtn').onclick = () => {
                document.body.removeChild(overlay);
            };

            // Enter key to search
            ['searchFirstName', 'searchLastName'].forEach(id => {
                document.getElementById(id).addEventListener('keypress', (e) => {
                    if (e.key === 'Enter') {
                        document.getElementById('searchBtn').click();
                    }
                });
            });

            // ESC key handler
            const escHandler = (e) => {
                if (e.key === 'Escape' && document.body.contains(overlay)) {
                    document.body.removeChild(overlay);
                    document.removeEventListener('keydown', escHandler);
                }
            };
            document.addEventListener('keydown', escHandler);

            // Focus first name field
            setTimeout(() => document.getElementById('searchFirstName')?.focus(), 100);
        }

        /**
         * Perform person search
         */
        async performPersonSearch() {
            const firstName = document.getElementById('searchFirstName').value.trim();
            const lastName = document.getElementById('searchLastName').value.trim();
            const resultsContainer = document.getElementById('searchResults');

            if (!firstName && !lastName) {
                alert('נא להזין לפחות שם פרטי או שם משפחה לחיפוש');
                return;
            }

            console.log('🔍 Searching persons:', { firstName, lastName });

            resultsContainer.innerHTML = '<div style="padding: 20px; text-align: center;">מחפש...</div>';
            resultsContainer.style.display = 'block';

            try {
                const params = new URLSearchParams();
                if (firstName) params.append('firstName', firstName);
                if (lastName) params.append('lastName', lastName);

                const response = await fetch(AppConfig.getApiUrl(`persons/search?${params.toString()}`), {
                    method: 'GET',
                    headers: {
                        'Content-Type': 'application/json',
                        'Authorization': `Bearer ${sessionStorage.getItem('authToken')}`
                    }
                });

                const result = await response.json();

                if (response.ok && result.success && result.data) {
                    this.displaySearchResults(result.data);
                } else {
                    resultsContainer.innerHTML = '<div style="padding: 20px; text-align: center; color: #6c757d;">לא נמצאו תוצאות</div>';
                }

            } catch (error) {
                console.error('❌ Error searching persons:', error);
                resultsContainer.innerHTML = '<div style="padding: 20px; text-align: center; color: #dc3545;">שגיאה בחיפוש</div>';
            }
        }

        /**
         * Display search results
         */
        displaySearchResults(persons) {
            const resultsContainer = document.getElementById('searchResults');

            if (!persons || persons.length === 0) {
                resultsContainer.innerHTML = '<div style="padding: 20px; text-align: center; color: #6c757d;">לא נמצאו תוצאות</div>';
                return;
            }

            const resultsHTML = persons.map(person => {
                // Concatenate phone prefix and number
                const phoneDisplay = [person.phoneNumberPrefix, "-", person.phoneNumber]
                    .filter(p => p)
                    .join('');

                return `
                <div class="person-result-item" 
                     data-person-id="${person.id}"
                     style="padding: 12px; border-bottom: 1px solid var(--border-color); cursor: pointer; direction: rtl; transition: background 0.2s;"
                     onmouseover="this.style.background='var(--hover-background)'"
                     onmouseout="this.style.background='white'">
                    <div style="display: grid; grid-template-columns: 1fr 1fr 1fr 1fr; gap: 15px; align-items: center;">
                        <div class="font-weight-bold">${person.firstName || '-'}</div>
                        <div class="font-weight-bold">${person.lastName || '-'}</div>
                        <div style="direction: ltr; text-align: right;">${phoneDisplay || '-'}</div>
                        <div>${person.position || '-'}</div>
                    </div>
                </div>
            `;
            }).join('');

            resultsContainer.innerHTML = resultsHTML;

            // Add click handlers to select person
            document.querySelectorAll('.person-result-item').forEach(item => {
                item.onclick = () => {
                    const personId = item.dataset.personId;
                    this.selectPersonFromSearch(personId);
                };
            });
        }

        /**
         * Select person from search results
         */

        async selectPersonFromSearch(personId) {
            console.log('✅ Person selected from search:', personId);

            try {
                // Get person details
                const response = await fetch(AppConfig.getApiUrl(`persons/${personId}`), {
                    method: 'GET',
                    headers: {
                        'Content-Type': 'application/json',
                        'Authorization': `Bearer ${sessionStorage.getItem('authToken')}`
                    }
                });

                const result = await response.json();

                if (response.ok && result.success && result.data) {
                    // ✅ Call callback with selected person data
                    if (this._currentCallback) {
                        this._currentCallback(result.data);
                        this._currentCallback = null;
                    }

                    // Close search modal
                    const overlay = document.querySelector('.dialog-overlay');
                    if (overlay) {
                        document.body.removeChild(overlay);
                    }
                } else {
                    alert('שגיאה בטעינת פרטי איש הקשר');
                }

            } catch (error) {
                console.error('❌ Error loading person details:', error);
                alert('שגיאה בטעינת פרטי איש הקשר');
            }
        }
        /**
         * Show new person modal
         */

        /**
 * Show new person modal
 */
        showNewPersonModal(personType, position) {
            console.log('➕ Opening new person modal');

            const modalTitle = this.getModalTitle(personType);
            const isTranslator = personType === 'signLanguageTranslator';

            const overlay = document.createElement('div');
            overlay.className = 'modal-overlay';

            const dialog = document.createElement('div');
            dialog.className = 'modal-content';

            dialog.innerHTML = `
                <div class="modal-header">
                    <h3>${modalTitle} - איש קשר חדש</h3>
                </div>
                <div class="modal-body">
                    <div class="form-group">
                        <label for="newPersonIdNumber" class="${isTranslator ? 'required' : ''}">
                            תעודת זהות:
                        </label>
                        <input type="text" id="newPersonIdNumber" class="form-input" placeholder="הזן תעודת זהות" maxlength="9" style="direction: rtl; text-align: left;">
                        <small class="text-muted d-block mt-1">
                            ${isTranslator ? '9 ספרות ללא מקפים (שדה חובה למתורגמנים)' : '9 ספרות ללא מקפים (אופציונלי)'}
                        </small>
                    </div>
                    
                    <div class="form-group">
                        <label class="required">שם מלא:</label>
                        <div class="d-flex gap-2">
                            
                                <input type="text" id="newPersonFirstName" class="form-input" placeholder="שם פרטי">
                            
                            
                                <input type="text" id="newPersonLastName" class="form-input" placeholder="שם משפחה">
                            
                        </div>
                    </div>
                    
                    <div class="form-group">
                        <label>טלפון:</label>
                        <div class="d-flex gap-2" style="direction: rtl;">
                        <input type="text" id="newPersonPhone" class="form-input" placeholder="מספר טלפון" maxlength="10" style="flex: 1; text-align: left;">
                            <input type="text" id="newPersonPhonePrefix" class="form-input" placeholder="קידומת" maxlength="7" style="width: 80px; text-align: left;">
                            
                        </div>
                    </div>
                    
                    <div class="form-group">
                        <label for="newPersonEmail">דוא"ל:</label>
                        <input type="email" id="newPersonEmail" class="form-input" placeholder="example@domain.com" style="direction: rtl; text-align: left;">
                    </div>
                    
                    <div class="form-group">
                        <label for="newPersonPosition">תפקיד:</label>
                        <input type="text" id="newPersonPosition" class="form-input" value="${position}" readonly disabled>
                    </div>
                </div>
                <div class="modal-footer" style="display: flex; gap: 10px; justify-content: flex-end;">
                    <button id="newPersonCancelBtn" class="dialog-btn cancel">ביטול</button>
                    <button id="newPersonOkBtn" class="dialog-btn save">אישור</button>
                </div>
            `;

            overlay.appendChild(dialog);
            document.body.appendChild(overlay);

            // OK button handler
            document.getElementById('newPersonOkBtn').onclick = async () => {
                await this.saveNewPerson(personType, position, overlay);
            };

            // Cancel button
            document.getElementById('newPersonCancelBtn').onclick = () => {
                document.body.removeChild(overlay);
            };

            // ESC key handler
            const escHandler = (e) => {
                if (e.key === 'Escape' && document.body.contains(overlay)) {
                    document.body.removeChild(overlay);
                    document.removeEventListener('keydown', escHandler);
                }
            };
            document.addEventListener('keydown', escHandler);

            // Focus ID number field for translators, first name for others
            setTimeout(() => {
                if (isTranslator) {
                    document.getElementById('newPersonIdNumber')?.focus();
                } else {
                    document.getElementById('newPersonFirstName')?.focus();
                }
            }, 100);
        }
        /**
         * Save contact update
         */
        async saveContactUpdate(personType, overlay, originalPersonData) {
            const recordId = document.getElementById('personRecordId').value;
            const phonePrefix = document.getElementById('personPhonePrefix')?.value.trim() || '';
            const phone = document.getElementById('personPhone')?.value.trim() || '';
            const email = document.getElementById('personEmail')?.value.trim() || '';

            // Validate email format if provided
            if (email && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) {
                alert('כתובת דוא"ל לא תקינה');
                document.getElementById('personEmail')?.focus();
                return;
            }

            console.log('💾 Updating contact details:', { recordId, phonePrefix, phone, email });

            try {
                const updatePayload = {
                    id: parseInt(recordId),
                    phoneNumberPrefix: phonePrefix || null,
                    phoneNumber: phone || null,
                    email: email || null
                };

                const response = await fetch(AppConfig.getApiUrl(`persons/${recordId}`), {
                    method: 'PUT',
                    headers: {
                        'Content-Type': 'application/json',
                        'Authorization': `Bearer ${sessionStorage.getItem('authToken')}`
                    },
                    body: JSON.stringify(updatePayload)
                });

                const result = await response.json();

                if (response.ok && result.success) {
                    console.log('✅ Contact details updated successfully');
                    document.body.removeChild(overlay);

                    // ✅ Call callback with SAME person (ID unchanged, just contact details updated)
                    if (this._currentCallback) {
                        this._currentCallback(originalPersonData);
                        this._currentCallback = null;
                    }

                    alert('פרטי התקשרות עודכנו בהצלחה');
                } else {
                    console.error('❌ Failed to update contact details:', result);
                    alert(result.message || 'שגיאה בעדכון פרטי התקשרות');
                }

            } catch (error) {
                console.error('💥 Error updating contact details:', error);
                alert('שגיאה בעדכון פרטי התקשרות');
            }
        }

        /**
         * Save new person
         */
        async saveNewPerson(personType, position, overlay) {
            const idNumber = document.getElementById('newPersonIdNumber')?.value.trim() || '';
            const firstName = document.getElementById('newPersonFirstName')?.value.trim() || '';
            const lastName = document.getElementById('newPersonLastName')?.value.trim() || '';
            const phonePrefix = document.getElementById('newPersonPhonePrefix')?.value.trim() || '';
            const phone = document.getElementById('newPersonPhone')?.value.trim() || '';
            const email = document.getElementById('newPersonEmail')?.value.trim() || '';

            const isTranslator = personType === 'signLanguageTranslator';

            // ✅ Validation for translators - ID is REQUIRED
            if (isTranslator && !idNumber) {
                alert('נא להזין תעודת זהות למתורגמן');
                document.getElementById('newPersonIdNumber')?.focus();
                return;
            }

            // ✅ Validate Israeli ID format if provided
            if (idNumber) {
                if (!/^\d{9}$/.test(idNumber)) {
                    alert('תעודת זהות חייבת להכיל 9 ספרות');
                    document.getElementById('newPersonIdNumber')?.focus();
                    return;
                }

                // Optional: Israeli ID checksum validation
                if (!this.validateIsraeliId(idNumber)) {
                    const confirmed = confirm('תעודת הזהות אינה תקינה. האם להמשיך בכל זאת?');
                    if (!confirmed) {
                        document.getElementById('newPersonIdNumber')?.focus();
                        return;
                    }
                }
            }

            // Validation for name fields
            if (!firstName || !lastName) {
                alert('נא למלא שם פרטי ושם משפחה');
                return;
            }

            if (email && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) {
                alert('כתובת דוא"ל לא תקינה');
                document.getElementById('newPersonEmail')?.focus();
                return;
            }

            // Set correct position based on person type
            let finalPosition = position;
            if (personType === 'inspector') {
                finalPosition = 'מפקח';
            }

            console.log('➕ Creating new person:', { idNumber, firstName, lastName, position: finalPosition });

            try {
                const createPayload = {
                    firstName: firstName,
                    lastName: lastName,
                    phoneNumberPrefix: phonePrefix || null,
                    phoneNumber: phone || null,
                    email: email || null,
                    position: finalPosition,
                    idNumber: idNumber || '0',  // ✅ Use actual ID or default '0'
                    idType: 0,                   // ✅ ID type = 0 as specified
                    gender: 99                   // Required field with default
                };

                const response = await fetch(AppConfig.getApiUrl('persons'), {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'Authorization': `Bearer ${sessionStorage.getItem('authToken')}`
                    },
                    body: JSON.stringify(createPayload)
                });

                const result = await response.json();

                if (response.ok && result.success) {
                    console.log('✅ New person created with ID:', result.data.id);
                    document.body.removeChild(overlay);

                    // ✅ Call callback with new person data
                    if (this._currentCallback) {
                        this._currentCallback({
                            id: result.data.id,
                            idNumber: idNumber || null,
                            firstName: firstName,
                            lastName: lastName,
                            position: finalPosition
                        });
                        this._currentCallback = null;
                    }

                    alert('איש קשר חדש נוצר בהצלחה');
                } else {
                    console.error('❌ Failed to create person:', result);
                    alert(result.message || 'שגיאה ביצירת איש קשר חדש');
                }

            } catch (error) {
                console.error('💥 Error creating new person:', error);
                alert('שגיאה ביצירת איש קשר חדש');
            }
        }

        /**
         * Validate Israeli ID number using checksum algorithm
         */
        validateIsraeliId(id) {
            if (!/^\d{9}$/.test(id)) return false;

            let sum = 0;
            for (let i = 0; i < 9; i++) {
                let digit = parseInt(id[i]);
                let step = digit * ((i % 2) + 1);
                sum += step > 9 ? step - 9 : step;
            }

            return sum % 10 === 0;
        }

        /**
         * Update school details with person
         */
        async updateSchoolWithPerson(personType, personData) {
            console.log('💾 Updating school with person:', { personType, personId: personData.id });

            // Update cached data
            if (window.lastSchoolDetailsData) {
                const displayName = `${personData.firstName} ${personData.lastName}`.trim();

                switch (personType) {
                    case 'principal':
                        window.lastSchoolDetailsData.principalId = personData.id;
                        window.lastSchoolDetailsData.principalName = displayName;
                        break;
                    case 'inspector':
                        window.lastSchoolDetailsData.inspectorId = personData.id;
                        window.lastSchoolDetailsData.inspectorName = displayName;
                        break;
                    case 'contactPerson':
                        window.lastSchoolDetailsData.contactPersonId = personData.id;
                        window.lastSchoolDetailsData.contactPersonName = displayName;
                        break;
                }

                // Raise changes flag
                window.hasUnsavedSchoolDetailsChanges = true;
                if (typeof updateSchoolDetailsCardTitle === 'function') {
                    updateSchoolDetailsCardTitle();
                }

                // Re-render display
                if (typeof displaySchoolDetails === 'function') {
                    displaySchoolDetails(window.lastSchoolDetailsData);
                }
            }
        }

        /**
         * Helper: Get modal title by person type
         */
        getModalTitle(personType) {
            switch (personType) {
                case 'principal':
                    return 'עריכת מנהל/ת';
                case 'inspector':
                    return 'עריכת מפקח/ת';
                case 'contactPerson':
                    return 'עריכת איש קשר';
                case 'signLanguageTranslator':
                    return 'מתורגמן/ית לשפת הסימנים';
                default:
                    return 'עריכת איש קשר';
            }
        }

        /**
         * Helper: Get position by person type
         */
        getPositionByType(personType) {
            switch (personType) {
                case 'principal':
                    return 'מנהל';
                case 'inspector':
                    return 'מפקח';
                case 'contactPerson':
                    return 'איש קשר';
                case 'signLanguageTranslator':
                    return 'מתורגמן/ית לשפת הסימנים';
                default:
                    return '';
            }
        }

        /**
         * Helper: Get current person type (stored temporarily)
         */
        getCurrentPersonType() {
            return window._currentPersonType || 'contactPerson';
        }

        /**
         * Helper: Set current person type
         */
        setCurrentPersonType(personType) {
            window._currentPersonType = personType;
        }
    }

    // ✅ Make class globally available
    window.PersonManagement = PersonManagement;
}

// ✅ Create singleton instance OUTSIDE the conditional block
if (typeof window.personManagementInstance === 'undefined') {
    window.personManagementInstance = new window.PersonManagement();
}

// ✅ Create shorthand reference (matches usage pattern in other files)
if (typeof window.PersonManagement.showPersonEditModal === 'undefined') {
    window.PersonManagement.showPersonEditModal = function (personType, currentPersonData, callback) {
        return window.personManagementInstance.showPersonEditModal(personType, currentPersonData, callback);
    };

    window.PersonManagement.setCurrentPersonType = function (personType) {
        return window.personManagementInstance.setCurrentPersonType(personType);
    };
}

console.log('✅ Person Management Module Loaded');