-- Migration: Add council_types, districts, and extend councils table
-- Run on all environments. Idempotent (safe to re-run).

-- ============================================================
-- 1. Create council_types lookup table
-- ============================================================
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT FROM pg_tables
        WHERE schemaname = 'petel_schema'
        AND tablename = 'council_types'
    ) THEN
        CREATE TABLE petel_schema.council_types (
            id            SERIAL PRIMARY KEY,
            name          VARCHAR(50) NOT NULL,
            sort_order    INTEGER NOT NULL DEFAULT 0,
            is_active     BOOLEAN NOT NULL DEFAULT true,
            created_at    TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
            created_user  INTEGER NULL REFERENCES petel_schema.users(id) ON DELETE SET NULL,
            updated_at    TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
            update_user   INTEGER NULL REFERENCES petel_schema.users(id) ON DELETE SET NULL,
            CONSTRAINT uk_council_types_name UNIQUE (name)
        );

        CREATE INDEX idx_council_types_name       ON petel_schema.council_types(name);
        CREATE INDEX idx_council_types_sort_order ON petel_schema.council_types(sort_order);

        RAISE NOTICE 'Table council_types created successfully';
    ELSE
        RAISE NOTICE 'Table council_types already exists';
    END IF;
END
$$;

-- Seed council types
INSERT INTO petel_schema.council_types (name, sort_order)
VALUES
    ('עירייה',          1),
    ('מועצה מקומית',   2),
    ('מועצה אזורית',   3)
ON CONFLICT (name) DO NOTHING;

-- ============================================================
-- 2. Create districts lookup table
-- ============================================================
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT FROM pg_tables
        WHERE schemaname = 'petel_schema'
        AND tablename = 'districts'
    ) THEN
        CREATE TABLE petel_schema.districts (
            id            SERIAL PRIMARY KEY,
            name          VARCHAR(50) NOT NULL,
            sort_order    INTEGER NOT NULL DEFAULT 0,
            is_active     BOOLEAN NOT NULL DEFAULT true,
            created_at    TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
            created_user  INTEGER NULL REFERENCES petel_schema.users(id) ON DELETE SET NULL,
            updated_at    TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
            update_user   INTEGER NULL REFERENCES petel_schema.users(id) ON DELETE SET NULL,
            CONSTRAINT uk_districts_name UNIQUE (name)
        );

        CREATE INDEX idx_districts_name       ON petel_schema.districts(name);
        CREATE INDEX idx_districts_sort_order ON petel_schema.districts(sort_order);

        RAISE NOTICE 'Table districts created successfully';
    ELSE
        RAISE NOTICE 'Table districts already exists';
    END IF;
END
$$;

-- Seed districts
INSERT INTO petel_schema.districts (name, sort_order)
VALUES
    ('חיפה',                     1),
    ('הדרום',                    2),
    ('תל אביב',                  3),
    ('המרכז',                    4),
    ('אזור יהודה והשומרון',      5),
    ('ירושלים',                  6),
    ('הצפון',                    7)
ON CONFLICT (name) DO NOTHING;

-- ============================================================
-- 3. Add new columns to councils table
-- ============================================================
DO $$
BEGIN
    -- long_name
    IF NOT EXISTS (
        SELECT FROM information_schema.columns
        WHERE table_schema = 'petel_schema'
          AND table_name   = 'councils'
          AND column_name  = 'long_name'
    ) THEN
        ALTER TABLE petel_schema.councils
            ADD COLUMN long_name VARCHAR(100) NULL;
        RAISE NOTICE 'Column long_name added to councils';
    ELSE
        RAISE NOTICE 'Column long_name already exists in councils';
    END IF;

    -- council_type_id
    IF NOT EXISTS (
        SELECT FROM information_schema.columns
        WHERE table_schema = 'petel_schema'
          AND table_name   = 'councils'
          AND column_name  = 'council_type_id'
    ) THEN
        ALTER TABLE petel_schema.councils
            ADD COLUMN council_type_id INTEGER NULL
                REFERENCES petel_schema.council_types(id) ON DELETE SET NULL;
        CREATE INDEX idx_councils_council_type_id ON petel_schema.councils(council_type_id);
        RAISE NOTICE 'Column council_type_id added to councils';
    ELSE
        RAISE NOTICE 'Column council_type_id already exists in councils';
    END IF;

    -- district_id
    IF NOT EXISTS (
        SELECT FROM information_schema.columns
        WHERE table_schema = 'petel_schema'
          AND table_name   = 'councils'
          AND column_name  = 'district_id'
    ) THEN
        ALTER TABLE petel_schema.councils
            ADD COLUMN district_id INTEGER NULL
                REFERENCES petel_schema.districts(id) ON DELETE SET NULL;
        CREATE INDEX idx_councils_district_id ON petel_schema.councils(district_id);
        RAISE NOTICE 'Column district_id added to councils';
    ELSE
        RAISE NOTICE 'Column district_id already exists in councils';
    END IF;
END
$$;
