@echo off
setlocal
cd /d "%~dp0"

if exist "%~dp0TelegramOrdersBot.exe" (
  start "Telegram Orders Bot" /min "%~dp0TelegramOrdersBot.exe"
  exit /b 0
)

if exist "%~dp0.venv\Scripts\python.exe" (
  "%~dp0.venv\Scripts\python.exe" "%~dp0main.py"
) else (
  python "%~dp0main.py"
)
