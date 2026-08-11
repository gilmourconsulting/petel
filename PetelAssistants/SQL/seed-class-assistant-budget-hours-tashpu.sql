-- =============================================================================
-- PetelAssistants — Seed class assistant budget hours for תשפו (תשפ"ו)
-- One pricing record per year × school_level × classification (with participation %).
-- Characterization codes match shared_schema.class_classifications.id
--   (fallback: foreign_id) as seeded from ATH special_needs_characterizations.
-- Idempotent — upserts; safe to run multiple times.
-- Requires: add-class-assistant-budget-hours.sql,
--           add-class-assistant-budget-hours-participation.sql,
--           hebrew year תשפו, classifications.
-- =============================================================================

DO $$
DECLARE
    v_year_id INTEGER;
    v_classification_id INTEGER;
    v_code INTEGER;
    v_elementary NUMERIC(10,2);
    v_high_school NUMERIC(10,2);
    v_upserted INTEGER := 0;
    v_missing INTEGER := 0;
    r RECORD;
BEGIN
    SELECT id INTO v_year_id
    FROM shared_schema.hebrew_years
    WHERE hebrew_year IN (
            E'\u05ea\u05e9\u05e4\u05d5',           -- תשפו (seeded form)
            E'\u05ea\u05e9\u05e4"\u05d5',          -- תשפ"ו
            E'\u05ea\u05e9\u05e4\u05f4\u05d5'      -- תשפ״ו (gershayim)
          )
       OR replace(replace(hebrew_year, '"', ''), E'\u05f4', '') = E'\u05ea\u05e9\u05e4\u05d5'
    ORDER BY id
    LIMIT 1;

    IF v_year_id IS NULL THEN
        RAISE EXCEPTION 'Hebrew year תשפו / תשפ"ו not found in shared_schema.hebrew_years';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'shared_schema'
          AND table_name = 'class_assistant_budget_hours'
          AND column_name = 'ministry_participation_pct'
    ) THEN
        RAISE EXCEPTION 'Run add-class-assistant-budget-hours-participation.sql first';
    END IF;

    -- Source matrix: (characterization_code, elementary_hours, high_school_hours)
    -- Participation default 100% on each pricing record (editable in Year Elements).
    FOR r IN
        SELECT * FROM (VALUES
            (11, 33.5::NUMERIC,  0::NUMERIC),
            (12, 33.5,          33.5),
            (15, 42.5,          42.5),
            (16, 24,             0),
            (17, 33.5,          33.5),
            (19, 42.5,          42.5),
            (20, 24,             0),
            (21, 56,            56),
            (23, 24,             0),
            (24, 42.5,          42.5),
            (26, 42.5,          42.5),
            (28, 50,            50),
            (29, 24,             0),
            (30, 2,              0),
            (31, 24,             0)
        ) AS t(code, elementary_hours, high_school_hours)
    LOOP
        v_code := r.code;
        v_elementary := r.elementary_hours;
        v_high_school := r.high_school_hours;

        SELECT c.id INTO v_classification_id
        FROM shared_schema.class_classifications c
        WHERE c.id = v_code
           OR c.foreign_id = v_code
        ORDER BY CASE WHEN c.id = v_code THEN 0 ELSE 1 END
        LIMIT 1;

        IF v_classification_id IS NULL THEN
            RAISE NOTICE 'Skipping characterization % — not found in class_classifications', v_code;
            v_missing := v_missing + 1;
            CONTINUE;
        END IF;

        INSERT INTO shared_schema.class_assistant_budget_hours
            (hebrew_year_id, school_level, class_classification_id, ministry_participation_pct,
             hours, created_at, updated_at)
        VALUES
            (v_year_id, 'elementary',  v_classification_id, 100,
             v_elementary,  CURRENT_TIMESTAMP, CURRENT_TIMESTAMP),
            (v_year_id, 'high_school', v_classification_id, 100,
             v_high_school, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)
        ON CONFLICT (hebrew_year_id, school_level, class_classification_id)
        DO UPDATE SET
            hours = EXCLUDED.hours,
            ministry_participation_pct = EXCLUDED.ministry_participation_pct,
            updated_at = CURRENT_TIMESTAMP;

        v_upserted := v_upserted + 2;
    END LOOP;

    RAISE NOTICE 'תשפו class assistant hours: upserted % row(s), missing classifications: %',
        v_upserted, v_missing;
END $$;
