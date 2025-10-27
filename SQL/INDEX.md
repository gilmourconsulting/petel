# PostgreSQL Schema Extractor for VS Code AI - Windows Edition

## 📦 Your Files

### ⚡ Quick Start (Do This First!)
1. **QUICKSTART_WINDOWS.md** - Start here! Step-by-step Windows instructions

### 🔧 Main Scripts
1. **extract_schema.cmd** - Main extraction script (Windows batch file)
2. **generate_schema_ddl.sql** - PostgreSQL DDL generation query
3. **run_extract.cmd** - Template script you can customize with your credentials

### 📖 Documentation
1. **README.md** - Comprehensive documentation (all platforms)
2. **QUICKSTART_WINDOWS.md** - Quick start guide for Windows
3. **QUICKSTART.md** - Quick start guide (Linux/Mac)

### ⚙️ Configuration Files
1. **.cursorrules.example** - Example AI instructions for Cursor IDE
2. **.gitignore.example** - Recommended .gitignore entries

## 🚀 Quick Setup (Windows)

### Step 1: Copy files to your project
```cmd
REM Copy all files to your project root
copy *.cmd C:\path\to\your\project\
copy *.sql C:\path\to\your\project\
copy *.md C:\path\to\your\project\
```

### Step 2: Configure your database
Edit `run_extract.cmd` and set:
- DB_HOST (e.g., localhost)
- DB_PORT (e.g., 5432)
- DB_USER (e.g., postgres)
- DB_PASSWORD (your password)
- DATABASE_NAME (your database)
- SCHEMA_NAME (usually "public")

### Step 3: Run extraction
```cmd
run_extract.cmd
```

### Step 4: Set up VS Code AI
```cmd
REM For Cursor IDE
copy .cursorrules.example .cursorrules

REM For GitHub Copilot
mkdir .github
copy .cursorrules.example .github\copilot-instructions.md
```

### Step 5: Protect credentials
```cmd
REM Add to your .gitignore
echo run_extract.cmd >> .gitignore
echo .env >> .gitignore
```

## 📁 What Gets Generated

After running extraction, you'll have:
```
db-schema/
├── public_schema.sql        - Full DDL (tables, keys, indexes)
└── public_schema_simple.sql - Simplified for AI context
```

## ✅ Prerequisites

- PostgreSQL client tools installed
- `psql` command available in PATH
- Access to your PostgreSQL database

Check if ready:
```cmd
psql --version
```

If not found, install from: https://www.postgresql.org/download/windows/

## 🆘 Need Help?

1. **Can't find psql?** - See QUICKSTART_WINDOWS.md → Prerequisites
2. **Connection issues?** - Check database host, port, and credentials
3. **Schema not found?** - Verify schema name with: `psql -d yourdb -c "\dn"`
4. **Password prompts?** - Use run_extract.cmd with credentials set

## 💡 Tips

- Update schema after each database migration
- Keep schema files in version control
- Never commit database passwords (use .gitignore)
- Reference schema in code comments for AI awareness

## 🔗 Useful Commands

```cmd
REM Extract schema
extract_schema.cmd mydb public db-schema

REM Extract specific schema
extract_schema.cmd mydb app_schema db-schema

REM After migrations
npm run migrate
extract_schema.cmd mydb public db-schema
git add db-schema/
git commit -m "Update schema"
```

## 📚 Files Reference

| File | Purpose | Edit? |
|------|---------|-------|
| extract_schema.cmd | Main script | No |
| generate_schema_ddl.sql | SQL queries | No |
| run_extract.cmd | Your config | Yes! |
| QUICKSTART_WINDOWS.md | Instructions | Read |
| .cursorrules.example | AI config | Copy & edit |
| .gitignore.example | Git settings | Merge |

## 🎯 VS Code AI Integration

Once you have your schema files:

1. **Cursor IDE**: Use `.cursorrules` to tell AI about schema
2. **GitHub Copilot**: Use `.github/copilot-instructions.md`
3. **Any AI**: Reference in comments:
   ```typescript
   // Database: db-schema/public_schema_simple.sql
   ```

The AI will then:
- Know your exact table structure
- Use correct column names
- Respect foreign key relationships
- Generate proper SQL queries

## ⚠️ Security Reminder

❌ Never commit:
- Database passwords
- Connection strings with credentials
- `run_extract.cmd` with real passwords

✅ Always:
- Use environment variables
- Add credential files to .gitignore
- Use .pgpass for automated scripts

## 📞 Support

For issues:
1. Check QUICKSTART_WINDOWS.md
2. Review README.md
3. Check PostgreSQL docs: https://www.postgresql.org/docs/

Happy coding! 🎉
