-- ✅ Add versioning columns and financial fields to school_additional_study_programs table

-- Add version tracking columns
ALTER TABLE petel_schema.school_additional_study_programs 
ADD COLUMN version INTEGER NOT NULL DEFAULT 1,
ADD COLUMN is_last_version BOOLEAN NOT NULL DEFAULT true,
ADD COLUMN master_id INTEGER NULL;

-- Add financial tracking columns
ALTER TABLE petel_schema.school_additional_study_programs
ADD COLUMN cost DECIMAL(10, 2) NULL,
ADD COLUMN approved_amount DECIMAL(10, 2) NULL;

-- Add foreign key for master_id (self-referencing)
ALTER TABLE petel_schema.school_additional_study_programs
ADD CONSTRAINT fk_additional_study_master
FOREIGN KEY (master_id) REFERENCES petel_schema.school_additional_study_programs(id);

-- Create index on master_id for performance
CREATE INDEX idx_additional_study_master_id 
ON petel_schema.school_additional_study_programs(master_id);

-- Create index on is_last_version for querying current versions
CREATE INDEX idx_additional_study_is_last_version 
ON petel_schema.school_additional_study_programs(is_last_version);

-- Add comments
COMMENT ON COLUMN petel_schema.school_additional_study_programs.version IS 'Version number for this record (1 = first version, increments on update)';
COMMENT ON COLUMN petel_schema.school_additional_study_programs.is_last_version IS 'Flag indicating if this is the most recent version of the record';
COMMENT ON COLUMN petel_schema.school_additional_study_programs.master_id IS 'Reference to the original (first version) record ID for version history tracking';
COMMENT ON COLUMN petel_schema.school_additional_study_programs.cost IS 'Estimated or budgeted cost for the program';
COMMENT ON COLUMN petel_schema.school_additional_study_programs.approved_amount IS 'Approved budget amount for the program';

-- ✅ CRITICAL: Update existing records to set master_id to their own id (self-reference for first version)
UPDATE petel_schema.school_additional_study_programs
SET master_id = id
WHERE master_id IS NULL;

-- Make master_id NOT NULL after setting initial values
ALTER TABLE petel_schema.school_additional_study_programs
ALTER COLUMN master_id SET NOT NULL;