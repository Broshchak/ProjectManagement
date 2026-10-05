param(
    [string]$InstallDirectory = (Join-Path $env:LOCALAPPDATA 'TelegramOrdersBot'),
    [string]$BuildDirectory = (Join-Path $env:LOCALAPPDATA 'TelegramOrdersBot-build'),
    [switch]$Autostart
)

$ErrorActionPreference = 'Stop'
$source = $PSScriptRoot
$buildSource = if (Test-Path $BuildDirectory) { $BuildDirectory } else { Join-Path $source 'dist' }
New-Item -ItemType Directory -Force -Path $InstallDirectory | Out-Null

foreach ($name in @('TelegramOrdersBot.exe', 'run_bot.bat', '.env.example')) {
    $file = Join-Path $buildSource $name
    if (Test-Path $file) { Copy-Item $file (Join-Path $InstallDirectory $name) -Force }
}
if (-not (Test-Path (Join-Path $InstallDirectory 'TelegramOrdersBot.exe')) -and -not (Test-Path (Join-Path $InstallDirectory 'run_bot.bat'))) {
    throw 'Run build_windows.ps1 first.'
}

if (-not (Test-Path (Join-Path $InstallDirectory '.env'))) {
    Copy-Item (Join-Path $InstallDirectory '.env.example') (Join-Path $InstallDirectory '.env')
}

if ($Autostart) {
    & (Join-Path $source 'install_autostart.ps1') -InstallDirectory $InstallDirectory
}
Write-Host "Installation complete: $InstallDirectory"
