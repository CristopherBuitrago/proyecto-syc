# Catalog & Inventory (Node.js)

API en Express. Dueño único del catálogo de productos y del stock — nada de pedidos, clientes ni descuentos vive acá (eso es responsabilidad exclusiva de `dotnet-orders`). Ver `DECISIONS.md` en la raíz del repo, puntos 2 y 6, para el porqué de esta separación.

## Arquitectura aplicada

**Rutas planas sobre SQL crudo (`pg`), sin ORM y sin capa de servicio intermedia.** A propósito, y no por simplicidad perezosa: el punto crítico de este servicio es el control fino de la transacción de reserva de stock (`SELECT ... FOR UPDATE`), y un ORM habría metido una capa de abstracción entre el código y el SQL real que termina ejecutándose — justo donde más importa verlo explícito. Es una decisión **local a este servicio**; el `.NET` sí usa EF Core porque ahí el modelo es más rico y no necesita ese nivel de control. Detalle completo en `DECISIONS.md`, punto 2.

```
Route (Express) → pool.query() (pg) → PostgreSQL
```

Sin capas intermedias porque el servicio es deliberadamente pequeño (2 rutas de negocio + un CRUD simple) — agregar `services/`/`repositories/` acá habría sido estructura sin sustancia detrás.

## Función de cada carpeta

| Archivo/carpeta | Responsabilidad |
|---|---|
| `src/server.js` | Bootstrap: crea la app de Express, monta middlewares (CORS, JSON, log mínimo de operaciones — sección 8 del enunciado) y las rutas. |
| `src/db.js` | Pool de conexión a Postgres (`pg`), lee `DATABASE_URL` del entorno — nunca credenciales hardcodeadas. |
| `src/errors.js` | `ApiError` + middleware `errorHandler` centralizado. Devuelve `{ message, code, details }` — el mismo shape que usa `.NET`, para que el frontend tenga un solo parser de errores. |
| `src/routes/products.js` | CRUD de catálogo: `GET /api/products` (con filtros y paginación), `GET /api/products/:id`, `POST /api/products`. Sin lógica de negocio compleja. |
| `src/routes/inventory.js` | El núcleo del servicio: `POST /api/inventory/reserve` y `POST /api/inventory/release`. Acá vive la estrategia de concurrencia (locking pesimista con `FOR UPDATE` dentro de una transacción) — ver el comentario en el propio archivo y `DECISIONS.md` punto 2. |

## Correr localmente

```bash
npm install
npm run dev
```
Necesita `DATABASE_URL` apuntando a Postgres (ver `docker-compose.yml` para el formato). Health check en `GET /health`.
