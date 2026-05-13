@echo off
setlocal

set REGASM=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe
set DLL=%~dp0bin\Debug\net48\SwComAddin.dll

if not exist "%DLL%" (
    echo DLL not found. Already unregistered?
    pause
    exit /b 0
)

echo Unregistering: %DLL%
echo.

net session >nul 2>&1
if errorlevel 1 (
    echo Requesting admin privileges...
    powershell -Command "Start-Process '%REGASM%' -ArgumentList '\"%DLL%\" /unregister' -Verb RunAs"
) else (
    "%REGASM%" "%DLL%" /unregister
)

echo.
echo Done! Restart SolidWorks.
pause
