# Transaction Accounts - Quick Start Guide

## Database Setup

```sql
-- Run migration script
psql -U PetelAdmin -d petelappdb -f PetelApp.Api/Migrations/Scripts/20260128_CreateTransactionsSchema.sql
```

## Testing the Feature

### 1. Access Transactions Page
1. Login to application
2. Navigate to **חשבונות עסקאות** (Transaction Accounts)
3. Click **view icon** (👁️) on any account
4. You'll be redirected to `/accounttransactions/{accountId}`

### 2. View Transactions
- See account balance and summary at top
- View list of all transactions with color coding:
  - **Green rows** = Credit (income)
  - **Red rows** = Debit (expense)
- Transaction types shown as colored badges

### 3. Use Filters
1. Click on **סינון עסקאות** header to expand
2. Set filters:
   - Date range (from/to)
   - Transaction type
   - Amount range
3. Click **החל סינון** (Apply Filter)
4. Click **נקה סינון** (Clear Filter) to reset

### 4. View Transaction Details
1. Click **view icon** (👁️) on any transaction
2. Modal opens showing:
   - Transaction information
   - Breakdown of detail components
   - Sum verification

## Creating Transactions (Via API)

### Example Request

```bash
curl -X POST http://localhost:5082/api/transactions \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -d '{
    "accountId": 1,
    "transactionTypeId": 3,
    "transactionDate": "2026-01-28",
    "amount": 1000.00,
    "description": "תשלום שכר לימוד",
    "relatedStudentId": 123,
    "schoolYearId": 5,
    "details": [
      {
        "detailTypeId": 1,
        "description": "סכום בסיס",
        "amount": 847.46
      },
      {
        "detailTypeId": 2,
        "description": "מע\"מ 18%",
        "amount": 152.54
      }
    ]
  }'
```

### Transaction Types (IDs)

| ID | Name | Description |
|----|------|-------------|
| 1 | income_government | הכנסה מהמדינה |
| 2 | income_council | הכנסה ממועצה |
| 3 | income_tuition | הכנסה משכר לימוד |
| 4 | income_donation | הכנסה מתרומה |
| 5 | expense_salary | הוצאה משכר |
| 6 | expense_service | הוצאה משירות |
| 7 | expense_material | הוצאה מחומרים |
| 8 | transfer_in | העברה פנימית (זיכוי) |
| 9 | transfer_out | העברה פנימית (חיוב) |
| 10 | adjustment_increase | התאמה (הגדלה) |
| 11 | adjustment_decrease | התאמה (הקטנה) |

### Detail Types (IDs)

| ID | Name | Description |
|----|------|-------------|
| 1 | base_amount | סכום בסיס |
| 2 | vat | מע"מ |
| 3 | withholding_tax | ניכוי מס במקור |
| 4 | discount | הנחה |
| 5 | surcharge | תוספת |
| 6 | fee | עמלה |
| 7 | other | אחר |

## Security Setup

### Assign Actions to Roles

```sql
-- Query to see new actions
SELECT id, name, action_type_id, reference
FROM petel_schema.actions
WHERE name LIKE 'accounttransactions_%'
   OR name = 'accounts_viewTransactions';

-- Assign to admin role (example)
INSERT INTO petel_schema.roles_actions (role_id, action_id)
SELECT 1, id  -- Replace 1 with your admin role ID
FROM petel_schema.actions
WHERE name IN (
    'accounttransactions',  -- Page access
    'accounttransactions_viewTransaction',
    'accounttransactions_addTransaction',
    'accounttransactions_refreshData',
    'accounttransactions_backToAccounts',
    'accounts_viewTransactions'
);
```

### Refresh Security Cache

1. Login as admin
2. Navigate to **ניהול תפקידים** (Roles Management)
3. Click **רענן מטמון אבטחה** (Refresh Security Cache)

## API Endpoints Reference

### GET /api/transactions/account/{accountId}
Get transactions for account with optional filters.

**Query Parameters**:
- `startDate` - Filter from date (yyyy-MM-dd)
- `endDate` - Filter to date (yyyy-MM-dd)
- `transactionTypeId` - Filter by type
- `schoolYearId` - Filter by year
- `relatedStudentId` - Filter by student
- `minAmount` - Minimum amount
- `maxAmount` - Maximum amount

### GET /api/transactions/{transactionId}/details
Get transaction with details breakdown.

### POST /api/transactions
Create new transaction with details.

**Required Fields**:
- `accountId`
- `transactionTypeId`
- `transactionDate`
- `amount`
- `description`
- `details` (array with at least 1 item)

**Validation**:
- Sum of details must equal transaction amount
- At least one detail required

### GET /api/transactions/types
Get all active transaction types.

### GET /api/transactions/detail-types
Get all active detail types.

## Common Issues

### "סכום הפירוטים חייב להיות שווה לסכום העסקה"
**Problem**: Sum of details doesn't match transaction amount.

**Solution**: Ensure detail amounts sum exactly to transaction amount (0.01 tolerance).

### "חובה להזין לפחות פירוט אחד לעסקה"
**Problem**: Creating transaction without details.

**Solution**: Include at least one item in the `details` array.

### "חשבון לא נמצא"
**Problem**: Invalid account ID.

**Solution**: Verify account exists and ID is correct.

### Page shows "אין לך הרשאה לגשת לעמוד זה"
**Problem**: User doesn't have access to page.

**Solution**: Assign `accounttransactions` page action to user's role.

### Button is hidden
**Problem**: User doesn't have permission for action.

**Solution**: Assign specific action to user's role (e.g., `accounttransactions_viewTransaction`).

## Business Rules

1. ✅ Transactions **cannot be deleted**
2. ✅ Details **must sum to transaction amount**
3. ✅ At least **one detail required** per transaction
4. ✅ Account balance **auto-updates** on transaction creation
5. ✅ Credit transactions **increase** balance
6. ✅ Debit transactions **decrease** balance
7. ✅ All transactions **track creating user**

## File Locations

- **Migration**: `PetelApp.Api/Migrations/Scripts/20260128_CreateTransactionsSchema.sql`
- **Controller**: `PetelApp.Api/Controllers/TransactionsController.cs`
- **Page**: `PetelApp.BlazorServer/Components/Pages/AccountTransactions.razor`
- **DTOs**: `PetelApp.BlazorServer/DTOs/TransactionDTOs.cs`

## Support

For detailed implementation information, see:
- `TRANSACTION_ACCOUNTS_TRANSACTIONS_FEATURE.md` - Complete feature documentation
- `BLAZOR_DEVELOPER_GUIDE.md` - Blazor development patterns
- `copilot-instructions.md` - Project coding guidelines
