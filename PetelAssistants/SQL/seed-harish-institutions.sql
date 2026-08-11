-- =============================================================================
-- Seed schools in חריש (entity_id = 5) into assist_schema.institutions.
-- Source: עיריית חריש — בתי הספר / חוברת רישום תשפ״ז.
-- Idempotent — skips rows that already exist for (entity_id, name).
-- =============================================================================

DO $$
DECLARE
    v_entity_id INTEGER;
BEGIN
    SELECT id INTO v_entity_id
    FROM shared_schema.entities
    WHERE name = 'חריש'
    LIMIT 1;

    IF v_entity_id IS NULL THEN
        RAISE EXCEPTION 'Entity חריש not found in shared_schema.entities';
    END IF;

    -- Elementary — state
    INSERT INTO assist_schema.institutions
        (entity_id, name, institution_type, school_level, is_special_education, is_active)
    VALUES
        (v_entity_id, 'תלמי רון',              'school', 'elementary', false, true),
        (v_entity_id, 'רונה רמון',             'school', 'elementary', false, true),
        (v_entity_id, 'דרך הפרחים',            'school', 'elementary', false, true),
        (v_entity_id, 'ממלכתי בצוותא',         'school', 'elementary', false, true),
        (v_entity_id, 'אתגרי העתיד (יסודי)',   'school', 'elementary', false, true),
        (v_entity_id, 'מיתר',                 'school', 'elementary', true,  true),
        (v_entity_id, 'פסגת אמיר (יסודי)',     'school', 'elementary', true,  true),
        -- Elementary — religious state (ממ״ד / תורני)
        (v_entity_id, 'שבילי בצוותא',          'school', 'elementary', false, true),
        (v_entity_id, 'תלמי הדר',              'school', 'elementary', false, true),
        (v_entity_id, 'חב"ד',                 'school', 'elementary', false, true),
        (v_entity_id, 'לבי"א',                'school', 'elementary', false, true),
        (v_entity_id, 'כנפי רוח בנים',         'school', 'elementary', false, true),
        (v_entity_id, 'כנפי רוח בנות',         'school', 'elementary', false, true),
        -- Elementary — state Haredi (ממ״ח)
        (v_entity_id, 'תורת חיים',             'school', 'elementary', false, true),
        (v_entity_id, 'מעיין האמונה',          'school', 'elementary', false, true),
        -- Elementary — recognized unofficial (מוכש״ר)
        (v_entity_id, 'תלמוד תורה חינוך באמונה', 'school', 'elementary', false, true),
        (v_entity_id, 'בית יעקב חריש',         'school', 'elementary', false, true),
        (v_entity_id, 'תלמוד תורה חזון שמעון', 'school', 'elementary', false, true),
        (v_entity_id, 'השיר והשבח',            'school', 'elementary', false, true),
        -- Secondary — state
        (v_entity_id, 'אתגרי העתיד (על-יסודי)', 'school', 'high_school', false, true),
        (v_entity_id, 'נעימת הלב',             'school', 'high_school', false, true),
        (v_entity_id, 'פסגת אמיר (על-יסודי)',  'school', 'high_school', true,  true),
        -- Secondary — religious state
        (v_entity_id, 'ישיבה תיכונית בני עקיבא חריש', 'school', 'high_school', false, true),
        (v_entity_id, 'אולפנת בני עקיבא חריש', 'school', 'high_school', false, true),
        -- Secondary — Haredi / Mokshar
        (v_entity_id, 'ישיבת זיו אור',         'school', 'high_school', false, true),
        (v_entity_id, 'סמינר אור יקרות',       'school', 'high_school', false, true),
        (v_entity_id, 'סמינר בנות רחל',        'school', 'high_school', false, true),
        (v_entity_id, 'שחרית',                'school', 'high_school', false, true)
    ON CONFLICT (entity_id, name) DO NOTHING;

    -- Special-education kindergartens (סמל מוסד from סייעות מוסדיות חריש.xlsx)
    INSERT INTO assist_schema.institutions
        (entity_id, name, symbol, institution_type, school_level, is_special_education, is_active)
    VALUES
        (v_entity_id, 'אביב',  '667253', 'kindergarten', NULL, true, true),
        (v_entity_id, 'אופק',  '629402', 'kindergarten', NULL, true, true),
        (v_entity_id, 'אמיר',  '652917', 'kindergarten', NULL, true, true),
        (v_entity_id, 'גיל',   '667212', 'kindergarten', NULL, true, true),
        (v_entity_id, 'חצב',   '618934', 'kindergarten', NULL, true, true),
        (v_entity_id, 'יובל',  '652909', 'kindergarten', NULL, true, true),
        (v_entity_id, 'כרמל',  '667204', 'kindergarten', NULL, true, true),
        (v_entity_id, 'מתן',   '667261', 'kindergarten', NULL, true, true),
        (v_entity_id, 'רותם',  '629394', 'kindergarten', NULL, true, true),
        (v_entity_id, 'רקפת',  '570184', 'kindergarten', NULL, true, true),
        (v_entity_id, 'שחף',   '652933', 'kindergarten', NULL, true, true)
    ON CONFLICT (entity_id, name) DO NOTHING;

    RAISE NOTICE 'seed-harish-institutions.sql completed for entity_id=%', v_entity_id;
END $$;
