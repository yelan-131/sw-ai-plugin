@echo off
setlocal

echo ========================================
echo   SolidWorks AI Plugin - 安装程序
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
    echo [错误] 未找到 SwComAddin.dll
    echo 请确认 DLL 文件与本脚本在同一目录下。
    echo 预期路径: %DLL%
    echo.
    pause
    exit /b 1
)

REM ---- Register the COM add-in ----
echo 正在注册 SolidWorks AI Plugin...
echo.
echo DLL:  %DLL%
echo RegAsm: %REGASM%
echo.

"%REGASM%" "%DLL%" /codebase /tlb
if %errorlevel% neq 0 (
    echo.
    echo [错误] 注册失败！错误代码: %errorlevel%
    echo 请检查：
    echo   1. 是否以管理员身份运行
    echo   2. DLL 文件是否完整
    echo   3. .NET Framework 4.x 是否已安装
    echo.
    pause
    exit /b 1
)

echo.
echo ========================================
echo   安装成功！
echo ========================================
echo.
echo   接下来的步骤：
echo   1. 打开 SolidWorks
echo   2. 菜单: 工具(T) ^> 插件(I)...
echo   3. 勾选 "SolidWorks AI Plugin"
echo   4. 点击确定
echo   5. 插件将出现在右侧任务窗格中
echo.
echo ========================================
echo.
pause
