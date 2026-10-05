"""SQLite persistence for users and last API payloads."""
from __future__ import annotations

import json
import os
from datetime import datetime, timezone
from pathlib import Path

import aiosqlite


class Database:
    def __init__(self, path: Path):
        self.path = path
        self._db: aiosqlite.Connection | None = None

    async def connect(self) -> None:
        try:
            self.path.parent.mkdir(parents=True, exist_ok=True)
        except OSError:
            # OneDrive and some protected Windows folders can temporarily reject
            # creation of a relative data directory. Keep the bot usable by
            # storing its local SQLite file in the user's application-data folder.
            local_app_data = os.getenv("LOCALAPPDATA") or os.getenv("APPDATA")
            fallback_root = Path(local_app_data) if local_app_data else Path.home() / ".local" / "share"
            self.path = fallback_root / "telegram-orders-bot" / self.path.name
            self.path.parent.mkdir(parents=True, exist_ok=True)
        self._db = await aiosqlite.connect(self.path)
        await self._db.execute("PRAGMA journal_mode=WAL")
        await self._db.executescript(
            """
            CREATE TABLE IF NOT EXISTS users (
                telegram_id INTEGER PRIMARY KEY,
                language TEXT NOT NULL DEFAULT 'ru',
                updated_at TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS api_cache (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                telegram_id INTEGER NOT NULL,
                kind TEXT NOT NULL,
                request_date TEXT,
                payload TEXT NOT NULL,
                created_at TEXT NOT NULL
            );
            """
        )
        await self._db.commit()

    async def close(self) -> None:
        if self._db:
            await self._db.close()
            self._db = None

    async def get_language(self, telegram_id: int, default: str = "ru") -> str:
        assert self._db is not None
        async with self._db.execute("SELECT language FROM users WHERE telegram_id = ?", (telegram_id,)) as cur:
            row = await cur.fetchone()
        return row[0] if row else default

    async def set_language(self, telegram_id: int, language: str) -> None:
        assert self._db is not None
        now = datetime.now(timezone.utc).isoformat()
        await self._db.execute(
            "INSERT INTO users(telegram_id, language, updated_at) VALUES (?, ?, ?) "
            "ON CONFLICT(telegram_id) DO UPDATE SET language=excluded.language, updated_at=excluded.updated_at",
            (telegram_id, language, now),
        )
        await self._db.commit()

    async def cache_payload(self, telegram_id: int, kind: str, payload: object, request_date: str | None = None) -> None:
        assert self._db is not None
        await self._db.execute(
            "INSERT INTO api_cache(telegram_id, kind, request_date, payload, created_at) VALUES (?, ?, ?, ?, ?)",
            (telegram_id, kind, request_date, json.dumps(payload, ensure_ascii=False), datetime.now(timezone.utc).isoformat()),
        )
        await self._db.commit()
