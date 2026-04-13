-- Drop the incorrect unique constraint on name only
ALTER TABLE petel_schema.document_types 
DROP CONSTRAINT IF EXISTS document_types_name_key;

-- Add correct composite unique constraint on (name, year_id)
ALTER TABLE petel_schema.document_types 
ADD CONSTRAINT document_types_name_year_key UNIQUE (name, year_id);