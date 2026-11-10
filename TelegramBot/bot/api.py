"""Authenticated client for the bot-only Orders API."""
from __future__ import annotations

import asyncio
from dataclasses import dataclass, field
from datetime import datetime, timedelta, timezone
from email.message import Message
from pathlib import PurePath

import httpx

from .config import Settings


class ApiError(RuntimeError):
    """Contains only a safe, predefined error category."""


@dataclass(frozen=True)
class TokenPair:
    access: str = field(repr=False)
    access_expiry: datetime
    refresh: str = field(repr=False)
    refresh_expiry: datetime


@dataclass(frozen=True)
class BotUser:
    full_name: str
    role: str
    permissions: frozenset[str]


@dataclass(frozen=True)
class Report:
    text: str | None = None
    content: bytes = field(default=b"", repr=False)
    filename: str = "report"


KNOWN_ERRORS = {"not_linked", "link_invalid", "telegram_already_linked", "bind_failed", "forbidden", "network_denied"}
TRANSIENT_STATUSES = {502, 503, 504}


class BackendApi:
    def __init__(self, settings: Settings, *, transport: httpx.AsyncBaseTransport | None = None):
        self._settings = settings
        self._client = httpx.AsyncClient(
            base_url=settings.api_base_url.rstrip("/") + "/",
            timeout=settings.request_timeout_seconds,
            follow_redirects=False,
            transport=transport,
        )
        self._tokens: TokenPair | None = None
        self._renew = asyncio.Lock()
        self._invalid_credentials = False

    async def aclose(self) -> None:
        await self._client.aclose()

    @staticmethod
    def _json(response: httpx.Response) -> dict:
        try:
            data = response.json()
        except ValueError:
            raise ApiError("invalid_response") from None
        if not isinstance(data, dict):
            raise ApiError("invalid_response")
        return data

    @classmethod
    def _error(cls, response: httpx.Response) -> ApiError:
        if response.status_code == 429:
            return ApiError("rate_limited")
        try:
            code = cls._json(response).get("code")
        except ApiError:
            code = None
        return ApiError(code if isinstance(code, str) and code in KNOWN_ERRORS else "unavailable")

    async def _request(self, method: str, path: str, *, retry_safe: bool = False, **kwargs) -> httpx.Response:
        attempts = 3 if retry_safe else 1
        for attempt in range(attempts):
            try:
                response = await self._client.request(method, path, **kwargs)
            except httpx.TransportError:
                if attempt == attempts - 1:
                    raise ApiError("unavailable") from None
            else:
                if response.status_code not in TRANSIENT_STATUSES or attempt == attempts - 1:
                    return response
            await asyncio.sleep(0.25 * 2 ** attempt)
        raise AssertionError("unreachable")

    async def _auth(self, path: str, body: dict) -> httpx.Response:
        return await self._request("POST", path, json=body)

    async def _access(self, rejected: TokenPair | None = None) -> TokenPair:
        async with self._renew:
            if self._invalid_credentials:
                raise ApiError("invalid_service_credentials")
            now = datetime.now(timezone.utc)
            current = self._tokens
            if current is not None and current is not rejected and current.access_expiry > now + timedelta(seconds=30):
                return current

            response = None
            if current is not None and current is not rejected and current.refresh_expiry > now:
                # A lost response may have consumed this refresh token.
                self._tokens = None
                response = await self._auth("api/auth/refresh", {"refreshToken": current.refresh})
                if response.status_code == 429:
                    self._tokens = current
                if response.status_code == 401:
                    response = None

            if response is None:
                self._tokens = None
                response = await self._auth("api/auth/login", {
                    "login": self._settings.bot_api_login,
                    "password": self._settings.bot_api_password,
                })
                if response.status_code == 401:
                    self._invalid_credentials = True
                    raise ApiError("invalid_service_credentials")
            if response.status_code != 200:
                raise self._error(response)

            data = self._json(response)
            try:
                access, refresh = data["accessToken"], data["refreshToken"]
                access_expiry = datetime.fromisoformat(data["accessTokenExpiresAt"].replace("Z", "+00:00"))
                refresh_expiry = datetime.fromisoformat(data["refreshTokenExpiresAt"].replace("Z", "+00:00"))
                if not isinstance(access, str) or not access or not isinstance(refresh, str) or not refresh:
                    raise ValueError
                if access_expiry <= now or refresh_expiry <= now:
                    raise ValueError
            except (KeyError, AttributeError, TypeError, ValueError):
                raise ApiError("invalid_response") from None
            self._tokens = TokenPair(access, access_expiry, refresh, refresh_expiry)
            return self._tokens

    async def _call(self, method: str, telegram_id: int, suffix: str = "", *,
                    retry_safe: bool = True, **kwargs) -> httpx.Response:
        if telegram_id <= 0:
            raise ApiError("not_linked")
        path = f"api/bot/users/{telegram_id}{suffix}"
        pair = await self._access()
        response = await self._request(method, path, retry_safe=retry_safe,
                                       headers={"Authorization": f"Bearer {pair.access}"}, **kwargs)
        if response.status_code == 401:
            # Authentication rejected the request before the action ran.
            pair = await self._access(rejected=pair)
            response = await self._request(method, path, retry_safe=False,
                                           headers={"Authorization": f"Bearer {pair.access}"}, **kwargs)
        if not response.is_success:
            raise self._error(response)
        return response

    @classmethod
    def _user(cls, response: httpx.Response) -> BotUser:
        data = cls._json(response)
        try:
            name, role, permissions = data["fullName"], data["role"], data["permissions"]
            if (not isinstance(name, str) or not isinstance(role, str) or not isinstance(permissions, list)
                    or not all(isinstance(p, str) for p in permissions)):
                raise ValueError
            return BotUser(name, role, frozenset(permissions))
        except (KeyError, ValueError):
            raise ApiError("invalid_response") from None

    async def resolve(self, telegram_id: int) -> BotUser | None:
        try:
            return self._user(await self._call("GET", telegram_id))
        except ApiError as exc:
            if str(exc) != "not_linked":
                raise
            return None

    async def link(self, telegram_id: int, token: str) -> BotUser:
        return self._user(await self._call("POST", telegram_id, "/link", retry_safe=False, json={"token": token}))

    async def unlink(self, telegram_id: int) -> None:
        await self._call("DELETE", telegram_id, "/link")

    async def report(self, telegram_id: int) -> Report:
        response = await self._call("POST", telegram_id, "/report")
        content_type = response.headers.get("Content-Type", "").split(";", 1)[0].strip().lower()
        if content_type == "application/json":
            data = self._json(response)
            if data.get("type") != "text" or not isinstance(data.get("text"), str):
                raise ApiError("invalid_response")
            return Report(text=data["text"])

        header = Message()
        header["Content-Disposition"] = response.headers.get("Content-Disposition", "")
        filename = PurePath((header.get_filename() or "report").replace(chr(92), "/")).name
        return Report(content=response.content, filename=filename or "report")
