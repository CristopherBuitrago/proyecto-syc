const express = require("express");
const { pool } = require("../db");
const { ApiError } = require("../errors");

const router = express.Router();

// GET /api/products?name=&minPrice=&maxPrice=&page=&pageSize=
router.get("/", async (req, res, next) => {
  try {
    const { name, minPrice, maxPrice, page = 1, pageSize = 20 } = req.query;

    const conditions = [];
    const params = [];

    if (name) {
      params.push(`%${name}%`);
      conditions.push(`name ILIKE $${params.length}`);
    }
    if (minPrice) {
      params.push(Number(minPrice));
      conditions.push(`price >= $${params.length}`);
    }
    if (maxPrice) {
      params.push(Number(maxPrice));
      conditions.push(`price <= $${params.length}`);
    }

    const whereClause = conditions.length ? `WHERE ${conditions.join(" AND ")}` : "";
    const limit = Math.min(Number(pageSize) || 20, 100);
    const offset = (Math.max(Number(page) || 1, 1) - 1) * limit;

    params.push(limit, offset);
    const { rows } = await pool.query(
      `SELECT id, sku, name, price, stock FROM products ${whereClause}
       ORDER BY id LIMIT $${params.length - 1} OFFSET $${params.length}`,
      params
    );

    const countResult = await pool.query(`SELECT COUNT(*) FROM products ${whereClause}`, params.slice(0, -2));

    res.json({
      data: rows,
      pagination: { page: Number(page), pageSize: limit, total: Number(countResult.rows[0].count) },
    });
  } catch (err) {
    next(err);
  }
});

router.get("/:id", async (req, res, next) => {
  try {
    const { rows } = await pool.query("SELECT id, sku, name, price, stock FROM products WHERE id = $1", [req.params.id]);
    if (rows.length === 0) throw new ApiError(404, `No existe un producto con id ${req.params.id}.`, "PRODUCT_NOT_FOUND");
    res.json(rows[0]);
  } catch (err) {
    next(err);
  }
});

router.post("/", async (req, res, next) => {
  try {
    const { sku, name, price, stock } = req.body;
    if (!sku || !name || price == null || stock == null) {
      throw new ApiError(400, "sku, name, price y stock son obligatorios.", "VALIDATION_ERROR");
    }
    if (price < 0 || stock < 0) {
      throw new ApiError(400, "price y stock no pueden ser negativos.", "VALIDATION_ERROR");
    }

    const { rows } = await pool.query(
      "INSERT INTO products (sku, name, price, stock) VALUES ($1, $2, $3, $4) RETURNING id, sku, name, price, stock",
      [sku, name, price, stock]
    );
    res.status(201).json(rows[0]);
  } catch (err) {
    if (err.code === "23505") {
      return next(new ApiError(409, `Ya existe un producto con el SKU '${req.body.sku}'.`, "DUPLICATE_SKU"));
    }
    next(err);
  }
});

module.exports = router;
