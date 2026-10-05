param(
    [switch]$InstallDependencies
)

$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$python = (Get-Command python -ErrorAction Stop).Source

if ($InstallDependencies) {
    Set-Location $projectRoot
    & $python -m pip install -r requirements.txt pyinstaller
}

& $python -m PyInstaller --version *> $null
if ($LASTEXITCODE -ne 0) {
    throw 'PyInstaller was not found. Run .\build_windows.ps1 -InstallDependencies'
}

# PyInstaller has trouble creating files under some non-ASCII OneDrive paths.
# Build in an ASCII temporary directory, then copy the finished executable back.
$staging = Join-Path $env:TEMP 'TelegramOrdersBot-build'
Remove-Item -Recurse -Force $staging -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $staging | Out-Null
Copy-Item (Join-Path $projectRoot 'main.py') $staging -Force
Copy-Item (Join-Path $projectRoot 'bot') (Join-Path $staging 'bot') -Recurse -Force
Set-Location $staging
& $python -m PyInstaller --noconfirm --clean --onefile --windowed --name TelegramOrdersBot main.py
if ($LASTEXITCODE -ne 0) { throw 'PyInstaller failed.' }

Set-Location $projectRoot
$dist = Join-Path $env:LOCALAPPDATA 'TelegramOrdersBot-build'
Remove-Item -Recurse -Force $dist -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $dist | Out-Null
Copy-Item (Join-Path $staging 'dist\TelegramOrdersBot.exe') (Join-Path $dist 'TelegramOrdersBot.exe') -Force
Copy-Item (Join-Path $projectRoot '.env.example') (Join-Path $dist '.env.example') -Force
Copy-Item (Join-Path $projectRoot 'run_bot.bat') (Join-Path $dist 'run_bot.bat') -Force
Write-Host "Build complete: $((Resolve-Path $dist).Path)"
