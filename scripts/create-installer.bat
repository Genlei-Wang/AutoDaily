@echo off
chcp 65001 >nul
echo ========================================
echo AutoDaily 安装包生成工具
echo ========================================
echo.

REM 检查是否在项目根目录
if not exist "AutoDaily.sln" (
    echo 错误：请在项目根目录运行此脚本
    pause
    exit /b 1
)

REM 调用 PowerShell 脚本
powershell -ExecutionPolicy Bypass -File "%~dp0create-installer.ps1" -Version "1.0.0"

pause

