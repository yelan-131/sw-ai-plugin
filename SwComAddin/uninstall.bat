@echo off
setlocal

echo ========================================
echo   SolidWorks AI Plugin - 卸载程序
echo ========================================
echo.

REM ---- Check if already running as admin ----
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo 正在请求管理员权限，请在弹出的窗口中点击"是"...
    echo.
    powershell -Command "Start-Process cmd -ArgumentList '/c \"%~f0\"' -Verb RunAs -Wait"
    exit /b 0
)

REM ---- Verify RegAsm exists ----
set REGASM=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe
if not exist "%REGASM%" (
    echo [错误] 未找到 RegAsm.exe
    echo 请确保已安装 .NET Framework 4.x
    echo 预期路径: %REGASM%
    echo.
    pause
    exit /b 1
)

REM ---- Verify DLL exists ----
set DLL=%~dp0SwComAddin.dll
if not exist "%DLL%" (
    echo [提示] 未找到 SwComAddin.dll，可能已经卸载。
    echo.
    pause
    exit /b 0
)

REM ---- Unregister the COM add-in ----
echo 正在卸载 SolidWorks AI Plugin...
echo.
echo DLL:  %DLL%
echo RegAsm: %REGASM%
echo.

"%REGASM%" "%DLL%" /unregister /tlb
if %errorlevel% neq 0 (
    echo.
    echo [警告] 卸载过程返回错误代码: %errorlevel%
    echo 这可能表示插件未注册或已卸载，可以忽略。
    echo.
) else (
    echo.
    echo 卸载成功。
)

echo ========================================
echo   卸载完成！
echo ========================================
echo.
echo   注意事项：
echo   - COM 注册已移除，SolidWorks 将不再加载此插件。
echo   - 本脚本不会删除任何文件。
echo   - 如需彻底清理，可以手动删除本文件夹：
echo     %~dp0
echo.
echo ========================================
echo.
pause
