-- ===================================================================
-- Transaction Accounts Migration
-- Created: 2026-01-27
-- Description: Creates transaction account infrastructure for managing
--              accounts between entities (e.g., external student fees)
-- ===================================================================

-- ===================================================================
-- STEP 1: Create transaction_account_types table
-- ===================================================================
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT FROM pg_tables
        WHERE schemaname = 'petel_schema'
        AND tablename = 'transaction_account_types'
    ) THEN
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

        -- Create indexes
        CREATE INDEX idx_transaction_account_types_name 
            ON petel_schema.transaction_account_types(name);
        
        CREATE INDEX idx_transaction_account_types_is_active 
            ON petel_schema.transaction_account_types(is_active);

        RAISE NOTICE 'Table transaction_account_types created successfully';
    ELSE
        RAISE NOTICE 'Table transaction_account_types already exists';
    END IF;
END
$$;

-- ===================================================================
-- STEP 2: Insert initial account types
-- ===================================================================
DO $$
BEGIN
    -- Add external students fees account type (אגרות תלמידי חוץ)
    IF NOT EXISTS (
        SELECT 1 FROM petel_schema.transaction_account_types 
        WHERE name = 'external_students_fees'
    ) THEN
        INSERT INTO petel_schema.transaction_account_types 
            (name, description, is_active, sort_order)
        VALUES 
            ('external_students_fees', 'אגרות תלמידי חוץ', true, 1);
        
        RAISE NOTICE 'Account type "external_students_fees" created';
    ELSE
        RAISE NOTICE 'Account type "external_students_fees" already exists';
    END IF;
END
$$;

-- ===================================================================
-- STEP 3: Create transaction_accounts table
-- ===================================================================
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT FROM pg_tables
        WHERE schemaname = 'petel_schema'
        AND tablename = 'transaction_accounts'
    ) THEN
        CREATE TABLE petel_schema.transaction_accounts (
            id SERIAL PRIMARY KEY,
            
            -- Ownership: Entity that owns this account
            owner_entity_id INTEGER NOT NULL REFERENCES petel_schema.entities(id) ON DELETE CASCADE,
            
            -- Related Entity: Entity for whom transactions are held
            related_entity_id INTEGER NOT NULL REFERENCES petel_schema.entities(id) ON DELETE RESTRICT,
            
            -- Account Type: Type of account (from transaction_account_types)
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

        -- ===================================================================
        -- STEP 4: Create indexes for performance
        -- ===================================================================
        CREATE INDEX idx_transaction_accounts_owner_entity 
            ON petel_schema.transaction_accounts(owner_entity_id);
        
        CREATE INDEX idx_transaction_accounts_related_entity 
            ON petel_schema.transaction_accounts(related_entity_id);
        
        CREATE INDEX idx_transaction_accounts_account_type 
            ON petel_schema.transaction_accounts(account_type_id);
        
        CREATE INDEX idx_transaction_accounts_is_active 
            ON petel_schema.transaction_accounts(is_active);
        
        CREATE INDEX idx_transaction_accounts_created_user 
            ON petel_schema.transaction_accounts(created_user);
        
        CREATE INDEX idx_transaction_accounts_update_user 
            ON petel_schema.transaction_accounts(update_user);

        RAISE NOTICE 'Table transaction_accounts created successfully with indexes';
    ELSE
        RAISE NOTICE 'Table transaction_accounts already exists';
    END IF;
END
$$;

-- ===================================================================
-- STEP 5: Add comments for documentation
-- ===================================================================
COMMENT ON TABLE petel_schema.transaction_accounts IS 
    'Transaction accounts for managing financial relationships between entities';

COMMENT ON COLUMN petel_schema.transaction_accounts.owner_entity_id IS 
    'Entity that owns this account (e.g., school network)';

COMMENT ON COLUMN petel_schema.transaction_accounts.related_entity_id IS 
    'Entity for whom transactions are held (e.g., council)';

COMMENT ON COLUMN petel_schema.transaction_accounts.account_type_id IS 
    'Type of account from transaction_account_types (e.g., external_students_fees)';

COMMENT ON COLUMN petel_schema.transaction_accounts.balance IS 
    'Current account balance (positive = credit, negative = debit)';

-- ===================================================================
-- STEP 6: Grant permissions
-- ===================================================================
GRANT SELECT, INSERT, UPDATE, DELETE ON petel_schema.transaction_accounts TO peteladmin;
GRANT USAGE, SELECT ON SEQUENCE petel_schema.transaction_accounts_id_seq TO peteladmin;

GRANT SELECT, INSERT, UPDATE, DELETE ON petel_schema.transaction_account_types TO peteladmin;
GRANT USAGE, SELECT ON SEQUENCE petel_schema.transaction_account_types_id_seq TO peteladmin;

-- ===================================================================
-- Migration Complete
-- ===================================================================
DO $$
BEGIN
    RAISE NOTICE '✅ Transaction accounts migration completed successfully';
END
$$;
