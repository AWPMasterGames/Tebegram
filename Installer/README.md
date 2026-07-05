# Установщик Tebegram

## Как собрать setup.exe

```
Installer\build-installer.bat        (двойной клик)
```

или из консоли:

```
powershell -File Installer\build-installer.ps1
```

Скрипт сам публикует клиент **со встроенным .NET** (пользователю ничего
доустанавливать не нужно) и компилирует классический `setup.exe` через Inno Setup.
Результат появится в `Installer\Output\TebegramSetup-<версия>.exe`.

Требуется один раз установить [Inno Setup 6](https://jrsoftware.org/isdl.php):

```
winget install JRSoftware.InnoSetup
```

## Почему «файлы терялись» при заливке на GitHub

Ничего не терялось — в `.gitignore` проекта игнорируются папки `bin/` и `obj/`
(и это правильно: собранные бинарники в git не хранят). В папке `Installer`
остаются только **исходники** установщика:

- `TebegramSetup.iss`, `build-installer.ps1(.bat)` — новый Inno Setup установщик (.exe)
- `*.wxs`, `*.wixproj`, `*.wxl` — старый WiX-проект (.msi), оставлен как альтернатива

Собранные `setup.exe`/`.msi` в репозиторий класть не нужно — их выкладывают в
**GitHub Releases** (страница релизов репозитория). Там файлы не «теряются»
и у каждого релиза своя версия.

Также в репозитории есть workflow `.github/workflows/build-installer.yml`:
при создании тега вида `v1.0.4` GitHub **сам соберёт установщик** и приложит
его к релизу — на своей машине можно вообще ничего не собирать.

## Как выпустить обновление

1. Подними версию в трёх местах:
   - `Installer\TebegramSetup.iss` → `MyAppVersion`
   - `Tebegram-client\Classes\UpdateChecker.cs` → `CurrentVersion`
   - `version.txt` в корне репозитория
2. Собери установщик (`build-installer.bat`) или запушь тег `v<версия>` —
   GitHub Actions соберёт сам.
3. Выложи `TebegramSetup-<версия>.exe` в GitHub Releases (если собирал вручную).
4. Смержи `version.txt` в ветку `main`.

После этого все клиенты при запуске увидят сообщение «Доступна новая версия»
с кнопкой перехода на страницу загрузки (`Classes/UpdateChecker.cs`).

Идея на будущее для полного автообновления «по одной кнопке»: клиент может сам
скачивать `TebegramSetup-<версия>.exe` из Releases и запускать его с ключами
`/SILENT /CLOSEAPPLICATIONS` — Inno Setup тихо обновит приложение поверх и
перезапустит его. Каркас для этого уже есть в `UpdateChecker`.
