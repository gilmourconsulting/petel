-- ============================================
-- PostgreSQL Schema DDL Exporter (FULL VERSION)
-- - CREATE ALL sequences in schema first (owned or not)
-- - Owned sequences: full CREATE SEQUENCE + ALTER ... OWNED BY
-- - Unowned sequences: CREATE SEQUENCE with only non-default params
-- - Then CREATE TABLEs with identity clauses referencing seq (only non-default identity params)
-- - Integer type normalization
-- - Uses pg_sequence (Postgres 10+)
-- ============================================

WITH
schema_config AS (
    SELECT 'petel_schema'::text AS schema_name  -- CHANGE THIS
),

-- Map seqtypid -> type defaults
type_defaults AS (
    SELECT
        t.oid AS typoid,
        CASE WHEN t.typname IN ('int2','int4','int8') THEN 1 ELSE 1 END AS default_min,
        CASE
            WHEN t.typname = 'int2' THEN 32767
            WHEN t.typname = 'int4' THEN 2147483647
            WHEN t.typname = 'int8' THEN 9223372036854775807
            ELSE 9223372036854775807
        END AS default_max
    FROM pg_type t
    WHERE t.typname IN ('int2','int4','int8')
),

-- All sequences in the target schema (owned or not). We left-join pg_depend to find ownership when present.
all_sequences AS (
    SELECT
        seq.oid AS seq_oid,
        seq_ns.nspname AS seq_schema,
        seq.relname AS seq_name,
        ps.seqstart,
        ps.seqincrement,
        ps.seqmin,
        ps.seqmax,
        ps.seqcache,
        ps.seqcycle,
        ps.seqtypid,
        -- Ownership info (may be null for unowned sequences)
        tbl_ns.nspname AS table_schema,
        tbl.relname AS table_name,
        att.attname AS col_name
    FROM pg_class seq
    JOIN pg_namespace seq_ns ON seq.relnamespace = seq_ns.oid
    JOIN pg_sequence ps ON ps.seqrelid = seq.oid
    LEFT JOIN pg_depend dep ON dep.objid = seq.oid AND dep.deptype = 'a'
    LEFT JOIN pg_class tbl ON dep.refobjid = tbl.oid
    LEFT JOIN pg_namespace tbl_ns ON tbl.relnamespace = tbl_ns.oid
    LEFT JOIN pg_attribute att ON att.attrelid = tbl.oid AND att.attnum = dep.refobjsubid
    JOIN schema_config sc ON seq_ns.nspname = sc.schema_name
    WHERE seq.relkind = 'S'
),

-- Sequence DDLs: owned => full parameters + OWNED BY; unowned => only non-default params
sequence_ddls AS (
    SELECT
        1 AS sort_order,
        seq_schema || '.' || seq_name AS sort_key,
        (
            E'\n-- Sequence: ' || seq_schema || '.' || seq_name || E'\n' ||
            'CREATE SEQUENCE ' || seq_schema || '.' || seq_name || E'\n' ||
            -- If owned by a table -> include full set (always)
            CASE
                WHEN table_schema IS NOT NULL THEN
                    '    START WITH ' || seqstart || E'\n' ||
                    '    INCREMENT BY ' || seqincrement || E'\n' ||
                    '    MINVALUE ' || seqmin || E'\n' ||
                    '    MAXVALUE ' || seqmax || E'\n' ||
                    '    CACHE ' || seqcache || E'\n' ||
                    (CASE WHEN seqcycle THEN '    CYCLE' ELSE '    NO CYCLE' END) || E';' || E'\n' ||
                    'ALTER SEQUENCE ' || seq_schema || '.' || seq_name ||
                    ' OWNED BY ' || table_schema || '.' || table_name || '.' || col_name || ';'
                ELSE
                    -- Unowned: include only non-default params (compare MIN/MAX to type defaults)
                    (
                        -- START: include if not 1
                        (CASE WHEN seqstart::text <> '1' THEN '    START WITH ' || seqstart || E'\n' ELSE '' END) ||
                        (CASE WHEN seqincrement::text <> '1' THEN '    INCREMENT BY ' || seqincrement || E'\n' ELSE '' END) ||
                        -- MINVALUE: include if different from type default
                        (CASE
                            WHEN seqmin IS NOT NULL THEN
                                CASE
                                    WHEN seqtypid IS NOT NULL THEN
                                        CASE WHEN seqmin::text <> COALESCE((SELECT default_min::text FROM type_defaults td WHERE td.typoid = seqtypid), '1') THEN '    MINVALUE ' || seqmin || E'\n' ELSE '' END
                                    ELSE
                                        CASE WHEN seqmin::text <> '1' THEN '    MINVALUE ' || seqmin || E'\n' ELSE '' END
                                END
                            ELSE ''
                        END) ||
                        (CASE
                            WHEN seqmax IS NOT NULL THEN
                                CASE
                                    WHEN seqtypid IS NOT NULL THEN
                                        CASE WHEN seqmax::text <> COALESCE((SELECT default_max::text FROM type_defaults td WHERE td.typoid = seqtypid), '9223372036854775807') THEN '    MAXVALUE ' || seqmax || E'\n' ELSE '' END
                                    ELSE
                                        CASE WHEN seqmax::text <> '9223372036854775807' THEN '    MAXVALUE ' || seqmax || E'\n' ELSE '' END
                                END
                            ELSE ''
                        END) ||
                        (CASE WHEN seqcache::text <> '1' THEN '    CACHE ' || seqcache || E'\n' ELSE '' END) ||
                        (CASE WHEN seqcycle THEN '    CYCLE' ELSE '' END) ||
                        CASE WHEN ( (CASE WHEN seqstart::text <> '1' THEN 1 ELSE 0 END) +
                                     (CASE WHEN seqincrement::text <> '1' THEN 1 ELSE 0 END) +
                                     (CASE WHEN seqcache::text <> '1' THEN 1 ELSE 0 END) +
                                     (CASE WHEN seqcycle THEN 1 ELSE 0 END) +
                                     (CASE WHEN seqmin IS NOT NULL AND (CASE WHEN seqtypid IS NOT NULL THEN (CASE WHEN seqmin::text <> COALESCE((SELECT default_min::text FROM type_defaults td WHERE td.typoid = seqtypid), '1') ELSE (CASE WHEN seqmin::text <> '1' THEN 1 ELSE 0 END) END) ELSE (CASE WHEN seqmin::text <> '1' THEN 1 ELSE 0 END) END) ELSE 0 END) +
                                     (CASE WHEN seqmax IS NOT NULL AND (CASE WHEN seqtypid IS NOT NULL THEN (CASE WHEN seqmax::text <> COALESCE((SELECT default_max::text FROM type_defaults td WHERE td.typoid = seqtypid), '9223372036854775807') ELSE (CASE WHEN seqmax::text <> '9223372036854775807' THEN 1 ELSE 0 END) END) ELSE (CASE WHEN seqmax::text <> '9223372036854775807' THEN 1 ELSE 0 END) END) ELSE 0 END
                                   ) = 0
                        THEN -- no non-default parts included: emit a simple semicolon
                            E';'
                        ELSE
                            E';'
                        END
                    )
            END
        ) AS ddl_statement
    FROM all_sequences
),

-- CREATE TABLE statements (identity uses owned sequences only; include only non-default identity params)
table_ddls AS (
    SELECT
        2 AS sort_order,
        t.table_name AS sort_key,
        E'\n-- Table: ' || t.table_schema || '.' || t.table_name || E'\n' ||
        'CREATE TABLE ' || t.table_schema || '.' || t.table_name || E' (' || E'\n' ||
        string_agg(
            '    ' || c.column_name || ' ' ||
            c.type_str ||
            COALESCE(c.identity_clause, c.null_default, ''),
            E',\n' ORDER BY c.ordinal_position
        ) || E'\n);'
        AS ddl_statement
    FROM (
        SELECT
            cols.table_schema,
            cols.table_name,
            cols.ordinal_position,
            cols.column_name,

            -- normalized type string
            CASE
                WHEN cols.udt_name LIKE '_%' THEN cols.udt_name
                WHEN cols.data_type = 'USER-DEFINED' THEN cols.udt_name
                WHEN cols.data_type = 'ARRAY' THEN cols.udt_name
                WHEN cols.data_type IN ('smallint','integer','bigint','int2','int4','int8') THEN
                    CASE
                        WHEN cols.udt_name = 'int2' THEN 'smallint'
                        WHEN cols.udt_name = 'int4' THEN 'integer'
                        WHEN cols.udt_name = 'int8' THEN 'bigint'
                        ELSE cols.data_type
                    END
                WHEN cols.data_type IN ('numeric','decimal') THEN
                    cols.data_type ||
                    CASE
                        WHEN cols.numeric_precision IS NOT NULL AND cols.numeric_scale IS NOT NULL THEN '(' || cols.numeric_precision || ',' || cols.numeric_scale || ')'
                        WHEN cols.numeric_precision IS NOT NULL THEN '(' || cols.numeric_precision || ')'
                        ELSE ''
                    END
                WHEN cols.character_maximum_length IS NOT NULL THEN
                    cols.data_type || '(' || cols.character_maximum_length || ')'
                ELSE cols.data_type
            END AS type_str,

            -- identity clause (look up owned sequence for this column)
            CASE WHEN cols.is_identity = 'YES' THEN
                (
                    -- find the owned sequence for this column (all_sequences where table_schema/table_name/col_name match)
                    WITH seq_info AS (
                        SELECT s.*
                        FROM all_sequences s
                        WHERE s.table_schema = cols.table_schema
                          AND s.table_name = cols.table_name
                          AND s.col_name = cols.column_name
                        LIMIT 1
                    )
                    SELECT
                        ' GENERATED ' || cols.identity_generation || ' AS IDENTITY (' ||
                        -- SEQUENCE NAME (qualified)
                        (SELECT 'SEQUENCE NAME ' || seq_schema || '.' || seq_name FROM seq_info) ||
                        -- include only non-default identity params (START != 1, INCREMENT != 1, MIN/MAX != type-default, CACHE !=1, CYCLE true)
                        COALESCE((SELECT CASE WHEN seqstart::text <> '1' THEN E'\n    START WITH ' || seqstart ELSE '' END FROM seq_info), '') ||
                        COALESCE((SELECT CASE WHEN seqincrement::text <> '1' THEN E'\n    INCREMENT BY ' || seqincrement ELSE '' END FROM seq_info), '') ||
                        COALESCE((SELECT CASE WHEN (seqmin::text IS NOT NULL AND seqtypid IS NOT NULL AND seqmin::text <> (SELECT default_min::text FROM type_defaults td WHERE td.typoid = seqtypid)) THEN E'\n    MINVALUE ' || seqmin ELSE '' END FROM seq_info), '') ||
                        COALESCE((SELECT CASE WHEN (seqmax::text IS NOT NULL AND seqtypid IS NOT NULL AND seqmax::text <> (SELECT default_max::text FROM type_defaults td WHERE td.typoid = seqtypid)) THEN E'\n    MAXVALUE ' || seqmax ELSE '' END FROM seq_info), '') ||
                        COALESCE((SELECT CASE WHEN seqcache::text <> '1' THEN E'\n    CACHE ' || seqcache ELSE '' END FROM seq_info), '') ||
                        COALESCE((SELECT CASE WHEN seqcycle THEN E'\n    CYCLE' ELSE '' END FROM seq_info), '') ||
                        E'\n)'
                )
            ELSE NULL END AS identity_clause,

            -- non-identity null/default
            CASE WHEN cols.is_identity = 'YES' THEN NULL
                 ELSE (CASE WHEN cols.is_nullable = 'NO' THEN ' NOT NULL' ELSE '' END) || (CASE WHEN cols.column_default IS NOT NULL THEN ' DEFAULT ' || cols.column_default ELSE '' END)
            END AS null_default

        FROM information_schema.columns cols
        JOIN schema_config sc ON cols.table_schema = sc.schema_name
        WHERE cols.table_schema = sc.schema_name
    ) c
    JOIN (
        SELECT table_schema, table_name FROM information_schema.tables
        WHERE table_schema = (SELECT schema_name FROM schema_config) AND table_type = 'BASE TABLE'
    ) t ON t.table_schema = c.table_schema AND t.table_name = c.table_name
    GROUP BY t.table_schema, t.table_name
),

-- PRIMARY KEYS
pk_ddls AS (
    SELECT
        3 AS sort_order,
        cls.relname AS sort_key,
        E'\nALTER TABLE ' || nsp.nspname || '.' || cls.relname ||
        E'\n  ADD CONSTRAINT ' || con.conname ||
        E'\n  PRIMARY KEY (' ||
        (
            SELECT string_agg(att.attname, ', ' ORDER BY array_position(con.conkey, att.attnum))
            FROM pg_attribute att
            WHERE att.attrelid = cls.oid AND att.attnum = ANY(con.conkey)
        ) ||
        ');' AS ddl_statement
    FROM pg_constraint con
    JOIN pg_class cls ON con.conrelid = cls.oid
    JOIN pg_namespace nsp ON cls.relnamespace = nsp.oid
    JOIN schema_config sc ON nsp.nspname = sc.schema_name
    WHERE con.contype = 'p' AND nsp.nspname = sc.schema_name
),

-- FOREIGN KEYS
fk_ddls AS (
    SELECT
        4 AS sort_order,
        cls.relname AS sort_key,
        E'\nALTER TABLE ' || nsp.nspname || '.' || cls.relname ||
        E'\n  ADD CONSTRAINT ' || con.conname ||
        E'\n  FOREIGN KEY (' ||
        (
            SELECT string_agg(att.attname, ', ' ORDER BY array_position(con.conkey, att.attnum))
            FROM pg_attribute att
            WHERE att.attrelid = cls.oid AND att.attnum = ANY(con.conkey)
        ) || ')' ||
        E'\n  REFERENCES ' || fnsp.nspname || '.' || fcls.relname || ' (' ||
        (
            SELECT string_agg(fatt.attname, ', ' ORDER BY array_position(con.confkey, fatt.attnum))
            FROM pg_attribute fatt
            WHERE fatt.attrelid = fcls.oid AND fatt.attnum = ANY(con.confkey)
        ) || ')' ||
        CASE WHEN con.confupdtype = 'c' THEN E'\n  ON UPDATE CASCADE'
             WHEN con.confupdtype = 'n' THEN E'\n  ON UPDATE SET NULL'
             WHEN con.confupdtype = 'd' THEN E'\n  ON UPDATE SET DEFAULT'
             WHEN con.confupdtype = 'r' THEN E'\n  ON UPDATE RESTRICT' ELSE '' END ||
        CASE WHEN con.confdeltype = 'c' THEN E'\n  ON DELETE CASCADE'
             WHEN con.confdeltype = 'n' THEN E'\n  ON DELETE SET NULL'
             WHEN con.confdeltype = 'd' THEN E'\n  ON DELETE SET DEFAULT'
             WHEN con.confdeltype = 'r' THEN E'\n  ON DELETE RESTRICT' ELSE '' END ||
        ';' AS ddl_statement
    FROM pg_constraint con
    JOIN pg_class cls ON con.conrelid = cls.oid
    JOIN pg_namespace nsp ON cls.relnamespace = nsp.oid
    JOIN pg_class fcls ON con.confrelid = fcls.oid
    JOIN pg_namespace fnsp ON fcls.relnamespace = fnsp.oid
    JOIN schema_config sc ON nsp.nspname = sc.schema_name
    WHERE con.contype = 'f' AND nsp.nspname = sc.schema_name
),

-- UNIQUE CONSTRAINTS
unique_ddls AS (
    SELECT
        5 AS sort_order,
        cls.relname AS sort_key,
        E'\nALTER TABLE ' || nsp.nspname || '.' || cls.relname ||
        E'\n  ADD CONSTRAINT ' || con.conname ||
        E'\n  UNIQUE (' ||
        (
            SELECT string_agg(att.attname, ', ' ORDER BY array_position(con.conkey, att.attnum))
            FROM pg_attribute att
            WHERE att.attrelid = cls.oid AND att.attnum = ANY(con.conkey)
        ) || ');' AS ddl_statement
    FROM pg_constraint con
    JOIN pg_class cls ON con.conrelid = cls.oid
    JOIN pg_namespace nsp ON cls.relnamespace = nsp.oid
    JOIN schema_config sc ON nsp.nspname = sc.schema_name
    WHERE con.contype = 'u' AND nsp.nspname = sc.schema_name
),

-- CHECK CONSTRAINTS
check_ddls AS (
    SELECT
        6 AS sort_order,
        cls.relname AS sort_key,
        E'\nALTER TABLE ' || nsp.nspname || '.' || cls.relname ||
        E'\n  ADD CONSTRAINT ' || con.conname ||
        E'\n  ' || pg_get_constraintdef(con.oid) || ';' AS ddl_statement
    FROM pg_constraint con
    JOIN pg_class cls ON con.conrelid = cls.oid
    JOIN pg_namespace nsp ON cls.relnamespace = nsp.oid
    JOIN schema_config sc ON nsp.nspname = sc.schema_name
    WHERE con.contype = 'c' AND nsp.nspname = sc.schema_name
),

-- INDEXES
index_ddls AS (
    SELECT
        7 AS sort_order,
        cls.relname AS sort_key,
        E'\n' || pg_get_indexdef(idx.indexrelid) || ';' AS ddl_statement
    FROM pg_index idx
    JOIN pg_class cls ON idx.indrelid = cls.oid
    JOIN pg_namespace nsp ON cls.relnamespace = nsp.oid
    JOIN schema_config sc ON nsp.nspname = sc.schema_name
    WHERE NOT idx.indisprimary AND nsp.nspname = sc.schema_name
),

-- TABLE COMMENTS
table_comments AS (
    SELECT
        8 AS sort_order,
        cls.relname AS sort_key,
        E'\nCOMMENT ON TABLE ' || nsp.nspname || '.' || cls.relname ||
        E'\n  IS ' || quote_literal(pg_catalog.obj_description(cls.oid, 'pg_class')) || ';' AS ddl_statement
    FROM pg_class cls
    JOIN pg_namespace nsp ON cls.relnamespace = nsp.oid
    JOIN schema_config sc ON nsp.nspname = sc.schema_name
    WHERE cls.relkind = 'r' AND nsp.nspname = sc.schema_name AND pg_catalog.obj_description(cls.oid, 'pg_class') IS NOT NULL
),

-- COLUMN COMMENTS
column_comments AS (
    SELECT
        9 AS sort_order,
        cls.relname AS sort_key,
        E'\nCOMMENT ON COLUMN ' || nsp.nspname || '.' || cls.relname || '.' || att.attname ||
        E'\n  IS ' || quote_literal(pg_catalog.col_description(cls.oid, att.attnum)) || ';' AS ddl_statement
    FROM pg_class cls
    JOIN pg_namespace nsp ON cls.relnamespace = nsp.oid
    JOIN pg_attribute att ON att.attrelid = cls.oid
    JOIN schema_config sc ON nsp.nspname = sc.schema_name
    WHERE cls.relkind = 'r' AND nsp.nspname = sc.schema_name AND att.attnum > 0 AND NOT att.attisdropped
      AND pg_catalog.col_description(cls.oid, att.attnum) IS NOT NULL
)

SELECT ddl_statement
FROM (
    SELECT * FROM sequence_ddls
    UNION ALL SELECT * FROM table_ddls
    UNION ALL SELECT * FROM pk_ddls
    UNION ALL SELECT * FROM fk_ddls
    UNION ALL SELECT * FROM unique_ddls
    UNION ALL SELECT * FROM check_ddls
    UNION ALL SELECT * FROM index_ddls
    UNION ALL SELECT * FROM table_comments
    UNION ALL SELECT * FROM column_comments
) all_ddls
ORDER BY sort_order, sort_key;
