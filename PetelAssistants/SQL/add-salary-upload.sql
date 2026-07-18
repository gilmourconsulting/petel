-- =============================================================================
-- PetelAssistants — Salary file upload tables and security actions
-- Creates:
--   assist_schema.salary_upload_processes
--   assist_schema.salaries
--   assist_schema.salary_upload_warnings
--   assist_schema.salary_field_mappings
-- Seeds button actions for Main Dashboard and Year Management.
-- Idempotent — safe to run multiple times.
-- =============================================================================

-- ─── 1. salary_upload_processes ───────────────────────────────────────────────
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT FROM pg_tables
        WHERE schemaname = 'assist_schema'
          AND tablename  = 'salary_upload_processes'
    ) THEN
        CREATE TABLE assist_schema.salary_upload_processes (
            id                SERIAL PRIMARY KEY,
            entity_id         INTEGER       NOT NULL REFERENCES shared_schema.entities(id) ON DELETE CASCADE,
            period_year       INTEGER       NOT NULL,
            period_month      INTEGER       NOT NULL,
            row_count         INTEGER       NULL,
            total_salary_sum  NUMERIC(14,2) NULL,
            source            VARCHAR(20)   NOT NULL DEFAULT 'manual',
            file_name         VARCHAR(255)  NULL,
            created_at        TIMESTAMP     NOT NULL DEFAULT CURRENT_TIMESTAMP,
            user_id           INTEGER       NULL REFERENCES assist_schema.users(id) ON DELETE SET NULL,
            updated_at        TIMESTAMP     NOT NULL DEFAULT CURRENT_TIMESTAMP,
            update_user       INTEGER       NULL REFERENCES assist_schema.users(id) ON DELETE SET NULL,
            CONSTRAINT salary_upload_processes_month_check CHECK (period_month BETWEEN 1 AND 12)
        );

        CREATE INDEX idx_salary_upload_processes_entity_id
            ON assist_schema.salary_upload_processes(entity_id);

        CREATE INDEX idx_salary_upload_processes_period
            ON assist_schema.salary_upload_processes(entity_id, period_year, period_month);

        RAISE NOTICE 'Table assist_schema.salary_upload_processes created';
    ELSE
        RAISE NOTICE 'Table assist_schema.salary_upload_processes already exists';
    END IF;
END $$;

-- ─── 2. salaries ─────────────────────────────────────────────────────────────
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT FROM pg_tables
        WHERE schemaname = 'assist_schema'
          AND tablename  = 'salaries'
    ) THEN
        CREATE TABLE assist_schema.salaries (
            id                      SERIAL PRIMARY KEY,
            entity_id               INTEGER       NOT NULL REFERENCES shared_schema.entities(id) ON DELETE CASCADE,
            period_year             INTEGER       NOT NULL,
            period_month            INTEGER       NOT NULL,
            national_id             VARCHAR(500)  NOT NULL,
            department_id           VARCHAR(50)   NOT NULL,
            department_name         VARCHAR(200)  NULL,
            position_percentage     NUMERIC(8,2)  NOT NULL,
            total_salary            NUMERIC(14,2) NOT NULL,
            matched_person_id       INTEGER       NULL REFERENCES assist_schema.persons(id) ON DELETE SET NULL,
            matched_allocation_id   INTEGER       NULL REFERENCES assist_schema.entitlement_allocations(id) ON DELETE SET NULL,
            has_id_warning          BOOLEAN       NOT NULL DEFAULT false,
            process_id              INTEGER       NOT NULL REFERENCES assist_schema.salary_upload_processes(id) ON DELETE RESTRICT,
            created_at              TIMESTAMP     NOT NULL DEFAULT CURRENT_TIMESTAMP,
            user_id                 INTEGER       NULL REFERENCES assist_schema.users(id) ON DELETE SET NULL,
            updated_at              TIMESTAMP     NOT NULL DEFAULT CURRENT_TIMESTAMP,
            update_user             INTEGER       NULL REFERENCES assist_schema.users(id) ON DELETE SET NULL,
            CONSTRAINT salaries_month_check CHECK (period_month BETWEEN 1 AND 12),
            CONSTRAINT salaries_biz_key UNIQUE (entity_id, period_year, period_month, national_id, department_id)
        );

        CREATE INDEX idx_salaries_entity_id
            ON assist_schema.salaries(entity_id);

        CREATE INDEX idx_salaries_period
            ON assist_schema.salaries(entity_id, period_year, period_month);

        CREATE INDEX idx_salaries_process_id
            ON assist_schema.salaries(process_id);

        RAISE NOTICE 'Table assist_schema.salaries created';
    ELSE
        RAISE NOTICE 'Table assist_schema.salaries already exists';
    END IF;
END $$;

-- ─── 3. salary_upload_warnings ───────────────────────────────────────────────
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT FROM pg_tables
        WHERE schemaname = 'assist_schema'
          AND tablename  = 'salary_upload_warnings'
    ) THEN
        CREATE TABLE assist_schema.salary_upload_warnings (
            id            SERIAL PRIMARY KEY,
            entity_id     INTEGER      NOT NULL REFERENCES shared_schema.entities(id) ON DELETE CASCADE,
            process_id    INTEGER      NOT NULL REFERENCES assist_schema.salary_upload_processes(id) ON DELETE CASCADE,
            salary_id     INTEGER      NOT NULL REFERENCES assist_schema.salaries(id) ON DELETE CASCADE,
            warning_type  VARCHAR(50)  NOT NULL,
            message       VARCHAR(500) NOT NULL,
            created_at    TIMESTAMP    NOT NULL DEFAULT CURRENT_TIMESTAMP,
            user_id       INTEGER      NULL REFERENCES assist_schema.users(id) ON DELETE SET NULL,
            updated_at    TIMESTAMP    NOT NULL DEFAULT CURRENT_TIMESTAMP,
            update_user   INTEGER      NULL REFERENCES assist_schema.users(id) ON DELETE SET NULL
        );

        CREATE INDEX idx_salary_upload_warnings_entity_id
            ON assist_schema.salary_upload_warnings(entity_id);

        CREATE INDEX idx_salary_upload_warnings_process_id
            ON assist_schema.salary_upload_warnings(process_id);

        CREATE INDEX idx_salary_upload_warnings_salary_id
            ON assist_schema.salary_upload_warnings(salary_id);

        RAISE NOTICE 'Table assist_schema.salary_upload_warnings created';
    ELSE
        RAISE NOTICE 'Table assist_schema.salary_upload_warnings already exists';
    END IF;
END $$;

-- ─── 4. salary_field_mappings ────────────────────────────────────────────────
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT FROM pg_tables
        WHERE schemaname = 'assist_schema'
          AND tablename  = 'salary_field_mappings'
    ) THEN
        CREATE TABLE assist_schema.salary_field_mappings (
            id                      SERIAL PRIMARY KEY,
            entity_id               INTEGER      NOT NULL REFERENCES shared_schema.entities(id) ON DELETE CASCADE,
            mapping_json            TEXT         NOT NULL,
            id_includes_check_digit BOOLEAN      NOT NULL DEFAULT true,
            created_at              TIMESTAMP    NOT NULL DEFAULT CURRENT_TIMESTAMP,
            user_id                 INTEGER      NULL REFERENCES assist_schema.users(id) ON DELETE SET NULL,
            updated_at              TIMESTAMP    NOT NULL DEFAULT CURRENT_TIMESTAMP,
            update_user             INTEGER      NULL REFERENCES assist_schema.users(id) ON DELETE SET NULL,
            CONSTRAINT salary_field_mappings_entity_unique UNIQUE (entity_id)
        );

        CREATE INDEX idx_salary_field_mappings_entity_id
            ON assist_schema.salary_field_mappings(entity_id);

        RAISE NOTICE 'Table assist_schema.salary_field_mappings created';
    ELSE
        RAISE NOTICE 'Table assist_schema.salary_field_mappings already exists';
    END IF;
END $$;

-- ─── 5. Security actions ─────────────────────────────────────────────────────
DO $$
DECLARE
    v_button_type_id INTEGER;
    v_action_id      INTEGER;
    v_role_rec       RECORD;
    v_action_name    TEXT;
BEGIN
    SELECT id INTO v_button_type_id
    FROM shared_schema.action_types
    WHERE lower(name) IN ('button', 'onclick_button')
    LIMIT 1;

    IF v_button_type_id IS NULL THEN
        RAISE EXCEPTION 'action_types row "button" not found';
    END IF;

    INSERT INTO shared_schema.actions (name, display_name, reference, description, action_type_id)
    VALUES
        ('maindashboard_salary_upload',  'העלאת קובץ שכר', 'maindashboard',  'כפתור העלאת קובץ שכר מדף הבית', v_button_type_id),
        ('yearmanagement_salary_upload', 'העלאת קובץ שכר', 'yearmanagement', 'כפתור העלאת קובץ שכר ממסך ניהול שנה', v_button_type_id)
    ON CONFLICT (name) DO NOTHING;

    FOREACH v_action_name IN ARRAY ARRAY['maindashboard_salary_upload', 'yearmanagement_salary_upload']
    LOOP
        SELECT id INTO v_action_id
        FROM shared_schema.actions
        WHERE name = v_action_name;

        IF v_action_id IS NOT NULL THEN
            FOR v_role_rec IN
                SELECT id AS role_id, entity_id
                FROM assist_schema.roles
            LOOP
                INSERT INTO assist_schema.roles_actions (entity_id, role_id, action_id)
                SELECT v_role_rec.entity_id, v_role_rec.role_id, v_action_id
                WHERE NOT EXISTS (
                    SELECT 1 FROM assist_schema.roles_actions
                    WHERE role_id = v_role_rec.role_id AND action_id = v_action_id
                );
            END LOOP;
        END IF;
    END LOOP;

    RAISE NOTICE 'Salary upload security actions seeded';
END $$;
