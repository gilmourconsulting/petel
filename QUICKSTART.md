# Quick Start Guide

## 1. Extract Your Database Schema

```bash
# Make script executable
chmod +x extract_schema.sh

# Run extraction (replace with your details)
DB_PASSWORD=yourpass ./extract_schema.sh your_database public ./db-schema
```

This creates:
- `db-schema/public_schema.sql` - Complete DDL
- `db-schema/public_schema_simple.sql` - Simplified for AI

## 2. Set Up VS Code AI Awareness

### For Cursor IDE

Copy `.cursorrules.example` to your project root as `.cursorrules`:

```bash
cp .cursorrules.example /path/to/your/project/.cursorrules
```

### For GitHub Copilot

Create `.github/copilot-instructions.md` in your project:

```bash
mkdir -p .github
cp .cursorrules.example .github/copilot-instructions.md
```

### For Any AI Assistant

Simply reference the schema in your code:

```python
# Database schema: db-schema/public_schema_simple.sql

class User(Base):
    __tablename__ = 'users'
    # AI will now know your table structure!
```

## 3. Keep It Updated

Run after database migrations:

```bash
# After running migrations
npm run migrate
./extract_schema.sh your_database public ./db-schema
git add db-schema/
git commit -m "Update database schema documentation"
```

## 4. Project Structure

```
your-project/
├── db-schema/                    # Generated schema files
│   ├── public_schema.sql        # Complete DDL
│   └── public_schema_simple.sql # Simple format
├── .cursorrules                  # AI instructions (Cursor)
├── .github/
│   └── copilot-instructions.md  # AI instructions (Copilot)
├── extract_schema.sh            # Extraction script
└── generate_schema_ddl.sql      # PostgreSQL script
```

## Tips

✅ **Do's**
- Keep schema files in version control
- Update after every migration
- Reference schema in code comments
- Use the simple format for AI context

❌ **Don'ts**  
- Don't commit database passwords
- Don't manually edit generated schema files
- Don't let schema get out of sync

## Example Workflow

```bash
# 1. Make database changes
npm run migration:create add_users_table

# 2. Run migrations
npm run migration:run

# 3. Update schema docs
./extract_schema.sh mydb public ./db-schema

# 4. Commit everything
git add migrations/ db-schema/
git commit -m "Add users table"

# 5. Now AI assistants know about your new table!
```

## Need Help?

See `README.md` for detailed documentation.
