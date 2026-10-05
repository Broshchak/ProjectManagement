"""Application entry point: python -m bot"""
from __future__ import annotations

import asyncio
import logging

from aiogram import Bot, Dispatcher

from .api import BackendApi
from .config import Settings
from .db import Database
from .handlers import build_router, configure_bot_commands


async def main() -> None:
    settings = Settings.from_env()
    logging.basicConfig(level=logging.INFO, format="%(asctime)s %(levelname)s %(name)s: %(message)s")
    db = Database(settings.database_path)
    await db.connect()
    bot = Bot(token=settings.telegram_bot_token)
    dispatcher = Dispatcher()
    dispatcher.include_router(build_router(BackendApi(settings), db, settings))
    try:
        await configure_bot_commands(bot)
        await dispatcher.start_polling(bot)
    finally:
        await bot.session.close()
        await db.close()


if __name__ == "__main__":
    asyncio.run(main())
