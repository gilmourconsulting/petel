-- =============================================================================
-- PetelAssistants — Monthly operations: mappings, summaries, salary anomalies
-- Creates:
--   shared_schema.statuses (+ salary_anomaly seed)
--   assist_schema.salary_department_mappings
--   shared_schema.meitar_topics.assistant_type_id
--   assist_schema.salary_month_summaries
--   assist_schema.meitar_month_summaries
--   assist_schema.salary_anomalies
-- Seeds security actions for mapping / summary / anomaly debug screens.
-- Idempotent — safe to run multiple times.
-- =============================================================================

-- ─── 1. shared_schema.statuses ───────────────────────────────────────────────
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT FROM pg_tables
        WHERE schemaname = 'shared_schema' AND tablename = 'statuses'
    ) THEN
        CREATE TABLE shared_schema.statuses (
            id          SERIAL PRIMARY KEY,
            object      VARCHAR(50)  NOT NULL,
            code        VARCHAR(50)  NOT NULL,
            name        VARCHAR(100) NOT NULL,
            sort_order  INTEGER      NOT NULL DEFAULT 0,
            is_active   BOOLEAN      NOT NULL DEFAULT true,
            CONSTRAINT statuses_object_code_unique UNIQUE (object, code)
        );

        RAISE NOTICE 'Table shared_schema.statuses created';
    ELSE
        RAISE NOTICE 'Table shared_schema.statuses already exists';
    END IF;
END $$;

INSERT INTO shared_schema.statuses (object, code, name, sort_order, is_active)
SELECT v.object, v.code, v.name, v.sort_order, true
FROM (VALUES
    ('salary_anomaly', 'new',     'חדש',  1),
    ('salary_anomaly', 'settled', 'טופל', 2),
    ('salary_anomaly', 'note',    'הערה', 3)
) AS v(object, code, name, sort_order)
WHERE NOT EXISTS (
    SELECT 1 FROM shared_schema.statuses s
    WHERE s.object = v.object AND s.code = v.code
);

-- ─── 2. meitar_topics.assistant_type_id (cross-system mapping) ───────────────
ALTER TABLE shared_schema.meitar_topics
    ADD COLUMN IF NOT EXISTS assistant_type_id INTEGER NULL;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'meitar_topics_assistant_type_id_fkey'
    ) THEN
        ALTER TABLE shared_schema.meitar_topics
            ADD CONSTRAINT meitar_topics_assistant_type_id_fkey
            FOREIGN KEY (assistant_type_id)
            REFERENCES shared_schema.assistant_types(id)
            ON DELETE SET NULL;
    END IF;
END $$;

-- ─── 3. salary_department_mappings (tenant) ──────────────────────────────────
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT FROM pg_tables
        WHERE schemaname = 'assist_schema' AND tablename = 'salary_department_mappings'
    ) THEN
        CREATE TABLE assist_schema.salary_department_mappings (
            id                  SERIAL PRIMARY KEY,
            entity_id           INTEGER      NOT NULL REFERENCES shared_schema.entities(id) ON DELETE CASCADE,
            department_id       VARCHAR(50)  NOT NULL,
            department_name     VARCHAR(200) NULL,
            assistant_type_id   INTEGER      NOT NULL REFERENCES shared_schema.assistant_types(id) ON DELETE RESTRICT,
            is_active           BOOLEAN      NOT NULL DEFAULT true,
            created_at          TIMESTAMP    NOT NULL DEFAULT CURRENT_TIMESTAMP,
            user_id             INTEGER      NULL REFERENCES assist_schema.users(id) ON DELETE SET NULL,
            updated_at          TIMESTAMP    NOT NULL DEFAULT CURRENT_TIMESTAMP,
            update_user         INTEGER      NULL REFERENCES assist_schema.users(id) ON DELETE SET NULL,
            CONSTRAINT salary_department_mappings_biz_key UNIQUE (entity_id, department_id)
        );

        CREATE INDEX idx_salary_department_mappings_entity_id
            ON assist_schema.salary_department_mappings(entity_id);

        RAISE NOTICE 'Table assist_schema.salary_department_mappings created';
    ELSE
        RAISE NOTICE 'Table assist_schema.salary_department_mappings already exists';
    END IF;
END $$;

-- ─── 4. salary_month_summaries ───────────────────────────────────────────────
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT FROM pg_tables
        WHERE schemaname = 'assist_schema' AND tablename = 'salary_month_summaries'
    ) THEN
        CREATE TABLE assist_schema.salary_month_summaries (
            id                  SERIAL PRIMARY KEY,
            entity_id           INTEGER       NOT NULL REFERENCES shared_schema.entities(id) ON DELETE CASCADE,
            process_id          INTEGER       NOT NULL REFERENCES assist_schema.salary_upload_processes(id) ON DELETE CASCADE,
            period_year         INTEGER       NOT NULL,
            period_month        INTEGER       NOT NULL,
            assistant_type_id   INTEGER       NULL REFERENCES shared_schema.assistant_types(id) ON DELETE SET NULL,
            row_count           INTEGER       NOT NULL DEFAULT 0,
            fte                 NUMERIC(10,2) NOT NULL DEFAULT 0,
            hours               NUMERIC(10,2) NOT NULL DEFAULT 0,
            amount              NUMERIC(14,2) NOT NULL DEFAULT 0,
            yearly_budget_id    INTEGER       NULL REFERENCES assist_schema.yearly_budgets(id) ON DELETE SET NULL,
            budget_fte          NUMERIC(10,2) NULL,
            budget_hours        NUMERIC(10,2) NULL,
            budget_amount       NUMERIC(14,2) NULL,
            has_budget          BOOLEAN       NOT NULL DEFAULT false,
            created_at          TIMESTAMP     NOT NULL DEFAULT CURRENT_TIMESTAMP,
            user_id             INTEGER       NULL REFERENCES assist_schema.users(id) ON DELETE SET NULL,
            updated_at          TIMESTAMP     NOT NULL DEFAULT CURRENT_TIMESTAMP,
            update_user         INTEGER       NULL REFERENCES assist_schema.users(id) ON DELETE SET NULL,
            CONSTRAINT salary_month_summaries_month_check CHECK (period_month BETWEEN 1 AND 12)
        );

        CREATE INDEX idx_salary_month_summaries_entity_period
            ON assist_schema.salary_month_summaries(entity_id, period_year, period_month);

        CREATE INDEX idx_salary_month_summaries_process_id
            ON assist_schema.salary_month_summaries(process_id);

        CREATE UNIQUE INDEX idx_salary_month_summaries_process_type
            ON assist_schema.salary_month_summaries(process_id, assistant_type_id)
            WHERE assistant_type_id IS NOT NULL;

        CREATE UNIQUE INDEX idx_salary_month_summaries_process_unmapped
            ON assist_schema.salary_month_summaries(process_id)
            WHERE assistant_type_id IS NULL;

        RAISE NOTICE 'Table assist_schema.salary_month_summaries created';
    ELSE
        RAISE NOTICE 'Table assist_schema.salary_month_summaries already exists';
    END IF;
END $$;

-- ─── 5. meitar_month_summaries ───────────────────────────────────────────────
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT FROM pg_tables
        WHERE schemaname = 'assist_schema' AND tablename = 'meitar_month_summaries'
    ) THEN
        CREATE TABLE assist_schema.meitar_month_summaries (
            id                  SERIAL PRIMARY KEY,
            entity_id           INTEGER       NOT NULL REFERENCES shared_schema.entities(id) ON DELETE CASCADE,
            process_id          INTEGER       NOT NULL REFERENCES assist_schema.meitar_retrieve_processes(id) ON DELETE CASCADE,
            period_year         INTEGER       NOT NULL,
            period_month        INTEGER       NOT NULL,
            assistant_type_id   INTEGER       NULL REFERENCES shared_schema.assistant_types(id) ON DELETE SET NULL,
            row_count           INTEGER       NOT NULL DEFAULT 0,
            fte                 NUMERIC(10,2) NOT NULL DEFAULT 0,
            hours               NUMERIC(14,4) NOT NULL DEFAULT 0,
            amount              NUMERIC(14,2) NOT NULL DEFAULT 0,
            yearly_budget_id    INTEGER       NULL REFERENCES assist_schema.yearly_budgets(id) ON DELETE SET NULL,
            budget_fte          NUMERIC(10,2) NULL,
            budget_hours        NUMERIC(10,2) NULL,
            budget_amount       NUMERIC(14,2) NULL,
            has_budget          BOOLEAN       NOT NULL DEFAULT false,
            created_at          TIMESTAMP     NOT NULL DEFAULT CURRENT_TIMESTAMP,
            user_id             INTEGER       NULL REFERENCES assist_schema.users(id) ON DELETE SET NULL,
            updated_at          TIMESTAMP     NOT NULL DEFAULT CURRENT_TIMESTAMP,
            update_user         INTEGER       NULL REFERENCES assist_schema.users(id) ON DELETE SET NULL,
            CONSTRAINT meitar_month_summaries_month_check CHECK (period_month BETWEEN 1 AND 12)
        );

        CREATE INDEX idx_meitar_month_summaries_entity_period
            ON assist_schema.meitar_month_summaries(entity_id, period_year, period_month);

        CREATE INDEX idx_meitar_month_summaries_process_id
            ON assist_schema.meitar_month_summaries(process_id);

        CREATE UNIQUE INDEX idx_meitar_month_summaries_process_type
            ON assist_schema.meitar_month_summaries(process_id, assistant_type_id)
            WHERE assistant_type_id IS NOT NULL;

        CREATE UNIQUE INDEX idx_meitar_month_summaries_process_unmapped
            ON assist_schema.meitar_month_summaries(process_id)
            WHERE assistant_type_id IS NULL;

        RAISE NOTICE 'Table assist_schema.meitar_month_summaries created';
    ELSE
        RAISE NOTICE 'Table assist_schema.meitar_month_summaries already exists';
    END IF;
END $$;

-- ─── 6. salary_anomalies ─────────────────────────────────────────────────────
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT FROM pg_tables
        WHERE schemaname = 'assist_schema' AND tablename = 'salary_anomalies'
    ) THEN
        CREATE TABLE assist_schema.salary_anomalies (
            id                          SERIAL PRIMARY KEY,
            entity_id                   INTEGER       NOT NULL REFERENCES shared_schema.entities(id) ON DELETE CASCADE,
            process_id                  INTEGER       NOT NULL REFERENCES assist_schema.salary_upload_processes(id) ON DELETE CASCADE,
            salary_id                   INTEGER       NULL REFERENCES assist_schema.salaries(id) ON DELETE SET NULL,
            national_id                 VARCHAR(500)  NOT NULL,
            department_id               VARCHAR(50)   NOT NULL,
            department_name             VARCHAR(200)  NULL,
            position_percentage         NUMERIC(8,2)  NOT NULL,
            total_salary                NUMERIC(14,2) NOT NULL,
            matched_person_id           INTEGER       NULL REFERENCES assist_schema.persons(id) ON DELETE SET NULL,
            matched_allocation_id       INTEGER       NULL REFERENCES assist_schema.entitlement_allocations(id) ON DELETE SET NULL,
            mapped_assistant_type_id    INTEGER       NULL REFERENCES shared_schema.assistant_types(id) ON DELETE SET NULL,
            allocation_assistant_type_id INTEGER      NULL REFERENCES shared_schema.assistant_types(id) ON DELETE SET NULL,
            reason_code                 VARCHAR(50)   NOT NULL,
            message                     VARCHAR(500)  NOT NULL,
            status_id                   INTEGER       NOT NULL REFERENCES shared_schema.statuses(id) ON DELETE RESTRICT,
            notes                       TEXT          NULL,
            created_at                  TIMESTAMP     NOT NULL DEFAULT CURRENT_TIMESTAMP,
            user_id                     INTEGER       NULL REFERENCES assist_schema.users(id) ON DELETE SET NULL,
            updated_at                  TIMESTAMP     NOT NULL DEFAULT CURRENT_TIMESTAMP,
            update_user                 INTEGER       NULL REFERENCES assist_schema.users(id) ON DELETE SET NULL
        );

        CREATE INDEX idx_salary_anomalies_entity_id
            ON assist_schema.salary_anomalies(entity_id);

        CREATE INDEX idx_salary_anomalies_process_id
            ON assist_schema.salary_anomalies(process_id);

        CREATE INDEX idx_salary_anomalies_salary_id
            ON assist_schema.salary_anomalies(salary_id);

        CREATE INDEX idx_salary_anomalies_status_id
            ON assist_schema.salary_anomalies(status_id);

        RAISE NOTICE 'Table assist_schema.salary_anomalies created';
    ELSE
        RAISE NOTICE 'Table assist_schema.salary_anomalies already exists';
    END IF;
END $$;

-- ─── 7. Security actions ─────────────────────────────────────────────────────
DO $$
DECLARE
    v_button_type_id      INTEGER;
    v_page_action_type_id INTEGER;
    v_action_id           INTEGER;
    v_role_rec            RECORD;
    v_action_name         TEXT;
BEGIN
    SELECT id INTO v_button_type_id
    FROM shared_schema.action_types
    WHERE lower(name) IN ('button', 'onclick_button')
    LIMIT 1;

    SELECT id INTO v_page_action_type_id
    FROM shared_schema.action_types
    WHERE lower(name) IN ('page_action', 'page')
    LIMIT 1;

    IF v_button_type_id IS NULL THEN
        RAISE EXCEPTION 'action_types row "button" not found';
    END IF;

    IF v_page_action_type_id IS NULL THEN
        RAISE EXCEPTION 'action_types row "page_action" not found';
    END IF;

    INSERT INTO shared_schema.actions (name, display_name, reference, description, action_type_id)
    VALUES
        ('salary_dept_map_page_action',          'גישה למיפוי מחלקות שכר',           'salary_dept_map',         'גישה לדף מיפוי מחלקות שכר',                    v_page_action_type_id),
        ('salary_dept_map_back',                 'חזרה ממיפוי מחלקות שכר',           'salary_dept_map',         'כפתור חזרה ממיפוי מחלקות שכר',                 v_button_type_id),
        ('salary_dept_map_refresh',              'רענון מיפוי מחלקות שכר',           'salary_dept_map',         'כפתור רענון מיפוי מחלקות שכר',                 v_button_type_id),
        ('salary_dept_map_save',                 'שמירת מיפוי מחלקת שכר',            'salary_dept_map',         'הוספה / עריכה של מיפוי מחלקה',                 v_button_type_id),
        ('salary_month_summary_page_action',     'גישה לסיכום שכר מול תקציב',        'salary_month_summary',    'גישה לדף סיכום שכר מול תקציב',                 v_page_action_type_id),
        ('salary_month_summary_back',            'חזרה מסיכום שכר מול תקציב',        'salary_month_summary',    'כפתור חזרה מסיכום שכר מול תקציב',              v_button_type_id),
        ('salary_month_summary_refresh',         'רענון סיכום שכר מול תקציב',        'salary_month_summary',    'כפתור רענון סיכום שכר מול תקציב',              v_button_type_id),
        ('salary_anomalies_page_action',         'גישה לחריגות שכר',                 'salary_anomalies',        'גישה לדף חריגות שכר',                          v_page_action_type_id),
        ('salary_anomalies_back',                'חזרה מחריגות שכר',                 'salary_anomalies',        'כפתור חזרה מחריגות שכר',                       v_button_type_id),
        ('salary_anomalies_refresh',             'רענון חריגות שכר',                 'salary_anomalies',        'כפתור רענון חריגות שכר',                       v_button_type_id),
        ('salary_anomalies_status',              'עדכון סטטוס חריגת שכר',            'salary_anomalies',        'שינוי סטטוס / הערה לחריגת שכר',                v_button_type_id),
        ('meitar_month_summary_page_action',     'גישה לסיכום מיתר מול תקציב',       'meitar_month_summary',    'גישה לדף סיכום מיתר מול תקציב',               v_page_action_type_id),
        ('meitar_month_summary_back',            'חזרה מסיכום מיתר מול תקציב',       'meitar_month_summary',    'כפתור חזרה מסיכום מיתר מול תקציב',            v_button_type_id),
        ('meitar_month_summary_refresh',         'רענון סיכום מיתר מול תקציב',       'meitar_month_summary',    'כפתור רענון סיכום מיתר מול תקציב',            v_button_type_id),
        ('salaries_month_summary',               'סיכום שכר מול תקציב',              'salaries',                'מעבר לסיכום שכר מול תקציב ממסך נתוני שכר',    v_button_type_id),
        ('salaries_anomalies',                   'חריגות שכר',                       'salaries',                'מעבר לחריגות שכר ממסך נתוני שכר',              v_button_type_id),
        ('salaries_dept_map',                    'מיפוי מחלקות שכר',                 'salaries',                'מעבר למיפוי מחלקות שכר ממסך נתוני שכר',        v_button_type_id),
        ('meitardata_month_summary',             'סיכום מיתר מול תקציב',             'meitardata',              'מעבר לסיכום מיתר מול תקציב ממסך נתוני מיתר', v_button_type_id),
        ('yearmanagement_salary_dept_map',       'מיפוי מחלקות שכר',                 'yearmanagement',          'מעבר למיפוי מחלקות שכר מניהול שנה',           v_button_type_id),
        ('yearmanagement_salary_month_summary',  'סיכום שכר מול תקציב',              'yearmanagement',          'מעבר לסיכום שכר מול תקציב מניהול שנה',         v_button_type_id),
        ('yearmanagement_salary_anomalies',      'חריגות שכר',                       'yearmanagement',          'מעבר לחריגות שכר מניהול שנה',                  v_button_type_id),
        ('yearmanagement_meitar_month_summary',  'סיכום מיתר מול תקציב',             'yearmanagement',          'מעבר לסיכום מיתר מול תקציב מניהול שנה',       v_button_type_id),
        ('maindashboard_salary_dept_map',        'מיפוי מחלקות שכר',                 'maindashboard',           'מעבר למיפוי מחלקות שכר מדף הבית',              v_button_type_id)
    ON CONFLICT (name) DO NOTHING;

    FOREACH v_action_name IN ARRAY ARRAY[
        'salary_dept_map_page_action',
        'salary_dept_map_back',
        'salary_dept_map_refresh',
        'salary_dept_map_save',
        'salary_month_summary_page_action',
        'salary_month_summary_back',
        'salary_month_summary_refresh',
        'salary_anomalies_page_action',
        'salary_anomalies_back',
        'salary_anomalies_refresh',
        'salary_anomalies_status',
        'meitar_month_summary_page_action',
        'meitar_month_summary_back',
        'meitar_month_summary_refresh',
        'salaries_month_summary',
        'salaries_anomalies',
        'salaries_dept_map',
        'meitardata_month_summary',
        'yearmanagement_salary_dept_map',
        'yearmanagement_salary_month_summary',
        'yearmanagement_salary_anomalies',
        'yearmanagement_meitar_month_summary',
        'maindashboard_salary_dept_map'
    ]
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

    RAISE NOTICE 'Monthly ops security actions seeded';
END $$;
