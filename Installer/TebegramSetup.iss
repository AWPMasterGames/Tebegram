; ─────────────────────────────────────────────────────────────────────────────
; Tebegram — установщик Inno Setup (обычный setup.exe)
;
; Перед компиляцией нужно опубликовать клиент со встроенным .NET:
;   запусти Installer\build-installer.ps1 — он сделает всё сам
;   (dotnet publish + компиляция этого скрипта).
;
; Версию поднимай синхронно в трёх местах:
;   1) MyAppVersion ниже
;   2) Tebegram-client\Classes\UpdateChecker.cs (CurrentVersion)
;   3) version.txt в корне репозитория (ветка main) — по нему клиенты
;      узнают о выходе обновления
; ─────────────────────────────────────────────────────────────────────────────

#define MyAppName "Tebegram"
#define MyAppVersion "1.0.7"
#define MyAppPublisher "Tebegram"
#define MyAppURL "https://github.com/AWPMasterGames/Tebegram"
#define MyAppExeName "Tebegrammmm.exe"
; Папка с результатом dotnet publish (заполняется build-installer.ps1)
#ifndef PublishDir
  #define PublishDir "..\Tebegram-client\bin\Release\net8.0-windows\win-x64\publish"
#endif

[Setup]
; Уникальный Id приложения — НЕ менять между версиями, иначе сломается обновление поверх
AppId={{8C6E4B7A-2D91-4A6F-B3E8-5F0A9C7D1234}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}/releases
; Установка без прав администратора — в папку пользователя (как Telegram/Discord).
; Это же избавляет от проблем с записью файлов в Program Files.
PrivilegesRequired=lowest
DefaultDirName={autopf}\{#MyAppName}
DisableProgramGroupPage=yes
OutputDir=Output
OutputBaseFilename=TebegramSetup-{#MyAppVersion}
SetupIconFile=..\Tebegram-client\Icon.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
; Клиент собирается только под x64
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[InstallDelete]
; ПЕРВАЯ установка не должна открываться под чужим аккаунтом: сохранённый вход
; мог остаться на машине от прошлых запусков (в т.ч. тестовых — test1), и
; поставленное приложение молча заходило под ним.
;
; При ОБНОВЛЕНИИ поверх существующей версии вход НЕ трогаем — иначе после каждого
; обновления пришлось бы вводить логин и пароль заново (см. IsFreshInstall).
; Обычное «закрыл и открыл приложение» вход тоже сохраняет: файл читается из
; AppData и удаляется только кнопкой «Выйти из аккаунта».
Type: files; Name: "{localappdata}\Tebegram\user.data"; Check: IsFreshInstall
; Старый файл входа рядом с exe убираем всегда: приложение его больше не читает,
; но и лежать паролю в папке установки незачем
Type: files; Name: "{app}\user.data"

[Files]
; Весь результат dotnet publish (self-contained — .NET уже внутри)
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Пользовательские данные приложения (сохранённый вход, настройки)
Type: filesandordirs; Name: "{localappdata}\Tebegram"

[Code]
{ Ставим ли ПОВЕРХ уже установленной версии? Определяем по записи деинсталлятора:
  Inno Setup создаёт её как <AppId>_is1. Установка «без прав администратора»
  (PrivilegesRequired=lowest) пишет запись в HKCU.

  ВАЖНО: строка ниже должна совпадать с AppId из секции [Setup]. Если AppId
  когда-нибудь поменяется — поправить и здесь, иначе обновление будет считаться
  первой установкой и станет сбрасывать сохранённый вход. }
function IsFreshInstall: Boolean;
var
  UninstallString: String;
begin
  Result := not RegQueryStringValue(HKEY_CURRENT_USER,
    'Software\Microsoft\Windows\CurrentVersion\Uninstall\{8C6E4B7A-2D91-4A6F-B3E8-5F0A9C7D1234}_is1',
    'UninstallString', UninstallString);
end;
