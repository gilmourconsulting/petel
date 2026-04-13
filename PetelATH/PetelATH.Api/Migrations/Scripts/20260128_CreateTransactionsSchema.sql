-- Transaction Types Lookup Table
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT FROM pg_tables
        WHERE schemaname = 'petel_schema'
        AND tablename = 'transaction_types'
    ) THEN
        CREATE TABLE petel_schema.transaction_types (
            id SERIAL PRIMARY KEY,
            name VARCHAR(100) NOT NULL UNIQUE,
            description VARCHAR(200) NOT NULL,
            is_credit BOOLEAN NOT NULL DEFAULT false,  -- true = credit (income), false = debit (expense)
            is_active BOOLEAN NOT NULL DEFAULT true,
            created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
            created_user INTEGER NULL REFERENCES petel_schema.users(id) ON DELETE SET NULL,
            updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
            update_user INTEGER NULL REFERENCES petel_schema.users(id) ON DELETE SET NULL
        );

        CREATE INDEX idx_transaction_types_name ON petel_schema.transaction_types(name);
        CREATE INDEX idx_transaction_types_is_active ON petel_schema.transaction_types(is_active);

        RAISE NOTICE 'Table transaction_types created successfully';
    ELSE
        RAISE NOTICE 'Table transaction_types already exists';
    END IF;
END
$$;

-- Transaction Detail Types Lookup Table
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT FROM pg_tables
        WHERE schemaname = 'petel_schema'
        AND tablename = 'transaction_detail_types'
    ) THEN
        CREATE TABLE petel_schema.transaction_detail_types (
            id SERIAL PRIMARY KEY,
            name VARCHAR(100) NOT NULL UNIQUE,
            description VARCHAR(200) NOT NULL,
            is_active BOOLEAN NOT NULL DEFAULT true,
            created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
            created_user INTEGER NULL REFERENCES petel_schema.users(id) ON DELETE SET NULL,
            updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
            update_user INTEGER NULL REFERENCES petel_schema.users(id) ON DELETE SET NULL
        );

        CREATE INDEX idx_transaction_detail_types_name ON petel_schema.transaction_detail_types(name);
        CREATE INDEX idx_transaction_detail_types_is_active ON petel_schema.transaction_detail_types(is_active);

        RAISE NOTICE 'Table transaction_detail_types created successfully';
    ELSE
        RAISE NOTICE 'Table transaction_detail_types already exists';
    END IF;
END
$$;

-- Transactions Table
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT FROM pg_tables
        WHERE schemaname = 'petel_schema'
        AND tablename = 'transactions'
    ) THEN
        CREATE TABLE petel_schema.transactions (
            id SERIAL PRIMARY KEY,
            account_id INTEGER NOT NULL REFERENCES petel_schema.transaction_accounts(id) ON DELETE RESTRICT,
            transaction_type_id INTEGER NOT NULL REFERENCES petel_schema.transaction_types(id) ON DELETE RESTRICT,
            transaction_date DATE NOT NULL DEFAULT CURRENT_DATE,
            amount DECIMAL(18, 2) NOT NULL,
            description VARCHAR(500) NOT NULL,
            related_transaction_id INTEGER NULL REFERENCES petel_schema.transactions(id) ON DELETE SET NULL,
            related_student_id INTEGER NULL REFERENCES petel_schema.school_students(id) ON DELETE SET NULL,
            school_year_id INTEGER NULL REFERENCES petel_schema.hebrew_years(id) ON DELETE SET NULL,
            user_id INTEGER NOT NULL REFERENCES petel_schema.users(id) ON DELETE RESTRICT,
            created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
            created_user INTEGER NULL REFERENCES petel_schema.users(id) ON DELETE SET NULL,
            updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
            update_user INTEGER NULL REFERENCES petel_schema.users(id) ON DELETE SET NULL
        );

        CREATE INDEX idx_transactions_account_id ON petel_schema.transactions(account_id);
        CREATE INDEX idx_transactions_transaction_type_id ON petel_schema.transactions(transaction_type_id);
        CREATE INDEX idx_transactions_transaction_date ON petel_schema.transactions(transaction_date);
        CREATE INDEX idx_transactions_related_transaction_id ON petel_schema.transactions(related_transaction_id);
        CREATE INDEX idx_transactions_related_student_id ON petel_schema.transactions(related_student_id);
        CREATE INDEX idx_transactions_school_year_id ON petel_schema.transactions(school_year_id);
        CREATE INDEX idx_transactions_user_id ON petel_schema.transactions(user_id);

        RAISE NOTICE 'Table transactions created successfully';
    ELSE
        RAISE NOTICE 'Table transactions already exists';
    END IF;
END
$$;

-- Transaction Details Table
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT FROM pg_tables
        WHERE schemaname = 'petel_schema'
        AND tablename = 'transaction_details'
    ) THEN
        CREATE TABLE petel_schema.transaction_details (
            id SERIAL PRIMARY KEY,
            transaction_id INTEGER NOT NULL REFERENCES petel_schema.transactions(id) ON DELETE CASCADE,
            detail_type_id INTEGER NOT NULL REFERENCES petel_schema.transaction_detail_types(id) ON DELETE RESTRICT,
            description VARCHAR(500) NOT NULL,
            amount DECIMAL(18, 2) NOT NULL,
            created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
            created_user INTEGER NULL REFERENCES petel_schema.users(id) ON DELETE SET NULL,
            updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
            update_user INTEGER NULL REFERENCES petel_schema.users(id) ON DELETE SET NULL
        );

        CREATE INDEX idx_transaction_details_transaction_id ON petel_schema.transaction_details(transaction_id);
        CREATE INDEX idx_transaction_details_detail_type_id ON petel_schema.transaction_details(detail_type_id);

        RAISE NOTICE 'Table transaction_details created successfully';
    ELSE
        RAISE NOTICE 'Table transaction_details already exists';
    END IF;
END
$$;

-- Insert default transaction types
INSERT INTO petel_schema.transaction_types (name, description, is_credit)
VALUES 
    ('external_students_fee', 'חיוב אגרת תלמידי חוץ', false),
    ('external_students_fee_credit', 'זיכוי אגרת תלמידי חוץ', true),
    ('payment', 'תשלום', true),
    ('return', 'החזר', false)

ON CONFLICT (name) DO NOTHING;

-- Insert default transaction detail types
INSERT INTO petel_schema.transaction_detail_types (name, description)
VALUES 
    ('Payment', 'תשלום'),
('Basic','בסיסית'),
('Long Day','חוק יום חינוך ארוך'),
('Psychologist','פסיכולוג'),
('class help','סייעת כיתתית'),
('Green house','חממה טיפולית'),
('Zoo','מרחב זואולוגי'),
('school help','סייעת תגבור מוסדית'),
('Tracks','מגמות'),
('Pool','בריכת שחייה'),
('Sign language interpreter','מתורגמן לשפת הסימנים'),
('Hydro therapists','הידרותרפיסטים'),
('Additional Studies','תלן'),
('Guard','שמירה'),
('Basic','בסיסית'),
('Long Day','חוק יום חינוך ארוך'),
('Psychologist','פסיכולוג'),
('class help','סייעת כיתתית'),
('Security','שמירה'),
('Green house','חממה טיפולית'),
('Zoo','מרחב זואולוגי'),
('school help','סייעת תגבור מוסדית'),
('Tracks','מגמות'),
('Pool','בריכת שחייה'),
('Sign language interpreter','מתורגמן לשפת הסימנים'),
('Hydro therapists','הידרותרפיסטים'),
('Additional Studies','תלן'),
('Guard','שמירה'),
('Basic','בסיסית'),
('Long Day','חוק יום חינוך ארוך'),
('Psychologist','פסיכולוג'),
('class help','סייעת כיתתית'),
('Green house','חממה טיפולית'),
('Zoo','מרחב זואולוגי'),
('school help','סייעת תגבור מוסדית'),
('Tracks','מגמות'),
('Pool','בריכת שחייה'),
('Sign language interpreter','מתורגמן לשפת הסימנים'),
('Hydro therapists','הידרותרפיסטים'),
('Additional Studies','תלן')
ON CONFLICT (name) DO NOTHING;

RAISE NOTICE 'Transaction schema setup completed successfully';
