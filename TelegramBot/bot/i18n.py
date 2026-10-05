"""Small message catalog. Telegram language is selected per user and persisted."""
from __future__ import annotations

MESSAGES: dict[str, dict[str, str]] = {
    "ru": {
        "start": "👋 <b>Привет, {name}!</b>\nЯ помогу быстро получить данные о заказах.\n\nВыберите действие на клавиатуре или откройте /help.",
        "help": "<b>📋 Доступные команды</b>\n\n/start — начать работу\n/help — открыть эту справку\n/lang ru|uk — выбрать язык\n\n<b>Для руководителя</b>\n/stats — заказы по статусам\n/orders_today — заказы за сегодня\n/report — готовый управленческий отчёт",
        "help_public": "<b>📋 Доступные команды</b>\n\n/start — начать работу\n/help — открыть эту справку\n/lang ru|uk — выбрать язык\n/id — узнать свой Telegram ID\n\nКоманды статистики доступны пользователям, указанным в настройках бота.",
        "language": "Язык интерфейса: русский 🇷🇺\nЧтобы сменить язык: /lang uk или /lang ru.",
        "language_changed": "Язык изменён на русский 🇷🇺.",
        "language_changed_uk": "Мову змінено на українську 🇺🇦.",
        "unknown": "Неизвестная команда. Используйте /help.",
        "unknown_uk": "Невідома команда. Скористайтеся /help.",
        "forbidden": "⛔ У вас нет доступа к этой команде.",
        "forbidden_uk": "⛔ У вас немає доступу до цієї команди.",
        "stats_title": "📊 Статистика заказов",
        "orders_title": "🛒 Заказы за {date}",
        "no_orders": "Заказов за сегодня нет.",
        "no_orders_uk": "Замовлень за сьогодні немає.",
        "report_title": "📈 Отчёт для руководителя за {date}",
        "api_error": "⚠️ Не удалось получить данные. Попробуйте позже.",
        "bad_language": "Укажите язык: /lang ru или /lang uk.",
        "telegram_id": "Ваш Telegram ID: <code>{id}</code>\nДобавьте его в ALLOWED_USER_IDS в файле .env и перезапустите бота.",
        "order_line": "• <b>#{id}</b> · {status} · {amount}",
        "report_fallback": "Ключевые показатели сформированы по данным статистики и заказов.",
    },
    "uk": {
        "start": "👋 <b>Вітаю, {name}!</b>\nЯ допоможу швидко отримати дані про замовлення.\n\nОберіть дію на клавіатурі або відкрийте /help.",
        "help": "<b>📋 Доступні команди</b>\n\n/start — почати роботу\n/help — відкрити цю довідку\n/lang ru|uk — обрати мову\n\n<b>Для керівника</b>\n/stats — замовлення за статусами\n/orders_today — замовлення за сьогодні\n/report — готовий управлінський звіт",
        "help_public": "<b>📋 Доступні команди</b>\n\n/start — почати роботу\n/help — відкрити цю довідку\n/lang ru|uk — обрати мову\n/id — дізнатися свій Telegram ID\n\nКоманди статистики доступні користувачам, вказаним у налаштуваннях бота.",
        "language": "Мова інтерфейсу: українська 🇺🇦\nЩоб змінити мову: /lang uk або /lang ru.",
        "language_changed": "Язык изменён на русский 🇷🇺.",
        "language_changed_uk": "Мову змінено на українську 🇺🇦.",
        "unknown": "Невідома команда. Скористайтеся /help.",
        "unknown_uk": "Невідома команда. Скористайтеся /help.",
        "forbidden": "⛔ У вас немає доступу до цієї команди.",
        "forbidden_uk": "⛔ У вас немає доступу до цієї команди.",
        "stats_title": "📊 Статистика замовлень",
        "orders_title": "🛒 Замовлення за {date}",
        "no_orders": "Замовлень за сьогодні немає.",
        "no_orders_uk": "Замовлень за сьогодні немає.",
        "report_title": "📈 Звіт для керівника за {date}",
        "api_error": "⚠️ Не вдалося отримати дані. Спробуйте пізніше.",
        "bad_language": "Вкажіть мову: /lang ru або /lang uk.",
        "telegram_id": "Ваш Telegram ID: <code>{id}</code>\nДодайте його до ALLOWED_USER_IDS у файлі .env та перезапустіть бота.",
        "order_line": "• <b>#{id}</b> · {status} · {amount}",
        "report_fallback": "Ключові показники сформовано за даними статистики та замовлень.",
    },
}


def tr(language: str, key: str, **kwargs: object) -> str:
    lang = language if language in MESSAGES else "ru"
    value = MESSAGES[lang].get(key) or MESSAGES["ru"].get(key, key)
    return value.format(**kwargs)
