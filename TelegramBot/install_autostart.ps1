param(
    [string]$InstallDirectory = $PSScriptRoot,
    [switch]$Remove
)

$ErrorActionPreference = 'Stop'
$startup = [Environment]::GetFolderPath('Startup')
$shortcutPath = Join-Path $startup 'Telegram Orders Bot.lnk'

if ($Remove) {
    Remove-Item -LiteralPath $shortcutPath -Force -ErrorAction SilentlyContinue
    Write-Host 'Autostart removed.'
    exit 0
}

$exe = Join-Path $InstallDirectory 'TelegramOrdersBot.exe'
$bat = Join-Path $InstallDirectory 'run_bot.bat'
if (Test-Path $exe) {
    $target = $exe
    $arguments = ''
} elseif (Test-Path $bat) {
    $target = $env:ComSpec
    $arguments = "/c `"$bat`""
} else {
    throw "TelegramOrdersBot.exe or run_bot.bat was not found in $InstallDirectory"
}

$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $target
$shortcut.Arguments = $arguments
$shortcut.WorkingDirectory = $InstallDirectory
$shortcut.WindowStyle = 7
$shortcut.Description = 'Telegram Orders Bot'
$shortcut.Save()
Write-Host "Autostart installed: $shortcutPath"
