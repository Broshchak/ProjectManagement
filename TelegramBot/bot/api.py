"""Backend API adapter for the project-management orders API."""
from __future__ import annotations

from collections import Counter
from datetime import date, datetime
from typing import Any
from zoneinfo import ZoneInfo

import httpx

from .config import Settings


class ApiError(RuntimeError):
    """A safe, user-facing backend failure category."""


class BackendApi:
    def __init__(self, settings: Settings):
        self.settings = settings

    async def _get(self, path: str, **params: str) -> Any:
        url = f"{self.settings.backend_api_url}/{path.lstrip('/')}"
        headers = {"Accept": "application/json"}
        if self.settings.backend_api_token:
            headers["Authorization"] = f"Bearer {self.settings.backend_api_token}"
        try:
            async with httpx.AsyncClient(timeout=self.settings.request_timeout_seconds) as client:
                response = await client.get(url, params=params, headers=headers)
                response.raise_for_status()
                return response.json()
        except (httpx.HTTPError, ValueError, TypeError) as exc:
            raise ApiError from exc

    @staticmethod
    def _as_orders(payload: Any) -> list[dict[str, Any]]:
        if isinstance(payload, list):
            return [item for item in payload if isinstance(item, dict)]
        if isinstance(payload, dict):
            for key in ("orders", "items", "results", "data"):
                value = payload.get(key)
                if isinstance(value, list):
                    return [item for item in value if isinstance(item, dict)]
        return []

    def _order_date(self, order: dict[str, Any]) -> date | None:
        value = order.get("createdAt") or order.get("created_at")
        if not isinstance(value, str):
            return None
        try:
            parsed = datetime.fromisoformat(value.replace("Z", "+00:00"))
            if parsed.tzinfo is None:
                parsed = parsed.replace(tzinfo=ZoneInfo("UTC"))
            return parsed.astimezone(ZoneInfo(self.settings.timezone_name)).date()
        except (ValueError, TypeError):
            return None

    async def orders(self, for_date: date | None = None) -> list[dict[str, Any]]:
        """Return all orders or those created on the requested local date."""
        payload = await self._get(self.settings.orders_path)
        orders = self._as_orders(payload)
        if for_date is None:
            return orders
        return [order for order in orders if self._order_date(order) == for_date]

    async def stats(self, for_date: date | None = None) -> dict[str, dict[str, int]]:
        """Aggregate orders by their backend status code."""
        orders = await self.orders(for_date)
        counts: Counter[str] = Counter()
        for order in orders:
            status = order.get("statusCode") or order.get("status") or order.get("state") or order.get("statusName") or "unknown"
            counts[str(status)] += 1
        return {"statuses": dict(counts)}

    async def report(self, for_date: date) -> dict[str, Any]:
        """Return the orders used to build the local management report."""
        return {"orders": await self.orders(for_date)}
