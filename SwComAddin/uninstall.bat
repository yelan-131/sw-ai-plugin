@echo off
setlocal

net session >nul 2>&1
if %errorlevel% neq 0 (
    echo ========================================
    echo   SW AI Plugin - Uninstall
    echo ========================================
    echo.
    echo Requesting admin privileges...
    echo Please click YES on the popup.
    echo.
    powershell -Command "Start-Process '%~f0' -Verb RunAs"
    exit /b 0
)

echo ========================================
echo   SW AI Plugin - Uninstall
echo ========================================
echo.

set REGASM=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe
if not exist "%REGASM%" (
    echo [ERROR] RegAsm.exe not found
    pause
    exit /b 1
)

set DLL=%~dp0SwComAddin.dll
if not exist "%DLL%" (
    echo DLL not found, already uninstalled.
    pause
    exit /b 0
)

echo Unregistering SW AI Plugin...
echo DLL:   %DLL%
echo.

"%REGASM%" "%DLL%" /unregister /tlb
if %errorlevel% neq 0 (
    echo.
    echo [WARN] Unregister error, may already be removed.
) else (
    echo Unregister SUCCESS.
)

echo.
echo ========================================
echo   Uninstall complete!
echo ========================================
echo.
echo   - COM registration removed
echo   - No files were deleted
echo   - You can safely delete this folder:
echo     %~dp0
echo.
echo ========================================
echo.
pause
