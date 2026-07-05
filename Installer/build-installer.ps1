# ─────────────────────────────────────────────────────────────────────────────
# Сборка установщика Tebegram одной командой.
#
#   1. Публикует клиент self-contained (со встроенным .NET, x64)
#   2. Компилирует Installer\TebegramSetup.iss через Inno Setup 6
#   3. Результат: Installer\Output\TebegramSetup-<версия>.exe
#
# Требуется: .NET SDK и Inno Setup 6 (https://jrsoftware.org/isdl.php
# или: winget install JRSoftware.InnoSetup)
#
# Запуск:  powershell -File Installer\build-installer.ps1
# ─────────────────────────────────────────────────────────────────────────────
$ErrorActionPreference = 'Stop'

$repoRoot   = Split-Path -Parent $PSScriptRoot
$clientProj = Join-Path $repoRoot 'Tebegram-client\Tebegrammmm.csproj'
$publishDir = Join-Path $repoRoot 'Tebegram-client\bin\Release\net8.0-windows\win-x64\publish'
$issFile    = Join-Path $PSScriptRoot 'TebegramSetup.iss'

Write-Host '[1/2] Публикация клиента (self-contained, win-x64)...' -ForegroundColor Cyan
dotnet publish $clientProj -c Release -r win-x64 --self-contained true -p:PublishReadyToRun=true
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish завершился с ошибкой' }

if (-not (Test-Path (Join-Path $publishDir 'Tebegrammmm.exe'))) {
    throw "Не найден результат публикации: $publishDir"
}

Write-Host '[2/2] Компиляция установщика (Inno Setup)...' -ForegroundColor Cyan
$isccCandidates = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
)
$iscc = $isccCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) {
    throw 'Inno Setup 6 не найден. Установи его: winget install JRSoftware.InnoSetup'
}

& $iscc "/DPublishDir=$publishDir" $issFile
if ($LASTEXITCODE -ne 0) { throw 'Компиляция установщика завершилась с ошибкой' }

$output = Get-ChildItem (Join-Path $PSScriptRoot 'Output') -Filter 'TebegramSetup-*.exe' |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1
Write-Host ''
Write-Host "Готово: $($output.FullName)" -ForegroundColor Green
Write-Host 'Этот .exe выкладывай в GitHub Releases (не в сам репозиторий!).' -ForegroundColor Yellow
