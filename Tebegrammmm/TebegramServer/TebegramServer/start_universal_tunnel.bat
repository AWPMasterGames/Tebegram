@echo off
chcp 65001 >nul
title 🌐 Universal LocalTunnel for Tebegram
color 0B
setlocal enabledelayedexpansion

echo.
echo ╔════════════════════════════════════════════════╗
echo ║         🌐 UNIVERSAL LOCALTUNNEL LAUNCHER      ║
echo ║                                                ║
echo ║  Принимает ЛЮБОЙ URL и автоматически сохраняет ║
echo ║  его для всех клиентов                         ║
echo ╚════════════════════════════════════════════════╝
echo.

REM Проверяем сервер
echo 🔍 Проверяем сервер на порту 5000...
curl -s http://localhost:5000 >nul 2>&1
if errorlevel 1 (
    echo ❌ Сервер не отвечает на порту 5000!
    echo    Сначала запустите: dotnet run
    pause
    exit /b 1
)
echo ✅ Сервер работает

echo.
echo 🚀 Запускаем LocalTunnel (принимаем любой URL)...
echo 💡 LocalTunnel может выдать любой субдомен - это нормально!
echo.

REM Создаем временный файл для захвата вывода
set TUNNEL_LOG=%TEMP%\localtunnel_output.log
del "%TUNNEL_LOG%" 2>nul

echo ⏳ Запускаем туннель...
REM Запускаем lt без субдомена (случайный URL)
start /b cmd /c "echo yes | lt --port 5000 > %TUNNEL_LOG% 2>&1"

REM Ждем появления URL
set ATTEMPTS=0
:WAIT_FOR_URL
timeout /t 2 /nobreak >nul
set /a ATTEMPTS+=1

if exist "%TUNNEL_LOG%" (
    REM Ищем строку с URL
    for /f "tokens=*" %%i in ('findstr /c:"your url is" "%TUNNEL_LOG%" 2^>nul') do (
        set "TUNNEL_LINE=%%i"
        REM Извлекаем URL из строки "your url is: https://..."
        for /f "tokens=4" %%u in ("!TUNNEL_LINE!") do (
            set "TUNNEL_URL=%%u"
            goto URL_FOUND
        )
    )
)

if %ATTEMPTS% LSS 15 (
    echo ⏳ Ожидание URL... попытка %ATTEMPTS%/15
    goto WAIT_FOR_URL
)

echo ❌ Не удалось получить URL туннеля за 30 секунд
pause
exit /b 1

:URL_FOUND
echo.
echo ✅ ТУННЕЛЬ УСПЕШНО СОЗДАН!
echo 🌐 Полученный URL: !TUNNEL_URL!

REM Конвертируем HTTPS в HTTP если нужно
set "HTTP_URL=!TUNNEL_URL!"
set "HTTP_URL=!HTTP_URL:https://=http://!"

echo 🔄 HTTP версия: !HTTP_URL!

REM Сохраняем URL в файл для клиентов
echo !HTTP_URL! > "%~dp0tunnel_url.txt"
echo 📝 URL сохранен в tunnel_url.txt

REM Сохраняем также для разработки
echo !HTTP_URL! > "%~dp0server_address.txt"

echo.
echo ╔════════════════════════════════════════════════╗
echo ║                ✅ ТУННЕЛЬ АКТИВЕН              ║
echo ║                                                ║
echo ║  🌐 Локальный:  http://localhost:5000         ║
echo ║  🌍 Публичный:  !HTTP_URL!          ║
echo ║                                                ║
echo ║  📝 Адрес сохранен в файлы:                    ║
echo ║     - tunnel_url.txt                           ║
echo ║     - server_address.txt                       ║
echo ║                                                ║
echo ║  ⚠️  НЕ ЗАКРЫВАЙТЕ ЭТО ОКНО!                   ║
echo ║  📱 Теперь можно запускать клиенты             ║
echo ╚════════════════════════════════════════════════╝
echo.

REM Показываем инструкции для клиентов
echo 📋 ИНСТРУКЦИИ ДЛЯ КЛИЕНТОВ:
echo    1. Запустите клиент Tebegram
echo    2. Клиент автоматически найдет адрес сервера
echo    3. Если не работает - проверьте файл tunnel_url.txt
echo.
echo 📊 Начинаем мониторинг...

:MONITOR
timeout /t 10 /nobreak >nul
echo ⏰ %date% %time% - Туннель работает

REM Проверяем локальный сервер
curl -s http://localhost:5000 >nul 2>&1
if errorlevel 1 (
    echo ❌ Локальный сервер недоступен!
    goto MONITOR
)

REM Проверяем публичный туннель
curl -s "!HTTP_URL!" >nul 2>&1
if errorlevel 1 (
    echo ⚠️  Публичный туннель недоступен - проверяем...
) else (
    echo ✅ Все системы работают
)

goto MONITOR
