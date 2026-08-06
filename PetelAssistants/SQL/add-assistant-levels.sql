-- =============================================================================
-- PetelAssistants — Assistant levels lookup (רמות סייעת)
-- shared_schema.assistant_levels stores English codes + Hebrew display names.
-- assistant_types.level continues to store the code (used by entitlement logic).
-- Idempotent — safe to run multiple times.
-- =============================================================================

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT FROM pg_tables
        WHERE schemaname = 'shared_schema' AND tablename = 'assistant_levels'
    ) THEN
        CREATE TABLE shared_schema.assistant_levels (
            id           SERIAL PRIMARY KEY,
            code         VARCHAR(30) NOT NULL UNIQUE,
            display_name VARCHAR(100) NOT NULL,
            sort_order   INTEGER NOT NULL DEFAULT 0,
            is_active    BOOLEAN NOT NULL DEFAULT true
        );
        RAISE NOTICE 'Table shared_schema.assistant_levels created';
    ELSE
        RAISE NOTICE 'Table shared_schema.assistant_levels already exists';
    END IF;
END $$;

INSERT INTO shared_schema.assistant_levels (code, display_name, sort_order)
SELECT v.code, v.display_name, v.sort_order
FROM (VALUES
    ('personal',     E'\u05d0\u05d9\u05e9\u05d9',           10),
    ('class',        E'\u05db\u05d9\u05ea\u05ea\u05d9',     20),
    ('school',       E'\u05d1\u05d9\u05ea \u05e1\u05e4\u05e8\u05d9', 30),
    ('kindergarten', E'\u05d2\u05df',                       40)
) AS v(code, display_name, sort_order)
WHERE NOT EXISTS (
    SELECT 1 FROM shared_schema.assistant_levels al WHERE al.code = v.code
);

-- Optional FK: assistant_types.level → assistant_levels.code (nullable)
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'fk_assistant_types_level'
    ) THEN
        -- Clear orphan codes before adding FK
        UPDATE shared_schema.assistant_types at
        SET level = NULL
        WHERE level IS NOT NULL
          AND NOT EXISTS (
              SELECT 1 FROM shared_schema.assistant_levels al WHERE al.code = at.level
          );

        ALTER TABLE shared_schema.assistant_types
            ADD CONSTRAINT fk_assistant_types_level
            FOREIGN KEY (level) REFERENCES shared_schema.assistant_levels(code)
            ON DELETE SET NULL
            ON UPDATE CASCADE;
        RAISE NOTICE 'FK fk_assistant_types_level added';
    END IF;
END $$;
