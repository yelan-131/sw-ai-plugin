@echo off
setlocal enabledelayedexpansion

echo ========================================
echo   SW AI Plugin - Release Package
echo ========================================
echo.

set VERSION=0.1.5
set CHANNEL=stable
set UPDATE_REPO=yelan-131/sw-ai-plugin
set GITEE_REPO=yelan1387/sw-ai-plugin

echo Version: %VERSION%
echo Channel: %CHANNEL%
echo.

REM Build
echo [1/4] Building...
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
echo [2/4] Packaging to %PKGDIR%...

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

REM Generate plugin_meta.json (随包发布的元数据)
set BUILD_DATE=%date% %time%
> "%PKGDIR%\plugin_meta.json" (
echo {
echo   "schema": "1.0",
echo   "version": "%VERSION%",
echo   "channel": "%CHANNEL%",
echo   "build_date": "%BUILD_DATE%",
echo   "update_repo": "%UPDATE_REPO%",
echo   "gitee_repo": "%GITEE_REPO%"
echo }
)

echo.
echo [3/4] Creating ZIP...
cd release
powershell -Command "Compress-Archive -Path 'SwAiPlugin_v%VERSION%' -DestinationPath 'SwAiPlugin_v%VERSION%.zip' -Force"
cd ..

echo.
echo [4/4] Generating manifest.json...

REM 计算 ZIP SHA256，并写 manifest.json
set ZIPFILE=release\SwAiPlugin_v%VERSION%.zip
powershell -NoProfile -Command ^
  "$zip='%ZIPFILE%';" ^
  "$hash=(Get-FileHash -Algorithm SHA256 $zip).Hash.ToLower();" ^
  "$size=(Get-Item $zip).Length;" ^
  "$obj=[ordered]@{" ^
    "schema='1.0';" ^
    "version='%VERSION%';" ^
    "channel='%CHANNEL%';" ^
    "released_at=(Get-Date -Format 'yyyy-MM-ddTHH:mm:ssZ');" ^
    "force_update=$false;" ^
    "package=[ordered]@{" ^
      "name='SwAiPlugin_v%VERSION%.zip';" ^
      "size=$size;" ^
      "sha256=$hash;" ^
      "primary_url='https://gitee.com/%GITEE_REPO%/releases/download/v%VERSION%/SwAiPlugin_v%VERSION%.zip';" ^
      "mirrors=@('https://github.com/%UPDATE_REPO%/releases/download/v%VERSION%/SwAiPlugin_v%VERSION%.zip')" ^
    "};" ^
    "preserve=@('user_config.json','Data/custom_library.json','SwAiBackend/config.json');" ^
    "release_notes_summary='请编辑 release_notes.md 后手动更新此字段。'" ^
  "};" ^
  "$obj | ConvertTo-Json -Depth 6 | Set-Content -Path 'release\manifest.json' -Encoding UTF8;"

if errorlevel 1 (
    echo MANIFEST GENERATION FAILED
    pause
    exit /b 1
)

echo.
echo [5/5] Signing manifest...
set SIGN_KEY=..\tools\ed25519_private.pem
if exist "%SIGN_KEY%" (
    python "%SIGN_KEY%\..\sign_manifest.py" sign "release\manifest.json" "%SIGN_KEY%"
    if errorlevel 1 (
        echo SIGNING FAILED - manifest.sig not created
        echo You can still publish without signature, but clients will show a warning.
    )
) else (
    echo [SKIP] Private key not found at %SIGN_KEY%
    echo   To enable signing, generate a key pair: python tools/sign_manifest.py generate-key
    echo   Manifest will be published without signature.
)

echo.
echo ========================================
echo   Package created!
echo.
echo   release\SwAiPlugin_v%VERSION%.zip
echo   release\manifest.json   ← upload to Release
echo   release\manifest.sig    ← upload to Release (if signed)
echo.
echo   ZIP 内结构：
echo   SwAiPlugin_v%VERSION%\
echo     install.bat          ← 双击安装
echo     uninstall.bat        ← 双击卸载
echo     SwComAddin.dll       + 其他依赖
echo     plugin_meta.json     ← 元数据，每次发布覆盖
echo     Data\standard_parts.json
echo.
echo   发布步骤：
echo     1. 在 Gitee / GitHub 创建 Release v%VERSION%
echo     2. 上传 SwAiPlugin_v%VERSION%.zip 与 manifest.json 两个资产
echo     3. 客户端在下次启动或周期检查时自动发现
echo     4. (可选) 同时上传 manifest.sig 实现签名验证
echo ========================================
echo.

pause
