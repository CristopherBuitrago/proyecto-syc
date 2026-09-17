# Frontend (React + Vite)

SPA de cara al cliente: catálogo, creación de pedido con desglose de descuentos, historial. El enunciado no evalúa diseño visual elaborado — el foco está en claridad, manejo de estados de carga/error, y que el desglose de descuentos sea entendible.

## Arquitectura aplicada

**Arquitectura de componentes con Context API para estado global** — sin Redux/Zustand ni ninguna librería de fetching (React Query/SWR). Organizado **por rol técnico**, no por feature (no hay una carpeta `orders/` con su propio componente+estado+API adentro) — el mismo criterio de capas que se usó en el `.NET`.

```
Context (estado) → Componente lee con un hook → interacción del usuario
→ llama una función del Context → el Context actualiza su estado
→ React re-renderiza lo que depende de ese estado
```

Flujo de datos unidireccional: ningún componente muta el estado de otro directamente, todo pasa por las funciones que exponen los Context (`addItem`, `setCouponCode`, `setCurrentCustomerId`, etc.).

Es una **SPA pura sin SSR**: Vite genera archivos estáticos y nginx los sirve (ver `Dockerfile`/`nginx.conf`), desacoplada del backend salvo por dos URLs base inyectadas en tiempo de **build** (`VITE_ORDERS_API_URL`/`VITE_CATALOG_API_URL` — ver nota en `docker-compose.yml` sobre por qué son build args y no `environment`).

## Función de cada carpeta

| Carpeta/archivo | Responsabilidad |
|---|---|
| `src/main.jsx` | Bootstrap: monta React, envuelve `<App />` en `BrowserRouter` → `CustomerSessionProvider` → `OrderDraftProvider`. |
| `src/App.jsx` | Define las 3 rutas y monta el `Header` fijo arriba. |
| `src/api/client.js` | Única capa que hace `fetch`. Un solo `ApiError` y un solo parser de errores para las dos APIs, porque ambas devuelven el mismo shape `{ message, code, details }`. |
| `src/context/CustomerSessionContext.jsx` | La "sesión de cliente simulada" (ver `DECISIONS.md`, punto 8): pide `GET /api/customers` una vez, guarda cuál es el cliente actual en `localStorage`. Ninguna pantalla vuelve a preguntar "¿de qué cliente es esto?". |
| `src/context/OrderDraftContext.jsx` | El carrito en memoria, compartido entre Catálogo y Crear Pedido: `addItem`, `updateQuantity`, `removeItem`, `clearDraft`, `couponCode`. |
| `src/components/Header.jsx` | Navegación (`NavLink`, resalta la ruta activa) + selector de sesión de cliente. |
| `src/components/ErrorBanner.jsx` | Renderiza cualquier `ApiError` de cualquiera de las dos APIs. Caso especial: `STOCK_UNAVAILABLE` muestra línea por línea qué producto falló y cuánto stock hay. |
| `src/components/DiscountBreakdown.jsx` | Desglose de descuentos (subtotal, cada regla aplicada con su ícono, total). Reutilizado en Crear Pedido e Historial — mismo shape de `order` en ambos casos. |
| `src/pages/CatalogPage.jsx` | Pantalla 1: lista productos (Node), agrega al carrito. |
| `src/pages/CreateOrderPage.jsx` | Pantalla 2: consume el carrito, selector de cupón (`GET /api/coupons`), `POST /api/orders`, muestra el desglose o el error. |
| `src/pages/OrderHistoryPage.jsx` | Pantalla 3: `GET /api/orders?customerId=...` filtrado al cliente de la sesión, detalle expandible, cancelar pedido. |
| `src/styles.css` | CSS plano, sin librería de UI — a propósito, dado que el enunciado no evalúa diseño elaborado. |
| `Dockerfile` | Build multi-stage: Node compila con Vite, nginx sirve los estáticos resultantes. |
| `nginx.conf` | Fallback de rutas (`try_files ... /index.html`) para que React Router funcione al recargar o entrar directo a una ruta como `/historial`. |

## Correr localmente

```bash
npm install
npm run dev
```
Sirve en `http://localhost:5173`, apuntando por defecto a `http://localhost:5000` (.NET) y `http://localhost:4000` (Node) — ver los defaults en `src/api/client.js`.
