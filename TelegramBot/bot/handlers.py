"""Private-chat commands; the API decides identity and permissions."""
from __future__ import annotations

import logging
import re

from aiogram import Bot, F, Router
from aiogram.exceptions import TelegramAPIError
from aiogram.types import BotCommand, BufferedInputFile, Message

from .api import ApiError, BackendApi

LOG = logging.getLogger(__name__)
REPORT_PERMISSIONS = {"reports.read", "reports.telegram.send"}
ERRORS = {
    "link_invalid": "Посилання недійсне або прострочене. Створіть нове у WPF.",
    "telegram_already_linked": "Цей Telegram уже прив’язано. Для зміни скористайтеся /unlink.",
    "bind_failed": "Не вдалося прив’язати акаунт. Створіть нове посилання у WPF.",
    "forbidden": "Ви не маєте доступу до цієї команди.",
    "rate_limited": "Забагато запитів. Спробуйте за хвилину.",
}


async def handle_message(api: BackendApi, message: Message) -> None:
    sender = message.from_user
    if message.chat.type != "private" or sender is None or sender.is_bot or not message.text:
        return
    parts = message.text.strip().split(maxsplit=1)
    if not parts:
        return
    command = parts[0].partition("@")[0].lower()
    payload = parts[1] if command == "/start" and len(parts) > 1 else ""
    telegram_id = sender.id
    try:
        if payload:
            if not re.fullmatch(r"[A-Za-z0-9_-]{20,64}", payload):
                raise ApiError("link_invalid")
            await message.bot.send_chat_action(message.chat.id, "typing")
            linked = await api.link(telegram_id, payload)
            await message.answer(f"Акаунт прив’язано: {linked.full_name}. Команди: /help.")
            return

        user = await api.resolve(telegram_id)
        if user is None:
            if command == "/start":
                await message.answer("Це приватний бот. Створіть посилання Telegram у WPF і відкрийте його.")
            return
        if command == "/start":
            await message.answer(f"Вітаю, {user.full_name}! Команди: /help.")
        elif command == "/help":
            commands = ["/me — профіль", "/unlink — від’єднати Telegram"]
            if REPORT_PERMISSIONS <= user.permissions:
                commands.append("/report — отримати звіт")
            await message.answer("\n".join(commands))
        elif command == "/me":
            await message.answer(f"{user.full_name}\nРоль: {user.role}")
        elif command == "/unlink":
            await api.unlink(telegram_id)
            await message.answer("Telegram від’єднано.")
        elif command == "/report":
            await message.bot.send_chat_action(message.chat.id, "typing")
            report = await api.report(telegram_id)
            if report.text is not None:
                text = report.text or "Звіт порожній."
                for start in range(0, len(text), 4096):
                    await message.answer(text[start:start + 4096])
            else:
                await message.answer_document(BufferedInputFile(report.content, filename=report.filename))
        elif command.startswith("/"):
            await message.answer("Невідома команда. Доступні команди: /help.")
    except ApiError as exc:
        code = str(exc)
        if code == "not_linked":
            return
        if code in {"invalid_service_credentials", "network_denied"}:
            LOG.error("Bot API configuration error: %s", code)
        reply = ERRORS.get(code, "Сервіс тимчасово недоступний. Спробуйте пізніше.")
        if payload and code == "unavailable":
            reply = "Результат прив’язування невідомий. Перевірте /start без посилання. Якщо акаунт не прив’язано, створіть нове посилання."
        await message.answer(reply)
    finally:
        if payload:
            try:
                await message.delete()
            except TelegramAPIError:
                pass


def build_router(api: BackendApi) -> Router:
    router = Router(name="orders")

    @router.message(F.chat.type == "private", F.text)
    async def receive(message: Message) -> None:
        await handle_message(api, message)

    return router


async def configure_bot_commands(bot: Bot) -> None:
    # /report is advertised in /help only when the API grants it.
    await bot.set_my_commands([
        BotCommand(command="start", description="Почати або прив’язати акаунт"),
        BotCommand(command="help", description="Доступні команди"),
        BotCommand(command="me", description="Профіль"),
        BotCommand(command="unlink", description="Від’єднати Telegram"),
    ])
