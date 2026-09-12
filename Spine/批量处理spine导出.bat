@echo off
setlocal
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0process_spine_exports.ps1"
set "EXIT_CODE=%ERRORLEVEL%"
echo.
if not "%EXIT_CODE%"=="0" echo Script failed with exit code %EXIT_CODE%.
pause
exit /b %EXIT_CODE%