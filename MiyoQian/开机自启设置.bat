@echo off
chcp 65001 >nul
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\autostart.ps1"
if errorlevel 1 (
    echo [错误] 开机自启设置脚本运行失败，请检查项目环境。
    pause
)
