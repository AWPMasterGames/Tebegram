@echo off
echo ================================================
echo   LocalTunnel запуск для Tebegram Server
echo ================================================
echo Дата и время: %date% %time%
echo.

echo Проверяем установку Node.js...
node --version >nul 2>&1
if %errorlevel% neq 0 (
    echo ОШИБКА: Node.js не найден!
    echo Пожалуйста, установите Node.js с https://nodejs.org
    pause
    exit /b 1
)

echo Node.js найден: 
node --version

echo.
echo Проверяем установку LocalTunnel...
where lt >nul 2>nul
if %errorlevel% neq 0 (
    echo LocalTunnel не найден. Устанавливаем глобально...
    npm install -g localtunnel
    if %errorlevel% neq 0 (
        echo ОШИБКА: Не удалось установить LocalTunnel глобально
        echo Попробуйте локальную установку...
        npm install localtunnel
        if %errorlevel% neq 0 (
            echo ОШИБКА: Не удалось установить LocalTunnel
            echo Проверьте права доступа или запустите от имени администратора
            pause
            exit /b 1
        )
        set USE_NPX=1
    )
) else (
    echo LocalTunnel уже установлен глобально
    set USE_NPX=0
)

echo.
echo ================================================
echo   Запуск LocalTunnel
echo ================================================
echo Порт: 5000
echo Поддомен: tebegrammmm  
echo Публичный URL: http://tebegrammmm.loca.lt
echo.

echo ИНФОРМАЦИЯ: LocalTunnel может запросить пароль в браузере
echo Это нормально - используйте любой символ и нажмите Enter
echo.

echo Запускаем LocalTunnel...
echo Время запуска: %time%
echo.

:: Запускаем localtunnel в зависимости от способа установки
if %USE_NPX%==1 (
    echo Используем npx для запуска...
    npx localtunnel --port 5000 --subdomain tebegrammmm
) else (
    echo Используем глобальную команду lt...
    lt --port 5000 --subdomain tebegrammmm
)

if %errorlevel% neq 0 (
    echo.
    echo ================================================
    echo   ОШИБКА при запуске LocalTunnel
    echo ================================================
    echo Код ошибки: %errorlevel%
    echo Время ошибки: %time%
    echo.
    echo Возможные причины:
    echo 1. Сервер на порту 5000 не запущен
    echo 2. Поддомен 'tebegrammmm' уже занят
    echo 3. Проблемы с сетевым подключением
    echo 4. Превышен лимит запросов к localtunnel
    echo.
    echo Что делать:
    echo 1. Убедитесь, что сервер запущен: dotnet run
    echo 2. Попробуйте другой поддомен
    echo 3. Проверьте интернет соединение
    echo 4. Подождите несколько минут и попробуйте снова
    echo.
) else (
    echo.
    echo ================================================
    echo   LocalTunnel запущен успешно!
    echo ================================================
    echo Время успешного запуска: %time%
    echo Туннель доступен по адресу: http://tebegrammmm.loca.lt
    echo.
)

echo Нажмите любую клавишу для завершения...
pause >nul
