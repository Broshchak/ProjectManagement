"""Convert flexible API payloads into compact Telegram HTML messages."""
from __future__ import annotations

import html
from collections.abc import Mapping
from typing import Any

from .i18n import tr


def _escape(value: Any) -> str:
    return html.escape(str(value), quote=False)


def _status_map(payload: Any) -> dict[str, Any]:
    if isinstance(payload, list):
        result: dict[str, Any] = {}
        for item in payload:
            if isinstance(item, Mapping):
                status = item.get("status") or item.get("state") or item.get("name")
                count = item.get("count", item.get("total", item.get("value")))
                if status is not None and count is not None:
                    result[str(status)] = count
        return result
    if isinstance(payload, Mapping):
        nested = payload.get("data")
        if isinstance(nested, Mapping):
            nested_result = _status_map(nested)
            if nested_result:
                return nested_result
        for key in ("statuses", "by_status", "orders_by_status", "status_counts", "counts"):
            value = payload.get(key)
            if isinstance(value, Mapping):
                return {str(k): v for k, v in value.items()}
        # A flat object with numeric values is a common API shape.
        numeric = {str(k): v for k, v in payload.items() if isinstance(v, (int, float))}
        if numeric:
            return numeric
    return {}


STATUS_ICONS = {
    "new": "🆕", "neworder": "🆕", "processing": "🔄", "done": "✅", "completed": "✅", "cancelled": "⛔", "canceled": "⛔", "pending": "🕒",
}

STATUS_LABELS = {
    "ru": {"new": "Новые", "neworder": "Новые", "processing": "В работе", "done": "Выполнены", "completed": "Выполнены", "cancelled": "Отменены", "canceled": "Отменены"},
    "uk": {"new": "Нові", "neworder": "Нові", "processing": "У роботі", "done": "Виконані", "completed": "Виконані", "cancelled": "Скасовані", "canceled": "Скасовані"},
}

def format_stats(payload: Any, language: str) -> str:
    statuses = _status_map(payload)
    title = tr(language, "stats_title")
    if not statuses:
        return f"<b>{title}</b>\nНет данных по статусам." if language == "ru" else f"<b>{title}</b>\nНемає даних за статусами."
    lines = [f"<b>{title}</b>", "<i>Сводка по текущим данным</i>" if language == "ru" else "<i>Зведення за поточними даними</i>", ""]
    total = 0
    labels = STATUS_LABELS.get(language, {})
    for status, count in statuses.items():
        status_key = status.lower()
        display_status = labels.get(status_key, status)
        icon = STATUS_ICONS.get(status_key, "•")
        lines.append(f"{icon} {_escape(display_status)}  <b>{_escape(count)}</b>")
        if isinstance(count, (int, float)):
            total += count
    if total:
        label = "Всего" if language == "ru" else "Усього"
        lines.append(f"\n{label}: <b>{total:g}</b>")
    return "\n".join(lines)


def _orders(payload: Any) -> list[Mapping[str, Any]]:
    if isinstance(payload, list):
        return [item for item in payload if isinstance(item, Mapping)]
    if isinstance(payload, Mapping):
        for key in ("orders", "items", "results", "data"):
            value = payload.get(key)
            if isinstance(value, list):
                return [item for item in value if isinstance(item, Mapping)]
            if isinstance(value, Mapping):
                nested = _orders(value)
                if nested:
                    return nested
    return []


def _order_value(order: Mapping[str, Any], *keys: str, default: str = "—") -> Any:
    for key in keys:
        if key in order and order[key] is not None:
            return order[key]
    return default


def format_orders(payload: Any, language: str, for_date: str) -> str:
    orders = _orders(payload)
    if not orders:
        return tr(language, "no_orders")
    total_label = "Всего" if language == "ru" else "Усього"
    lines = [f"<b>{tr(language, 'orders_title', date=for_date)}</b>", f"<i>{total_label}: {len(orders)}</i>", ""]
    for order in orders[:50]:
        order_id = _order_value(order, "orderNumber", "id", "order_id", "number")
        status = _order_value(order, "statusName", "statusCode", "status", "state")
        amount = _order_value(order, "totalAmount", "amount", "total", "sum")
        currency = _order_value(order, "currencyCode", default="")
        status_key = str(_order_value(order, "statusCode", "status", "state", "statusName")).lower()
        status_label = STATUS_LABELS.get(language, {}).get(status_key, status)
        icon = STATUS_ICONS.get(status_key, "▫️")
        customer = _order_value(order, "customerFullName", "customer", "customer_name", "client", default="")
        amount_text = f"{amount} {currency}".strip()
        line = f"<b>#{_escape(order_id)}</b>  {icon} {_escape(status_label)}  ·  <b>{_escape(amount_text)}</b>"
        if customer != "":
            line += f"\n   👤 {_escape(customer)}"
        lines.append(line)
    if len(orders) > 50:
        lines.append("…")
    return "\n".join(lines)[:3900]


def format_report(payload: Any, stats_payload: Any, orders_payload: Any, language: str, for_date: str) -> str:
    if isinstance(payload, Mapping):
        ready = payload.get("text") or payload.get("summary") or payload.get("report")
        if isinstance(ready, str) and ready.strip():
            return f"<b>{tr(language, 'report_title', date=for_date)}</b>\n\n{_escape(ready.strip())}"[:3900]
    stats = format_stats(stats_payload, language)
    orders = format_orders(orders_payload, language, for_date)
    note = tr(language, "report_fallback")
    return f"<b>{tr(language, 'report_title', date=for_date)}</b>\n\n{_escape(note)}\n\n{stats}\n\n{orders}"[:3900]
