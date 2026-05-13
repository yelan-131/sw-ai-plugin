@echo off
setlocal enabledelayedexpansion

echo ========================================
echo   SW AI Plugin - Release Package
echo ========================================
echo.

set VERSION=0.1.0

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

REM Prepare package directory (flat structure, no subfolder)
set PKGDIR=release\SwAiPlugin_v%VERSION%
echo [2/3] Packaging to %PKGDIR%...

if exist "release" rd /s /q "release"
mkdir "%PKGDIR%"
mkdir "%PKGDIR%\Data"

REM Copy main DLL and dependencies (all at root level)
copy "bin\Debug\net48\SwComAddin.dll" "%PKGDIR%\" >nul
copy "bin\Debug\net48\SwComAddin.tlb" "%PKGDIR%\" >nul
copy "bin\Debug\net48\SolidWorks.Interop.sldworks.dll" "%PKGDIR%\" >nul
copy "bin\Debug\net48\SolidWorks.Interop.swconst.dll" "%PKGDIR%\" >nul
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

REM Copy install scripts (at root level, user sees them first)
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
echo   ZIP 内结构（用户解压后直接看到）：
echo   SwAiPlugin_v%VERSION%\
echo     install.bat          ← 双击安装
echo     uninstall.bat        ← 双击卸载
echo     SwComAddin.dll       + 其他依赖
echo     plugin_config.json
echo     Data\standard_parts.json
echo ========================================
echo.
pause
