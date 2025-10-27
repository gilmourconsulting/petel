# Quick Start Guide - Windows

## Prerequisites

1. **PostgreSQL Client Tools** must be installed and `psql` must be in your PATH

   Check if psql is available:
   ```cmd
   psql --version
   ```

   If not found, install PostgreSQL:
   - Download from: https://www.postgresql.org/download/windows/
   - Or use the installer from EnterpriseDB
   - Make sure to check "Command Line Tools" during installation

2. **Add psql to PATH** (if not already):
   - Find PostgreSQL bin folder (usually `C:\Program Files\PostgreSQL\16\bin`)
   - Add to System Environment Variables:
     - Press `Win + X` → System → Advanced system settings
     - Environment Variables → System variables → Path → Edit
     - Add: `C:\Program Files\PostgreSQL\16\bin`
   - Restart Command Prompt

## Step 1: Extract Your Database Schema

### Option A: Using Environment Variables (Recommended)

```cmd
REM Set your database credentials
set DB_HOST=localhost
set DB_PORT=5432
set DB_USER=postgres
set DB_PASSWORD=yourpassword

REM Run the extraction
extract_schema.cmd your_database public db-schema
```

### Option B: Using .env Style (Create a run.cmd)

Create `run_extract.cmd`:
```cmd
@echo off
set DB_HOST=localhost
set DB_PORT=5432
set DB_USER=postgres
set DB_PASSWORD=yourpassword

extract_schema.cmd your_database public db-schema
```

Then just run:
```cmd
run_extract.cmd
```

### Option C: Simple (Will Prompt for Password)

```cmd
extract_schema.cmd your_database public db-schema
```

PostgreSQL will prompt you for the password.

## Step 2: What You Get

After running the script, you'll have:
```
db-schema/
├── public_schema.sql        - Complete DDL (tables, constraints, indexes)
└── public_schema_simple.sql - Simplified format for AI
```

## Step 3: Set Up VS Code AI Awareness

### For Cursor IDE

Copy the example file to your project root:
```cmd
copy .cursorrules.example C:\path\to\your\project\.cursorrules
```

### For GitHub Copilot

Create instructions file:
```cmd
mkdir .github
copy .cursorrules.example .github\copilot-instructions.md
```

Edit the file to reference your schema location.

### For Any AI Assistant

Just reference the schema in comments:
```typescript
// Database schema: db-schema/public_schema_simple.sql
interface User {
  id: number;
  email: string;
  // AI now knows your structure!
}
```

## Step 4: Update After Migrations

Create `update_schema.cmd` in your project:

```cmd
@echo off
echo Updating database schema documentation...

set DB_HOST=localhost
set DB_USER=postgres
set DB_PASSWORD=yourpassword

cd /d "%~dp0"
extract_schema.cmd your_database public db-schema

echo.
echo Schema updated! Don't forget to commit:
echo   git add db-schema/
echo   git commit -m "Update database schema"
```

## Common Issues

### Issue: 'psql' is not recognized

**Solution**: Add PostgreSQL bin folder to PATH (see Prerequisites)

### Issue: Password authentication failed

**Solutions**:
1. Set the password in environment variable:
   ```cmd
   set PGPASSWORD=yourpassword
   extract_schema.cmd your_database
   ```

2. Or use .pgpass file:
   - Create: `%APPDATA%\postgresql\pgpass.conf`
   - Add line: `localhost:5432:*:postgres:yourpassword`
   - Format: `hostname:port:database:username:password`

### Issue: Connection refused

**Solution**: Check if PostgreSQL is running:
```cmd
REM Check PostgreSQL service
sc query postgresql-x64-16

REM Start service if needed
net start postgresql-x64-16
```

### Issue: Schema not found

**Solution**: List available schemas:
```cmd
psql -h localhost -U postgres -d your_database -c "\dn"
```

## Project Structure

```
your-project/
├── db-schema/                      # Generated schema files
│   ├── public_schema.sql          # Complete DDL
│   └── public_schema_simple.sql   # Simple format for AI
├── .cursorrules                    # AI instructions (Cursor)
├── .github/
│   └── copilot-instructions.md    # AI instructions (Copilot)
├── extract_schema.cmd             # Extraction script (this file)
├── generate_schema_ddl.sql        # PostgreSQL script
└── update_schema.cmd              # Quick update script
```

## Tips for Windows Users

### 1. Use PowerShell for Better Experience

Save as `extract_schema.ps1`:
```powershell
$env:DB_PASSWORD = "yourpassword"
.\extract_schema.cmd your_database public db-schema
```

### 2. Add to package.json

```json
{
  "scripts": {
    "db:schema": "extract_schema.cmd your_database public db-schema"
  }
}
```

Then run:
```cmd
npm run db:schema
```

### 3. Schedule Automatic Updates

Use Task Scheduler:
1. Open Task Scheduler
2. Create Basic Task
3. Trigger: Daily or after specific event
4. Action: Start a program → `update_schema.cmd`

### 4. Integration with Git Bash

If you have Git Bash installed, you can use the Linux script:
```bash
./extract_schema.sh your_database public ./db-schema
```

## Example Workflow

```cmd
REM 1. Make database changes
npm run migration:create add_users_table

REM 2. Run migrations  
npm run migration:run

REM 3. Update schema docs
extract_schema.cmd mydb public db-schema

REM 4. Commit everything
git add migrations/ db-schema/
git commit -m "Add users table"

REM 5. AI assistants now know about your new table!
```

## Security Note

❌ **Don't commit passwords!**

Use one of these methods:
- Environment variables (set in system, not in scripts)
- `.env` file (add to `.gitignore`)
- `.pgpass` file (PostgreSQL standard)
- Prompt for password each time

Example `.gitignore`:
```
.env
run_extract.cmd
update_schema.cmd
```

## Need Help?

See `README.md` for detailed documentation.

For Windows-specific PostgreSQL issues: https://www.postgresql.org/docs/current/windows.html
