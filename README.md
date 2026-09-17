# Sistema de Gestión de Pedidos y Descuentos

Prueba técnica para Sistemas y Computadores S.A. (SYC). Sistema compuesto por tres servicios independientes que se comunican entre sí, más una base de datos PostgreSQL compartida, todo orquestado con Docker Compose.

Las decisiones de diseño y su justificación (motor de descuentos, manejo de concurrencia, arquitectura) están documentadas en **[`DECISIONS.md`](./DECISIONS.md)** — léelo para entender el *por qué* detrás de lo que hay acá.

## Servicios

| Servicio | Carpeta | Responsabilidad | Puerto |
|---|---|---|---|
| **Orders & Discount Engine** | `dotnet-orders/` | .NET 8 / ASP.NET Core + EF Core. Crear/consultar/listar/cancelar pedidos, motor de descuentos (volumen + nivel de cliente + cupón), valida stock contra el servicio Node antes de confirmar. | `5000` |
| **Catalog & Inventory** | `node-catalog/` | Node.js + Express. CRUD de productos, reserva y liberación de stock con locking pesimista (`SELECT ... FOR UPDATE`) para evitar condiciones de carrera. | `4000` |
| **Frontend** | `frontend/` | React. Catálogo, creación de pedidos con desglose de descuentos, historial. **Pendiente de construir** (ver estado actual más abajo). | `5173` |
| **Base de datos** | `db/init.sql` | PostgreSQL 16. Esquema + datos semilla, se ejecuta automático al levantar el contenedor. | `5432` |

## Requisitos previos

- Docker y Docker Compose.
- Para desarrollo local sin Docker (opcional): .NET 8 SDK y Node.js 20+.

## Cómo levantar el proyecto

> **Estado actual:** el servicio `frontend` todavía no existe como carpeta, así que `docker-compose up` sin argumentos fallará intentando construir esa imagen. Mientras tanto, levanta solo los servicios ya construidos:

```bash
docker-compose up --build postgres node-catalog dotnet-orders
```

Una vez exista `frontend/` (ver pendientes), el comando definitivo para levantar **todo** el sistema con un solo paso será:

```bash
docker-compose up --build
```

Esto levanta, en orden (Postgres primero, luego Node, luego .NET, que depende de ambos):

- Postgres en `localhost:5432` (usuario `syc`, base `syc_orders`), con el esquema y datos de ejemplo ya cargados desde `db/init.sql`.
- Catalog & Inventory en `http://localhost:4000`.
- Orders & Discount Engine en `http://localhost:5000`, con Swagger en `http://localhost:5000/swagger`.

Para bajar todo (y borrar el volumen de datos, si se quiere empezar de cero):

```bash
docker-compose down -v
```

## Desarrollo local sin Docker (opcional)

**Node (`node-catalog/`):**
```bash
cd node-catalog
npm install
npm run dev
```
Necesita `DATABASE_URL` apuntando a Postgres (ver `docker-compose.yml` para el formato).

**.NET (`dotnet-orders/`):**
```bash
cd dotnet-orders
dotnet run
```
La configuración local ya está en `appsettings.Development.json` (conexión a `localhost:5432` y `CatalogService:BaseUrl` a `localhost:4000`) — solo asegúrate de tener Postgres y Node corriendo antes.

## Variables de entorno

| Variable | Servicio | Descripción |
|---|---|---|
| `DATABASE_URL` | Node | Cadena de conexión a Postgres. |
| `PORT` | Node | Puerto HTTP (default `4000`). |
| `ConnectionStrings__Default` | .NET | Cadena de conexión a Postgres. |
| `CatalogService__BaseUrl` | .NET | URL base del servicio Node, para validar/reservar stock. |
| `ASPNETCORE_URLS` | .NET | URL de escucha del servidor Kestrel. |

Ninguna credencial va hardcodeada en el código — todas se inyectan por variables de entorno (ver `docker-compose.yml`).

## Endpoints principales

**Catalog & Inventory (Node) — `http://localhost:4000`**
| Método | Ruta | Descripción |
|---|---|---|
| GET | `/api/products` | Listar productos (filtros: `name`, `minPrice`, `maxPrice`, paginación). |
| GET | `/api/products/:id` | Ver un producto. |
| POST | `/api/products` | Crear producto. |
| POST | `/api/inventory/reserve` | Reservar stock de una o más líneas (todo o nada). Usado internamente por .NET. |
| POST | `/api/inventory/release` | Liberar stock previamente reservado. Usado internamente por .NET. |

**Orders & Discount Engine (.NET) — `http://localhost:5000`** (documentación interactiva en `/swagger`)
| Método | Ruta | Descripción |
|---|---|---|
| POST | `/api/orders` | Crear pedido: calcula descuentos, valida/reserva stock contra Node, persiste. |
| GET | `/api/orders/{id}` | Consultar un pedido, con desglose de descuentos aplicados. |
| GET | `/api/orders` | Listar pedidos (filtros: cliente, estado, rango de fechas, paginación). |
| POST | `/api/orders/{id}/cancel` | Cancelar un pedido confirmado y liberar su stock reservado. |

## Base de datos

El esquema completo (`customers`, `products`, `coupons`, `orders`, `order_items`, `applied_discounts`) vive en [`db/init.sql`](./db/init.sql) y se ejecuta automáticamente al crear el contenedor de Postgres (monta como script de inicialización). Incluye datos de ejemplo para poder probar el flujo completo sin cargar nada a mano.

## Pruebas

Pruebas unitarias del motor de descuentos (`dotnet-orders.Tests/DiscountEngineTests.cs`):

```bash
cd dotnet-orders.Tests
dotnet test
```

## Estado actual / pendientes

- [x] Base de datos (esquema + seed).
- [x] Servicio Node (catálogo + inventario + concurrencia).
- [x] Servicio .NET (pedidos + motor de descuentos + pruebas unitarias).
- [x] `DECISIONS.md` con las decisiones de diseño obligatorias.
- [ ] Frontend React.
- [ ] Colección Postman (o exportar el OpenAPI de Swagger).
- [ ] Ajustar `docker-compose.yml` una vez exista `frontend/` (el bloque ya está preparado, solo falta el código).
