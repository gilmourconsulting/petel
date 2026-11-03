-- Allow daily hours_unit on entitlements and entitlement_allocations.
-- Run against assist_schema (tenant operational data).

DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'entitlements_hours_unit_check'
    ) THEN
        ALTER TABLE assist_schema.entitlements
            DROP CONSTRAINT entitlements_hours_unit_check;
    END IF;

    ALTER TABLE assist_schema.entitlements
        ADD CONSTRAINT entitlements_hours_unit_check
        CHECK (hours_unit IN ('weekly', 'monthly', 'daily'));

    IF EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'entitlement_allocations_unit_check'
    ) THEN
        ALTER TABLE assist_schema.entitlement_allocations
            DROP CONSTRAINT entitlement_allocations_unit_check;
    END IF;

    ALTER TABLE assist_schema.entitlement_allocations
        ADD CONSTRAINT entitlement_allocations_unit_check
        CHECK (hours_unit IN ('weekly', 'monthly', 'daily'));
END $$;
