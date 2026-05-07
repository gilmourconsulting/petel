-- SQL/add-excel-reports.sql
-- Excel Report Generation System - Phase 1 Foundation
-- Run on all environments. Idempotent (safe to re-run).

DO $$
BEGIN
    -- =====================================================================
    -- 1. excel_report_definitions
    -- =====================================================================
    IF NOT EXISTS (
        SELECT FROM pg_tables
        WHERE schemaname = 'petel_schema' AND tablename = 'excel_report_definitions'
    ) THEN
        CREATE TABLE petel_schema.excel_report_definitions (
            id                      SERIAL PRIMARY KEY,
            name                    VARCHAR(150) NOT NULL,
            description             VARCHAR(500) NULL,
            report_type             VARCHAR(30)  NOT NULL
                                        CHECK (report_type IN ('query_builder','advanced_sql','template')),
            allow_cross_year        BOOLEAN NOT NULL DEFAULT false,
            requires_entity_context BOOLEAN NOT NULL DEFAULT true,
            is_active               BOOLEAN NOT NULL DEFAULT true,
            sort_order              INTEGER NOT NULL DEFAULT 0,
            required_action_id      INTEGER NULL
                                        REFERENCES petel_schema.system_actions(id) ON DELETE SET NULL,
            created_at              TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
            created_user            INTEGER   NULL REFERENCES petel_schema.users(id) ON DELETE SET NULL,
            updated_at              TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
            update_user             INTEGER   NULL REFERENCES petel_schema.users(id) ON DELETE SET NULL
        );

        CREATE INDEX idx_excel_report_definitions_active
            ON petel_schema.excel_report_definitions(is_active);
        CREATE INDEX idx_excel_report_definitions_type
            ON petel_schema.excel_report_definitions(report_type);

        RAISE NOTICE 'Table excel_report_definitions created';
    ELSE
        RAISE NOTICE 'Table excel_report_definitions already exists';
    END IF;

    -- =====================================================================
    -- 2. excel_report_queries
    -- =====================================================================
    IF NOT EXISTS (
        SELECT FROM pg_tables
        WHERE schemaname = 'petel_schema' AND tablename = 'excel_report_queries'
    ) THEN
        CREATE TABLE petel_schema.excel_report_queries (
            id          SERIAL PRIMARY KEY,
            report_id   INTEGER NOT NULL
                            REFERENCES petel_schema.excel_report_definitions(id) ON DELETE CASCADE,
            entity_name VARCHAR(100) NULL,   -- null for advanced_sql type
            fields_json TEXT NOT NULL DEFAULT '[]',
                -- JSON array: [{field, label_override}]
            filters_json TEXT NOT NULL DEFAULT '[]',
                -- JSON array: [{field, operator, value, param_name}]
            sort_json   TEXT NOT NULL DEFAULT '[]',
                -- JSON array: [{field, direction}]
            sql_query   TEXT NULL,           -- null for query_builder type
            sheet_name  VARCHAR(100) NOT NULL DEFAULT 'נתונים',
            created_at  TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
            updated_at  TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
            CONSTRAINT uq_report_query UNIQUE (report_id)
        );

        CREATE INDEX idx_excel_report_queries_report_id
            ON petel_schema.excel_report_queries(report_id);

        RAISE NOTICE 'Table excel_report_queries created';
    ELSE
        RAISE NOTICE 'Table excel_report_queries already exists';
    END IF;

    -- =====================================================================
    -- 3. excel_report_templates
    -- =====================================================================
    IF NOT EXISTS (
        SELECT FROM pg_tables
        WHERE schemaname = 'petel_schema' AND tablename = 'excel_report_templates'
    ) THEN
        CREATE TABLE petel_schema.excel_report_templates (
            id                  SERIAL PRIMARY KEY,
            report_id           INTEGER NOT NULL
                                    REFERENCES petel_schema.excel_report_definitions(id) ON DELETE CASCADE,
            template_filename   VARCHAR(255) NOT NULL,
            template_blob       BYTEA NOT NULL,
            cell_mappings_json  TEXT NOT NULL DEFAULT '[]',
                -- JSON array: [{placeholder, entity_name, field_name, is_collection}]
            created_at          TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
            updated_at          TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
            CONSTRAINT uq_report_template UNIQUE (report_id)
        );

        CREATE INDEX idx_excel_report_templates_report_id
            ON petel_schema.excel_report_templates(report_id);

        RAISE NOTICE 'Table excel_report_templates created';
    ELSE
        RAISE NOTICE 'Table excel_report_templates already exists';
    END IF;

    -- =====================================================================
    -- 4. excel_report_parameters
    -- =====================================================================
    IF NOT EXISTS (
        SELECT FROM pg_tables
        WHERE schemaname = 'petel_schema' AND tablename = 'excel_report_parameters'
    ) THEN
        CREATE TABLE petel_schema.excel_report_parameters (
            id              SERIAL PRIMARY KEY,
            report_id       INTEGER NOT NULL
                                REFERENCES petel_schema.excel_report_definitions(id) ON DELETE CASCADE,
            param_name      VARCHAR(100) NOT NULL,
            param_label_he  VARCHAR(150) NOT NULL,
            param_type      VARCHAR(30)  NOT NULL
                                CHECK (param_type IN (
                                    'year_selector','entity_selector',
                                    'date_range','text','enum'
                                )),
            is_required     BOOLEAN NOT NULL DEFAULT true,
            default_value   VARCHAR(500) NULL,
            options_json    TEXT NULL,   -- JSON array of {value, label} for enum type
            sort_order      INTEGER NOT NULL DEFAULT 0,
            created_at      TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
            updated_at      TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
            CONSTRAINT uq_report_param UNIQUE (report_id, param_name)
        );

        CREATE INDEX idx_excel_report_parameters_report_id
            ON petel_schema.excel_report_parameters(report_id);

        RAISE NOTICE 'Table excel_report_parameters created';
    ELSE
        RAISE NOTICE 'Table excel_report_parameters already exists';
    END IF;

END
$$;
