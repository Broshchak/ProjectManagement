# ProjectManagement

API documentation: https://project-management3.runasp.net/scalar/v1#description/introduction

OpenAPI schema: https://project-management3.runasp.net/openapi/v1.json

For an existing PostgreSQL database, apply `SQL/03_auth.sql`, `SQL/04_telegram.sql`, `SQL/05_roles.sql`, `SQL/06_role_permissions.sql` and `SQL/07_order_workflow.sql` in that order before starting this API. `SQL/01_create_schema.sql` recreates tables and is for new or disposable databases.

Set the connection string and JWT signing key through server configuration. If the API sits behind a reverse proxy, set `ReverseProxy__KnownProxies` to the comma-separated IP addresses of the trusted proxies. The Telegram allowlist and rate limits use the resulting client IP.

The WPF client uses the login/refresh token pair and can generate a Telegram link. See [Telegram bot setup](TelegramBot/README.md). Admin manages roles and section visibility in WPF under **Roles and access**. The API reads current visibility from PostgreSQL, including for users who signed in before a permission change. Re-running `SQL/06_role_permissions.sql` preserves existing grants.

Admin and Director configure order states, transition roles and rules in **Order workflow**. Apply `SQL/07_order_workflow.sql` before starting the new API, then use the matching WPF client. The migration keeps existing orders and status history and installs the six default states and seven transitions.
