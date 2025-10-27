#!/bin/bash

# Script to generate PostgreSQL schema DDL
# Usage: ./extract_schema.sh [database_name] [schema_name] [output_dir]

# Default values
DB_NAME="${1:-postgres}"
SCHEMA_NAME="${2:-public}"
OUTPUT_DIR="${3:-./db-schema}"

# Database connection settings (can be set via environment variables)
DB_HOST="${DB_HOST:-localhost}"
DB_PORT="${DB_PORT:-5432}"
DB_USER="${DB_USER:-postgres}"

# Create output directory if it doesn't exist
mkdir -p "$OUTPUT_DIR"

OUTPUT_FILE="$OUTPUT_DIR/${SCHEMA_NAME}_schema.sql"

echo "Generating DDL for schema: $SCHEMA_NAME"
echo "Database: $DB_NAME"
echo "Output: $OUTPUT_FILE"
echo "---"

# Run the DDL generation script
PGPASSWORD="$DB_PASSWORD" psql \
    -h "$DB_HOST" \
    -p "$DB_PORT" \
    -U "$DB_USER" \
    -d "$DB_NAME" \
    -f generate_schema_ddl.sql \
    -v schema_name="$SCHEMA_NAME" \
    > "$OUTPUT_FILE" 2>&1

if [ $? -eq 0 ]; then
    echo "✓ DDL generated successfully: $OUTPUT_FILE"
    echo "✓ File size: $(wc -l < "$OUTPUT_FILE") lines"
else
    echo "✗ Error generating DDL. Check the output file for details."
    exit 1
fi

# Optional: Also generate a simplified version for AI context
SIMPLE_OUTPUT="$OUTPUT_DIR/${SCHEMA_NAME}_schema_simple.sql"

echo ""
echo "Generating simplified version for AI context..."

PGPASSWORD="$DB_PASSWORD" psql \
    -h "$DB_HOST" \
    -p "$DB_PORT" \
    -U "$DB_USER" \
    -d "$DB_NAME" \
    -t -A \
    -c "
SELECT 
    'TABLE: ' || table_schema || '.' || table_name || E'\n' ||
    string_agg(
        '  - ' || column_name || ': ' || 
        CASE 
            WHEN data_type = 'USER-DEFINED' THEN udt_name
            WHEN data_type = 'ARRAY' THEN udt_name
            ELSE data_type 
        END ||
        CASE 
            WHEN character_maximum_length IS NOT NULL 
            THEN '(' || character_maximum_length || ')'
            ELSE ''
        END ||
        CASE WHEN is_nullable = 'NO' THEN ' NOT NULL' ELSE '' END,
        E'\n'
        ORDER BY ordinal_position
    ) || E'\n'
FROM information_schema.columns
WHERE table_schema = '$SCHEMA_NAME'
  AND table_name IN (SELECT tablename FROM pg_tables WHERE schemaname = '$SCHEMA_NAME')
GROUP BY table_schema, table_name
ORDER BY table_name;
" > "$SIMPLE_OUTPUT"

echo "✓ Simplified schema: $SIMPLE_OUTPUT"
echo ""
echo "Done! Add these files to your VS Code workspace for AI awareness."
