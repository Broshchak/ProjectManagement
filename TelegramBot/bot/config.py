"""Configuration for the Telegram service account."""
from __future__ import annotations

import math
import os
import sys
from dataclasses import dataclass, field
from pathlib import Path
from urllib.parse import urlsplit

from dotenv import load_dotenv


@dataclass(frozen=True)
class Settings:
    telegram_bot_token: str = field(repr=False)
    api_base_url: str
    bot_api_login: str
    bot_api_password: str = field(repr=False)
    request_timeout_seconds: float = 30.0

    def __post_init__(self) -> None:
        url = urlsplit(self.api_base_url)
        if url.scheme != "https" or not url.hostname or url.username or url.password or url.query or url.fragment:
            raise ValueError("API_BASE_URL must be an HTTPS URL without credentials, query or fragment")

    @classmethod
    def from_env(cls, env_file: str | Path | None = ".env") -> Settings:
        if env_file:
            path = Path(env_file)
            if not path.is_absolute() and getattr(sys, "frozen", False):
                path = Path(sys.executable).resolve().parent / path
            load_dotenv(dotenv_path=path, override=False)

        def required(name: str) -> str:
            value = os.getenv(name, "").strip()
            if not value:
                raise ValueError(f"{name} is required")
            return value

        try:
            timeout = float(os.getenv("REQUEST_TIMEOUT_SECONDS", "30"))
            if not math.isfinite(timeout) or timeout <= 0:
                raise ValueError
        except ValueError:
            raise ValueError("REQUEST_TIMEOUT_SECONDS must be positive") from None

        return cls(
            required("TELEGRAM_BOT_TOKEN"),
            required("API_BASE_URL").rstrip("/"),
            os.getenv("BOT_API_LOGIN", "telegram-bot").strip() or "telegram-bot",
            required("BOT_API_PASSWORD"),
            timeout,
        )
