@echo off
setlocal enabledelayedexpansion

echo ========================================
echo   SW AI Plugin - Release Package
echo ========================================
echo.

REM Read version from plugin_config.json
set VERSION=0.1.0
if exist "bin\Debug\net48\plugin_config.json" (
    for /f "tokens=2 delims=:," %%a in ('findstr "version" "bin\Debug\net48\plugin_config.json"') do (
        set "V=%%~a"
        set "V=!V: =!"
        set "V=!V:"=!"
        if not "!V!"=="" set "VERSION=!V!"
    )
)

echo Version: %VERSION%
echo.

REM Build
echo [1/3] Building...
cd /d "%~dp0"
dotnet build -c Debug
if errorlevel 1 (
    echo BUILD FAILED
    pause
    exit /b 1
)
echo Build OK.
echo.

REM Prepare package directory
set PKGDIR=release\SwAiPlugin_v%VERSION%
echo [2/3] Packaging to %PKGDIR%...

if exist "release" rd /s /q "release"
mkdir "%PKGDIR%"
mkdir "%PKGDIR%\Data"

REM Copy main DLL and dependencies
copy "bin\Debug\net48\SwComAddin.dll" "%PKGDIR%\" >nul
copy "bin\Debug\net48\SwComAddin.tlb" "%PKGDIR%\" >nul
copy "bin\Debug\net48\SolidWorks.Interop.sldworks.dll" "%PKGDIR%\" >nul
copy "bin\Debug\net48\SolidWorks.Interop.swconst.dll" "%PKGDIR%\" >nul

REM Copy .NET dependencies
copy "bin\Debug\net48\Microsoft.Bcl.AsyncInterfaces.dll" "%PKGDIR%\" >nul
copy "bin\Debug\net48\System.Buffers.dll" "%PKGDIR%\" >nul
copy "bin\Debug\net48\System.Memory.dll" "%PKGDIR%\" >nul
copy "bin\Debug\net48\System.Numerics.Vectors.dll" "%PKGDIR%\" >nul
copy "bin\Debug\net48\System.Runtime.CompilerServices.Unsafe.dll" "%PKGDIR%\" >nul
copy "bin\Debug\net48\System.Text.Encodings.Web.dll" "%PKGDIR%\" >nul
copy "bin\Debug\net48\System.Text.Json.dll" "%PKGDIR%\" >nul
copy "bin\Debug\net48\System.Threading.Tasks.Extensions.dll" "%PKGDIR%\" >nul
copy "bin\Debug\net48\System.ValueTuple.dll" "%PKGDIR%\" >nul

REM Copy data files
copy "bin\Debug\net48\Data\standard_parts.json" "%PKGDIR%\Data\" >nul
if exist "bin\Debug\net48\Data\custom_library.json" copy "bin\Debug\net48\Data\custom_library.json" "%PKGDIR%\Data\" >nul

REM Copy install scripts
copy "install.bat" "%PKGDIR%\" >nul
copy "uninstall.bat" "%PKGDIR%\" >nul

REM Generate default config
echo {"backend_url":"http://localhost:8765","model_library_path":"","version":"%VERSION%","update_repo":"yelan-131/sw-ai-plugin"} > "%PKGDIR%\plugin_config.json"

echo.
echo [3/3] Creating ZIP...
cd release
powershell -Command "Compress-Archive -Path 'SwAiPlugin_v%VERSION%' -DestinationPath 'SwAiPlugin_v%VERSION%.zip' -Force"
cd ..

echo.
echo ========================================
echo   Package created!
echo.
echo   %~dp0release\SwAiPlugin_v%VERSION%.zip
echo.
echo   Next: Upload to GitHub Release
echo   1. git tag v%VERSION%
echo   2. git push origin v%VERSION%
echo   3. Create Release on GitHub, upload ZIP
echo ========================================
echo.
pause
