/**
 * Person Management Module
 * Handles person-related operations including search, selection, and updates
 */

console.log('📦 Person Management Module Loading...');

// Person Management Functions
const PersonManagement = {
    /**
     * Show person edit modal with option to update contact or change person
     */
    async showPersonEditModal(personType, currentPersonData) {
        console.log('👤 Opening person edit choice modal for:', personType);

        const modalTitle = this.getModalTitle(personType);
        const position = this.getPositionByType(personType);

        const overlay = document.createElement('div');
        overlay.className = 'dialog-overlay';

        const dialog = document.createElement('div');
        dialog.className = 'dialog-box';
        dialog.style.minWidth = '400px';

        const hasCurrentPerson = currentPersonData && currentPersonData.id;
        const personName = hasCurrentPerson 
            ? `${currentPersonData.firstName} ${currentPersonData.lastName}` 
            : 'אין איש קשר נוכחי';

        dialog.innerHTML = `
            <h3 class="dialog-title">${modalTitle}</h3>
            <div style="margin: 20px 0;">
                ${hasCurrentPerson ? `
                    <div style="margin-bottom: 20px; padding: 15px; background: #f8f9fa; border-radius: 4px; direction: rtl;">
                        <div style="font-weight: 600; margin-bottom: 5px;">איש קשר נוכחי:</div>
                        <div style="color: #495057;">${personName}</div>
                        ${currentPersonData.position ? `<div style="color: #6c757d; font-size: 0.9em;">תפקיד: ${currentPersonData.position}</div>` : ''}
                    </div>
                ` : `
                    <div style="margin-bottom: 20px; padding: 15px; background: #fff3cd; border-radius: 4px; direction: rtl;">
                        <div style="color: #856404;">לא הוגדר איש קשר</div>
                    </div>
                `}
                
                <div style="display: flex; flex-direction: column; gap: 10px;">
                    ${hasCurrentPerson ? `
                        <button id="updateContactBtn" class="dialog-btn" style="background: #007bff; width: 100%;">
                            עדכון פרטי התקשרות
                        </button>
                    ` : ''}
                    <button id="changePersonBtn" class="dialog-btn" style="background: #28a745; width: 100%;">
                        ${hasCurrentPerson ? 'החלפת איש קשר' : 'בחירת איש קשר'}
                    </button>
                </div>
            </div>
            <div class="dialog-buttons">
                <button id="cancelChoiceBtn" class="dialog-btn cancel">ביטול</button>
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

        // Cancel button
        document.getElementById('cancelChoiceBtn').onclick = () => {
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
    },

    /**
     * Show modal to update contact details only (phone/email)
     */
    showUpdateContactModal(personType, personData) {
        console.log('📞 Opening update contact modal');

        const modalTitle = this.getModalTitle(personType);

        const overlay = document.createElement('div');
        overlay.className = 'dialog-overlay';

        const dialog = document.createElement('div');
        dialog.className = 'dialog-box';
        dialog.style.minWidth = '500px';

        dialog.innerHTML = `
            <h3 class="dialog-title">${modalTitle} - עדכון פרטי התקשרות</h3>
            <div style="margin: 20px 0;">
                <div style="margin-bottom: 15px;">
                    <label style="display: block; margin-bottom: 5px; font-weight: 600;">שם פרטי:</label>
                    <input type="text" 
                           id="personFirstName" 
                           value="${personData.firstName || ''}"
                           readonly
                           style="
                               width: 100%;
                               padding: 8px;
                               border: 1px solid #dee2e6;
                               border-radius: 4px;
                               font-size: 14px;
                               direction: rtl;
                               background: #e9ecef;
                               cursor: not-allowed;
                           ">
                </div>
                
                <div style="margin-bottom: 15px;">
                    <label style="display: block; margin-bottom: 5px; font-weight: 600;">שם משפחה:</label>
                    <input type="text" 
                           id="personLastName" 
                           value="${personData.lastName || ''}"
                           readonly
                           style="
                               width: 100%;
                               padding: 8px;
                               border: 1px solid #dee2e6;
                               border-radius: 4px;
                               font-size: 14px;
                               direction: rtl;
                               background: #e9ecef;
                               cursor: not-allowed;
                           ">
                </div>
                
                <div style="margin-bottom: 15px;">
                    <label style="display: block; margin-bottom: 5px; font-weight: 600;">טלפון:</label>
                    <div style="display: flex; gap: 8px; direction: ltr;">
                        <input type="text" 
                               id="personPhonePrefix" 
                               value="${personData.phoneNumberPrefix || ''}"
                               placeholder="קידומת"
                               maxlength="7"
                               style="
                                   width: 80px;
                                   padding: 8px;
                                   border: 1px solid #dee2e6;
                                   border-radius: 4px;
                                   font-size: 14px;
                                   text-align: left;
                               ">
                        <input type="text" 
                               id="personPhone" 
                               value="${personData.phoneNumber || ''}"
                               placeholder="מספר טלפון"
                               maxlength="10"
                               style="
                                   flex: 1;
                                   padding: 8px;
                                   border: 1px solid #dee2e6;
                                   border-radius: 4px;
                                   font-size: 14px;
                                   text-align: left;
                               ">
                    </div>
                </div>
                
                <div style="margin-bottom: 15px;">
                    <label style="display: block; margin-bottom: 5px; font-weight: 600;">דוא"ל:</label>
                    <input type="email" 
                           id="personEmail" 
                           value="${personData.email || ''}"
                           placeholder="example@domain.com"
                           style="
                               width: 100%;
                               padding: 8px;
                               border: 1px solid #dee2e6;
                               border-radius: 4px;
                               font-size: 14px;
                               direction: ltr;
                               text-align: left;
                           ">
                </div>
                
                <input type="hidden" id="personRecordId" value="${personData.id}">
            </div>
            <div class="dialog-buttons">
                <button id="updateContactOkBtn" class="dialog-btn save">אישור</button>
                <button id="updateContactCancelBtn" class="dialog-btn cancel">ביטול</button>
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
    },

    /**
     * Show person search modal
     */
    showPersonSearchModal(personType, position) {
        console.log('🔍 Opening person search modal');

        const modalTitle = this.getModalTitle(personType);

        const overlay = document.createElement('div');
        overlay.className = 'dialog-overlay';

        const dialog = document.createElement('div');
        dialog.className = 'dialog-box';
        dialog.style.minWidth = '600px';
        dialog.style.maxHeight = '80vh';

        dialog.innerHTML = `
            <h3 class="dialog-title">${modalTitle} - חיפוש/בחירה</h3>
            <div style="margin: 20px 0;">
                <div style="margin-bottom: 15px;">
                    <label style="display: block; margin-bottom: 5px; font-weight: 600;">חיפוש לפי שם:</label>
                    <div style="display: flex; gap: 10px;">
                        <input type="text" 
                               id="searchFirstName" 
                               placeholder="שם פרטי"
                               style="
                                   flex: 1;
                                   padding: 8px;
                                   border: 1px solid #dee2e6;
                                   border-radius: 4px;
                                   font-size: 14px;
                                   direction: rtl;
                               ">
                        <input type="text" 
                               id="searchLastName" 
                               placeholder="שם משפחה"
                               style="
                                   flex: 1;
                                   padding: 8px;
                                   border: 1px solid #dee2e6;
                                   border-radius: 4px;
                                   font-size: 14px;
                                   direction: rtl;
                               ">
                        <button id="searchBtn" class="dialog-btn" style="background: #007bff; padding: 8px 20px;">
                            חפש
                        </button>
                    </div>
                </div>
                
                <div id="searchResults" style="
                    max-height: 300px;
                    overflow-y: auto;
                    border: 1px solid #dee2e6;
                    border-radius: 4px;
                    margin-bottom: 15px;
                    display: none;
                "></div>
                
                <div style="text-align: center; padding: 10px;">
                    <button id="createNewPersonBtn" class="dialog-btn" style="background: #28a745;">
                        + איש קשר חדש
                    </button>
                </div>
                
                <input type="hidden" id="selectedPersonId" value="">
                <input type="hidden" id="personPosition" value="${position}">
            </div>
            <div class="dialog-buttons">
                <button id="searchCancelBtn" class="dialog-btn cancel">ביטול</button>
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
    },

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
    },

    /**
     * Display search results
     */
    displaySearchResults(persons) {
        const resultsContainer = document.getElementById('searchResults');

        if (!persons || persons.length === 0) {
            resultsContainer.innerHTML = '<div style="padding: 20px; text-align: center; color: #6c757d;">לא נמצאו תוצאות</div>';
            return;
        }

        const resultsHTML = persons.map(person => `
            <div class="person-result-item" 
                 data-person-id="${person.id}"
                 style="
                     padding: 12px;
                     border-bottom: 1px solid #dee2e6;
                     cursor: pointer;
                     direction: rtl;
                     transition: background 0.2s;
                 "
                 onmouseover="this.style.background='#f8f9fa'"
                 onmouseout="this.style.background='white'">
                <div style="font-weight: 600; margin-bottom: 4px;">
                    ${person.firstName} ${person.lastName}
                </div>
                ${person.position ? `
                    <div style="color: #6c757d; font-size: 0.9em;">
                        תפקיד: ${person.position}
                    </div>
                ` : ''}
                ${person.phoneNumber ? `
                    <div style="color: #6c757d; font-size: 0.9em;">
                        טלפון: ${person.phoneNumberPrefix || ''}${person.phoneNumber}
                    </div>
                ` : ''}
            </div>
        `).join('');

        resultsContainer.innerHTML = resultsHTML;

        // Add click handlers to select person
        document.querySelectorAll('.person-result-item').forEach(item => {
            item.onclick = () => {
                const personId = item.dataset.personId;
                this.selectPersonFromSearch(personId);
            };
        });
    },

    /**
     * Select person from search results
     */
    async selectPersonFromSearch(personId) {
        console.log('✅ Person selected from search:', personId);

        const personType = this.getCurrentPersonType();
        
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
                // Update school details with selected person
                await this.updateSchoolWithPerson(personType, result.data);
                
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
    },

    /**
     * Show new person modal
     */
    showNewPersonModal(personType, position) {
        console.log('➕ Opening new person modal');

        const modalTitle = this.getModalTitle(personType);

        const overlay = document.createElement('div');
        overlay.className = 'dialog-overlay';

        const dialog = document.createElement('div');
        dialog.className = 'dialog-box';
        dialog.style.minWidth = '500px';

        dialog.innerHTML = `
            <h3 class="dialog-title">${modalTitle} - איש קשר חדש</h3>
            <div style="margin: 20px 0;">
                <div style="margin-bottom: 15px;">
                    <label for="newPersonFirstName" style="display: block; margin-bottom: 5px; font-weight: 600;">שם פרטי: *</label>
                    <input type="text" 
                           id="newPersonFirstName" 
                           placeholder="הזן שם פרטי"
                           style="
                               width: 100%;
                               padding: 8px;
                               border: 1px solid #dee2e6;
                               border-radius: 4px;
                               font-size: 14px;
                               direction: rtl;
                           ">
                </div>
                
                <div style="margin-bottom: 15px;">
                    <label for="newPersonLastName" style="display: block; margin-bottom: 5px; font-weight: 600;">שם משפחה: *</label>
                    <input type="text" 
                           id="newPersonLastName" 
                           placeholder="הזן שם משפחה"
                           style="
                               width: 100%;
                               padding: 8px;
                               border: 1px solid #dee2e6;
                               border-radius: 4px;
                               font-size: 14px;
                               direction: rtl;
                           ">
                </div>
                
                <div style="margin-bottom: 15px;">
                    <label style="display: block; margin-bottom: 5px; font-weight: 600;">טלפון:</label>
                    <div style="display: flex; gap: 8px; direction: ltr;">
                        <input type="text" 
                               id="newPersonPhonePrefix" 
                               placeholder="קידומת"
                               maxlength="7"
                               style="
                                   width: 80px;
                                   padding: 8px;
                                   border: 1px solid #dee2e6;
                                   border-radius: 4px;
                                   font-size: 14px;
                                   text-align: left;
                               ">
                        <input type="text" 
                               id="newPersonPhone" 
                               placeholder="מספר טלפון"
                               maxlength="10"
                               style="
                                   flex: 1;
                                   padding: 8px;
                                   border: 1px solid #dee2e6;
                                   border-radius: 4px;
                                   font-size: 14px;
                                   text-align: left;
                               ">
                    </div>
                </div>
                
                <div style="margin-bottom: 15px;">
                    <label for="newPersonEmail" style="display: block; margin-bottom: 5px; font-weight: 600;">דוא"ל:</label>
                    <input type="email" 
                           id="newPersonEmail" 
                           placeholder="example@domain.com"
                           style="
                               width: 100%;
                               padding: 8px;
                               border: 1px solid #dee2e6;
                               border-radius: 4px;
                               font-size: 14px;
                               direction: ltr;
                               text-align: left;
                           ">
                </div>
                
                <div style="margin-bottom: 15px;">
                    <label for="newPersonPosition" style="display: block; margin-bottom: 5px; font-weight: 600;">תפקיד:</label>
                    <input type="text" 
                           id="newPersonPosition" 
                           value="${position}"
                           readonly
                           style="
                               width: 100%;
                               padding: 8px;
                               border: 1px solid #dee2e6;
                               border-radius: 4px;
                               font-size: 14px;
                               direction: rtl;
                               background: #e9ecef;
                               cursor: not-allowed;
                           ">
                </div>
            </div>
            <div class="dialog-buttons">
                <button id="newPersonOkBtn" class="dialog-btn save">אישור</button>
                <button id="newPersonCancelBtn" class="dialog-btn cancel">ביטול</button>
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

        // Focus first name field
        setTimeout(() => document.getElementById('newPersonFirstName')?.focus(), 100);
    },

    /**
     * Save contact update
     */
    async saveContactUpdate(personType, overlay) {
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
                
                // Reload school details to show updated data
                if (typeof loadSchoolDetails === 'function') {
                    await loadSchoolDetails();
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
    },

    /**
     * Save new person
     */
    async saveNewPerson(personType, position, overlay) {
        const firstName = document.getElementById('newPersonFirstName')?.value.trim() || '';
        const lastName = document.getElementById('newPersonLastName')?.value.trim() || '';
        const phonePrefix = document.getElementById('newPersonPhonePrefix')?.value.trim() || '';
        const phone = document.getElementById('newPersonPhone')?.value.trim() || '';
        const email = document.getElementById('newPersonEmail')?.value.trim() || '';

        // Validation
        if (!firstName || !lastName) {
            alert('נא למלא שם פרטי ושם משפחה');
            return;
        }

        if (email && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) {
            alert('כתובת דוא"ל לא תקינה');
            document.getElementById('newPersonEmail')?.focus();
            return;
        }

        console.log('➕ Creating new person:', { firstName, lastName, position });

        try {
            const createPayload = {
                firstName: firstName,
                lastName: lastName,
                phoneNumberPrefix: phonePrefix || null,
                phoneNumber: phone || null,
                email: email || null,
                position: position,
                idNumber: '0', // Required field with default
                idType: 0,     // Required field with default
                gender: 99     // Required field with default
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
                
                // Update school details with new person
                await this.updateSchoolWithPerson(personType, result.data);
                
                document.body.removeChild(overlay);
                alert('איש קשר חדש נוצר בהצלחה');
            } else {
                console.error('❌ Failed to create person:', result);
                alert(result.message || 'שגיאה ביצירת איש קשר חדש');
            }

        } catch (error) {
            console.error('💥 Error creating new person:', error);
            alert('שגיאה ביצירת איש קשר חדש');
        }
    },

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
    },

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
            default:
                return 'עריכת איש קשר';
        }
    },

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
            default:
                return '';
        }
    },

    /**
     * Helper: Get current person type (stored temporarily)
     */
    getCurrentPersonType() {
        return window._currentPersonType || 'contactPerson';
    },

    /**
     * Helper: Set current person type
     */
    setCurrentPersonType(personType) {
        window._currentPersonType = personType;
    }
};

// Make PersonManagement globally available
window.PersonManagement = PersonManagement;

console.log('✅ Person Management Module Loaded');