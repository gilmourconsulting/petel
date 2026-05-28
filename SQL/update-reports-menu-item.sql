-- ============================================================================
-- update-reports-menu-item.sql
-- Replaces the /excelreports menu item with /reports.
-- Idempotent: safe to run multiple times on any environment.
-- ============================================================================

DO $$
DECLARE
    v_sort_order  INTEGER;
    v_action_id   INTEGER;
    v_deleted     INTEGER;
BEGIN

    -- ── Step 1: Capture sort_order and action_id from old row (if it exists) ──
    SELECT sort_order, action_id
      INTO v_sort_order, v_action_id
      FROM petel_schema.menu_items
     WHERE reference = '/excelreports'
     LIMIT 1;

    -- Default sort_order if the old row is missing
    IF v_sort_order IS NULL THEN
        v_sort_order := 90;
    END IF;

    -- ── Step 2: Remove the old /excelreports row ──────────────────────────
    DELETE FROM petel_schema.menu_items
     WHERE reference = '/excelreports';

    GET DIAGNOSTICS v_deleted = ROW_COUNT;
    RAISE NOTICE 'Deleted /excelreports menu item (% rows)', v_deleted;

    -- ── Step 3: Insert /reports row (skip if already exists) ─────────────
    INSERT INTO petel_schema.menu_items (name, reference, text, action_id, sort_order, is_active)
    VALUES ('reports', '/reports', 'דוחות', v_action_id, v_sort_order, true)
    ON CONFLICT DO NOTHING;

    RAISE NOTICE 'Inserted /reports menu item (sort_order=%, action_id=%)', v_sort_order, v_action_id;

END
$$;

-- ── Verify ────────────────────────────────────────────────────────────────
SELECT id, name, reference, text, action_id, sort_order, is_active
  FROM petel_schema.menu_items
 WHERE name IN ('reports', 'excelreports')
 ORDER BY sort_order;
