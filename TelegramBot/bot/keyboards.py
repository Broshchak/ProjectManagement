"""Telegram reply keyboard shared by the public and manager flows."""
from __future__ import annotations

from aiogram.types import KeyboardButton, ReplyKeyboardMarkup


def main_keyboard(language: str, manager: bool = False) -> ReplyKeyboardMarkup:
    """Show only commands the current user can actually use."""
    if language == "uk":
        language_button = "🌐 Русский"
        stats_button = "📊 Статистика"
        orders_button = "🛒 Замовлення сьогодні"
        report_button = "📈 Звіт"
        help_button = "❓ Допомога"
        id_button = "🆔 Мій ID"
    else:
        language_button = "🌐 Українська"
        stats_button = "📊 Статистика"
        orders_button = "🛒 Заказы сегодня"
        report_button = "📈 Отчёт"
        help_button = "❓ Помощь"
        id_button = "🆔 Мой ID"
    if manager:
        rows = [
            [KeyboardButton(text=stats_button), KeyboardButton(text=orders_button)],
            [KeyboardButton(text=report_button), KeyboardButton(text=help_button)],
            [KeyboardButton(text=language_button)],
        ]
    else:
        rows = [
            [KeyboardButton(text=help_button), KeyboardButton(text=id_button)],
            [KeyboardButton(text=language_button)],
        ]
    return ReplyKeyboardMarkup(keyboard=rows, resize_keyboard=True, is_persistent=True)
