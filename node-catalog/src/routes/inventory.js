const express = require("express");
const { pool } = require("../db");
const { ApiError } = require("../errors");

const router = express.Router();

/**
 * Estrategia de concurrencia elegida: LOCKING PESIMISTA con `SELECT ... FOR UPDATE`
 * dentro de una transacción, en vez de locking optimista (versión/timestamp).
 *
 * Por qué: la reserva de stock es una operación corta y de alta contención esperada
 * (varios pedidos pueden pelear por el mismo producto al mismo tiempo). Con locking
 * optimista, bajo contención alta se generarían muchos reintentos fallidos (choques
 * de versión) que el cliente tendría que manejar. Con `FOR UPDATE`, la segunda
 * transacción simplemente espera a que la primera termine (commit o rollback) antes
 * de leer el stock actualizado — más simple de razonar y sin necesidad de lógica de
 * reintento en el servicio .NET que consume este endpoint. El costo (bloqueo breve)
 * es aceptable porque la transacción es muy corta (unas pocas escrituras).
 * Ver DECISIONS.md para la justificación completa.
 */
router.post("/reserve", async (req, res, next) => {
  const client = await pool.connect();
  try {
    const items = req.body.items;

    // Validación básica de entrada
    if (!Array.isArray(items) || items.length === 0) {
      throw new ApiError(400, "items debe ser un arreglo no vacío.", "VALIDATION_ERROR");
    }

    // Inicia la transacción
    await client.query("BEGIN");

    const failedItems = []; // Para almacenar productos con stock insuficiente
    const lockedStock = new Map(); // Para almacenar el stock bloqueado de cada producto

    // Bloquea y verifica el stock de cada producto
    for (const { productId, quantity } of items) {
      // Bloquea la fila del producto para evitar que otros procesos modifiquen el stock mientras verificamos
      const { rows } = await client.query("SELECT stock FROM products WHERE id = $1 FOR UPDATE", [productId]);
      if (rows.length === 0) {
        await client.query("ROLLBACK");
        throw new ApiError(404, `No existe un producto con id ${productId}.`, "PRODUCT_NOT_FOUND");
      }
      lockedStock.set(productId, rows[0].stock);
      if (rows[0].stock < quantity) {
        failedItems.push({ productId, requested: quantity, available: rows[0].stock });
      }
    }

    // Si hay productos con stock insuficiente, hacemos rollback y lanzamos un error
    if (failedItems.length > 0) {
      await client.query("ROLLBACK");
      throw new ApiError(409, "Stock insuficiente para uno o más productos.", "STOCK_UNAVAILABLE", { failedItems });
    }

    // Actualiza el stock de los productos reservados
    for (const { productId, quantity } of items) {
      await client.query("UPDATE products SET stock = stock - $1, updated_at = now() WHERE id = $2", [quantity, productId]);
    }

    // Commit de la transacción
    await client.query("COMMIT");
    console.log(`[RESERVE] ${items.map((i) => `producto ${i.productId} x${i.quantity}`).join(", ")}`);
    res.json({ reserved: true });
  } catch (err) {
    try { await client.query("ROLLBACK"); } catch { /* ya se hizo rollback arriba */ }
    next(err);
  } finally {
    client.release();
  }
});

// Endpoint para liberar stock reservado (por ejemplo, si un pedido se cancela)
router.post("/release", async (req, res, next) => {
  const client = await pool.connect();
  try {
    const items = req.body.items;
    if (!Array.isArray(items) || items.length === 0) {
      throw new ApiError(400, "items debe ser un arreglo no vacío.", "VALIDATION_ERROR");
    }

    await client.query("BEGIN");
    for (const { productId, quantity } of items) {
      const result = await client.query(
        "UPDATE products SET stock = stock + $1, updated_at = now() WHERE id = $2",
        [quantity, productId]
      );
      if (result.rowCount === 0) {
        await client.query("ROLLBACK");
        throw new ApiError(404, `No existe un producto con id ${productId}.`, "PRODUCT_NOT_FOUND");
      }
    }
    await client.query("COMMIT");
    console.log(`[RELEASE] ${items.map((i) => `producto ${i.productId} x${i.quantity}`).join(", ")}`);
    res.json({ released: true });
  } catch (err) {
    try { await client.query("ROLLBACK"); } catch { /* ya se hizo rollback arriba */ }
    next(err);
  } finally {
    client.release();
  }
});

module.exports = router;
