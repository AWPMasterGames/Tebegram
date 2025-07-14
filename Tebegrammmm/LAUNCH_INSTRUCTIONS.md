# 🚀 ОБНОВЛЕННАЯ ИНСТРУКЦИЯ ПО ЗАПУСКУ TEBEGRAM

## ⚠️ ВАЖНЫЕ ИЗМЕНЕНИЯ: Новый универсальный туннель

### 🗂️ **Шаг 1: Переходим в правильную папку**

**ОБЯЗАТЕЛЬНО откройте терминал в папке сервера!**

```bash
# Вариант 1: Через командную строку
cd "C:\Users\makar\Documents\GitHub\Tebegram\Tebegrammmm\TebegramServer\TebegramServer"

# Вариант 2: Через проводник
1. Откройте папку: C:\Users\makar\Documents\GitHub\Tebegram\Tebegrammmm\TebegramServer\TebegramServer\
2. В адресной строке наберите: cmd
3. Нажмите Enter
```

### 🎯 **Шаг 2: Запускаем систему**

**В открывшемся терминале выполните:**

```bash
# Основной способ (полная система)
start_full_system.bat

# Альтернативный (только туннель)
start_universal_tunnel.bat
```

### 🆕 **ЧТО ИЗМЕНИЛОСЬ:**

- **Универсальный туннель**: Теперь принимает ЛЮБОЙ URL от LocalTunnel
- **Автосохранение адреса**: URL автоматически сохраняется в `tunnel_url.txt`
- **Умное определение**: Клиенты автоматически находят правильный адрес
- **Нет привязки к поддомену**: Работает с любым случайным URL

### 🔍 **Что увидите при запуске:**

```
✅ ТУННЕЛЬ УСПЕШНО СОЗДАН!
🌐 Полученный URL: https://bad-bat-63.loca.lt
🔄 HTTP версия: http://bad-bat-63.loca.lt
📝 URL сохранен в tunnel_url.txt
```

**Это нормально!** LocalTunnel может выдать любой случайный URL - система автоматически сохранит его.

### 📁 **Шаг 3: Запускаем клиентов**

После того, как сервер запустился:

1. Перейдите в папку: `C:\Users\makar\Documents\GitHub\Tebegram\Tebegrammmm\Tebegrammmm\bin\Debug\net8.0-windows\`
2. Запустите: `Tebegrammmm.exe`
3. Повторите для других клиентов

## 🔧 **Автоматический запуск (если лень вводить команды)**

Создайте ярлык на рабочем столе:

1. **Правый клик на рабочем столе** → Создать → Ярлык
2. **Объект**: 
   ```
   cmd /c "cd /d C:\Users\makar\Documents\GitHub\Tebegram\Tebegrammmm\TebegramServer\TebegramServer && start_full_system.bat"
   ```
3. **Имя**: "Запуск Tebegram Server"
4. **Готово!** Теперь двойной клик запустит всё правильно

## 📋 **Что должно произойти при правильном запуске:**

```
✅ .NET версия: 8.x.x
✅ Node.js версия: 22.x.x  
✅ LocalTunnel версия: x.x.x
✅ Проект собран успешно!
🌐 Сервер запущен на http://localhost:5000
✅ LocalTunnel активен!
   🌐 Публичный адрес: http://[subdomain].loca.lt
✅ СИСТЕМА ЗАПУЩЕНА!
```

## 🐛 **Если что-то не работает:**

### Проблема: "Скрипт должен быть запущен из папки TebegramServer/TebegramServer/"
**Решение**: Убедитесь, что вы находитесь в правильной папке перед запуском

### Проблема: "Node.js не найден"
**Решение**: Установите Node.js с https://nodejs.org/

### Проблема: ".NET не найден"
**Решение**: Установите .NET 8 SDK с https://dotnet.microsoft.com/download

### Проблема: "Туннель недоступен"
**Решение**: Попробуйте `start_smart_tunnel.bat` - он найдет свободный субдомен

---

## 🎯 **Краткая версия для опытных:**

```bash
cd "C:\Users\makar\Documents\GitHub\Tebegram\Tebegrammmm\TebegramServer\TebegramServer"
start_full_system.bat
# Дождитесь "СИСТЕМА ЗАПУЩЕНА!"
# Запустите Tebegrammmm.exe из папки bin\Debug\net8.0-windows\
```
