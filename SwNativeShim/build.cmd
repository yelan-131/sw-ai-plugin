@echo off
setlocal enabledelayedexpansion

set MSVC=C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\VC\Tools\MSVC\14.44.35207
set SDK=C:\Program Files (x86)\Windows Kits\10
set SDKVER=10.0.22621.0

set CL_EXE=%MSVC%\bin\Hostx86\x64\cl.exe
set INCLUDE=%MSVC%\include;%SDK%\Include\%SDKVER%\um;%SDK%\Include\%SDKVER%\shared;%SDK%\Include\%SDKVER%\ucrt
set LIB=%MSVC%\lib\x64;%SDK%\Lib\%SDKVER%\um\x64;%SDK%\Lib\%SDKVER%\ucrt\x64

cd /d "%~dp0"

echo Compiling SwNativeShim.dll ...
echo CL: %CL_EXE%

"%CL_EXE%" /nologo /LD /EHsc /O2 /DNDEBUG /DUNICODE /D_UNICODE SwNativeShim.cpp /link /DEF:SwNativeShim.def /OUT:SwNativeShim.dll ole32.lib oleaut32.lib shell32.lib advapi32.lib user32.lib kernel32.lib shfolder.lib

if exist SwNativeShim.dll (
    echo.
    echo BUILD SUCCESS: SwNativeShim.dll
) else (
    echo.
    echo BUILD FAILED
)
