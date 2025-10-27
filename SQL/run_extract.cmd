@echo off
REM ========================================
REM Database Schema Extraction Script
REM ========================================
REM 
REM INSTRUCTIONS:
REM 1. Edit the settings below with your database details
REM 2. Save this file
REM 3. Run: run_extract.cmd
REM 
REM SECURITY: Add this file to .gitignore to avoid committing credentials!
REM ========================================

REM Database Connection Settings
REM -----------------------------
set DB_HOST=localhost
set DB_PORT=5432
set DB_USER=postgres
set DB_PASSWORD=yourpassword

REM Database and Schema to Extract
REM -------------------------------
set DATABASE_NAME=your_database
set SCHEMA_NAME=public

REM Output Directory
REM ----------------
set OUTPUT_DIR=db-schema

REM ========================================
REM Run the extraction
REM ========================================
echo.
echo ========================================
echo  Database Schema Extraction
echo ========================================
echo  Database: %DATABASE_NAME%
echo  Schema:   %SCHEMA_NAME%
echo  Output:   %OUTPUT_DIR%
echo ========================================
echo.

extract_schema.cmd %DATABASE_NAME% %SCHEMA_NAME% %OUTPUT_DIR%

if %ERRORLEVEL% EQU 0 (
    echo.
    echo ========================================
    echo  SUCCESS!
    echo ========================================
    echo  Your schema files are ready in: %OUTPUT_DIR%
    echo.
    echo  Next steps:
    echo  1. Review the generated files
    echo  2. Add to version control: git add %OUTPUT_DIR%/
    echo  3. Set up .cursorrules for VS Code AI
    echo ========================================
) else (
    echo.
    echo ========================================
    echo  ERROR!
    echo ========================================
    echo  Schema extraction failed.
    echo  Check your database connection settings.
    echo ========================================
)

echo.
pause
