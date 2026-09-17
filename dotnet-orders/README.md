# Orders & Discount Engine (.NET 8)

API en ASP.NET Core 8 + EF Core. Dueño de pedidos, clientes, cupones y el motor de descuentos. Delega toda la escritura de stock al servicio Node (`node-catalog`) — ver `DECISIONS.md` en la raíz del repo, punto 6.

## Arquitectura aplicada

**Arquitectura en capas (layered), en un solo proyecto** — el patrón por defecto de ASP.NET Core Web API, no Clean Architecture con proyectos separados (Domain/Application/Infrastructure como class libraries). Para el alcance de esta prueba, capas por carpeta alcanza; separar en proyectos distintos habría sido sobre-ingeniería sin beneficio real.

```
Controller → Service → DbContext (EF Core) → PostgreSQL
```

- El **Controller** nunca habla con la base de datos directamente ni contiene lógica de negocio — solo recibe la petición HTTP, llama al Service, y traduce excepciones de negocio a códigos HTTP.
- El **Service** contiene toda la lógica de negocio y es la única capa que orquesta: valida, calcula descuentos, llama a Node, persiste.
- Dentro de `Services/Discounts/` hay un patrón aparte: **Strategy** — Volumen y Loyalty son clases independientes detrás de una interfaz (`IVolumeDiscountRule`, `ILoyaltyDiscountRule`), inyectadas por DI. El cupón todavía vive inline en `DiscountEngine` (ver `DECISIONS.md`, punto 7, para el porqué y cómo evolucionarlo).

## Función de cada carpeta

| Carpeta | Contiene | Responsabilidad |
|---|---|---|
| `Controllers/` | `OrdersController`, `CustomersController`, `CouponsController` | Reciben HTTP, delegan al Service correspondiente, mapean excepciones → códigos HTTP. Ningún controller accede a la base de datos directo. |
| `Services/` | `OrderService`, `CustomerService`, `CouponService`, `CatalogClient`, `OrderExceptions` | Lógica de negocio. `OrderService` es el orquestador principal: valida, calcula descuentos, reserva stock en Node, persiste. `CatalogClient` es el cliente HTTP hacia Node. `OrderExceptions` define las excepciones de dominio (una por caso de error de negocio). |
| `Services/Discounts/` | `DiscountEngine`, reglas, modelos | El motor de descuentos, aislado del resto — no toca la base de datos ni HTTP, por eso es fácil de probar unitariamente sin mocks pesados. |
| `Data/` | `OrdersDbContext` | Configuración de EF Core: mapeo de tablas (snake_case ← convención de Postgres), conversión de enums a los strings que usa `db/init.sql`. |
| `Models/` | `Customer`, `Product`, `Order`, `Coupon` | Entidades de dominio, mapeadas 1:1 a las tablas de Postgres. |
| `DTOs/` | `OrderDtos`, `CustomerDtos`, `CouponDtos` | Contratos de la API (request/response), separados de los `Models/` a propósito — un cambio en la tabla no debería romper el contrato público, y viceversa. Incluye `ApiError`, el shape de error uniforme compartido con Node. |
| `Program.cs` | — | Bootstrap: DI de todos los servicios, configuración de EF Core/Npgsql, `CatalogClient` como `HttpClient` tipado, CORS, Swagger, y el manejador de errores global (`UseExceptionHandler`) que garantiza que cualquier excepción no capturada explícitamente siga devolviendo el mismo shape de error. |

## Correr localmente

```bash
dotnet run
```
Usa `appsettings.Development.json` (conexión a `localhost:5432` y `CatalogService:BaseUrl` a `localhost:4000`) — necesita Postgres y Node corriendo antes. Swagger disponible en `/swagger`.

## Pruebas

```bash
cd ../dotnet-orders.Tests
dotnet test
```
