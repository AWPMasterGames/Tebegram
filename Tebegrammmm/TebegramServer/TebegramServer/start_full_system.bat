@echo off
setlocal enabledelayedexpansion
chcp 65001 >nul
title 🚀 Tebegram - Полный запуск системы
color 0A

echo.
echo ╔══════════════════════════════════════════════════════════════╗
echo ║                    🚀 TEBEGRAM SYSTEM LAUNCHER               ║
echo ║                                                              ║
echo ║  Этот скрипт запустит:                                       ║
echo ║  1. ASP.NET Core сервер на порту 5000                        ║
echo ║  2. Универсальный LocalTunnel (любой URL)                    ║
echo ║                                                              ║
echo ║  🌐 Адрес будет автоматически сохранен в файл               ║
echo ╚══════════════════════════════════════════════════════════════╝
echo.

REM Проверяем, что мы в правильной папке
if not exist "Program.cs" (
    echo ❌ ОШИБКА: Скрипт должен быть запущен из папки TebegramServer/TebegramServer/
    echo    Текущая папка: %CD%
    echo    Ожидаемые файлы: Program.cs, TebegramServer.csproj
    echo.
    pause
    exit /b 1
)

echo 🔍 Проверка зависимостей...

REM Проверяем .NET
dotnet --version >nul 2>&1
if errorlevel 1 (
    echo ❌ .NET не найден! Установите .NET 8 SDK
    echo    Скачать: https://dotnet.microsoft.com/download
    pause
    exit /b 1
) else (
    for /f "tokens=*" %%i in ('dotnet --version') do echo ✅ .NET версия: %%i
)

REM Проверяем Node.js
node --version >nul 2>&1
if errorlevel 1 (
    echo ❌ Node.js не найден! Устанавливаю...
    echo    Если автоустановка не сработает, скачайте с https://nodejs.org/
    
    REM Попытка установить через Chocolatey
    choco install nodejs -y >nul 2>&1
    if errorlevel 1 (
        echo ❌ Не удалось автоматически установить Node.js
        echo    Установите вручную с https://nodejs.org/
        pause
        exit /b 1
    )
    
    echo ✅ Node.js установлен!
    refreshenv >nul 2>&1
) else (
    for /f "tokens=*" %%i in ('node --version') do echo ✅ Node.js версия: %%i
)

REM Проверяем LocalTunnel
lt --version >nul 2>&1
if errorlevel 1 (
    echo 📦 Устанавливаю LocalTunnel...
    call npm install -g localtunnel
    if errorlevel 1 (
        echo ❌ Не удалось установить LocalTunnel
        echo    Попробуйте выполнить: npm install -g localtunnel
        pause
        exit /b 1
    )
    echo ✅ LocalTunnel установлен!
) else (
    for /f "tokens=*" %%i in ('lt --version') do echo ✅ LocalTunnel версия: %%i
)

echo.
echo 🔧 Сборка проекта...
dotnet build --configuration Release --verbosity quiet
if errorlevel 1 (
    echo ❌ Ошибка сборки проекта!
    echo    Проверьте код на наличие ошибок
    pause
    exit /b 1
)
echo ✅ Проект собран успешно!

echo.
echo 🚀 Запуск системы...
echo.
echo ⚠️  ВАЖНО: Не закрывайте это окно!
echo    Для остановки системы нажмите Ctrl+C
echo.

REM Создаем временные файлы для логов
set SERVER_LOG=%TEMP%\tebegram_server.log
set TUNNEL_LOG=%TEMP%\tebegram_tunnel.log

echo 📊 Логи сохраняются в:
echo    Сервер: %SERVER_LOG%
echo    Туннель: %TUNNEL_LOG%
echo.

REM Запускаем сервер в фоне
echo 🌐 Запуск сервера на http://localhost:5000...
start "Tebegram Server" /min cmd /c "dotnet run --configuration Release > \"%SERVER_LOG%\" 2>&1"

REM Ждем запуска сервера
echo ⏳ Ожидание запуска сервера...
timeout /t 5 /nobreak >nul

REM Проверяем, что сервер запустился
:CHECK_SERVER
echo 🔍 Проверка сервера...
curl -s http://localhost:5000 >nul 2>&1
if errorlevel 1 (
    echo ⏳ Сервер еще запускается... (ожидание 3 сек)
    timeout /t 3 /nobreak >nul
    goto CHECK_SERVER
)

echo ✅ Сервер запущен на http://localhost:5000

REM Запускаем универсальный туннель
echo 🌐 Запуск универсального LocalTunnel...
echo    Принимаем любой доступный URL...

REM Запускаем универсальный туннель в отдельном окне
start "Tebegram Universal Tunnel" cmd /c "start_universal_tunnel.bat"

REM Ждем создания файла с URL
echo ⏳ Ожидание получения URL туннеля...
set WAIT_ATTEMPTS=0
:WAIT_TUNNEL_URL
timeout /t 2 /nobreak >nul
set /a WAIT_ATTEMPTS+=1

if exist "tunnel_url.txt" (
    set /p TUNNEL_URL=<tunnel_url.txt
    echo ✅ Получен URL туннеля: !TUNNEL_URL!
    goto TUNNEL_READY
)

if %WAIT_ATTEMPTS% LSS 20 (
    echo ⏳ Ожидание URL... попытка %WAIT_ATTEMPTS%/20
    goto WAIT_TUNNEL_URL
)

echo ❌ Не удалось получить URL туннеля за 40 секунд
echo 💡 Попробуйте запустить start_universal_tunnel.bat вручную
echo.
pause
)

echo ⏳ Туннель еще устанавливается... (ожидание 3 сек)
timeout /t 3 /nobreak >nul
goto CHECK_TUNNEL

:TUNNEL_READY
echo.
echo ╔══════════════════════════════════════════════════════════════╗
echo ║                    ✅ СИСТЕМА ЗАПУЩЕНА!                      ║
echo ║                                                              ║
echo ║  🌐 Локальный адрес:  http://localhost:5000                 ║
echo ║  🌍 Публичный адрес:  http://tebegrammmm.loca.lt            ║
echo ║                                                              ║
echo ║  📊 Мониторинг:                                              ║
echo ║     • Сервер лог:     %TEMP%\tebegram_server.log ║
echo ║     • Туннель лог:    %TEMP%\tebegram_tunnel.log ║
echo ║                                                              ║
echo ║  🎯 Теперь можно запускать клиентские приложения!           ║
echo ║                                                              ║
echo ║  ⚠️  Для остановки нажмите Ctrl+C                           ║
echo ╚══════════════════════════════════════════════════════════════╝
echo.

REM Мониторинг системы
:MONITOR
echo 📊 Мониторинг системы... (обновление каждые 30 сек)
echo    Время: %date% %time%

REM Проверяем сервер
curl -s http://localhost:5000 >nul 2>&1
if errorlevel 1 (
    echo ❌ Сервер недоступен!
    goto ERROR_RECOVERY
) else (
    echo ✅ Сервер работает
)

REM Проверяем туннель
curl -s http://tebegrammmm.loca.lt >nul 2>&1
if errorlevel 1 (
    echo ⚠️  Туннель недоступен (возможно, временная проблема)
) else (
    echo ✅ Туннель работает
)

echo.
timeout /t 30 /nobreak >nul
goto MONITOR

:ERROR_RECOVERY
echo.
echo ❌ Обнаружена проблема с сервером!
echo    Попытка перезапуска...
echo.

REM Останавливаем процессы
taskkill /f /im dotnet.exe >nul 2>&1
taskkill /f /im node.exe >nul 2>&1

echo 🔄 Перезапуск через 5 секунд...
timeout /t 5 /nobreak >nul

REM Запускаем заново
goto :EOF

REM Обработка завершения
:EXIT
echo.
echo 🛑 Остановка системы...
taskkill /f /im dotnet.exe >nul 2>&1
taskkill /f /im node.exe >nul 2>&1
echo ✅ Система остановлена
pause
