"""Environment based application configuration."""
from __future__ import annotations

import os
import sys
from dataclasses import dataclass
from pathlib import Path

from dotenv import load_dotenv


@dataclass(frozen=True)
class Settings:
    telegram_bot_token: str
    backend_api_url: str
    backend_api_token: str | None
    allowed_user_ids: frozenset[int]
    database_path: Path
    default_language: str = "ru"
    stats_path: str = "/api/orders"
    orders_path: str = "/api/orders"
    report_path: str = "/api/orders"
    request_timeout_seconds: float = 10.0
    timezone_name: str = "Europe/Moscow"

    @classmethod
    def from_env(cls, env_file: str | Path | None = ".env") -> "Settings":
        if env_file:
            env_path = Path(env_file)
            if not env_path.is_absolute():
                # A packaged executable is normally started by a shortcut whose
                # working directory is not the install directory.
                executable_dir = Path(sys.executable).resolve().parent
                candidates = [executable_dir / env_path, Path.cwd() / env_path]
                env_path = next((candidate for candidate in candidates if candidate.exists()), candidates[0])
            load_dotenv(dotenv_path=env_path, override=False)
        token = os.getenv("TELEGRAM_BOT_TOKEN", "").strip()
        if not token:
            raise ValueError("TELEGRAM_BOT_TOKEN is required")
        api_url = os.getenv("BACKEND_API_URL", "").strip().rstrip("/")
        if not api_url:
            raise ValueError("BACKEND_API_URL is required")
        ids: set[int] = set()
        for value in os.getenv("ALLOWED_USER_IDS", "").replace(";", ",").split(","):
            value = value.strip()
            if value:
                try:
                    ids.add(int(value))
                except ValueError as exc:
                    raise ValueError(f"Invalid Telegram user id: {value}") from exc
        language = os.getenv("DEFAULT_LANGUAGE", "ru").strip().lower()
        if language not in {"ru", "uk"}:
            language = "ru"
        try:
            timeout = float(os.getenv("REQUEST_TIMEOUT_SECONDS", "10"))
        except ValueError:
            timeout = 10.0
        return cls(
            telegram_bot_token=token,
            backend_api_url=api_url,
            backend_api_token=os.getenv("BACKEND_API_TOKEN", "").strip() or None,
            allowed_user_ids=frozenset(ids),
            database_path=Path(os.getenv("DATABASE_PATH", "data/bot.sqlite3")),
            default_language=language,
            stats_path=os.getenv("BACKEND_STATS_PATH", "/api/orders"),
            orders_path=os.getenv("BACKEND_ORDERS_PATH", "/api/orders"),
            report_path=os.getenv("BACKEND_REPORT_PATH", "/api/orders"),
            request_timeout_seconds=max(1.0, timeout),
            timezone_name=os.getenv("TIMEZONE", "Europe/Moscow").strip() or "Europe/Moscow",
        )
