-- Esquema inicial: Sistema de Gestión de Pedidos y Descuentos
-- Se ejecuta automáticamente al levantar el contenedor de Postgres (ver docker-compose.yml)

CREATE TABLE customers (
    id              SERIAL PRIMARY KEY,
    name            VARCHAR(150) NOT NULL,
    email           VARCHAR(150) NOT NULL UNIQUE,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE products (
    id              SERIAL PRIMARY KEY,
    sku             VARCHAR(50) NOT NULL UNIQUE,
    name            VARCHAR(150) NOT NULL,
    price           NUMERIC(12,2) NOT NULL CHECK (price >= 0),
    stock           INTEGER NOT NULL CHECK (stock >= 0),
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE coupons (
    id                  SERIAL PRIMARY KEY,
    code                VARCHAR(50) NOT NULL UNIQUE,
    discount_type       VARCHAR(10) NOT NULL CHECK (discount_type IN ('PERCENT', 'FIXED')),
    discount_value      NUMERIC(12,2) NOT NULL CHECK (discount_value >= 0),
    min_purchase_amount NUMERIC(12,2) NOT NULL DEFAULT 0,
    expires_at          TIMESTAMPTZ NOT NULL,
    stackable           BOOLEAN NOT NULL DEFAULT false
);

-- Estados posibles de un pedido. CONFIRMED = stock reservado y descuentos aplicados;
-- CANCELLED = se liberó el stock reservado.
CREATE TABLE orders (
    id              SERIAL PRIMARY KEY,
    customer_id     INTEGER NOT NULL REFERENCES customers(id),
    status          VARCHAR(20) NOT NULL DEFAULT 'CONFIRMED' CHECK (status IN ('CONFIRMED', 'CANCELLED')),
    coupon_code     VARCHAR(50),
    subtotal        NUMERIC(12,2) NOT NULL,
    total_discount  NUMERIC(12,2) NOT NULL DEFAULT 0,
    total_final     NUMERIC(12,2) NOT NULL,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE order_items (
    id                  SERIAL PRIMARY KEY,
    order_id            INTEGER NOT NULL REFERENCES orders(id) ON DELETE CASCADE,
    product_id          INTEGER NOT NULL REFERENCES products(id),
    quantity            INTEGER NOT NULL CHECK (quantity > 0),
    unit_price          NUMERIC(12,2) NOT NULL,
    line_discount       NUMERIC(12,2) NOT NULL DEFAULT 0,
    line_total          NUMERIC(12,2) NOT NULL
);

-- Desglose de descuentos aplicados a un pedido, para mostrar al cliente qué regla
-- aplicó y cuánto descontó cada una (ver sección 5 del enunciado, punto 2).
CREATE TABLE applied_discounts (
    id              SERIAL PRIMARY KEY,
    order_id        INTEGER NOT NULL REFERENCES orders(id) ON DELETE CASCADE,
    discount_type   VARCHAR(20) NOT NULL CHECK (discount_type IN ('VOLUME', 'LOYALTY', 'COUPON')),
    description     VARCHAR(255) NOT NULL,
    amount          NUMERIC(12,2) NOT NULL
);

CREATE INDEX idx_orders_customer_id ON orders(customer_id);
CREATE INDEX idx_orders_status ON orders(status);
CREATE INDEX idx_order_items_order_id ON order_items(order_id);
CREATE INDEX idx_applied_discounts_order_id ON applied_discounts(order_id);

-- Datos de ejemplo para poder probar el flujo completo sin cargar nada a mano.
INSERT INTO customers (name, email) VALUES
    ('Juan Pérez', 'juan.perez@example.com'),
    ('María Gómez', 'maria.gomez@example.com');

INSERT INTO products (sku, name, price, stock) VALUES
    ('SKU-001', 'Teclado mecánico', 150000, 50),
    ('SKU-002', 'Mouse inalámbrico', 60000, 100),
    ('SKU-003', 'Monitor 24"', 650000, 15);

INSERT INTO coupons (code, discount_type, discount_value, min_purchase_amount, expires_at, stackable) VALUES
    ('BIENVENIDA10', 'PERCENT', 10, 100000, '2027-01-01', true),
    ('SUPER50K', 'FIXED', 50000, 300000, '2027-01-01', false);
