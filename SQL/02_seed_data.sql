INSERT INTO roles (code, name)
VALUES
    ('Admin', 'Administrator'),
    ('Manager', 'Order manager'),
    ('Viewer', 'Read-only user'),
    ('Director', 'Director')
ON CONFLICT (code) DO NOTHING;

INSERT INTO order_statuses (code, name, sort_order, is_final)
VALUES
    ('NewOrder', 'New order', 1, FALSE),
    ('Registered', 'Registered order', 2, FALSE),
    ('Granted', 'Granted order', 3, FALSE),
    ('Shipped', 'Shipped order', 4, FALSE),
    ('Invoiced', 'Invoiced order', 5, TRUE),
    ('Cancelled', 'Cancelled order', 6, TRUE)
ON CONFLICT (code) DO NOTHING;

INSERT INTO app_users (login, password_hash, full_name, role_id)
SELECT u.login, u.password_hash, u.full_name, r.id
FROM (
    VALUES
        ('admin', 'CHANGE_ME_HASH_ADMIN', 'System Administrator', 'Admin'),
        ('manager', 'CHANGE_ME_HASH_MANAGER', 'Order Manager', 'Manager'),
        ('viewer', 'CHANGE_ME_HASH_VIEWER', 'Read Only User', 'Viewer'),
        ('director', 'CHANGE_ME_HASH_DIRECTOR', 'Company Director', 'Director')
) AS u(login, password_hash, full_name, role_code)
JOIN roles r ON r.code = u.role_code
ON CONFLICT (login) DO NOTHING;

INSERT INTO product_categories (name, description)
VALUES
    ('Office supplies', 'Basic office products for test orders'),
    ('Electronics', 'Small electronic products for demo scenarios'),
    ('Furniture', 'Furniture items for larger orders')
ON CONFLICT (name) DO NOTHING;

INSERT INTO products (category_id, name, description)
SELECT c.id, p.name, p.description
FROM (
    VALUES
        ('Office supplies', 'Notebook A5', 'A5 paper notebook'),
        ('Office supplies', 'Blue pen', 'Simple blue ballpoint pen'),
        ('Office supplies', 'Folder', 'Document folder'),
        ('Electronics', 'USB flash drive 32GB', 'USB 3.0 flash drive'),
        ('Electronics', 'Wireless mouse', 'Compact wireless mouse'),
        ('Furniture', 'Office chair', 'Basic office chair')
) AS p(category_name, name, description)
JOIN product_categories c ON c.name = p.category_name
ON CONFLICT (category_id, name) DO NOTHING;

INSERT INTO product_prices (product_id, price, currency_code)
SELECT p.id, v.price, 'UAH'
FROM (
    VALUES
        ('Office supplies', 'Notebook A5', 45.00),
        ('Office supplies', 'Blue pen', 12.50),
        ('Office supplies', 'Folder', 18.00),
        ('Electronics', 'USB flash drive 32GB', 220.00),
        ('Electronics', 'Wireless mouse', 420.00),
        ('Furniture', 'Office chair', 2400.00)
) AS v(category_name, product_name, price)
JOIN product_categories c ON c.name = v.category_name
JOIN products p ON p.category_id = c.id AND p.name = v.product_name
ON CONFLICT (product_id) DO NOTHING;

INSERT INTO product_stocks (product_id, quantity)
SELECT p.id, v.quantity
FROM (
    VALUES
        ('Office supplies', 'Notebook A5', 120),
        ('Office supplies', 'Blue pen', 300),
        ('Office supplies', 'Folder', 80),
        ('Electronics', 'USB flash drive 32GB', 35),
        ('Electronics', 'Wireless mouse', 25),
        ('Furniture', 'Office chair', 10)
) AS v(category_name, product_name, quantity)
JOIN product_categories c ON c.name = v.category_name
JOIN products p ON p.category_id = c.id AND p.name = v.product_name
ON CONFLICT (product_id) DO NOTHING;

INSERT INTO customers (full_name, phone, email, address)
VALUES ('Demo Customer', '+380000000000', 'customer@example.com', 'Lviv, Demo street 1')
ON CONFLICT (email) DO NOTHING;

INSERT INTO orders (
    order_number,
    status_id,
    customer_id,
    comment,
    created_by_user_id
)
SELECT
    'ORD-0001',
    os.id,
    c.id,
    'Demo order for first run',
    u.id
FROM order_statuses os
JOIN customers c ON c.email = 'customer@example.com'
JOIN app_users u ON u.login = 'admin'
WHERE os.code = 'NewOrder'
ON CONFLICT (order_number) DO NOTHING;

INSERT INTO order_items (order_id, product_id, quantity)
SELECT o.id, p.id, 2
FROM orders o
JOIN product_categories c ON c.name = 'Office supplies'
JOIN products p ON p.category_id = c.id AND p.name = 'Notebook A5'
WHERE o.order_number = 'ORD-0001'
ON CONFLICT (order_id, product_id) DO NOTHING;

INSERT INTO order_status_history (order_id, previous_status_id, new_status_id, changed_by_user_id, comment)
SELECT o.id, NULL, o.status_id, u.id, 'Initial status'
FROM orders o
JOIN app_users u ON u.login = 'admin'
WHERE o.order_number = 'ORD-0001'
  AND NOT EXISTS (
      SELECT 1
      FROM order_status_history h
      WHERE h.order_id = o.id
  );
