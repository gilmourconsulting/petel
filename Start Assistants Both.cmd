@echo off
echo Starting Petel Assistants API and Blazor Server in separate windows...
echo.
start "Assistants API" cmd /k "cd /d "%~dp0PetelAssistants\PetelAssistants.Api" && dotnet run"
timeout /t 2 >nul
start "Assistants Blazor" cmd /k "cd /d "%~dp0PetelAssistants\PetelAssistants.BlazorServer" && dotnet run"
echo.
echo Started both processes.
echo API:    http://localhost:5238
echo Blazor: http://localhost:5088/login
pause
