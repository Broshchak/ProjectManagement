-- Extend the existing PostgreSQL schema without recreating data.
BEGIN;

ALTER TABLE roles
    ADD COLUMN IF NOT EXISTS is_active BOOLEAN NOT NULL DEFAULT TRUE,
    ADD COLUMN IF NOT EXISTS is_system BOOLEAN NOT NULL DEFAULT FALSE,
    ADD COLUMN IF NOT EXISTS version BIGINT NOT NULL DEFAULT 1,
    ADD COLUMN IF NOT EXISTS created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    ADD COLUMN IF NOT EXISTS updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW();

UPDATE roles
SET is_system = TRUE
WHERE code IN ('Admin', 'Director', 'Manager', 'Viewer', 'TelegramBot') AND NOT is_system;

CREATE UNIQUE INDEX IF NOT EXISTS ux_roles_code_lower ON roles (LOWER(code));

COMMIT;
