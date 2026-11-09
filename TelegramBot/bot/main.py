"""Application entry point: python -m bot."""
from __future__ import annotations

import asyncio
import logging

from aiogram import Bot, Dispatcher

from .api import BackendApi
from .config import Settings
from .handlers import build_router, configure_bot_commands


async def main() -> None:
    settings = Settings.from_env()
    logging.basicConfig(level=logging.INFO, format="%(asctime)s %(levelname)s %(name)s: %(message)s")
    logging.getLogger("httpx").setLevel(logging.WARNING)
    bot = Bot(token=settings.telegram_bot_token)
    api = BackendApi(settings)
    dispatcher = Dispatcher()
    dispatcher.include_router(build_router(api))
    try:
        await configure_bot_commands(bot)
        await dispatcher.start_polling(bot, allowed_updates=["message"], close_bot_session=False)
    finally:
        await api.aclose()
        await bot.session.close()


if __name__ == "__main__":
    asyncio.run(main())
