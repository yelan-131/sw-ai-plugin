@echo off
setlocal

echo ========================================
echo   SolidWorks AI Plugin - Registration
echo ========================================
echo.

set DLLDIR=%~dp0bin\Debug\net48
set DLL=%DLLDIR%\SwComAddin.dll
set REGASM=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe
set NATIVE=%~dp0..\SwNativeShim\SwNativeShim.dll

REM Step 1: Build managed DLL
echo [1/4] Building SwComAddin...
cd /d "%~dp0"
dotnet build -c Debug
if errorlevel 1 (
    echo BUILD FAILED
    pause
    exit /b 1
)
echo Build OK.
echo.

REM Step 2: Unregister native shim (if previously registered)
echo [2/4] Unregistering old native shim (if any)...
if exist "%NATIVE%" (
    regsvr32 /u /s "%NATIVE%" 2>nul
    echo Done.
) else (
    echo No native shim found, skipping.
)
echo.

REM Step 3: Unregister old managed DLL (clean registration)
echo [3/4] Cleaning old registration...
if exist "%REGASM%" (
    "%REGASM%" "%DLL%" /unregister /tlb 2>nul
    echo Done.
) else (
    echo ERROR: RegAsm not found at %REGASM%
    pause
    exit /b 1
)
echo.

REM Step 4: Register managed DLL
echo [4/4] Registering managed COM addin...
net session >nul 2>&1
if errorlevel 1 (
    echo Requesting admin privileges...
    powershell -Command "Start-Process '%REGASM%' -ArgumentList '\"%DLL%\" /codebase /tlb' -Verb RunAs -Wait"
) else (
    "%REGASM%" "%DLL%" /codebase /tlb
)
echo.

echo ========================================
echo   Registration complete!
echo.
echo   DLL: %DLL%
echo.
echo   Next steps:
echo   1. Start SolidWorks
echo   2. Tools ^> Plugins ^> check "SolidWorks AI Plugin"
echo   3. Plugin will appear in right Task Pane
echo ========================================
echo.
pause
