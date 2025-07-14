@echo off
chcp 65001 >nul
title 🌐 LocalTunnel Smart Start
color 0B

echo.
echo ╔════════════════════════════════════════════════╗
echo ║          🌐 SMART LOCALTUNNEL LAUNCHER         ║
echo ║                                                ║
echo ║  Попробует несколько субдоменов по порядку     ║
echo ║  до успешного подключения                      ║
echo ╚════════════════════════════════════════════════╝
echo.

REM Список субдоменов для попытки
set SUBDOMAINS=tebegrammmm tebegram-server tebegram-chat tebegram-msg tebegram-app

echo 🔍 Проверяем, что сервер запущен на порту 5000...
curl -s http://localhost:5000 >nul 2>&1
if errorlevel 1 (
    echo ❌ Сервер не отвечает на порту 5000!
    echo    Сначала запустите сервер: dotnet run
    pause
    exit /b 1
)
echo ✅ Сервер работает на порту 5000

echo.
echo 🚀 Начинаем поиск свободного субдомена...

REM Пытаемся подключиться с разными субдоменами
for %%s in (%SUBDOMAINS%) do (
    echo.
    echo 🔄 Пробуем субдомен: %%s
    echo    URL будет: http://%%s.loca.lt
    
    REM Создаем временный файл для лога этой попытки
    set ATTEMPT_LOG=%TEMP%\lt_attempt_%%s.log
    
    REM Запускаем LocalTunnel с таймаутом
    timeout /t 1 /nobreak >nul
    echo yes | lt --port 5000 --subdomain %%s > "%TEMP%\lt_attempt_%%s.log" 2>&1 &
    
    REM Ждем 10 секунд и проверяем результат
    echo ⏳ Ожидание подключения (10 сек)...
    timeout /t 10 /nobreak >nul
    
    REM Проверяем, появился ли URL в логе
    if exist "%TEMP%\lt_attempt_%%s.log" (
        findstr /C:"your url is" "%TEMP%\lt_attempt_%%s.log" >nul 2>&1
        if not errorlevel 1 (
            echo ✅ УСПЕХ! Найден свободный субдомен: %%s
            
            REM Извлекаем URL из лога
            for /f "tokens=*" %%u in ('findstr /C:"your url is" "%TEMP%\lt_attempt_%%s.log"') do (
                echo 🌐 Публичный адрес: %%u
                
                REM Сохраняем успешный URL в файл для клиентов
                echo %%u > "%~dp0tunnel_url.txt"
                echo 📝 URL сохранен в tunnel_url.txt
            )
            
            echo.
            echo ╔════════════════════════════════════════════════╗
            echo ║                ✅ ТУННЕЛЬ АКТИВЕН              ║
            echo ║                                                ║
            echo ║  🌐 Локальный:  http://localhost:5000         ║
            echo ║  🌍 Публичный:  http://%%s.loca.lt     ║
            echo ║                                                ║
            echo ║  📝 URL сохранен в tunnel_url.txt              ║
            echo ║  ⚠️  Не закрывайте это окно!                   ║
            echo ╚════════════════════════════════════════════════╝
            echo.
            
            REM Мониторинг туннеля
            goto MONITOR
        )
    )
    
    echo ❌ Субдомен %%s недоступен или занят
    
    REM Останавливаем процесс LocalTunnel для этой попытки
    taskkill /f /im node.exe >nul 2>&1
    timeout /t 2 /nobreak >nul
)

echo.
echo ❌ Все субдомены заняты или недоступны!
echo 💡 Попробуйте:
echo    1. Подождать несколько минут
echo    2. Использовать другой сервис туннелирования
echo    3. Запустить с случайным субдоменом: lt --port 5000
pause
exit /b 1

:MONITOR
echo 📊 Мониторинг туннеля (проверка каждые 30 сек)...
echo ⏰ %date% %time%

REM Проверяем локальный сервер
curl -s http://localhost:5000 >nul 2>&1
if errorlevel 1 (
    echo ❌ Локальный сервер недоступен!
) else (
    echo ✅ Локальный сервер работает
)

REM Проверяем публичный туннель
if exist "%~dp0tunnel_url.txt" (
    set /p TUNNEL_URL=<"%~dp0tunnel_url.txt"
    curl -s "%TUNNEL_URL%" >nul 2>&1
    if errorlevel 1 (
        echo ⚠️  Публичный туннель недоступен
    ) else (
        echo ✅ Публичный туннель работает
    )
)

echo.
timeout /t 30 /nobreak >nul
goto MONITOR
