@echo off
rem Сборка установщика Tebegram двойным кликом
powershell -NoProfile -File "%~dp0build-installer.ps1"
pause
