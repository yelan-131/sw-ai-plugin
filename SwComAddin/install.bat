@echo off
chcp 65001 >nul
setlocal

echo ========================================
echo   SW AI Plugin - Install
echo ========================================
echo.

net session >nul 2>&1
if %errorlevel% neq 0 (
    echo Requesting admin...
    powershell -Command "Start-Process cmd -ArgumentList '/c chcp 65001 ^>nul ^& \"%~f0\"' -Verb RunAs -Wait"
    exit /b 0
)

set REGASM=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe
if not exist "%REGASM%" (
    echo [ERROR] RegAsm.exe not found
    echo Please install .NET Framework 4.x
    echo Expected: %REGASM%
    echo.
    pause
    exit /b 1
)

set DLL=%~dp0SwComAddin.dll
if not exist "%DLL%" (
    echo [ERROR] SwComAddin.dll not found
    echo Make sure DLL is in the same folder as this script.
    echo.
    pause
    exit /b 1
)

echo Registering SW AI Plugin...
echo.

"%REGASM%" "%DLL%" /codebase /tlb
if %errorlevel% neq 0 (
    echo.
    echo [ERROR] Registration failed!
    pause
    exit /b 1
)

echo.
echo ========================================
echo   Install SUCCESS!
echo ========================================
echo.
echo   Next steps:
echo   1. Open SolidWorks
echo   2. Menu: Tools ^> Plugins...
echo   3. Check "SolidWorks AI Plugin"
echo   4. Click OK
echo   5. Plugin appears in right TaskPane
echo.
echo ========================================
echo.
pause
