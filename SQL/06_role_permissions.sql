BEGIN;

-- Seed only on first creation. Running this migration again must not restore revoked permissions.
DO $$
BEGIN
    IF to_regclass('role_permissions') IS NULL THEN
        CREATE TABLE role_permissions (
            role_id INTEGER NOT NULL REFERENCES roles(id) ON DELETE CASCADE,
            permission_code VARCHAR(50) NOT NULL,
            PRIMARY KEY (role_id, permission_code)
        );

        INSERT INTO role_permissions (role_id, permission_code)
        SELECT r.id, p.code
        FROM roles r
        JOIN (VALUES
            ('Admin','dashboard.read'), ('Admin','orders.read'), ('Admin','board.read'),
            ('Admin','products.read'), ('Admin','customers.read'), ('Admin','users.read'), ('Admin','reports.read'),
            ('Director','dashboard.read'), ('Director','orders.read'), ('Director','board.read'),
            ('Director','products.read'), ('Director','customers.read'), ('Director','users.read'), ('Director','reports.read'),
            ('Manager','orders.read'), ('Manager','board.read'), ('Manager','products.read'), ('Manager','customers.read'),
            ('Viewer','dashboard.read'), ('Viewer','orders.read'), ('Viewer','board.read'),
            ('Viewer','products.read'), ('Viewer','customers.read'), ('Viewer','reports.read')
        ) AS p(role_code, code) ON p.role_code = r.code;
    END IF;
END $$;

COMMIT;
