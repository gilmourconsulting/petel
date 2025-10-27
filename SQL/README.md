# PostgreSQL Schema DDL Generator

Scripts to extract database schema DDL for use with VS Code AI assistants (Copilot, Cursor, etc.)

## Files

- `generate_schema_ddl.sql` - PostgreSQL script that generates complete DDL
- `extract_schema.sh` - Bash script to automate extraction
- `README.md` - This file

## Quick Start

### Method 1: Using the Bash Script (Recommended)

```bash
# Make the script executable
chmod +x extract_schema.sh

# Run with defaults (public schema, localhost)
./extract_schema.sh your_database_name

# Specify schema and output directory
./extract_schema.sh your_database public ./db-schema

# With environment variables for connection
DB_HOST=localhost \
DB_PORT=5432 \
DB_USER=postgres \
DB_PASSWORD=yourpassword \
./extract_schema.sh your_database public ./db-schema
```

### Method 2: Using psql Directly

```bash
# Create output directory
mkdir -p db-schema

# Run the SQL script
psql -d your_database \
     -f generate_schema_ddl.sql \
     -v schema_name=public \
     > db-schema/schema.sql
```

## Output Files

The scripts generate two files:

1. **`{schema}_schema.sql`** - Complete DDL with:
   - CREATE TABLE statements
   - Primary keys
   - Foreign keys
   - Unique constraints
   - Check constraints
   - Indexes
   - Comments

2. **`{schema}_schema_simple.sql`** - Simplified format optimized for AI context

## VS Code Setup for AI Awareness

### Option 1: Add to .cursorrules or .github/copilot-instructions.md

Create a file in your project root:

**`.cursorrules`** (for Cursor) or **`.github/copilot-instructions.md`** (for GitHub Copilot):

```markdown
# Database Schema

The database schema is defined in `db-schema/schema_simple.sql`. 
When writing database queries or ORM code, refer to this schema.

Key tables:
- users: User accounts and authentication
- products: Product catalog
- orders: Customer orders
- etc.
```

### Option 2: Add Schema Files to VS Code Workspace

1. Create a `db-schema` folder in your project root
2. Run the extraction script to populate it
3. Reference these files in your code comments:

```python
# See db-schema/public_schema_simple.sql for table structure
def get_user(user_id):
    ...
```

### Option 3: Include in Context Files

For Cursor, Copilot, or other AI assistants:

1. Keep schema files in `db-schema/` directory
2. When starting a new coding session, mention: 
   "The database schema is in db-schema/public_schema_simple.sql"

### Option 4: Add to Project Documentation

Create a `docs/database.md` file that includes or references the schema:

```markdown
# Database Documentation

## Schema Overview

See [Schema DDL](../db-schema/public_schema.sql) for complete definitions.

## Main Tables

[Include simplified schema here or reference the simple file]
```

## Automation

### Run on Database Changes

Add to your project's `package.json` or `Makefile`:

```json
{
  "scripts": {
    "db:extract-schema": "./extract_schema.sh mydb public ./db-schema",
    "db:update-docs": "npm run db:extract-schema && git add db-schema/ && echo 'Schema updated'"
  }
}
```

### Git Pre-commit Hook

Create `.git/hooks/pre-commit`:

```bash
#!/bin/bash
# Auto-update schema if migrations were added
if git diff --cached --name-only | grep -q "migrations/"; then
    ./extract_schema.sh mydb public ./db-schema
    git add db-schema/
fi
```

## Environment Variables

- `DB_HOST` - Database host (default: localhost)
- `DB_PORT` - Database port (default: 5432)
- `DB_USER` - Database user (default: postgres)
- `DB_PASSWORD` - Database password

### Using .env File

```bash
# .env
DB_HOST=localhost
DB_PORT=5432
DB_USER=myuser
DB_PASSWORD=mypassword

# Load and run
source .env && ./extract_schema.sh mydb public ./db-schema
```

## Features

### What's Included

✅ Table definitions with all columns and types
✅ Primary keys
✅ Foreign key relationships
✅ Unique constraints
✅ Check constraints
✅ Indexes
✅ Column and table comments
✅ Array and custom types support

### What's Not Included

❌ Views (can be added if needed)
❌ Functions and procedures
❌ Triggers
❌ Sequences (separate objects)
❌ Permissions and grants

## Tips for AI Awareness

1. **Keep It Updated**: Run the script after migrations or schema changes

2. **Use Simple Format for Context**: The `*_simple.sql` file is better for AI context windows

3. **Add Comments to Tables**: The script includes table/column comments, so document your database:
   ```sql
   COMMENT ON TABLE users IS 'Application user accounts';
   COMMENT ON COLUMN users.email IS 'Primary email for authentication';
   ```

4. **Reference in Code Comments**: Help AI assistants by mentioning schema files:
   ```typescript
   // Database schema: db-schema/public_schema_simple.sql
   interface User {
     id: number;
     email: string;
     // ...
   }
   ```

5. **Size Considerations**: If your schema is very large (100+ tables), consider:
   - Splitting by functional area
   - Creating a summary file with just table names and key columns
   - Using the simple format which is more compact

## Troubleshooting

### Permission Denied

```bash
chmod +x extract_schema.sh
```

### psql: command not found

Install PostgreSQL client tools:
```bash
# Ubuntu/Debian
sudo apt-get install postgresql-client

# macOS
brew install postgresql
```

### Connection Refused

Check your connection settings and ensure PostgreSQL is running:
```bash
psql -h localhost -U postgres -l
```

### Schema Not Found

Verify the schema exists:
```bash
psql -d your_database -c "\dn"
```

## Advanced Usage

### Extract Multiple Schemas

```bash
for schema in public app_data analytics; do
    ./extract_schema.sh mydb $schema ./db-schema
done
```

### Compare Schemas

```bash
# Extract from different environments
./extract_schema.sh prod_db public ./db-schema/prod
./extract_schema.sh dev_db public ./db-schema/dev

# Compare
diff db-schema/prod/public_schema.sql db-schema/dev/public_schema.sql
```

### Include in CI/CD

```yaml
# .github/workflows/schema-docs.yml
name: Update Schema Documentation

on:
  push:
    paths:
      - 'migrations/**'

jobs:
  update-schema:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v2
      - name: Extract Schema
        run: |
          ./extract_schema.sh ${{ secrets.DB_NAME }} public ./db-schema
      - name: Commit Changes
        run: |
          git config --local user.email "action@github.com"
          git config --local user.name "GitHub Action"
          git add db-schema/
          git commit -m "Update schema documentation" || echo "No changes"
          git push
```

## License

Feel free to use and modify these scripts for your projects.
