@echo off
chcp 65001 >nul
title 🚀 Tebegram - Быстрый запуск
color 0E

echo.
echo ╔════════════════════════════════════════════╗
echo ║          🚀 TEBEGRAM QUICK START           ║
echo ║                                            ║
echo ║  Быстрый запуск без проверок               ║
echo ║  (используйте, если зависимости готовы)    ║
echo ╚════════════════════════════════════════════╝
echo.

if not exist "Program.cs" (
    echo ❌ Запустите из папки TebegramServer/TebegramServer/
    pause
    exit /b 1
)

echo 🚀 Запуск сервера...
start "Tebegram Server" dotnet run --configuration Release

echo ⏳ Ожидание сервера (5 сек)...
timeout /t 5 /nobreak >nul

echo 🌐 Запуск LocalTunnel...
start "Tebegram Tunnel" cmd /c "echo yes | lt --port 5000 --subdomain tebegrammmm"

echo ⏳ Ожидание туннеля (8 сек)...
timeout /t 8 /nobreak >nul

echo.
echo ✅ Система запущена!
echo 🌐 Локальный:  http://localhost:5000
echo 🌍 Публичный:  http://tebegrammmm.loca.lt
echo.
echo ⚠️  Не закрывайте окна сервера и туннеля!
echo 🎯 Теперь запускайте клиентские приложения
echo.
pause
