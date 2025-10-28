-- ============================================
-- PostgreSQL Schema DDL Generator for pgAdmin
-- SINGLE QUERY VERSION - All output in one result
-- ============================================
-- 
-- INSTRUCTIONS:
-- 1. Change 'public' to your schema name in the WITH clause below
-- 2. Run this script (F5)
-- 3. Copy all rows from the output
-- 4. Paste into a text editor and save as .sql
--
-- ============================================

WITH schema_config AS (
    SELECT 'petel_schema'::text AS schema_name  -- ⚠️ CHANGE THIS TO YOUR SCHEMA NAME
)
SELECT 
    row_number() OVER (ORDER BY sort_order, sort_key) as line_number,
    ddl_statement as "-- DDL STATEMENTS (copy all rows) --"
FROM (
    -- Header
    SELECT 
        1 as sort_order,
        '' as sort_key,
        '-- ============================================' || E'\n' ||
        '-- Schema DDL Export: ' || schema_name || E'\n' ||
        '-- Generated: ' || CURRENT_TIMESTAMP || E'\n' ||
        '-- ============================================' || E'\n' as ddl_statement
    FROM schema_config

    UNION ALL

    -- CREATE TABLE statements
    SELECT 
        2 as sort_order,
        '' as sort_key,
        E'\n-- ============================================\n' ||
        '-- TABLES' || E'\n' ||
        '-- ============================================' || E'\n' as ddl_statement
    FROM schema_config

    UNION ALL

    SELECT 
        3 as sort_order,
        table_name as sort_key,
        E'\n-- Table: ' || table_schema || '.' || table_name || E'\n' ||
        'CREATE TABLE ' || table_schema || '.' || table_name || E' (\n' ||
        string_agg(
            '    ' || column_name || ' ' || 
            CASE 
                WHEN data_type = 'USER-DEFINED' THEN udt_name
                WHEN data_type = 'ARRAY' THEN udt_name
                ELSE data_type 
            END ||
            CASE 
                WHEN character_maximum_length IS NOT NULL 
                THEN '(' || character_maximum_length || ')'
                WHEN numeric_precision IS NOT NULL AND numeric_scale IS NOT NULL
                THEN '(' || numeric_precision || ',' || numeric_scale || ')'
                WHEN numeric_precision IS NOT NULL
                THEN '(' || numeric_precision || ')'
                ELSE ''
            END ||
            CASE WHEN is_nullable = 'NO' THEN ' NOT NULL' ELSE '' END ||
            CASE WHEN column_default IS NOT NULL THEN ' DEFAULT ' || column_default ELSE '' END,
            E',\n'
            ORDER BY ordinal_position
        ) || E'\n);' || E'\n' as ddl_statement
    FROM information_schema.columns, schema_config
    WHERE table_schema = schema_config.schema_name
      AND table_name IN (SELECT tablename FROM pg_tables WHERE schemaname = schema_config.schema_name)
    GROUP BY table_schema, table_name

    UNION ALL

    -- Primary Keys section header
    SELECT 
        4 as sort_order,
        '' as sort_key,
        E'\n-- ============================================\n' ||
        '-- PRIMARY KEYS' || E'\n' ||
        '-- ============================================' || E'\n' as ddl_statement
    FROM schema_config

    UNION ALL

    -- Primary Keys
    SELECT 
        5 as sort_order,
        cls.relname as sort_key,
        E'\nALTER TABLE ' || nsp.nspname || '.' || cls.relname || 
        E'\n  ADD CONSTRAINT ' || con.conname || 
        E'\n  PRIMARY KEY (' || 
        (
            SELECT string_agg(att.attname, ', ' ORDER BY array_position(con.conkey, att.attnum))
            FROM pg_attribute att
            WHERE att.attrelid = cls.oid 
            AND att.attnum = ANY(con.conkey)
        ) || 
        ');' as ddl_statement
    FROM pg_constraint con
    JOIN pg_class cls ON con.conrelid = cls.oid
    JOIN pg_namespace nsp ON cls.relnamespace = nsp.oid
    CROSS JOIN schema_config
    WHERE con.contype = 'p'
      AND nsp.nspname = schema_config.schema_name

    UNION ALL

    -- Foreign Keys section header
    SELECT 
        6 as sort_order,
        '' as sort_key,
        E'\n-- ============================================\n' ||
        '-- FOREIGN KEYS' || E'\n' ||
        '-- ============================================' || E'\n' as ddl_statement
    FROM schema_config

    UNION ALL

    -- Foreign Keys
    SELECT 
        7 as sort_order,
        cls.relname as sort_key,
        E'\nALTER TABLE ' || nsp.nspname || '.' || cls.relname || 
        E'\n  ADD CONSTRAINT ' || con.conname || 
        E'\n  FOREIGN KEY (' || 
        (
            SELECT string_agg(att.attname, ', ' ORDER BY array_position(con.conkey, att.attnum))
            FROM pg_attribute att
            WHERE att.attrelid = cls.oid 
            AND att.attnum = ANY(con.conkey)
        ) || 
        ')' ||
        E'\n  REFERENCES ' || fnsp.nspname || '.' || fcls.relname || 
        ' (' || 
        (
            SELECT string_agg(fatt.attname, ', ' ORDER BY array_position(con.confkey, fatt.attnum))
            FROM pg_attribute fatt
            WHERE fatt.attrelid = fcls.oid 
            AND fatt.attnum = ANY(con.confkey)
        ) || 
        ')' ||
        CASE 
            WHEN con.confupdtype = 'c' THEN E'\n  ON UPDATE CASCADE'
            WHEN con.confupdtype = 'n' THEN E'\n  ON UPDATE SET NULL'
            WHEN con.confupdtype = 'd' THEN E'\n  ON UPDATE SET DEFAULT'
            WHEN con.confupdtype = 'r' THEN E'\n  ON UPDATE RESTRICT'
            ELSE ''
        END ||
        CASE 
            WHEN con.confdeltype = 'c' THEN E'\n  ON DELETE CASCADE'
            WHEN con.confdeltype = 'n' THEN E'\n  ON DELETE SET NULL'
            WHEN con.confdeltype = 'd' THEN E'\n  ON DELETE SET DEFAULT'
            WHEN con.confdeltype = 'r' THEN E'\n  ON DELETE RESTRICT'
            ELSE ''
        END ||
        ';' as ddl_statement
    FROM pg_constraint con
    JOIN pg_class cls ON con.conrelid = cls.oid
    JOIN pg_namespace nsp ON cls.relnamespace = nsp.oid
    JOIN pg_class fcls ON con.confrelid = fcls.oid
    JOIN pg_namespace fnsp ON fcls.relnamespace = fnsp.oid
    CROSS JOIN schema_config
    WHERE con.contype = 'f'
      AND nsp.nspname = schema_config.schema_name

    UNION ALL

    -- Unique Constraints section header
    SELECT 
        8 as sort_order,
        '' as sort_key,
        E'\n-- ============================================\n' ||
        '-- UNIQUE CONSTRAINTS' || E'\n' ||
        '-- ============================================' || E'\n' as ddl_statement
    FROM schema_config
    WHERE EXISTS (
        SELECT 1 FROM pg_constraint con
        JOIN pg_namespace nsp ON nsp.oid = con.connamespace
        CROSS JOIN schema_config sc
        WHERE con.contype = 'u' AND nsp.nspname = sc.schema_name
    )

    UNION ALL

    -- Unique Constraints
    SELECT 
        9 as sort_order,
        cls.relname as sort_key,
        E'\nALTER TABLE ' || nsp.nspname || '.' || cls.relname || 
        E'\n  ADD CONSTRAINT ' || con.conname || 
        E'\n  UNIQUE (' || 
        (
            SELECT string_agg(att.attname, ', ' ORDER BY array_position(con.conkey, att.attnum))
            FROM pg_attribute att
            WHERE att.attrelid = cls.oid 
            AND att.attnum = ANY(con.conkey)
        ) || 
        ');' as ddl_statement
    FROM pg_constraint con
    JOIN pg_class cls ON con.conrelid = cls.oid
    JOIN pg_namespace nsp ON cls.relnamespace = nsp.oid
    CROSS JOIN schema_config
    WHERE con.contype = 'u'
      AND nsp.nspname = schema_config.schema_name

    UNION ALL

    -- Check Constraints section header
    SELECT 
        10 as sort_order,
        '' as sort_key,
        E'\n-- ============================================\n' ||
        '-- CHECK CONSTRAINTS' || E'\n' ||
        '-- ============================================' || E'\n' as ddl_statement
    FROM schema_config
    WHERE EXISTS (
        SELECT 1 FROM pg_constraint con
        JOIN pg_namespace nsp ON nsp.oid = con.connamespace
        CROSS JOIN schema_config sc
        WHERE con.contype = 'c' AND nsp.nspname = sc.schema_name
    )

    UNION ALL

    -- Check Constraints
    SELECT 
        11 as sort_order,
        cls.relname as sort_key,
        E'\nALTER TABLE ' || nsp.nspname || '.' || cls.relname || 
        E'\n  ADD CONSTRAINT ' || con.conname || 
        E'\n  ' || pg_get_constraintdef(con.oid) || ';' as ddl_statement
    FROM pg_constraint con
    JOIN pg_class cls ON con.conrelid = cls.oid
    JOIN pg_namespace nsp ON cls.relnamespace = nsp.oid
    CROSS JOIN schema_config
    WHERE con.contype = 'c'
      AND nsp.nspname = schema_config.schema_name

    UNION ALL

    -- Indexes section header
    SELECT 
        12 as sort_order,
        '' as sort_key,
        E'\n-- ============================================\n' ||
        '-- INDEXES' || E'\n' ||
        '-- ============================================' || E'\n' as ddl_statement
    FROM schema_config
    WHERE EXISTS (
        SELECT 1 FROM pg_index idx
        JOIN pg_class cls ON idx.indrelid = cls.oid
        JOIN pg_namespace nsp ON cls.relnamespace = nsp.oid
        CROSS JOIN schema_config sc
        WHERE nsp.nspname = sc.schema_name AND NOT idx.indisprimary
    )

    UNION ALL

    -- Indexes
    SELECT 
        13 as sort_order,
        cls.relname as sort_key,
        E'\n' || pg_get_indexdef(idx.indexrelid) || ';' as ddl_statement
    FROM pg_index idx
    JOIN pg_class cls ON idx.indrelid = cls.oid
    JOIN pg_namespace nsp ON cls.relnamespace = nsp.oid
    CROSS JOIN schema_config
    WHERE nsp.nspname = schema_config.schema_name
      AND NOT idx.indisprimary

    UNION ALL

    -- Table Comments section header
    SELECT 
        14 as sort_order,
        '' as sort_key,
        E'\n-- ============================================\n' ||
        '-- TABLE COMMENTS' || E'\n' ||
        '-- ============================================' || E'\n' as ddl_statement
    FROM schema_config
    WHERE EXISTS (
        SELECT 1 FROM pg_class cls
        JOIN pg_namespace nsp ON cls.relnamespace = nsp.oid
        CROSS JOIN schema_config sc
        WHERE nsp.nspname = sc.schema_name 
        AND cls.relkind = 'r'
        AND pg_catalog.obj_description(cls.oid, 'pg_class') IS NOT NULL
    )

    UNION ALL

    -- Table Comments
    SELECT 
        15 as sort_order,
        cls.relname as sort_key,
        E'\nCOMMENT ON TABLE ' || nsp.nspname || '.' || cls.relname || 
        E'\n  IS ' || quote_literal(pg_catalog.obj_description(cls.oid, 'pg_class')) || ';' as ddl_statement
    FROM pg_class cls
    JOIN pg_namespace nsp ON cls.relnamespace = nsp.oid
    CROSS JOIN schema_config
    WHERE nsp.nspname = schema_config.schema_name
      AND cls.relkind = 'r'
      AND pg_catalog.obj_description(cls.oid, 'pg_class') IS NOT NULL

    UNION ALL

    -- Column Comments section header
    SELECT 
        16 as sort_order,
        '' as sort_key,
        E'\n-- ============================================\n' ||
        '-- COLUMN COMMENTS' || E'\n' ||
        '-- ============================================' || E'\n' as ddl_statement
    FROM schema_config
    WHERE EXISTS (
        SELECT 1 FROM pg_class cls
        JOIN pg_namespace nsp ON cls.relnamespace = nsp.oid
        JOIN pg_attribute att ON att.attrelid = cls.oid
        CROSS JOIN schema_config sc
        WHERE nsp.nspname = sc.schema_name 
        AND cls.relkind = 'r'
        AND att.attnum > 0
        AND NOT att.attisdropped
        AND pg_catalog.col_description(cls.oid, att.attnum) IS NOT NULL
    )

    UNION ALL

    -- Column Comments
    SELECT 
        17 as sort_order,
        cls.relname as sort_key,
        E'\nCOMMENT ON COLUMN ' || nsp.nspname || '.' || cls.relname || '.' || att.attname ||
        E'\n  IS ' || quote_literal(pg_catalog.col_description(cls.oid, att.attnum)) || ';' as ddl_statement
    FROM pg_class cls
    JOIN pg_namespace nsp ON cls.relnamespace = nsp.oid
    JOIN pg_attribute att ON att.attrelid = cls.oid
    CROSS JOIN schema_config
    WHERE nsp.nspname = schema_config.schema_name
      AND cls.relkind = 'r'
      AND att.attnum > 0
      AND NOT att.attisdropped
      AND pg_catalog.col_description(cls.oid, att.attnum) IS NOT NULL

    UNION ALL

    -- Footer
    SELECT 
        999 as sort_order,
        '' as sort_key,
        E'\n-- ============================================\n' ||
        '-- END OF DDL EXPORT' || E'\n' ||
        '-- ============================================' as ddl_statement
    FROM schema_config

) AS all_ddl
ORDER BY sort_order, sort_key;
