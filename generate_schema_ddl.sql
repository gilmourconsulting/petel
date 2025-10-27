-- Script to generate DDL for all tables in a specific schema
-- Usage: psql -d your_database -f generate_schema_ddl.sql -v schema_name=your_schema > output/schema_ddl.sql

-- Set the schema name (can be overridden with -v schema_name=xxx)
\set schema_name 'petel_schema'

-- Disable output pagination
\pset pager off

-- Format output for better readability
\pset format unaligned
\pset tuples_only on

-- Generate CREATE TABLE statements
SELECT 
    '-- Table: ' || table_schema || '.' || table_name || E'\n' ||
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
    ) || E'\n);' || E'\n'
FROM information_schema.columns
WHERE table_schema = :'schema_name'
  AND table_name IN (SELECT tablename FROM pg_tables WHERE schemaname = :'schema_name')
GROUP BY table_schema, table_name
ORDER BY table_name;

-- Generate PRIMARY KEY constraints
SELECT E'\n-- Primary Keys\n' || 
    string_agg(
        'ALTER TABLE ' || nsp.nspname || '.' || cls.relname || 
        ' ADD CONSTRAINT ' || con.conname || 
        ' PRIMARY KEY (' || 
        string_agg(att.attname, ', ' ORDER BY array_position(con.conkey, att.attnum)) || 
        ');',
        E'\n'
    )
FROM pg_constraint con
JOIN pg_class cls ON con.conrelid = cls.oid
JOIN pg_namespace nsp ON cls.relnamespace = nsp.oid
JOIN pg_attribute att ON att.attrelid = cls.oid AND att.attnum = ANY(con.conkey)
WHERE con.contype = 'p'
  AND nsp.nspname = :'schema_name'
GROUP BY nsp.nspname, cls.relname, con.conname, con.conkey;

-- Generate FOREIGN KEY constraints
SELECT E'\n-- Foreign Keys\n' || 
    string_agg(
        'ALTER TABLE ' || nsp.nspname || '.' || cls.relname || 
        ' ADD CONSTRAINT ' || con.conname || 
        ' FOREIGN KEY (' || 
        string_agg(att.attname, ', ' ORDER BY array_position(con.conkey, att.attnum)) || 
        ') REFERENCES ' || fnsp.nspname || '.' || fcls.relname || 
        '(' || string_agg(fatt.attname, ', ' ORDER BY array_position(con.confkey, fatt.attnum)) || ')',
        E';\n'
    ) || ';'
FROM pg_constraint con
JOIN pg_class cls ON con.conrelid = cls.oid
JOIN pg_namespace nsp ON cls.relnamespace = nsp.oid
JOIN pg_class fcls ON con.confrelid = fcls.oid
JOIN pg_namespace fnsp ON fcls.relnamespace = fnsp.oid
JOIN pg_attribute att ON att.attrelid = cls.oid AND att.attnum = ANY(con.conkey)
JOIN pg_attribute fatt ON fatt.attrelid = fcls.oid AND fatt.attnum = ANY(con.confkey)
WHERE con.contype = 'f'
  AND nsp.nspname = :'schema_name'
GROUP BY nsp.nspname, cls.relname, con.conname, con.conkey, fnsp.nspname, fcls.relname, con.confkey;

-- Generate UNIQUE constraints
SELECT E'\n-- Unique Constraints\n' || 
    string_agg(
        'ALTER TABLE ' || nsp.nspname || '.' || cls.relname || 
        ' ADD CONSTRAINT ' || con.conname || 
        ' UNIQUE (' || 
        string_agg(att.attname, ', ' ORDER BY array_position(con.conkey, att.attnum)) || 
        ');',
        E'\n'
    )
FROM pg_constraint con
JOIN pg_class cls ON con.conrelid = cls.oid
JOIN pg_namespace nsp ON cls.relnamespace = nsp.oid
JOIN pg_attribute att ON att.attrelid = cls.oid AND att.attnum = ANY(con.conkey)
WHERE con.contype = 'u'
  AND nsp.nspname = :'schema_name'
GROUP BY nsp.nspname, cls.relname, con.conname, con.conkey;

-- Generate CHECK constraints
SELECT E'\n-- Check Constraints\n' || 
    string_agg(
        'ALTER TABLE ' || nsp.nspname || '.' || cls.relname || 
        ' ADD CONSTRAINT ' || con.conname || 
        ' CHECK ' || pg_get_constraintdef(con.oid),
        E';\n'
    ) || ';'
FROM pg_constraint con
JOIN pg_class cls ON con.conrelid = cls.oid
JOIN pg_namespace nsp ON cls.relnamespace = nsp.oid
WHERE con.contype = 'c'
  AND nsp.nspname = :'schema_name';

-- Generate INDEXES
SELECT E'\n-- Indexes\n' || 
    string_agg(
        pg_get_indexdef(idx.indexrelid) || ';',
        E'\n'
    )
FROM pg_index idx
JOIN pg_class cls ON idx.indrelid = cls.oid
JOIN pg_namespace nsp ON cls.relnamespace = nsp.oid
WHERE nsp.nspname = :'schema_name'
  AND NOT idx.indisprimary;

-- Generate COMMENTS on tables and columns
SELECT E'\n-- Table Comments\n' || 
    string_agg(
        'COMMENT ON TABLE ' || nsp.nspname || '.' || cls.relname || 
        ' IS ' || quote_literal(pg_catalog.obj_description(cls.oid, 'pg_class')) || ';',
        E'\n'
    )
FROM pg_class cls
JOIN pg_namespace nsp ON cls.relnamespace = nsp.oid
WHERE nsp.nspname = :'schema_name'
  AND cls.relkind = 'r'
  AND pg_catalog.obj_description(cls.oid, 'pg_class') IS NOT NULL;

SELECT E'\n-- Column Comments\n' || 
    string_agg(
        'COMMENT ON COLUMN ' || nsp.nspname || '.' || cls.relname || '.' || att.attname ||
        ' IS ' || quote_literal(pg_catalog.col_description(cls.oid, att.attnum)) || ';',
        E'\n'
    )
FROM pg_class cls
JOIN pg_namespace nsp ON cls.relnamespace = nsp.oid
JOIN pg_attribute att ON att.attrelid = cls.oid
WHERE nsp.nspname = :'schema_name'
  AND cls.relkind = 'r'
  AND att.attnum > 0
  AND NOT att.attisdropped
  AND pg_catalog.col_description(cls.oid, att.attnum) IS NOT NULL;
