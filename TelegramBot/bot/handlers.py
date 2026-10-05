"""Telegram command handlers and access checks."""
from __future__ import annotations

from datetime import date, datetime
import html
from zoneinfo import ZoneInfo
from typing import Awaitable, Callable

from aiogram import Bot, Dispatcher, F, Router
from aiogram.filters import Command, CommandObject
from aiogram.types import BotCommand, Message

from .api import ApiError, BackendApi
from .config import Settings
from .db import Database
from .formatting import format_orders, format_report, format_stats
from .i18n import tr
from .keyboards import main_keyboard


def build_router(api: BackendApi, db: Database, settings: Settings) -> Router:
    router = Router(name="orders")

    async def language_for(message: Message) -> str:
        return await db.get_language(message.from_user.id, settings.default_language)  # type: ignore[union-attr]

    def current_date() -> date:
        try:
            return datetime.now(ZoneInfo(settings.timezone_name)).date()
        except Exception:
            return datetime.now().date()

    def is_allowed(message: Message) -> bool:
        return bool(message.from_user and message.from_user.id in settings.allowed_user_ids)

    async def deny(message: Message, language: str) -> None:
        await message.answer(tr(language, "forbidden"))

    def keyboard_for(message: Message, language: str):
        return main_keyboard(language, manager=is_allowed(message))

    @router.message(Command("start"))
    async def start(message: Message) -> None:
        language = await language_for(message)
        name = message.from_user.first_name if message.from_user else ""
        await message.answer(tr(language, "start", name=html.escape(name or "друг")), parse_mode="HTML", reply_markup=keyboard_for(message, language))

    @router.message(Command("help"))
    async def help_command(message: Message) -> None:
        language = await language_for(message)
        help_key = "help" if is_allowed(message) else "help_public"
        await message.answer(tr(language, help_key), parse_mode="HTML", reply_markup=keyboard_for(message, language))

    @router.message(Command("id"))
    async def id_command(message: Message) -> None:
        """Tell a user which Telegram ID must be put in ALLOWED_USER_IDS."""
        if message.from_user:
            await message.answer(tr(await language_for(message), "telegram_id", id=message.from_user.id), parse_mode="HTML")

    @router.message(Command("lang"))
    async def language_command(message: Message, command: CommandObject) -> None:
        current = await language_for(message)
        value = (command.args or "").strip().lower()
        if value not in {"ru", "uk", "ua", "українська", "русский"}:
            await message.answer(tr(current, "bad_language"))
            return
        language = "uk" if value in {"uk", "ua", "українська"} else "ru"
        await db.set_language(message.from_user.id, language)  # type: ignore[union-attr]
        await message.answer(tr(language, "language_changed_uk" if language == "uk" else "language_changed"), reply_markup=keyboard_for(message, language))

    async def language_button(message: Message) -> None:
        current = await language_for(message)
        language = "uk" if current == "ru" else "ru"
        await db.set_language(message.from_user.id, language)  # type: ignore[union-attr]
        await message.answer(
            tr(language, "language_changed_uk" if language == "uk" else "language_changed"),
            reply_markup=keyboard_for(message, language),
        )

    async def run_api(message: Message, operation: Callable[[], Awaitable[object]], language: str) -> object | None:
        try:
            return await operation()
        except ApiError:
            await message.answer(tr(language, "api_error"))
            return None

    @router.message(Command("stats"))
    async def stats_command(message: Message) -> None:
        language = await language_for(message)
        if not is_allowed(message):
            await deny(message, language)
            return
        payload = await run_api(message, api.stats, language)
        if payload is not None:
            await db.cache_payload(message.from_user.id, "stats", payload)  # type: ignore[union-attr]
            await message.answer(format_stats(payload, language), parse_mode="HTML")

    @router.message(Command("orders_today"))
    async def orders_today_command(message: Message) -> None:
        language = await language_for(message)
        if not is_allowed(message):
            await deny(message, language)
            return
        today = current_date()
        payload = await run_api(message, lambda: api.orders(today), language)
        if payload is not None:
            await db.cache_payload(message.from_user.id, "orders", payload, today.isoformat())  # type: ignore[union-attr]
            await message.answer(format_orders(payload, language, today.isoformat()), parse_mode="HTML")

    @router.message(Command("report"))
    async def report_command(message: Message) -> None:
        language = await language_for(message)
        if not is_allowed(message):
            await deny(message, language)
            return
        today = current_date()
        stats_payload: object = {}
        orders_payload: object = {}
        try:
            report_payload = await api.report(today)
        except ApiError:
            # Some MVP backends expose only stats and orders; build a report from them.
            try:
                import asyncio
                stats_payload, orders_payload = await asyncio.gather(api.stats(today), api.orders(today))
                report_payload = {}
            except ApiError:
                await message.answer(tr(language, "api_error"))
                return
        await db.cache_payload(message.from_user.id, "report", report_payload, today.isoformat())  # type: ignore[union-attr]
        # A ready-made report needs one request. Otherwise enrich it with stats and orders.
        if isinstance(report_payload, dict) and not (report_payload.get("text") or report_payload.get("summary") or report_payload.get("report")) and not stats_payload:
            try:
                import asyncio
                stats_payload, orders_payload = await asyncio.gather(api.stats(today), api.orders(today))
            except ApiError:
                pass
        await message.answer(format_report(report_payload, stats_payload, orders_payload, language, today.isoformat()), parse_mode="HTML")

    # Friendly reply-keyboard actions. Slash commands above remain available.
    @router.message(F.text == "📊 Статистика")
    async def stats_button(message: Message) -> None:
        await stats_command(message)

    @router.message(F.text.in_({"🛒 Заказы сегодня", "🛒 Замовлення сьогодні"}))
    async def orders_button(message: Message) -> None:
        await orders_today_command(message)

    @router.message(F.text.in_({"📈 Отчёт", "📈 Звіт"}))
    async def report_button(message: Message) -> None:
        await report_command(message)

    @router.message(F.text.in_({"❓ Помощь", "❓ Допомога", "/help"}))
    async def help_button(message: Message) -> None:
        await help_command(message)

    @router.message(F.text.in_({"🆔 Мой ID", "🆔 Мій ID"}))
    async def id_button(message: Message) -> None:
        await id_command(message)

    @router.message(F.text.in_({"🌐 Українська", "🌐 Русский", "🌐 Язык: русский", "🌐 Мова: українська", "/lang ru", "/lang uk"}))
    async def language_keyboard_button(message: Message) -> None:
        await language_button(message)

    @router.message()
    async def unknown_command(message: Message) -> None:
        if message.text and message.text.startswith("/"):
            language = await language_for(message)
            await message.answer(tr(language, "unknown"))

    return router


async def configure_bot_commands(bot: Bot) -> None:
    await bot.set_my_commands(
        [
            BotCommand(command="start", description="Начать / Почати"),
            BotCommand(command="help", description="Справка / Довідка"),
            BotCommand(command="lang", description="Язык / Мова"),
            BotCommand(command="id", description="Мой Telegram ID"),
            BotCommand(command="stats", description="Статистика заказов / замовлень"),
            BotCommand(command="orders_today", description="Заказы сегодня / сьогодні"),
            BotCommand(command="report", description="Отчёт / Звіт"),
        ]
    )
