@echo off
echo Starting Petel Assistants Blazor Server...
echo.
echo Backend API should be running on http://localhost:5238
echo.
cd /d "%~dp0PetelAssistants\PetelAssistants.BlazorServer"
dotnet run
pause
