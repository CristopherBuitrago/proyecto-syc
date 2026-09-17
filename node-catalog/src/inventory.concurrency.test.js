const { test, after } = require("node:test");
const assert = require("node:assert/strict");
const { Pool } = require("pg");

// Prueba de integración real (no mockeada): dispara reservas concurrentes contra el
// servicio corriendo de verdad y contra Postgres real, para comprobar en la práctica
// lo que DECISIONS.md (punto 2) justifica en la teoría: el locking pesimista
// (SELECT ... FOR UPDATE) evita que el stock quede negativo bajo contención.
//
// Requiere que el servicio y Postgres ya estén corriendo (docker-compose up, o
// `npm run dev` + Postgres local). No es un test unitario del motor de descuentos
// (ese vive en dotnet-orders.Tests) — este cubre el criterio de aceptación de
// concurrencia del enunciado, que es específico de este servicio.

const BASE_URL = process.env.CATALOG_BASE_URL || "http://localhost:4000";
const DATABASE_URL =
  process.env.DATABASE_URL || "postgres://syc:syc_dev_pass@localhost:5432/syc_orders";

const pool = new Pool({ connectionString: DATABASE_URL });

async function createTestProduct(stock) {
  const sku = `CONCURRENCY-TEST-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;
  const { rows } = await pool.query(
    "INSERT INTO products (sku, name, price, stock) VALUES ($1, $2, $3, $4) RETURNING id",
    [sku, "Producto de prueba de concurrencia", 10000, stock]
  );
  return rows[0].id;
}

async function deleteTestProduct(productId) {
  await pool.query("DELETE FROM products WHERE id = $1", [productId]);
}

async function getStock(productId) {
  const { rows } = await pool.query("SELECT stock FROM products WHERE id = $1", [productId]);
  return rows[0].stock;
}

function reserve(productId, quantity) {
  return fetch(`${BASE_URL}/api/inventory/reserve`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ items: [{ productId, quantity }] }),
  });
}

test("reservas concurrentes por el mismo producto nunca dejan el stock en negativo", async () => {
  // 10 unidades disponibles, 5 solicitudes de 3 unidades cada una en paralelo = 15
  // pedidas. Con locking correcto, exactamente 3 deben ganar (9 reservadas, queda 1,
  // insuficiente para las 2 restantes) — el resultado es determinístico porque todas
  // las solicitudes piden la misma cantidad.
  const productId = await createTestProduct(10);

  try {
    const results = await Promise.all([
      reserve(productId, 3),
      reserve(productId, 3),
      reserve(productId, 3),
      reserve(productId, 3),
      reserve(productId, 3),
    ]);

    const succeeded = results.filter((r) => r.status === 200);
    const failed = results.filter((r) => r.status === 409);

    assert.equal(succeeded.length, 3, "deben ganar exactamente 3 de las 5 reservas de 3 unidades");
    assert.equal(failed.length, 2, "las otras 2 deben fallar por stock insuficiente");

    const finalStock = await getStock(productId);
    assert.equal(finalStock, 1, "10 - (3 reservas x 3 unidades) = 1 unidad restante");
    assert.ok(finalStock >= 0, "el stock nunca puede quedar negativo");
  } finally {
    await deleteTestProduct(productId);
  }
});

test("una sola reserva no puede exceder el stock disponible", async () => {
  const productId = await createTestProduct(5);

  try {
    const response = await reserve(productId, 6);
    const body = await response.json();

    assert.equal(response.status, 409);
    assert.equal(body.code, "STOCK_UNAVAILABLE");
    assert.equal(body.details.failedItems[0].available, 5);

    const finalStock = await getStock(productId);
    assert.equal(finalStock, 5, "el stock no debe cambiar si la reserva completa falla");
  } finally {
    await deleteTestProduct(productId);
  }
});

after(async () => {
  await pool.end();
});
