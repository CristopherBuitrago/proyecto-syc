const express = require("express");
const cors = require("cors");
const productsRouter = require("./routes/products");
const inventoryRouter = require("./routes/inventory");
const { errorHandler } = require("./errors");

const app = express();
const PORT = process.env.PORT || 4000;

app.use(cors());
app.use(express.json());

// Log mínimo de operaciones críticas (sección 8 del enunciado).
app.use((req, res, next) => {
  console.log(`[${new Date().toISOString()}] ${req.method} ${req.path}`);
  next();
});

app.get("/health", (req, res) => res.json({ status: "ok" }));

app.use("/api/products", productsRouter);
app.use("/api/inventory", inventoryRouter);

app.use(errorHandler);

app.listen(PORT, () => {
  console.log(`Catalog & Inventory service escuchando en el puerto ${PORT}`);
});
