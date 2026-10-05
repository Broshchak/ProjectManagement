Telegram Orders Bot



Telegram-бот показує статистику замовлень з API проєкту.



Швидкий запуск



Установіть Python 3.11 або новішої версії.



Відкрийте папку проєкту в PowerShell і виконайте:



python -m venv .venv

.\\.venv\\Scripts\\Activate.ps1

pip install -r requirements.txt



Скопіюйте .env.example у файл .env.



Відкрийте .env і вкажіть токен Telegram-бота:



TELEGRAM\_BOT\_TOKEN=токен\_від\_BotFather

BACKEND\_API\_URL=https://project-management3.runasp.net



Запустіть бота:



python main.py



Напишіть боту /id. Скопіюйте отриманий номер у .env:



ALLOWED\_USER\_IDS=ваш\_telegram\_id



Перезапустіть бота та надішліть /start.



Запуск під час увімкнення Windows



Щоб зібрати один файл .exe, виконайте:



.\\build\_windows.ps1 -InstallDependencies



Потім установіть бота:



.\\install.ps1 -Autostart



Після встановлення бот буде розташований у %LOCALAPPDATA%\\TelegramOrdersBot і запускатиметься разом із Windows. Перед першим запуском перевірте файл .env у цій папці.



Також можна запустити install.bat подвійним клацанням.



Щоб видалити бота, виконайте:



.\\uninstall.ps1

Налаштування



Усі налаштування містяться у файлі .env поруч із main.py або поруч із готовим .exe:



TELEGRAM\_BOT\_TOKEN=

BACKEND\_API\_URL=https://project-management3.runasp.net

BACKEND\_API\_TOKEN=

ALLOWED\_USER\_IDS=

DATABASE\_PATH=data/bot.sqlite3

DEFAULT\_LANGUAGE=ru

TIMEZONE=Europe/Moscow

REQUEST\_TIMEOUT\_SECONDS=10

Що потрібно заповнити обов'язково



TELEGRAM\_BOT\_TOKEN — токен, виданий BotFather.



BACKEND\_API\_URL — адреса API.



ALLOWED\_USER\_IDS — ваш Telegram ID, отриманий за допомогою команди /id.



База даних



DATABASE\_PATH визначає розташування бази SQLite. Наприклад:



DATABASE\_PATH=data/bot.sqlite3



У цьому випадку поруч із програмою буде створено папку data, а в ній — файл bot.sqlite3.



Можна вказати інший шлях:



DATABASE\_PATH=C:/TelegramOrdersBot/data/bot.sqlite3



У базі зберігаються вибрана користувачами мова та кеш відповідей API.



Заздалегідь створювати базу даних не потрібно — бот створить папку та файл під час першого запуску.



Якщо Windows або OneDrive не дозволить створити вказану папку, бот автоматично використовуватиме %LOCALAPPDATA%\\telegram-orders-bot.



Інші параметри



DEFAULT\_LANGUAGE — мова за замовчуванням: ru або uk.



TIMEZONE — часовий пояс, який використовується для визначення замовлень за сьогодні.



REQUEST\_TIMEOUT\_SECONDS — час очікування відповіді API у секундах.



Не передавайте файл .env іншим людям, оскільки він містить токен бота.

