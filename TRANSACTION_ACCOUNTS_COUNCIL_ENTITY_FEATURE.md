# Transaction Accounts - Council Entity Creation Feature

## Overview
Enhanced the Transaction Accounts page with dynamic entity filtering and on-the-fly council entity creation capability.

## Implementation Date
January 28, 2026

## Features Implemented

### 1. Dynamic Entity Filtering by Account Type
**Purpose**: Show only relevant entities based on the selected account type.

**Logic**:
- When "אגרות תלמידי חוץ" (external_students_fees) is selected → Show only council entities (entity_type_id = 2)
- Other account types → Show all non-school entities

**Implementation**:
- `OnAccountTypeChanged()` method reloads entities when account type changes
- `LoadAvailableEntities()` filters by `EntityTypeId == 2` for external_students_fees
- Entity dropdown automatically refreshes when account type is selected

### 2. Create Council Entity Button
**Purpose**: Allow users to create council entities on-the-fly when the required council is not in the entity list.

**UI Location**: 
- Button appears next to "Related Entity" dropdown
- Only visible when account type is "אגרות תלמידי חוץ"
- Button text: "צור ישות מועצה" (Create Council Entity)

**Modal Dialog Features**:
- Autocomplete search input for councils
- Shows top 10 matching councils as user types
- Selected council displayed with highlight
- "Create" button enables only when council is selected

### 3. Council Autocomplete Search
**Implementation**:
- `FilterCouncils(ChangeEventArgs e)` - Filters councils as user types
- `SelectCouncil(CouncilDto council)` - Handles council selection from dropdown
- Uses existing `CouncilDto` from `PetelApp.BlazorServer.DTOs`
- Searches by council name (case-insensitive)
- Displays top 10 results

### 4. Council Entity Creation Flow
**Workflow**:
1. User clicks "צור ישות מועצה" button
2. Modal opens with autocomplete search
3. User types to search for council
4. User selects council from dropdown
5. User clicks "צור ישות" (Create Entity)
6. API creates new entity (entity_type_id = 2, council_id = selected council)
7. Entity dropdown refreshes automatically
8. New entity auto-selected in dropdown
9. Success message displayed
10. Modal closes

**API Endpoint**: `POST transactionaccounts/create-council-entity`
**Request Body**:
```json
{
  "councilId": 123,
  "accountTypeId": 1
}
```

**Response**:
```json
{
  "success": true,
  "data": {
    "id": 456,
    "entityName": "מועצה X",
    "entityTypeId": 2
  }
}
```

## Files Modified

### 1. TransactionAccounts.razor
**Changes**:
- Added `@bind:after="OnAccountTypeChanged"` to account type dropdown
- Added "צור ישות מועצה" button next to entity dropdown (conditional visibility)
- Added Create Council Entity modal dialog with autocomplete
- Fixed duplicate `@oninput` attribute issue (replaced `@bind` with `value` and `@oninput`)

### 2. TransactionAccounts.razor.cs
**Changes**:
- Added state fields:
  - `_selectedAccountTypeName` - Tracks selected account type name
  - `_showCreateCouncilDialog` - Modal visibility
  - `_councilSearchText` - Search input text
  - `_allCouncils` - Full list of councils
  - `_filteredCouncils` - Filtered councils matching search
  - `_selectedCouncil` - Currently selected council

- Added methods:
  - `OnAccountTypeChanged()` - Reloads entities when account type changes
  - `ShowCreateCouncilEntityDialog()` - Opens modal and loads councils
  - `FilterCouncils(ChangeEventArgs e)` - Filters councils by search text
  - `SelectCouncil(CouncilDto council)` - Handles council selection
  - `CreateCouncilEntity()` - Creates entity via API and updates UI
  - `CloseCreateCouncilDialog()` - Closes modal and resets state

- Updated methods:
  - `LoadAvailableEntities()` - Added entity type filtering logic
  - `CloseAddDialog()` - Resets `_selectedAccountTypeName`

- Removed duplicate `CouncilDto` class (using existing one from DTOs)

## Database Schema
No database changes required - uses existing:
- `entities` table (entity_type_id, council_id fields)
- `transaction_account_types` table (name field for filtering logic)

## Business Rules

### Entity Filtering Rules
1. **external_students_fees** account type → Only show council entities (type 2)
2. **Other account types** → Show all non-school entities (future expansion)

### Council Entity Creation Rules
1. API validates that council doesn't already have an entity
2. Created entity gets:
   - `entity_type_id` = 2 (council)
   - `council_id` = selected council ID
   - `name` = council name
   - Owner determined by API logic
3. Entity automatically selected in dropdown after creation

## User Experience Flow

### Scenario 1: Council Entity Exists
1. User selects "אגרות תלמידי חוץ" account type
2. Entity dropdown shows only council entities
3. User finds and selects their council
4. Proceeds with account creation

### Scenario 2: Council Entity Doesn't Exist
1. User selects "אגרות תלמידי חוץ" account type
2. Entity dropdown shows limited councils
3. User clicks "צור ישות מועצה" button
4. Modal opens
5. User types council name in search (e.g., "מועצה אזורית")
6. Dropdown shows matching councils
7. User clicks desired council
8. Council name appears in highlighted box
9. User clicks "צור ישות"
10. Success message: "הישות עבור [council name] נוצרה בהצלחה"
11. Modal closes
12. Entity dropdown automatically refreshes
13. New council entity is pre-selected
14. User proceeds with account creation

## Security Considerations
- Uses `SecurePageBase` for page-level security
- Council entity creation goes through existing API endpoint with validation
- Duplicate entity check handled by API
- No direct database access from frontend

## Testing Checklist
- [x] Build succeeds without errors
- [ ] Account type dropdown changes entity list correctly
- [ ] "צור ישות מועצה" button appears only for external_students_fees
- [ ] Modal opens with council search
- [ ] Autocomplete filters councils correctly
- [ ] Council selection works and highlights chosen council
- [ ] Create button disabled until council selected
- [ ] API creates entity successfully
- [ ] Entity dropdown refreshes with new entity
- [ ] New entity auto-selected after creation
- [ ] Modal closes properly
- [ ] Error handling displays user-friendly messages
- [ ] Full account creation flow works end-to-end

## Future Enhancements
1. **Duplicate Prevention**: Pre-filter councils that already have entities (requires EntityDto to include council_id)
2. **Multiple Account Types**: Extend entity filtering logic for other account types
3. **Entity Search**: Add autocomplete to entity dropdown for large lists
4. **Validation**: Add client-side validation for duplicate entity creation before API call
5. **Loading States**: Add loading spinners during API calls

## Related Documentation
- [TRANSACTION_ACCOUNTS_DESIGN.md](TRANSACTION_ACCOUNTS_DESIGN.md) - Overall design document
- [TRANSACTION_ACCOUNTS_QUICKSTART.md](TRANSACTION_ACCOUNTS_QUICKSTART.md) - Implementation guide
- [BLAZOR_DEVELOPER_GUIDE.md](BLAZOR_DEVELOPER_GUIDE.md) - Blazor patterns reference

## API Dependencies
- **GET** `systemattributes/councils` - Load all councils (no authentication or year required)
- **GET** `entities/non-schools` - Load non-school entities
- **GET** `transactionaccounts/account-types` - Load account types
- **POST** `transactionaccounts/create-council-entity` - Create council entity

## Notes
- CouncilDto reuses existing class from `PetelApp.BlazorServer.DTOs.SchoolDetailsDto.cs`
- EntityDto doesn't expose council_id, so we can't pre-filter councils that have entities
- API handles duplicate entity check on creation
- All Hebrew text follows RTL conventions
- Modal uses standard Blazor modal overlay pattern
