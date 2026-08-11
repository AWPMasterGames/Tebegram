# Установщик Tebegram

Каталог содержит исходники установщика на Inno Setup. Готовые файлы `setup.exe`
в репозитории не хранятся: они публикуются на странице
[Releases](https://github.com/AWPMasterGames/Tebegram/releases).

## Требования

Inno Setup 6. Устанавливается один раз:

```
winget install JRSoftware.InnoSetup
```

Ссылка на дистрибутив: [jrsoftware.org/isdl.php](https://jrsoftware.org/isdl.php).

## Сборка

```
Installer\build-installer.bat
```

То же самое из консоли:

```
powershell -File Installer\build-installer.ps1
```

Скрипт публикует клиент со встроенной средой выполнения .NET и вызывает Inno Setup.
Пользователю устанавливать .NET отдельно не требуется. Результат появляется в
`Installer\Output\TebegramSetup-<версия>.exe`.

## Состав каталога

| Файл | Назначение |
|---|---|
| `TebegramSetup.iss` | сценарий Inno Setup: состав пакета, ярлыки, версия |
| `build-installer.ps1` | публикация клиента и вызов компилятора Inno Setup |
| `build-installer.bat` | запуск скрипта двойным щелчком |
| `Output/` | каталог результата, в репозиторий не попадает |

## Выпуск обновления

1. Поднять версию в трёх местах, значения должны совпадать:

   | Файл | Параметр |
   |---|---|
   | `Installer\TebegramSetup.iss` | `MyAppVersion` |
   | `Tebegram-client\Classes\UpdateChecker.cs` | `CurrentVersion` |
   | `version.txt` в корне репозитория | вся строка |

2. Собрать установщик локально либо отправить тег вида `v2.0.1`: сценарий
   `.github/workflows/build-installer.yml` соберёт пакет на стороне GitHub
   и приложит его к релизу.
3. При ручной сборке выложить `TebegramSetup-<версия>.exe` в Releases.
4. Слить `version.txt` в ветку `main`.

Проверка обновлений в клиенте читает `version.txt` из ветки `main` и сравнивает
значение с `UpdateChecker.CurrentVersion`. При расхождении показывается сообщение
о доступной версии со ссылкой на страницу загрузки.

## Развитие

Полное автообновление реализуется на существующем каркасе `UpdateChecker`:
клиент загружает `TebegramSetup-<версия>.exe` из Releases и запускает его с
ключами `/SILENT /CLOSEAPPLICATIONS`, после чего Inno Setup обновляет приложение
поверх текущей установки и перезапускает его.
