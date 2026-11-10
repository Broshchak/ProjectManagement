import asyncio
import json
import unittest
from datetime import datetime, timedelta, timezone
from types import SimpleNamespace
from unittest.mock import AsyncMock, patch

import httpx

from bot.api import ApiError, BackendApi, BotUser, Report
from bot.config import Settings
from bot.handlers import handle_message


SETTINGS = Settings("test-token", "https://orders.example.test", "telegram-bot", "test-password")
USER = {"fullName": "Test Director", "role": "Director", "permissions": ["reports.read", "reports.telegram.send"]}


def tokens(suffix, access_seconds=900):
    now = datetime.now(timezone.utc)
    return {
        "accessToken": "access-" + suffix, "refreshToken": "refresh-" + suffix,
        "accessTokenExpiresAt": (now + timedelta(seconds=access_seconds)).isoformat(),
        "refreshTokenExpiresAt": (now + timedelta(days=14)).isoformat(),
    }


class ClientTests(unittest.IsolatedAsyncioTestCase):
    def client(self, handler):
        api = BackendApi(SETTINGS, transport=httpx.MockTransport(handler))
        self.addAsyncCleanup(api.aclose)
        return api

    async def test_rotation_is_shared_by_concurrent_calls(self):
        calls = []

        async def server(request):
            path = request.url.path
            calls.append(path)
            if path == "/api/auth/login":
                return httpx.Response(200, json=tokens("initial", access_seconds=10))
            if path == "/api/auth/refresh":
                self.assertEqual(json.loads(request.content), {"refreshToken": "refresh-initial"})
                await asyncio.sleep(0)
                return httpx.Response(200, json=tokens("rotated"))
            self.assertIn(request.headers["Authorization"], {"Bearer access-initial", "Bearer access-rotated"})
            return httpx.Response(200, json=USER)

        api = self.client(server)
        await api.resolve(1)
        results = await asyncio.gather(*(api.resolve(i) for i in range(1, 6)))
        self.assertTrue(all(user.role == "Director" for user in results))
        self.assertEqual(calls.count("/api/auth/login"), 1)
        self.assertEqual(calls.count("/api/auth/refresh"), 1)

    async def test_401_logs_in_once_and_does_not_follow_redirects(self):
        logins = 0
        protected = 0

        def server(request):
            nonlocal logins, protected
            if request.url.path == "/api/auth/login":
                logins += 1
                return httpx.Response(200, json=tokens(str(logins)))
            self.assertEqual(request.url.host, "orders.example.test")
            protected += 1
            if protected == 1:
                return httpx.Response(401)
            return httpx.Response(307, headers={"Location": "https://untrusted.example.test/"})

        api = self.client(server)
        with self.assertRaisesRegex(ApiError, "unavailable"):
            await api.resolve(1)
        self.assertEqual((logins, protected), (2, 2))

    async def test_invalid_credentials_do_not_start_a_login_loop(self):
        calls = 0

        def server(request):
            nonlocal calls
            calls += 1
            return httpx.Response(401)

        api = self.client(server)
        for _ in range(3):
            with self.assertRaisesRegex(ApiError, "invalid_service_credentials"):
                await api.resolve(1)
        self.assertEqual(calls, 1)

    async def test_safe_request_retries_but_link_does_not(self):
        counts = {"GET": 0, "POST": 0}

        def server(request):
            if request.url.path == "/api/auth/login":
                return httpx.Response(200, json=tokens("initial"))
            counts[request.method] += 1
            if request.method == "GET" and counts["GET"] == 3:
                return httpx.Response(404, json={"code": "not_linked"})
            return httpx.Response(503)

        api = self.client(server)
        with patch("bot.api.asyncio.sleep", new_callable=AsyncMock):
            self.assertIsNone(await api.resolve(1))
            with self.assertRaisesRegex(ApiError, "unavailable"):
                await api.link(1, "a" * 24)
        self.assertEqual(counts, {"GET": 3, "POST": 1})

    async def test_link_profile_report_and_unlink(self):
        linked = False
        report_count = 0

        def server(request):
            nonlocal linked, report_count
            path = request.url.path
            if path == "/api/auth/login":
                return httpx.Response(200, json=tokens("initial"))
            self.assertEqual(request.headers["Authorization"], "Bearer access-initial")
            if path.endswith("/link") and request.method == "POST":
                self.assertEqual(json.loads(request.content), {"token": "a" * 24})
                linked = True
                return httpx.Response(200, json=USER)
            if path.endswith("/link") and request.method == "DELETE":
                linked = False
                return httpx.Response(204)
            if not linked:
                return httpx.Response(404, json={"code": "not_linked"})
            if path.endswith("/report"):
                report_count += 1
                if report_count == 1:
                    return httpx.Response(200, json={"type": "text", "text": "<plain text>"})
                return httpx.Response(200, content=b"%PDF",
                                      headers={"Content-Type": "application/pdf", "Content-Disposition": "attachment; filename*=UTF-8''orders.pdf"})
            return httpx.Response(200, json=USER)

        api = self.client(server)
        self.assertIsNone(await api.resolve(42))
        self.assertEqual((await api.link(42, "a" * 24)).role, "Director")
        self.assertEqual((await api.resolve(42)).full_name, USER["fullName"])
        self.assertEqual((await api.report(42)).text, "<plain text>")
        report = await api.report(42)
        self.assertEqual((report.filename, report.content), ("orders.pdf", b"%PDF"))
        await api.unlink(42)
        self.assertIsNone(await api.resolve(42))

    def test_configuration_requires_https(self):
        with self.assertRaises(ValueError):
            Settings("test-token", "http://orders.example.test", "telegram-bot", "test-password")


class HandlerTests(unittest.IsolatedAsyncioTestCase):
    @staticmethod
    def message(text, *, chat_type="private", is_bot=False):
        return SimpleNamespace(
            chat=SimpleNamespace(type=chat_type, id=123),
            from_user=SimpleNamespace(id=456, is_bot=is_bot),
            bot=SimpleNamespace(send_chat_action=AsyncMock()),
            text=text, answer=AsyncMock(), answer_document=AsyncMock(), delete=AsyncMock(),
        )

    async def test_unlinked_is_silent_except_start_and_invalid_chats_are_ignored(self):
        api = SimpleNamespace(resolve=AsyncMock(return_value=None))
        for msg in [self.message("/report", chat_type="group"), self.message("/report", is_bot=True), self.message(None)]:
            await handle_message(api, msg)
            msg.answer.assert_not_awaited()
        api.resolve.assert_not_awaited()
        msg = self.message("/report")
        await handle_message(api, msg)
        msg.answer.assert_not_awaited()
        msg = self.message("/start")
        await handle_message(api, msg)
        msg.answer.assert_awaited_once()

    async def test_uses_sender_id_and_sends_plain_text_or_file(self):
        api = SimpleNamespace(
            link=AsyncMock(return_value=BotUser("Director", "Director", frozenset())),
            resolve=AsyncMock(return_value=BotUser("Director", "Director", frozenset())),
            report=AsyncMock(side_effect=[Report(text="<a>" + "x" * 4096), Report(content=b"%PDF", filename="report.pdf")]),
        )
        start = self.message("/start " + "a" * 24)
        await handle_message(api, start)
        api.link.assert_awaited_once_with(456, "a" * 24)
        start.delete.assert_awaited_once()
        report = self.message("/report 777")
        await handle_message(api, report)
        api.report.assert_awaited_with(456)
        self.assertEqual(report.answer.await_count, 2)
        self.assertTrue(all(not call.kwargs for call in report.answer.await_args_list))
        file_report = self.message("/report")
        await handle_message(api, file_report)
        self.assertEqual(file_report.answer_document.await_args.args[0].filename, "report.pdf")
