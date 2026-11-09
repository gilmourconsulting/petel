-- =============================================================================
-- PetelAssistants — Year-scoped institution roster on institution_year_settings
-- 1. Add name/symbol/type/level/special-ed columns
-- 2. Backfill from institutions for current (+ previous) years + entitlement pairs
-- 3. Unique indexes per year for name and symbol
-- 4. Security actions: copy-from-previous + first-time upload + year hub card
-- 5. Deactivate side-menu org_units (entry is year hub only)
-- Idempotent — safe to run multiple times.
-- =============================================================================

DO $$
BEGIN
    IF EXISTS (
        SELECT FROM pg_tables
        WHERE schemaname = 'assist_schema' AND tablename = 'institution_year_settings'
    ) THEN
        ALTER TABLE assist_schema.institution_year_settings
            ADD COLUMN IF NOT EXISTS name VARCHAR(200) NULL,
            ADD COLUMN IF NOT EXISTS symbol VARCHAR(20) NULL,
            ADD COLUMN IF NOT EXISTS institution_type VARCHAR(20) NULL,
            ADD COLUMN IF NOT EXISTS school_level VARCHAR(20) NULL,
            ADD COLUMN IF NOT EXISTS is_special_education BOOLEAN NOT NULL DEFAULT FALSE;

        RAISE NOTICE 'Year-roster columns ensured on institution_year_settings';
    ELSE
        RAISE EXCEPTION 'assist_schema.institution_year_settings does not exist — run add-institution-year-settings.sql first';
    END IF;
END $$;

-- Backfill attributes onto existing calendar rows from master institutions
UPDATE assist_schema.institution_year_settings s
SET
    name = COALESCE(s.name, i.name),
    symbol = COALESCE(s.symbol, i.symbol),
    institution_type = COALESCE(s.institution_type, i.institution_type),
    school_level = CASE
        WHEN COALESCE(s.institution_type, i.institution_type) = 'kindergarten' THEN NULL
        ELSE COALESCE(s.school_level, i.school_level)
    END,
    is_special_education = COALESCE(s.is_special_education, i.is_special_education, FALSE)
FROM assist_schema.institutions i
WHERE s.institution_id = i.id
  AND (
      s.name IS NULL
      OR s.institution_type IS NULL
      OR (s.symbol IS NULL AND i.symbol IS NOT NULL)
      OR (s.school_level IS NULL AND i.school_level IS NOT NULL AND COALESCE(s.institution_type, i.institution_type) <> 'kindergarten')
  );

-- Ensure year roster rows for EVERY institution on the CURRENT Hebrew year
INSERT INTO assist_schema.institution_year_settings (
    entity_id, institution_id, hebrew_year_id,
    name, symbol, institution_type, school_level, is_special_education,
    school_days_per_week, weekly_hours, has_extended_school_day,
    created_at, user_id, updated_at, update_user
)
SELECT
    i.entity_id,
    i.id,
    y.id,
    i.name,
    i.symbol,
    i.institution_type,
    CASE WHEN i.institution_type = 'kindergarten' THEN NULL ELSE i.school_level END,
    i.is_special_education,
    0,
    0,
    FALSE,
    CURRENT_TIMESTAMP,
    NULL::integer,
    CURRENT_TIMESTAMP,
    NULL::integer
FROM assist_schema.institutions i
CROSS JOIN shared_schema.hebrew_years y
WHERE y.is_current = TRUE
  AND NOT EXISTS (
      SELECT 1
      FROM assist_schema.institution_year_settings s
      WHERE s.entity_id = i.entity_id
        AND s.institution_id = i.id
        AND s.hebrew_year_id = y.id
  );

-- Also ensure previous-year roster rows exist (supports copy-from-previous)
INSERT INTO assist_schema.institution_year_settings (
    entity_id, institution_id, hebrew_year_id,
    name, symbol, institution_type, school_level, is_special_education,
    school_days_per_week, weekly_hours, has_extended_school_day,
    created_at, user_id, updated_at, update_user
)
SELECT
    i.entity_id,
    i.id,
    y.id,
    i.name,
    i.symbol,
    i.institution_type,
    CASE WHEN i.institution_type = 'kindergarten' THEN NULL ELSE i.school_level END,
    i.is_special_education,
    0,
    0,
    FALSE,
    CURRENT_TIMESTAMP,
    NULL::integer,
    CURRENT_TIMESTAMP,
    NULL::integer
FROM assist_schema.institutions i
CROSS JOIN shared_schema.hebrew_years y
WHERE y.is_previous = TRUE
  AND NOT EXISTS (
      SELECT 1
      FROM assist_schema.institution_year_settings s
      WHERE s.entity_id = i.entity_id
        AND s.institution_id = i.id
        AND s.hebrew_year_id = y.id
  );

-- Ensure year roster rows for any institution×year already used by entitlements
INSERT INTO assist_schema.institution_year_settings (
    entity_id, institution_id, hebrew_year_id,
    name, symbol, institution_type, school_level, is_special_education,
    school_days_per_week, weekly_hours, has_extended_school_day,
    created_at, user_id, updated_at, update_user
)
SELECT DISTINCT
    i.entity_id,
    i.id,
    e.hebrew_year_id,
    i.name,
    i.symbol,
    i.institution_type,
    CASE WHEN i.institution_type = 'kindergarten' THEN NULL ELSE i.school_level END,
    i.is_special_education,
    0,
    0,
    FALSE,
    CURRENT_TIMESTAMP,
    NULL::integer,
    CURRENT_TIMESTAMP,
    NULL::integer
FROM assist_schema.entitlements e
JOIN assist_schema.institutions i ON i.id = e.institution_id
WHERE e.institution_id IS NOT NULL
  AND e.hebrew_year_id IS NOT NULL
  AND NOT EXISTS (
      SELECT 1
      FROM assist_schema.institution_year_settings s
      WHERE s.entity_id = i.entity_id
        AND s.institution_id = i.id
        AND s.hebrew_year_id = e.hebrew_year_id
  );

-- Require roster attribute columns after backfill
DO $$
BEGIN
    -- Fill any remaining null names/types from master (safety)
    UPDATE assist_schema.institution_year_settings s
    SET
        name = COALESCE(s.name, i.name, 'מוסד'),
        institution_type = COALESCE(s.institution_type, i.institution_type, 'school')
    FROM assist_schema.institutions i
    WHERE s.institution_id = i.id
      AND (s.name IS NULL OR s.institution_type IS NULL);

    ALTER TABLE assist_schema.institution_year_settings
        ALTER COLUMN name SET NOT NULL,
        ALTER COLUMN institution_type SET NOT NULL;

    RAISE NOTICE 'institution_year_settings name/institution_type set NOT NULL';
EXCEPTION
    WHEN others THEN
        RAISE NOTICE 'Could not set NOT NULL on roster columns (may already be set): %', SQLERRM;
END $$;

-- Unique name per entity × year
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_indexes
        WHERE schemaname = 'assist_schema'
          AND indexname = 'ux_institution_year_settings_name'
    ) THEN
        CREATE UNIQUE INDEX ux_institution_year_settings_name
            ON assist_schema.institution_year_settings (entity_id, hebrew_year_id, name);
        RAISE NOTICE 'Created ux_institution_year_settings_name';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_indexes
        WHERE schemaname = 'assist_schema'
          AND indexname = 'ux_institution_year_settings_symbol'
    ) THEN
        CREATE UNIQUE INDEX ux_institution_year_settings_symbol
            ON assist_schema.institution_year_settings (entity_id, hebrew_year_id, symbol)
            WHERE symbol IS NOT NULL;
        RAISE NOTICE 'Created ux_institution_year_settings_symbol';
    END IF;
END $$;

-- Security actions
DO $$
DECLARE
    v_button_type_id      INTEGER;
    v_action_id           INTEGER;
    v_role_rec            RECORD;
    v_action_name         TEXT;
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
        ('org_units_copy_from_previous', 'העתקת מוסדות משנה קודמת', 'org_units', 'העתקת רשימת מוסדות משנת הלימודים הקודמת לשנה ריקה', v_button_type_id),
        ('org_units_upload',             'העלאת מוסדות מקובץ',       'org_units', 'העלאת רשימת מוסדות לשנה ריקה מקובץ Excel/CSV', v_button_type_id),
        ('yearmanagement_org_units',     'מעבר לבתי ספר וגנים',      'yearmanagement', 'כרטיס מעבר לניהול מוסדות לשנת לימודים', v_button_type_id)
    ON CONFLICT (name) DO NOTHING;

    FOREACH v_action_name IN ARRAY ARRAY[
        'org_units_copy_from_previous',
        'org_units_upload',
        'yearmanagement_org_units'
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

    RAISE NOTICE 'Institution year roster security actions seeded';
END $$;

-- Side menu entry removed: institutions are opened only from Year Management
UPDATE shared_schema.menu_items
SET is_active = FALSE
WHERE name = 'org_units'
  AND is_active = TRUE;
