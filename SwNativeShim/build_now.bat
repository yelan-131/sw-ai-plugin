@echo off
setlocal

set MSVC=C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\VC\Tools\MSVC\14.44.35207
set SDK=C:\Program Files (x86)\Windows Kits\10

for /f %%i in ('dir /b /ad "%SDK%\Include\" 2^>nul ^| sort /r') do set SDKVER=%%i

echo MSVC: %MSVC%
echo SDK: %SDKVER%

set CL=%MSVC%\bin\Hostx86\x64\cl.exe
set INCLUDE=%MSVC%\include;%SDK%\Include\%SDKVER%\um;%SDK%\Include\%SDKVER%\shared;%SDK%\Include\%SDKVER%\ucrt
set LIB=%MSVC%\lib\x64;%SDK%\Lib\%SDKVER%\um\x64;%SDK%\Lib\%SDKVER%\ucrt\x64

cd /d "%~dp0"

echo Compiling SwNativeShim.dll ...
"%MSVC%\bin\Hostx86\x64\cl.exe" /nologo /LD /EHsc /O2 /D "NDEBUG" /D "UNICODE" /D "_UNICODE" SwNativeShim.cpp /link /DEF:SwNativeShim.def /OUT:SwNativeShim.dll ole32.lib oleaut32.lib shell32.lib advapi32.lib user32.lib kernel32.lib

if exist SwNativeShim.dll (
    echo.
    echo BUILD SUCCESS
    copy /Y SwNativeShim.dll "..\SwComAddin\bin\Debug\net48\" >nul 2>&1
    echo Copied to SwComAddin output
) else (
    echo.
    echo BUILD FAILED
)
