@echo off

chcp 65001 >nul

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\launcher.ps1" -Action Start

if errorlevel 1 pause

