@echo off
echo Starting Petel Blazor Server Application...
echo.
echo Backend API must be running on http://localhost:5082
echo.
cd /d "%~dp0PetelATH\PetelATH.BlazorServer"
dotnet run
pause
