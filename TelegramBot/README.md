# Telegram Orders Bot

Бот використовує лише службові маршрути `/api/bot/…`. Прив’язки та права зберігає Orders API. Бот не використовує SQLite, локальний список користувачів чи статичний Bearer-токен.

## Запуск

1. Застосуйте `SQL/03_auth.sql`, `SQL/04_telegram.sql`, `SQL/05_roles.sql` та `SQL/06_role_permissions.sql` на серверній БД і задайте `Telegram__BotUsername` в API.
2. Створіть через адміністративний API службового користувача `telegram-bot` із роллю `TelegramBot` і випадковим паролем довжиною щонайменше 48 символів. За потреби обмежте IP бота через `Bot__AllowedIps`.
3. Встановіть Python 3.11+ і залежності: `python -m pip install -r requirements.txt`.
4. Задайте `TELEGRAM_BOT_TOKEN`, `API_BASE_URL` (HTTPS), `BOT_API_LOGIN` і `BOT_API_PASSWORD` через середовище або локальний ігнорований файл `.env` за зразком `.env.example`.
5. Запустіть `python -m bot`. Увійдіть у WPF як Admin/Director, натисніть «Підключити Telegram» і відкрийте одноразове посилання в боті.

Команди: `/start`, `/help`, `/me`, `/report` (за дозволом), `/unlink`. Звіти можуть бути текстом або файлом. Старі `/stats` і `/orders_today` потребують окремих захищених API.

Локальні перевірки без Telegram: `python -m unittest discover -s tests`. Windows-пакет: `.\build_windows.ps1 -InstallDependencies`; установлення: `.\install.ps1 -Autostart`. Старі параметри `BACKEND_API_TOKEN` та `ALLOWED_USER_IDS` більше не використовуються.
