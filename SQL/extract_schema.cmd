@echo off
REM Script to generate PostgreSQL schema DDL on Windows
REM Usage: extract_schema.cmd [database_name] [schema_name] [output_dir]

setlocal EnableDelayedExpansion

REM Default values
set "DB_NAME=%~1"
set "SCHEMA_NAME=%~2"
set "OUTPUT_DIR=%~3"

if "%DB_NAME%"=="" set "DB_NAME=postgres"
if "%SCHEMA_NAME%"=="" set "SCHEMA_NAME=public"
if "%OUTPUT_DIR%"=="" set "OUTPUT_DIR=.\db-schema"

REM Database connection settings (can be set via environment variables)
if "%DB_HOST%"=="" set "DB_HOST=localhost"
if "%DB_PORT%"=="" set "DB_PORT=5432"
if "%DB_USER%"=="" set "DB_USER=postgres"

REM Create output directory if it doesn't exist
if not exist "%OUTPUT_DIR%" mkdir "%OUTPUT_DIR%"

set "OUTPUT_FILE=%OUTPUT_DIR%\%SCHEMA_NAME%_schema.sql"

echo Generating DDL for schema: %SCHEMA_NAME%
echo Database: %DB_NAME%
echo Output: %OUTPUT_FILE%
echo ---
echo.

REM Run the DDL generation script
psql -h %DB_HOST% -p %DB_PORT% -U %DB_USER% -d %DB_NAME% -f generate_schema_ddl.sql -v schema_name=%SCHEMA_NAME% > "%OUTPUT_FILE%" 2>&1

if %ERRORLEVEL% EQU 0 (
    echo [OK] DDL generated successfully: %OUTPUT_FILE%
    for /f %%A in ('find /c /v "" ^< "%OUTPUT_FILE%"') do set "LINE_COUNT=%%A"
    echo [OK] File size: !LINE_COUNT! lines
) else (
    echo [ERROR] Error generating DDL. Check the output file for details.
    exit /b 1
)

REM Generate a simplified version for AI context
set "SIMPLE_OUTPUT=%OUTPUT_DIR%\%SCHEMA_NAME%_schema_simple.sql"

echo.
echo Generating simplified version for AI context...

psql -h %DB_HOST% -p %DB_PORT% -U %DB_USER% -d %DB_NAME% -t -A -c "SELECT 'TABLE: ' || table_schema || '.' || table_name || E'\n' || string_agg('  - ' || column_name || ': ' || CASE WHEN data_type = 'USER-DEFINED' THEN udt_name WHEN data_type = 'ARRAY' THEN udt_name ELSE data_type END || CASE WHEN character_maximum_length IS NOT NULL THEN '(' || character_maximum_length || ')' ELSE '' END || CASE WHEN is_nullable = 'NO' THEN ' NOT NULL' ELSE '' END, E'\n' ORDER BY ordinal_position) || E'\n' FROM information_schema.columns WHERE table_schema = '%SCHEMA_NAME%' AND table_name IN (SELECT tablename FROM pg_tables WHERE schemaname = '%SCHEMA_NAME%') GROUP BY table_schema, table_name ORDER BY table_name;" > "%SIMPLE_OUTPUT%" 2>&1

if %ERRORLEVEL% EQU 0 (
    echo [OK] Simplified schema: %SIMPLE_OUTPUT%
) else (
    echo [WARNING] Could not generate simplified version
)

echo.
echo Done! Add these files to your VS Code workspace for AI awareness.
echo.
pause
