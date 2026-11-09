# ProjectManagement

API documentation: https://project-management3.runasp.net/scalar/v1#description/introduction

OpenAPI schema: https://project-management3.runasp.net/openapi/v1.json

For an existing PostgreSQL database, apply `SQL/03_auth.sql` and then `SQL/04_telegram.sql` before starting this API. `SQL/01_create_schema.sql` recreates tables and is for new or disposable databases.

Set the connection string and JWT signing key through server configuration. If the API sits behind a reverse proxy, set `ReverseProxy__KnownProxies` to the comma-separated IP addresses of the trusted proxies. The Telegram allowlist and rate limits use the resulting client IP.

The WPF client uses the login/refresh token pair. The Telegram API is available separately; the existing Python bot still needs to be migrated to that API contract.
