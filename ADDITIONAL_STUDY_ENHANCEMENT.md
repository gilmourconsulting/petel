# Additional Study Programs Enhancement - Implementation Summary

**Date**: January 13, 2026  
**Feature**: Add Number of Sessions, Approval Status, and Calculation Mode

## Overview

Enhanced the additional study programs (תל"ן) feature with new fields and calculation modes to provide more flexibility and tracking capabilities.

## New Fields Added

### 1. Number of Sessions (מספר מפגשים)
- **Type**: Integer (required)
- **Default**: 30
- **Purpose**: Track the total number of meetings/sessions for each program
- **Database Column**: `number_of_sessions`

### 2. Approval Status (סטטוס אישור)
- **Type**: Integer (required)
- **Values**:
  - `0` = לא מאושר (Not Approved) - Default
  - `1` = מאושר (Approved)
  - `2` = אישור חריג (Exception)
- **Database Column**: `approval_status`
- **Constraint**: CHECK constraint ensures only valid values (0, 1, 2)

### 3. Calculation Mode (אופן חישוב)
- **Type**: Boolean (required)
- **Values**:
  - `false` = Calculate by Total Cost (default, current behavior)
  - `true` = Calculate by Hourly Cost
- **Database Column**: `calculate_by_hourly_cost`
- **Purpose**: Determines which field is input and which is calculated

## Calculation Modes

### Total Cost Mode (calculate_by_hourly_cost = false)
- **User inputs**: Total Cost
- **System calculates**: Hourly Cost = Total Cost ÷ (Weekly Hours × Sessions)
- **Example**: ₪10,000 ÷ (4 hours × 30 sessions) = ₪83.33 per hour

### Hourly Cost Mode (calculate_by_hourly_cost = true)
- **User inputs**: Hourly Cost
- **System calculates**: Total Cost = Hourly Cost × Weekly Hours × Sessions
- **Example**: ₪100 × 4 hours × 30 sessions = ₪12,000

## Files Modified

### Backend (C#)

#### 1. Database Migration
**File**: `SQL/Migrations/Add_Sessions_And_Approval_To_AdditionalStudy.sql`
- Adds `number_of_sessions` column (INTEGER, DEFAULT 30)
- Adds `approval_status` column (INTEGER, DEFAULT 0, CHECK constraint)
- Adds `calculate_by_hourly_cost` column (BOOLEAN, DEFAULT false)
- Creates index on `approval_status` for filtering
- Sets default values for existing records

#### 2. Entity Model
**File**: `PetelApp.Api/Data/SchoolAdditionalStudyProgram.cs`
```csharp
[Required]
[Column("number_of_sessions")]
public int NumberOfSessions { get; set; } = 30;

[Required]
[Column("approval_status")]
public int ApprovalStatus { get; set; } = 0;

[Required]
[Column("calculate_by_hourly_cost")]
public bool CalculateByHourlyCost { get; set; } = false;
```

#### 3. DTOs
**File**: `PetelApp.Api/DTOs/SchoolAdditionalStudyProgramDtos.cs`
- Added fields to `SchoolAdditionalStudyProgramDto`
- Added fields to `CreateSchoolAdditionalStudyProgramDto` with defaults
- Added fields to `UpdateSchoolAdditionalStudyProgramDto`

#### 4. Controller
**File**: `PetelApp.Api/Controllers/SchoolAdditionalStudyProgramsController.cs`
- Updated `GetBySchoolYear` to include new fields in projection
- Updated `GetVersionHistory` to include new fields
- Updated `CreateProgram` to save new fields
- Updated `UpdateProgram` to:
  - Include new fields in hasChanges check
  - Save new fields in new version
  - Return new fields in response DTOs

### Frontend (JavaScript/HTML)

#### 1. Table Display
**File**: `petelapp-frontend/public/schooldetails.html`
- Added "מספר מפגשים" (Number of Sessions) column
- Added "סטטוס" (Approval Status) column with color-coded badges:
  - Gray background: לא מאושר (Not Approved)
  - Green background: מאושר (Approved)
  - Yellow background: אישור חריג (Exception)

#### 2. Add/Edit Modal
Added new fields to the modal:

**Number of Sessions Input**:
```html
<label for="programSessions">מספר מפגשים: <span style="color: red;">*</span></label>
<input type="number" id="programSessions" placeholder="מספר מפגשים" min="1" required>
<small>ברירת מחדל: 30 מפגשים</small>
```

**Calculation Mode Toggle**:
```html
<div style="padding: 15px; background-color: #f8f9fa; border-radius: 8px;">
    <label>אופן חישוב:</label>
    <input type="radio" name="calculationMode" value="totalCost" checked>
    <span>לפי עלות כוללת</span>
    
    <input type="radio" name="calculationMode" value="hourlyCost">
    <span>לפי עלות שעתית</span>
</div>
```

**Approval Status Dropdown**:
```html
<label for="programApprovalStatus">סטטוס אישור:</label>
<select id="programApprovalStatus">
    <option value="0">לא מאושר</option>
    <option value="1">מאושר</option>
    <option value="2">אישור חריג</option>
</select>
```

#### 3. JavaScript Logic
- **Dynamic field behavior**: Cost and Hourly Cost fields switch between input/calculated based on mode
- **Real-time calculation**: Values update automatically as user types
- **Field styling**: Input fields have white background, calculated fields have gray background
- **Auto-calculation on change**: Recalculates when hours or sessions are modified

## Deployment Steps

### 1. Database Migration
```bash
# Connect to database
psql -h <host> -U <user> -d <database>

# Run migration script
\i SQL/Migrations/Add_Sessions_And_Approval_To_AdditionalStudy.sql

# Verify columns were added
\d petel_schema.school_additional_study_programs
```

### 2. Backend Deployment
```bash
# Build and publish API
cd PetelApp.Api
dotnet build
dotnet publish -c Release

# Restart service
# (Method depends on hosting environment - IIS/Azure/Docker)
```

### 3. Frontend Deployment
```bash
# Copy updated schooldetails.html to web server
cp petelapp-frontend/public/schooldetails.html /path/to/webserver/public/

# Clear browser cache or force refresh
# Ctrl+Shift+R (Windows/Linux) or Cmd+Shift+R (Mac)
```

## Testing Checklist

### Database
- [x] Migration script runs without errors
- [x] New columns exist with correct types and defaults
- [x] Existing records have default values (30, 0, false)
- [x] CHECK constraint prevents invalid approval_status values

### Backend API
- [x] GET endpoint returns new fields
- [x] POST endpoint accepts and saves new fields
- [x] PUT endpoint creates new version with new fields
- [x] Version history includes new fields

### Frontend
- [x] Table displays all new columns correctly
- [x] Approval status shows color-coded badges
- [x] Modal loads with default values for new programs
- [x] Modal loads existing values when editing
- [x] Calculation mode toggle works correctly
- [x] Cost fields calculate automatically based on mode
- [x] Total Cost Mode: Input cost → calculates hourly
- [x] Hourly Cost Mode: Input hourly → calculates total
- [x] Changing hours/sessions triggers recalculation
- [x] Approval status dropdown saves correctly
- [x] Required field validation includes new fields

## Usage Examples

### Example 1: Creating Program with Total Cost Mode
1. User selects class: "י א"
2. User enters name: "מתמטיקה מתקדמת"
3. User enters weekly hours: 4
4. User enters number of students: 15
5. User enters number of sessions: 35
6. User selects "לפי עלות כוללת" (Total Cost Mode)
7. User enters total cost: ₪14,000
8. **System auto-calculates**: Hourly Cost = 14,000 ÷ (4 × 35) = ₪100.00
9. User selects approval status: "מאושר"
10. System saves with `calculateByHourlyCost = false`

### Example 2: Creating Program with Hourly Cost Mode
1. User selects class: "יא ב"
2. User enters name: "פיזיקה תגבור"
3. User enters weekly hours: 3
4. User enters number of students: 12
5. User enters number of sessions: 30
6. User selects "לפי עלות שעתית" (Hourly Cost Mode)
7. User enters hourly cost: ₪120
8. **System auto-calculates**: Total Cost = 120 × 3 × 30 = ₪10,800
9. User selects approval status: "אישור חריג"
10. System saves with `calculateByHourlyCost = true`

## Backward Compatibility

✅ **Fully backward compatible** - All existing programs receive default values:
- `numberOfSessions`: 30
- `approvalStatus`: 0 (Not Approved)
- `calculateByHourlyCost`: false (Total Cost Mode - current behavior)

## Benefits

1. **Flexible Calculation**: Schools can enter data the way they receive it (total or hourly)
2. **Better Tracking**: Number of sessions provides more accurate cost calculations
3. **Approval Workflow**: Track approval status throughout the budgeting process
4. **Audit Trail**: Version history includes all changes to new fields
5. **User-Friendly**: Automatic calculations reduce manual work and errors

## Notes

- The formula uses sessions instead of a fixed divisor (previously 35)
- Cost calculations now: `HourlyCost = TotalCost ÷ (WeeklyHours × Sessions)`
- Validation still enforces maximum allowed amounts based on pricing tiers
- All monetary values stored as `decimal(10,2)` for precision

## Support

For questions or issues, contact the development team or refer to:
- Main documentation: `README.md`
- Coding guide: `.github/copilot-instructions.md`
- Database schema: `SQL/petel_schema.sql`
