# Transaction Accounts - Quick Implementation Guide

## Files Created

### 1. Database Migration
**File:** `SQL/migrations/20260127_create_transaction_accounts.sql`

**Contains:**
- Table `transaction_account_types` with account type definitions
- Initial account type: `external_students_fees` (אגרות תלמידי חוץ)
- Table `transaction_accounts` with all audit fields
- Indexes for performance
- Unique constraint: (owner_entity_id, related_entity_id, account_type_id)

### 2. Entity Model
**File:** `PetelApp.Api/Models/TransactionAccount.cs`

**Features:**
- Complete property mapping to database columns
- Navigation properties for Entity Framework
- Full audit field support
- Decimal balance field (18,2 precision)

### 3. Database Context
**File:** `PetelApp.Api/Data/AppDbContext.cs`

**Changes:**
- Added `DbSet<TransactionAccount> TransactionAccounts`
- Entity configuration with proper relationships
- Indexes matching database schema
- Cascade/Restrict delete behaviors

### 4. API Controller
**File:** `PetelApp.Api/Controllers/TransactionAccountsController.cs`

**Endpoints:**
- `GET /api/transactionaccounts` - Get all accounts
- `GET /api/transactionaccounts/{id}` - Get specific account
- `GET /api/transactionaccounts/by-related-entity/{id}` - Filter by related entity
- `GET /api/transactionaccounts/account-types` - Get available account types
- `POST /api/transactionaccounts` - Create new account
- `POST /api/transactionaccounts/create-council-entity` - Create council entity
- `PUT /api/transactionaccounts/{id}` - Update account
- `DELETE /api/transactionaccounts/{id}` - Soft delete account

### 5. Design Documentation
**File:** `TRANSACTION_ACCOUNTS_DESIGN.md`

**Sections:**
- Business requirements and use cases
- Complete database schema
- Entity model documentation
- API endpoint reference
- Business logic workflows
- Security patterns
- Migration steps
- Future enhancements roadmap

---

## Quick Start

### Step 1: Run Database Migration

```bash
# Connect to PostgreSQL
psql -h localhost -U PetelAdmin -d petelappdb

# Run migration
\i 'c:/dev/PetelFullApp/SQL/migrations/20260127_create_transaction_accounts.sql'

# Verify tables created
\dt petel_schema.transaction_accounts
```

Expected output:
```
✅ Table transaction_account_types created successfully
✅ Account type "external_students_fees" created
✅ Table transaction_accounts created successfully with indexes
```

### Step 2: Build and Test API

```bash
cd c:\dev\PetelFullApp\PetelApp.Api
dotnet build
```

If build succeeds, all dependencies are resolved correctly.

### Step 3: Test Endpoints (After Starting API)

```bash
# Start API
cd c:\dev\PetelFullApp\PetelApp.Api
dotnet run

# Test in another terminal (requires auth token)
curl -H "Authorization: Bearer YOUR_TOKEN" http://localhost:5082/api/transactionaccounts/account-types
```

---

## Key Concepts

### Account Structure

```
TransactionAccount
├── Owner Entity (e.g., School Network)
├── Related Entity (e.g., Council)
├── Account Type (e.g., External Students Fees)
├── Balance (Decimal 18,2)
└── Audit Fields (created_at, created_user, etc.)
```

### Unique Constraint

**One account per combination:**
- Owner Entity + Related Entity + Account Type = UNIQUE

This prevents:
- ❌ Multiple "External Students Fees" accounts for same council
- ✅ Different account types for same council (e.g., fees + grants)

### Entity Creation Logic

**Council Entity Creation:**

1. **Manual**: Admin creates entity first via UI
2. **Automatic**: System creates entity when first account is created

```csharp
// When creating account, if related entity doesn't exist:
POST /api/transactionaccounts/create-council-entity
Body: { "councilId": 5 }

// Then create account:
POST /api/transactionaccounts
Body: {
  "relatedEntityId": [newly created entity ID],
  "accountTypeId": 15,
  "accountName": "חשבון אגרות - גוש עציון"
}
```

---

## Common Operations

### Get Available Account Types

```bash
GET /api/transactionaccounts/account-types
```

Response:
```json
{
  "success": true,
  "data": [
    {
      "id": 15,
      "name": "external_students_fees",
      "description": "אגרות תלמידי חוץ",
      "value": "external_students_fees"
    }
  ]
}
```

### Create Account for Council

**Step 1:** Create council entity (if not exists)
```bash
POST /api/transactionaccounts/create-council-entity
{
  "councilId": 5
}
```

**Step 2:** Create account
```bash
POST /api/transactionaccounts
{
  "relatedEntityId": 50,  # From step 1 response
  "accountTypeId": 15,     # From account-types endpoint
  "accountName": "חשבון אגרות - גוש עציון",
  "description": "ניהול תשלומי אגרות תלמידי חוץ"
}
```

### List All Accounts

```bash
GET /api/transactionaccounts
```

Response includes:
- Account details
- Owner entity name
- Related entity name
- Account type description
- Current balance
- Active status

---

## Database Schema Quick Reference

### Account Types Table

```sql
petel_schema.transaction_account_types
├── id (PK)
├── name VARCHAR(100) UNIQUE
├── description VARCHAR(200)
├── is_active BOOLEAN
├── sort_order INTEGER
├── created_at TIMESTAMP
├── created_user (FK → users)
├── updated_at TIMESTAMP
└── update_user (FK → users)
```

### Main Table

```sql
petel_schema.transaction_accounts
├── id (PK)
├── owner_entity_id (FK → entities)
├── related_entity_id (FK → entities)
├── account_type_id (FK → transaction_account_types)
├── account_name VARCHAR(200)
├── description VARCHAR(500)
├── balance DECIMAL(18,2)
├── is_active BOOLEAN
├── created_at TIMESTAMP
├── created_user (FK → users)
├── updated_at TIMESTAMP
└── update_user (FK → users)
```

### Indexes

- `owner_entity_id` - Fast lookup by owner
- `related_entity_id` - Fast lookup by related entity
- `account_type_id` - Fast lookup by type
- `is_active` - Filter active/inactive
- `(owner_entity_id, related_entity_id, account_type_id)` - UNIQUE

---

## Security Features

### Entity Scoping
✅ All queries filtered by user's entity  
✅ Users only see accounts they own  
✅ Cross-entity access prevented

### Audit Trail
✅ Created by user tracked  
✅ Last updated by user tracked  
✅ Timestamps on all changes  
✅ Full history available for compliance

### Soft Delete
✅ Accounts marked inactive, not deleted  
✅ Historical data preserved  
✅ Can be reactivated if needed

---

## Future: Transaction System

**Next Phase:**
- `account_transactions` table
- Transaction types: debit, credit, adjustment
- Automatic balance updates
- Transaction history and reporting

**Balance Calculation:**
```
Current Balance = Initial Balance + Credits - Debits
```

**Example:**
```
Account: External Students Fees - Council A
Initial: 0.00
+ Credit: 50,000.00 (annual allocation)
- Debit: 25,000.00 (first semester payment)
= Balance: 25,000.00
```

---

## Troubleshooting

### Issue: Account Type Not Found

**Symptom:** `GET /account-types` returns empty array

**Solution:**
```sql
-- Verify account types exist
SELECT * FROM petel_schema.transaction_account_types 
WHERE is_active = true;

-- If missing, rerun migration script
```

### Issue: Related Entity Not Found

**Symptom:** `400 Bad Request - ישות קשורה לא נמצאה`

**Solution:**
```bash
# Create council entity first
POST /api/transactionaccounts/create-council-entity
{
  "councilId": 5
}

# Then use returned entityId in account creation
```

### Issue: Duplicate Account Error

**Symptom:** `400 Bad Request - חשבון כבר קיים עבור ישות וסוג זה`

**Solution:**
- Check existing accounts: `GET /api/transactionaccounts`
- If duplicate exists, update it instead of creating new
- If needed, change account type to create another account

---

## Architecture Patterns Used

✅ **BaseController** - Session management and entity scoping  
✅ **Navigation Properties** - Proper EF Core relationships  
✅ **HasDefaultSchema** - No hardcoded schema names  
✅ **Audit Fields** - created_at, created_user, updated_at, update_user  
✅ **Soft Delete** - is_active flag instead of hard delete  
✅ **Entity Scoping** - All queries filtered by user's entity  
✅ **Dedicated Tables** - Account types in own table, not generic attributes  
✅ **Standard Naming** - Concise field names without redundant prefixes

---

## File Locations Summary

```
c:\dev\PetelFullApp\
├── SQL\migrations\
│   └── 20260127_create_transaction_accounts.sql
├── PetelApp.Api\
│   ├── Models\
│   │   └── TransactionAccount.cs
│   ├── Controllers\
│   │   └── TransactionAccountsController.cs
│   └── Data\
│       └── AppDbContext.cs (modified)
├── TRANSACTION_ACCOUNTS_DESIGN.md
└── TRANSACTION_ACCOUNTS_QUICKSTART.md (this file)
```

---

## Next Steps

1. ✅ Review design documentation
2. ⏳ Run database migration
3. ⏳ Build and test API endpoints
4. ⏳ Plan frontend UI implementation
5. ⏳ Create user documentation

---

## Questions & Support

**Design Questions:** See `TRANSACTION_ACCOUNTS_DESIGN.md`  
**API Reference:** Controller XML comments and Swagger  
**Database Schema:** Migration script has full DDL

---

**Status:** ✅ Design Complete - Ready for Migration  
**Created:** January 27, 2026  
**Version:** 1.0
