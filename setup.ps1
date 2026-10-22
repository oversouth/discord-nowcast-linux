# Установка и удаление: ярлык в автозагрузке, который запускает nowcast.ps1
# без окна, и остановка уже запущенной копии.

param(
    [Parameter(Mandatory)][ValidateSet('install', 'uninstall')][string]$Action
)

$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$script = Join-Path $here 'nowcast.ps1'
$shortcut = Join-Path ([Environment]::GetFolderPath('Startup')) 'discord-nowcast.lnk'

# conhost --headless запускает PowerShell совсем без окна, даже без мелькания.
$target = Join-Path $env:SystemRoot 'System32\conhost.exe'
$arguments = "--headless powershell.exe -NoProfile -ExecutionPolicy Bypass -File `"$script`""

function Stop-Nowcast {
    Get-CimInstance Win32_Process -Filter "Name = 'powershell.exe'" |
        Where-Object { $_.CommandLine -like '*nowcast.ps1*' -and $_.ProcessId -ne $PID } |
        ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
}

if ($Action -eq 'install') {
    $config = Get-Content -LiteralPath (Join-Path $here 'config.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    if (-not ([string]$config.clientId -match '^\d{15,}$')) {
        Write-Host 'Сначала впишите clientId в config.json (как его получить — в README).' -ForegroundColor Yellow
        exit 1
    }
    $shell = New-Object -ComObject WScript.Shell
    $link = $shell.CreateShortcut($shortcut)
    $link.TargetPath = $target
    $link.Arguments = $arguments
    $link.WorkingDirectory = $here
    $link.Description = 'discord-nowcast: трек в статусе Discord'
    $link.Save()

    Stop-Nowcast
    Start-Process -FilePath $target -ArgumentList $arguments -WorkingDirectory $here
    Write-Host 'Готово: discord-nowcast запущен и будет стартовать вместе с Windows.' -ForegroundColor Green
}
else {
    if (Test-Path -LiteralPath $shortcut) { Remove-Item -LiteralPath $shortcut }
    Stop-Nowcast
    Write-Host 'discord-nowcast остановлен и убран из автозагрузки.' -ForegroundColor Green
}
