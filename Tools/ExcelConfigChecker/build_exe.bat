@echo off
setlocal
cd /d "%~dp0"

echo [1/3] Checking PyInstaller...
python -m PyInstaller --version >nul 2>nul
if errorlevel 1 (
    echo PyInstaller not found, installing...
    python -m pip install --disable-pip-version-check pyinstaller
    if errorlevel 1 goto fail
)

echo [2/3] Building ExcelConfigChecker.exe...
python -m PyInstaller --noconfirm --clean --onefile --windowed --name ExcelConfigChecker --collect-all openpyxl --add-data "type_whitelist.txt;." excel_config_checker.py
if errorlevel 1 goto fail

echo [3/3] Copying exe to tool directory...
copy /Y "dist\ExcelConfigChecker.exe" "ExcelConfigChecker.exe" >nul
if errorlevel 1 goto fail

if exist build rmdir /S /Q build
if exist dist rmdir /S /Q dist
if exist ExcelConfigChecker.spec del /Q ExcelConfigChecker.spec

echo.
echo Build OK: %~dp0ExcelConfigChecker.exe
exit /b 0

:fail
echo.
echo Build FAILED. See messages above.
exit /b 1
