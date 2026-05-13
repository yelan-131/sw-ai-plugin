@echo off
chcp 65001 >nul
setlocal enabledelayedexpansion

echo ========================================
echo   SolidWorks AI Plugin 启动中...
echo ========================================

set BACKEND_PID=
set FRONTEND_PID=

REM 启动后端服务
echo [1/2] 启动 AI 后端服务...
cd /d "%~dp0SwAiBackend"
start /min "SW AI Backend" cmd /c "python app.py && pause"

REM 等待后端就绪
echo [2/2] 等待后端就绪...
timeout /t 2 /nobreak >nul

REM 启动前端
echo [3/3] 启动 WPF 前端...
cd /d "%~dp0SwAiPlugin"
dotnet run

REM 清理：关闭后端窗口
taskkill /FI "WINDOWTITLE eq SW AI Backend*" /F >nul 2>&1

echo.
echo 程序已退出。
pause
