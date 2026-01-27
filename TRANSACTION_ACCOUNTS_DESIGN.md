# Transaction Accounts Design Document

## Overview

The Transaction Account system manages financial relationships between entities in the Petel Educational Management System. This design supports complex scenarios like school networks managing external student fees with councils.

**Created**: January 27, 2026  
**Status**: Design Complete - Ready for Implementation

---

## Business Requirements

### Core Concepts

1. **Transaction Account (חשבון עסקאות)**
   - Owned by one entity (e.g., school network)
   - Relates to another entity (e.g., council)
   - Has a specific type (e.g., external students fees)
   - Tracks balance and transaction history

2. **Account Types**
   - **External Students Fees (אגרות תלמידי חוץ)** - First implementation
   - Additional types can be added via `transaction_account_types` table
   - Managed through dedicated account types table

3. **Entity Creation Rules for Councils**
   
   A council becomes an entity in two ways:
   
   **Option 1: Manual Creation**
   - Administrator manually creates entity for council
   - Entity references council in `council_id` field
   - Entity type = 2 (Council)
   
   **Option 2: Automatic Creation on First Transaction**
   - When first transaction account is created for a council
   - System automatically creates entity if it doesn't exist
   - Entity references itself: `council_id` points to the same council

### Example Scenario

**School Network (Entity Type 3) managing councils:**

- Network entity ID: 100 (entity_type_id = 3)
- Council A (council_id = 5) - needs entity created
- Council B (council_id = 8) - needs entity created

**Transaction Accounts:**
1. Network creates account for Council A:
   - `owner_entity_id` = 100 (network)
   - `related_entity_id` = auto-created entity for Council A
   - `account_type_id` = external_students_fees
   - `balance` = 0.00

2. Network creates account for Council B:
   - Similar structure
   - Each account tracks its own balance

---

## Database Schema

### Table: `transaction_account_types`

```sql
CREATE TABLE petel_schema.transaction_account_types (
    id SERIAL PRIMARY KEY,
    
    -- Type identification
    name VARCHAR(100) NOT NULL UNIQUE,
    description VARCHAR(200) NOT NULL,
    
    -- Status
    is_active BOOLEAN NOT NULL DEFAULT true,
    sort_order INTEGER NOT NULL DEFAULT 0,
    
    -- Audit Fields (REQUIRED)
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    created_user INTEGER NULL REFERENCES petel_schema.users(id) ON DELETE SET NULL,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    update_user INTEGER NULL REFERENCES petel_schema.users(id) ON DELETE SET NULL
);
```

### Table: `transaction_accounts`

```sql
CREATE TABLE petel_schema.transaction_accounts (
    id SERIAL PRIMARY KEY,
    
    -- Ownership and Relationships
    owner_entity_id INTEGER NOT NULL REFERENCES petel_schema.entities(id) ON DELETE CASCADE,
    related_entity_id INTEGER NOT NULL REFERENCES petel_schema.entities(id) ON DELETE RESTRICT,
    account_type_id INTEGER NOT NULL REFERENCES petel_schema.transaction_account_types(id) ON DELETE RESTRICT,
    
    -- Account Details
    account_name VARCHAR(200) NOT NULL,
    description VARCHAR(500) NULL,
    
    -- Financial Data
    balance DECIMAL(18, 2) NOT NULL DEFAULT 0.00,
    
    -- Status
    is_active BOOLEAN NOT NULL DEFAULT true,
    
    -- Audit Fields (REQUIRED)
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    created_user INTEGER NULL REFERENCES petel_schema.users(id) ON DELETE SET NULL,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    update_user INTEGER NULL REFERENCES petel_schema.users(id) ON DELETE SET NULL,
    
    -- Constraints
    CONSTRAINT uk_transaction_account UNIQUE (owner_entity_id, related_entity_id, account_type_id)
);
```

### Indexes

```sql
CREATE INDEX idx_transaction_accounts_owner_entity ON petel_schema.transaction_accounts(owner_entity_id);
CREATE INDEX idx_transaction_accounts_related_entity ON petel_schema.transaction_accounts(related_entity_id);
CREATE INDEX idx_transaction_accounts_account_type ON petel_schema.transaction_accounts(account_type_id);
CREATE INDEX idx_transaction_accounts_is_active ON petel_schema.transaction_accounts(is_active);
CREATE INDEX idx_transaction_accounts_created_user ON petel_schema.transaction_accounts(created_user);
CREATE INDEX idx_transaction_accounts_update_user ON petel_schema.transaction_accounts(update_user);
```

### Account Types Table

**Table: `transaction_account_types`**

| Name | Description | Sort Order |
|------|-------------|------------|
| `external_students_fees` | אגרות תלמידי חוץ | 1 |

Future account types can be added directly to this table.

---

## Entity Model

### C# Class: `TransactionAccount`

```csharp
[Table("transaction_accounts")]
public class TransactionAccount
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    // Foreign Keys
    [Required]
    [Column("owner_entity_id")]
    public int OwnerEntityId { get; set; }

    [Required]
    [Column("related_entity_id")]
    public int RelatedEntityId { get; set; }

    [Required]
    [Column("account_type_id")]
    public int AccountTypeId { get; set; }

    // Account Details
    [Required]
    [MaxLength(200)]
    [Column("account_name")]
    public string AccountName { get; set; } = string.Empty;

    [MaxLength(500)]
    [Column("description")]
    public string? Description { get; set; }

    // Financial
    [Column("balance")]
    public decimal Balance { get; set; } = 0.00m;

    // Status
    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    // Audit Fields
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("created_user")]
    public int? CreatedUser { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [Column("update_user")]
    public int? UpdateUser { get; set; }

    // Navigation Properties (REQUIRED)
    public virtual Entity OwnerEntity { get; set; } = null!;
    public virtual Entity RelatedEntity { get; set; } = null!;
    public virtual TransactionAccountType AccountType { get; set; } = null!;
    public virtual User? CreatedByUser { get; set; }
    public virtual User? UpdatedByUser { get; set; }
}
```

### AppDbContext Configuration

```csharp
modelBuilder.Entity<TransactionAccount>(entity =>
{
    entity.ToTable("transaction_accounts");

    // Indexes
    entity.HasIndex(e => e.OwnerEntityId);
    entity.HasIndex(e => e.RelatedEntityId);
    entity.HasIndex(e => e.AccountTypeId);
    entity.HasIndex(e => e.IsActive);
    entity.HasIndex(e => new { e.OwnerEntityId, e.RelatedEntityId, e.AccountTypeId }).IsUnique();

    // Relationships
    entity.HasOne(ta => ta.OwnerEntity)
        .WithMany()
        .HasForeignKey(ta => ta.OwnerEntityId)
        .OnDelete(DeleteBehavior.Cascade);

    entity.HasOne(ta => ta.RelatedEntity)
        .WithMany()
        .HasForeignKey(ta => ta.RelatedEntityId)
        .OnDelete(DeleteBehavior.Restrict);

    entity.HasOne(ta => ta.AccountType)
        .WithMany(at => at.TransactionAccounts)
        .HasForeignKey(ta => ta.AccountTypeId)
        .OnDelete(DeleteBehavior.Restrict);

    entity.HasOne(ta => ta.CreatedByUser)
        .WithMany()
        .HasForeignKey(ta => ta.CreatedUser)
        .OnDelete(DeleteBehavior.SetNull);

    entity.HasOne(ta => ta.UpdatedByUser)
        .WithMany()
        .HasForeignKey(ta => ta.UpdateUser)
        .OnDelete(DeleteBehavior.SetNull);
});
```

---

## API Endpoints

### Base URL: `/api/transactionaccounts`

#### 1. Get All Accounts

**GET** `/api/transactionaccounts`

Returns all transaction accounts owned by the current entity.

**Response:**
```json
{
  "success": true,
  "data": [
    {
      "id": 1,
      "ownerEntityId": 100,
      "ownerEntityName": "רשת בית יעקב",
      "relatedEntityId": 50,
      "relatedEntityName": "מועצה אזורית גוש עציון",
      "accountTypeId": 15,
      "accountTypeName": "אגרות תלמידי חוץ",
      "accountName": "חשבון אגרות - גוש עציון",
      "description": "ניהול תשלומי אגרות תלמידי חוץ",
      "balance": 25000.00,
      "isActive": true,
      "createdAt": "2026-01-27T10:00:00Z",
      "updatedAt": "2026-01-27T10:00:00Z"
    }
  ]
}
```

#### 2. Get Account by ID

**GET** `/api/transactionaccounts/{id}`

Returns specific account details.

#### 3. Get Accounts by Related Entity

**GET** `/api/transactionaccounts/by-related-entity/{relatedEntityId}`

Returns all accounts for a specific related entity (e.g., all accounts with a council).

#### 4. Get Account Types

**GET** `/api/transactionaccounts/account-types`

Returns available account types from system attributes.

**Response:**
```json
{
  "success": true,
  "data": [
    {
      "id": 15,
      "name": "external_students_fees",
      "description": "אגרות תלמידי חוץ"
    }
  ]
}
```

#### 5. Create Account

**POST** `/api/transactionaccounts`

Creates a new transaction account.

**Request Body:**
```json
{
  "relatedEntityId": 50,
  "accountTypeId": 15,
  "accountName": "חשבון אגרות - גוש עציון",
  "description": "ניהול תשלומי אגרות תלמידי חוץ"
}
```

**Response:**
```json
{
  "success": true,
  "message": "חשבון נוצר בהצלחה",
  "accountId": 1
}
```

#### 6. Create Council Entity

**POST** `/api/transactionaccounts/create-council-entity`

Creates an entity for a council (prerequisite for creating accounts).

**Request Body:**
```json
{
  "councilId": 5
}
```

**Response:**
```json
{
  "success": true,
  "message": "ישות מועצה נוצרה בהצלחה",
  "entityId": 50
}
```

#### 7. Update Account

**PUT** `/api/transactionaccounts/{id}`

Updates account details (name, description, active status).

**Request Body:**
```json
{
  "accountName": "חשבון אגרות מעודכן",
  "description": "תיאור חדש",
  "isActive": true
}
```

#### 8. Delete Account (Soft Delete)

**DELETE** `/api/transactionaccounts/{id}`

Soft deletes account by setting `is_active = false`.

---

## Business Logic

### Account Creation Workflow

1. **Verify Related Entity Exists**
   - Check if entity exists for the council
   - If not, use `create-council-entity` endpoint first

2. **Validate Account Type**
   - Ensure account type ID exists in transaction_account_types
   - Verify it's active

3. **Check for Duplicates**
   - Unique constraint: (owner_entity_id, related_entity_id, account_type_id)
   - Prevents multiple accounts of same type between same entities

4. **Create Account**
   - Initial balance = 0.00
   - Set audit fields (created_user, created_at)
   - Status = active

### Council Entity Creation

**Manual Creation:**
```csharp
var councilEntity = new Entity
{
    EntityName = council.CouncilName,
    EntityTypeId = 2, // Council type
    CouncilId = councilId,
    IsActive = true
};
```

**Automatic Creation (when creating first account):**
- Same as manual, but triggered by account creation workflow
- Ensures council has entity before account is created

### Balance Management

**Current Phase:**
- Balance field exists but is not actively managed
- Default value: 0.00
- Will be updated by transaction system (future implementation)

**Future Phase (Transactions):**
- Transactions will debit/credit the account balance
- Balance tracks net position (positive = credit, negative = debit)
- Balance updates will be transactional (ACID compliant)

---

## Security & Authorization

### Entity Scoping

All queries filtered by user's entity:
```csharp
var entityId = int.Parse(session.EntityId);
var accounts = await _context.TransactionAccounts
    .Where(ta => ta.OwnerEntityId == entityId)
    .ToListAsync();
```

### Session Validation

All endpoints require valid session:
```csharp
var session = GetCurrentSession();
if (session == null)
{
    return Unauthorized(new { success = false, message = "נדרש אימות" });
}
```

### Audit Trail

Every create/update operation tracks:
- `created_user` - User who created record
- `created_at` - Creation timestamp
- `update_user` - User who last modified record
- `updated_at` - Last modification timestamp

---

## Frontend Integration (Future)

### UI Components Needed

1. **Account List Page**
   - Display all accounts for current entity
   - Filter by related entity, account type, status
   - Search by account name

2. **Account Creation Modal**
   - Select related entity (council)
   - Select account type (dropdown from system attributes)
   - Enter account name and description
   - Auto-create council entity if needed

3. **Account Details Card**
   - Display account information
   - Show current balance
   - Link to transaction history (future)

### Navigation Pattern

```javascript
// Load accounts for current entity
async function loadTransactionAccounts() {
    const response = await fetch(AppConfig.getApiUrl('transactionaccounts'), {
        headers: { 'Authorization': `Bearer ${authToken}` }
    });
    
    if (response.ok) {
        const result = await response.json();
        renderAccountsTable(result.data);
    }
}

// Create new account
async function createAccount(accountData) {
    const response = await fetch(AppConfig.getApiUrl('transactionaccounts'), {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            'Authorization': `Bearer ${authToken}`
        },
        body: JSON.stringify(accountData)
    });
    
    if (response.ok) {
        alert('חשבון נוצר בהצלחה');
        await loadTransactionAccounts();
    }
}
```

---

## Migration Steps

### 1. Run Database Migration

```bash
# Execute migration script
psql -h localhost -U PetelAdmin -d petelappdb -f SQL/migrations/20260127_create_transaction_accounts.sql
```

Expected output:
- ✅ Table "transaction_account_types" created
- ✅ Account type "external_students_fees" created
- ✅ Table "transaction_accounts" created with indexes

### 2. Add Entity Framework Migration

```bash
cd PetelApp.Api
dotnet ef migrations add AddTransactionAccounts
dotnet ef database update
```

### 3. Verify Configuration

- ✅ `TransactionAccount` entity class exists
- ✅ DbSet added to `AppDbContext`
- ✅ Entity configuration in `OnModelCreating`
- ✅ Controller registered and accessible

### 4. Test Endpoints

```bash
# Get account types
GET /api/transactionaccounts/account-types

# Create council entity
POST /api/transactionaccounts/create-council-entity
Body: { "councilId": 5 }

# Create transaction account
POST /api/transactionaccounts
Body: {
  "relatedEntityId": 50,
  "accountTypeId": 15,
  "accountName": "Test Account",
  "description": "Test"
}

# Get all accounts
GET /api/transactionaccounts
```

---

## Future Enhancements

### Phase 2: Transactions

**New Tables:**
- `account_transactions` - Individual transaction records
- Transaction types: debit, credit, adjustment
- Link to source documents (invoices, payments)

**Features:**
- Post transactions to accounts
- Automatic balance updates
- Transaction history and audit trail
- Reconciliation tools

### Phase 3: Reporting

**Reports:**
- Account statements by date range
- Aging reports (receivables/payables)
- Balance summary by account type
- Entity-to-entity transaction flow

### Phase 4: Automation

**Features:**
- Automatic account creation on student registration
- Scheduled balance reconciliation
- Alert notifications for unusual balances
- Integration with external accounting systems

---

## Testing Checklist

### Unit Tests

- ✅ Account creation with valid data
- ✅ Duplicate account prevention
- ✅ Entity scoping enforcement
- ✅ Validation for missing required fields
- ✅ Council entity auto-creation logic

### Integration Tests

- ✅ Create account → verify in database
- ✅ Update account → verify changes persist
- ✅ Delete account → verify soft delete
- ✅ Query accounts by filters
- ✅ Navigation properties load correctly

### Security Tests

- ✅ Unauthorized access blocked
- ✅ Cross-entity access prevented
- ✅ Audit fields populated correctly
- ✅ Soft delete prevents data loss

---

## Glossary

| Term | Hebrew | Description |
|------|--------|-------------|
| Transaction Account | חשבון עסקאות | Financial account tracking relationship between entities |
| Owner Entity | ישות בעלים | Entity that owns the account (e.g., school network) |
| Related Entity | ישות קשורה | Entity for whom transactions are held (e.g., council) |
| External Students Fees | אגרות תלמידי חוץ | Fees for students from outside the council |
| Balance | יתרה | Current account balance (credit or debit) |
| Account Type | סוג חשבון | Category of account (from system attributes) |

---

## Contact & Support

**Implementation Team:**
- Database: Schema migration and indexing
- Backend: Entity model and controller
- Frontend: UI components (future phase)
- Testing: Unit and integration tests

**Documentation:**
- Design: This document
- API Reference: Swagger/OpenAPI (auto-generated)
- User Guide: To be created with UI implementation

---

**Design Status**: ✅ Complete  
**Implementation Status**: Backend complete, Frontend pending  
**Next Steps**: Run migration, test endpoints, plan UI implementation
