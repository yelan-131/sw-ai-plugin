@echo off
REM Build the native COM shim DLL for SolidWorks 2026

set MSVC=C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\VC\Tools\MSVC\14.44.35207
set SDK=C:\Program Files (x86)\Windows Kits\10

REM Find latest Windows SDK
for /f %%i in ('dir /b /ad "%SDK%\Include\" 2^>nul ^| sort /r') do set SDKVER=%%i

echo Using MSVC: %MSVC%
echo Using SDK: %SDKVER%

set CL=%MSVC%\bin\Hostx86\x64\cl.exe
set INCLUDE=%MSVC%\include;%SDK%\Include\%SDKVER%\um;%SDK%\Include\%SDKVER%\shared;%SDK%\Include\%SDKVER%\ucrt
set LIB=%MSVC%\lib\x64;%SDK%\Lib\%SDKVER%\um\x64;%SDK%\Lib\%SDKVER%\ucrt\x64

cd /d "%~dp0"

echo Compiling SwNativeShim.dll ...
%CL% /nologo /LD /EHsc /O2 /D NDEBUG /D UNICODE /D _UNICODE ^
    SwNativeShim.cpp /link /DEF:SwNativeShim.def /OUT:SwNativeShim.dll ^
    ole32.lib oleaut32.lib shell32.lib advapi32.lib user32.lib kernel32.lib

if exist SwNativeShim.dll (
    echo.
    echo BUILD SUCCESS: SwNativeShim.dll
    echo Copying to SwComAddin output...
    copy /Y SwNativeShim.dll "..\SwComAddin\bin\Debug\net48\" >nul
    echo Done.
) else (
    echo.
    echo BUILD FAILED
)

pause
