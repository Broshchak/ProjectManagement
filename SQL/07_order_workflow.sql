BEGIN;

ALTER TABLE order_statuses DROP CONSTRAINT IF EXISTS order_statuses_sort_order_key;
ALTER TABLE order_statuses ADD COLUMN IF NOT EXISTS can_edit_items BOOLEAN NOT NULL DEFAULT FALSE;
ALTER TABLE order_statuses ADD COLUMN IF NOT EXISTS category VARCHAR(16) NOT NULL DEFAULT 'InProgress';

CREATE TABLE IF NOT EXISTS order_workflow (
    id INTEGER PRIMARY KEY CHECK (id = 1),
    revision BIGINT NOT NULL CHECK (revision > 0),
    draft_version BIGINT NOT NULL CHECK (draft_version > 0),
    published JSONB NOT NULL,
    draft JSONB NOT NULL
);

UPDATE order_statuses
SET can_edit_items = code IN ('NewOrder', 'Registered'),
    category = CASE code
        WHEN 'NewOrder' THEN 'New'
        WHEN 'Shipped' THEN 'Shipped'
        WHEN 'Invoiced' THEN 'Completed'
        WHEN 'Cancelled' THEN 'Cancelled'
        ELSE 'InProgress'
    END
WHERE NOT EXISTS (SELECT 1 FROM order_workflow WHERE id = 1);

WITH initial AS (
    SELECT jsonb_build_object(
        'states', (SELECT jsonb_agg(jsonb_build_object(
            'code', code, 'name', name, 'sortOrder', sort_order, 'category', category,
            'isInitial', code = 'NewOrder', 'isFinal', is_final, 'canEditItems', can_edit_items
        ) ORDER BY sort_order) FROM order_statuses),
        'transitions', (
            SELECT jsonb_agg(jsonb_build_object(
                'code', code, 'name', name, 'from', source, 'to', target, 'roles', '[]'::jsonb, 'rules', rules
            ))
            FROM (VALUES
                ('register', 'Register', 'NewOrder', 'Registered', '["HasItems"]'::jsonb),
                ('cancel', 'Cancel', 'NewOrder', 'Cancelled', '[]'::jsonb),
                ('grant', 'Approve', 'Registered', 'Granted', '["HasItems","ProductsActive","StockAvailable"]'::jsonb),
                ('cancel', 'Cancel', 'Registered', 'Cancelled', '[]'::jsonb),
                ('ship', 'Ship', 'Granted', 'Shipped', '[]'::jsonb),
                ('cancel', 'Cancel', 'Granted', 'Cancelled', '[]'::jsonb),
                ('invoice', 'Invoice', 'Shipped', 'Invoiced', '[]'::jsonb)
            ) AS transitions(code, name, source, target, rules)
        )
    ) AS definition
)
INSERT INTO order_workflow (id, revision, draft_version, published, draft)
SELECT 1, 1, 1, definition, definition FROM initial
ON CONFLICT (id) DO NOTHING;

COMMIT;
