param(
    [string]$InstallDirectory = (Join-Path $env:LOCALAPPDATA 'TelegramOrdersBot')
)

$ErrorActionPreference = 'Stop'
$startup = [Environment]::GetFolderPath('Startup')
Remove-Item -LiteralPath (Join-Path $startup 'Telegram Orders Bot.lnk') -Force -ErrorAction SilentlyContinue
if (Test-Path $InstallDirectory) {
    Remove-Item -LiteralPath $InstallDirectory -Recurse -Force
}
Write-Host "Telegram Orders Bot removed."
