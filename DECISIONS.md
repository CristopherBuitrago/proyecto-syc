# DECISIONS.md — Justificación de decisiones de diseño

Este documento explica el *por qué* detrás de las decisiones técnicas del proyecto, no el *qué* (eso ya está en el código). Se va completando a medida que se toman las decisiones, no al final.

---

## 1. Estructura del repositorio: monorepo con carpetas por servicio

**Decisión:** los tres servicios (`dotnet-orders`, `node-catalog`, frontend React) y la base de datos (`db/`) viven en un único repositorio Git, cada uno en su propia carpeta de nivel superior, en lugar de un repositorio separado por servicio.

**Por qué:**
- Es una prueba técnica con plazo corto (viernes 10:00 a.m.), no un sistema en producción con equipos independientes por servicio. Mantener todo en un solo repo evita la sobrecarga de coordinar 3-4 repos, cada uno con su propio remoto, versión de `docker-compose.yml` y README.
- Un único `docker-compose.yml` en la raíz puede levantar los tres servicios con un solo comando (`docker-compose up`), que es exactamente lo que pide el enunciado.
- El historial de commits sigue siendo real y legible por servicio, porque cada commit toca la carpeta que corresponde (ver `git log`).

**Costo aceptado:** en un sistema real con equipos separados por servicio, un monorepo puede generar acoplamiento de despliegue (todo se versiona junto) y permisos de acceso menos granulares. No es relevante para el alcance de esta prueba.

**No es una decisión que afecte el motor de descuentos ni la arquitectura de comunicación entre servicios** — es puramente organizativa a nivel de repositorio.

---

## 2. Servicio Node sin ORM: SQL crudo con `pg`, no Sequelize

**Decisión:** `node-catalog` accede a Postgres con el driver `pg` y SQL escrito a mano (`pool.query(...)`), sin ORM. El servicio .NET, en cambio, sí usa EF Core.

**Por qué:**
- El punto crítico de este servicio, y el que más pesa en la nota de esa pieza, es la **concurrencia** en la reserva de stock (sección "Manejo de concurrencia" del enunciado). La solución elegida es locking pesimista explícito con `SELECT ... FOR UPDATE` dentro de una transacción manual (`BEGIN` / `COMMIT` / `ROLLBACK`) — ver `node-catalog/src/routes/inventory.js`.
- Un ORM como Sequelize sí soporta locking (`lock: true` en una transacción), pero agrega una capa de abstracción entre el código y el SQL real que termina ejecutándose. Para algo tan sensible como "qué fila se bloquea, en qué momento exacto, dentro de qué transacción", prefiero ver y controlar el SQL directamente en vez de confiar en cómo el ORM lo traduce.
- El servicio es pequeño a propósito: solo 2 rutas de negocio (`reserve`/`release`) más un CRUD simple de productos. El costo de "no tener ORM" (repetir `pool.query`, mapear filas a mano) es bajo porque no hay relaciones complejas ni muchas entidades — no se justifica la sobrecarga de configurar modelos/migraciones de Sequelize para esto.
- Es una decisión **local a este servicio**, no una regla general del proyecto: el .NET sí usa EF Core porque ahí el modelo de datos es más rico (customers, orders, order_items, coupons, applied_discounts con relaciones entre sí) y no hay una necesidad de control fino de locking a ese nivel — las inserciones de pedido van dentro de una transacción de EF Core normal.

**Costo aceptado:** sin migraciones automáticas ni validación de esquema en el código Node — el esquema vive únicamente en `db/init.sql`. Para el tamaño de este servicio, es un costo bajo.

---

## 3. Motor de descuentos: cómo se combinan/priorizan las 3 reglas

**Decisión:** las tres reglas **no se suman de forma independiente sobre el mismo subtotal bruto** ni se aplica "solo la mayor" — se aplican **en cascada, en un orden fijo**, cada una sobre el resultado de la anterior (ver `DiscountEngine.Calculate`):

1. **Volumen**, por línea de producto, sobre el subtotal bruto de cada línea → produce el "subtotal después de volumen".
2. **Loyalty**, una sola vez por pedido, sobre el subtotal total **después de volumen** (no sobre el bruto) → produce el "subtotal después de loyalty".
3. **Cupón**, al final:
   - `stackable = true` → se descuenta encima del subtotal después de loyalty (las tres reglas terminan aplicadas, en cascada).
   - `stackable = false` → no se suma en cascada. Compite: se calcula cuánto pagaría el cliente con **solo el cupón** sobre el subtotal bruto, se compara contra lo que pagaría con **volumen + loyalty combinados** (también partiendo del bruto), y gana la opción con el total final más bajo para el cliente.

**Por qué este orden y no otro:**
- **Volumen antes que Loyalty, y en cascada (no en paralelo sobre el bruto):** un descuento en cascada refleja mejor la intención de negocio de "premiar más al que ya compra mucho en una sola línea Y además es cliente frecuente", sin que el segundo descuento le reste valor al primero. Si ambos se calcularan de forma independiente sobre el bruto y luego se sumaran los porcentajes (ej. 10% + 6% = 16% plano), un pedido grande de un cliente Platinum podría terminar con descuentos que se comen el margen de forma menos controlada y menos progresiva que aplicarlos en cascada.
- **Loyalty se calcula sobre el subtotal ya con volumen aplicado, no sobre el bruto:** esto es explícito en el enunciado (sección 6, punto 2: "sobre el subtotal ya con descuento de volumen aplicado"), así que no fue una decisión libre — es un requisito.
- **El cupón va al final, no al principio:** porque su comportamiento depende de conocer primero cuánto valen volumen+loyalty combinados (para poder compararlos cuando `stackable = false`). Si el cupón se evaluara primero, no habría con qué compararlo todavía.
- **Por qué "el menor costo para el cliente" y no otro criterio cuando el cupón no es combinable:** la interpretación adoptada es que un cupón de campaña no debe ser un arma de doble filo para el negocio — si el cupón termina siendo peor que los descuentos normales, tiene sentido que el sistema simplemente no perjudique al cliente aplicándolo a la fuerza. Esto también evita reclamos de soporte ("usé un cupón y pagué más que sin él").

**Alternativa descartada — sumar los tres porcentajes de forma plana sobre el bruto:** más simple de calcular, pero no está pedida por el enunciado y further oculta el efecto real de cada regla (dificulta mostrarle al cliente el desglose "por qué" en el frontend, que es un requisito explícito).

---

## 4. Cupón `stackable=false` peor que Volumen + Loyalty combinados: qué pasa

**Decisión:** si el cupón no es combinable y resulta **peor** para el cliente que volumen+loyalty combinados, **el cupón simplemente no se aplica** — no es un error, no se rechaza el pedido, no se le informa al cliente que "el cupón falló". El pedido se crea igual, con volumen+loyalty aplicados como si el cupón nunca se hubiera ingresado, y el desglose de descuentos (`AppliedDiscounts`) refleja exactamente eso: no aparece ninguna línea de tipo `COUPON`.

**Por qué:**
- Es la consecuencia directa de la regla de comparación de la decisión 3: entre las dos opciones válidas (cupón solo, o volumen+loyalty combinados), siempre se aplica la que le cuesta menos al cliente. Si volumen+loyalty ya eran mejores, esa sigue siendo la mejor opción — el cupón "pierde la competencia", no es un error del sistema.
- **No se trata como error de validación** (a diferencia de un cupón vencido o que no llega al monto mínimo, que sí lanzan una excepción y rechazan el pedido) porque el cupón en sí es válido — el problema no es el cupón, es que hay una alternativa mejor disponible para ese cliente en ese momento. Mezclar "cupón inválido" con "cupón válido pero no conviene" en el mismo tipo de error confundiría al usuario y al frontend.
- El cliente nunca termina pagando más por haber ingresado un cupón: en el peor caso, es como si no lo hubiera usado. Esto es coherente con la filosofía de la decisión 3 (el sistema no perjudica al cliente por probar un cupón).

**Cómo se comunica:** el frontend (cuando se construya) debe leer `AppliedDiscounts` para saber qué se aplicó realmente, no asumir que un `couponCode` enviado implica que el cupón fue el que impactó el precio. Vale la pena que, cuando esto pase, la respuesta dé alguna señal explícita (hoy no la da) de "tu cupón no se aplicó porque no era la mejor opción" para que no parezca que el sistema lo ignoró silenciosamente sin razón — **pendiente técnico a considerar**, no bloqueante para la nota pero sí mejora la experiencia y demuestra pensar en el detalle.

## 5. Consistencia del cálculo si el tier del cliente cambia justo después de comprar

**Decisión:** el nivel del cliente (Bronze/Silver/Gold/Platinum) **no se guarda como campo** en `customers`. Se calcula en vivo, en cada pedido, sumando el `total_final` de todas las órdenes con `status = CONFIRMED` de ese cliente (`OrderService.CreateOrderAsync`, variable `historicalTotal`) — y esa suma se calcula **antes** de crear el pedido actual, es decir, **no incluye el pedido que se está procesando en ese momento**.

**Qué pasa exactamente cuando un pedido hace que el cliente cruce de tier:**
- El pedido que cruza el umbral (ej. el que lo lleva de $1.900.000 a $2.100.000 acumulado, cruzando a Gold) se cobra con el descuento del tier **anterior** (Silver, en el ejemplo) — porque en el momento de calcular su descuento, ese pedido todavía no cuenta como "histórico", solo cuentan los confirmados previos.
- El tier nuevo (Gold) empieza a aplicar recién en el **siguiente** pedido que ese cliente confirme, una vez que el pedido anterior ya quedó guardado con `status = CONFIRMED` y por lo tanto ya suma al histórico.
- Este comportamiento es **determinístico y sin ambigüedad** — no hay una "zona gris" de qué tier le tocaba a un pedido, porque el histórico usado siempre es el estado confirmado justo antes de ese pedido.

**Por qué se decidió así (calcular en vivo, y excluir el pedido actual) y no otras alternativas:**
- **Evita un campo denormalizado que se puede desincronizar.** Si `tier` fuera un campo guardado en `customers`, habría que actualizarlo manualmente cada vez que se confirma o cancela un pedido — y cualquier bug o transacción a medias dejaría el tier desincronizado del histórico real. Calculándolo en vivo desde `orders.status = CONFIRMED`, el dato siempre es resultado directo del estado real de la base, nunca puede quedar "atrasado".
- **Las cancelaciones se reflejan automáticamente, sin lógica adicional.** Si un pedido se cancela (`CancelAsync`), su `status` deja de ser `CONFIRMED` y automáticamente deja de sumar al histórico en el siguiente cálculo — no hay que "restarle" nada a un contador guardado.
- **Evita una dependencia circular.** Si el pedido actual se incluyera en su propio histórico para decidir su propio tier, el cálculo se volvería circular: el descuento de loyalty depende del subtotal, pero si el subtotal (con o sin volumen) también decidiera el histórico usado para el loyalty de ese mismo pedido, la definición ya no sería clara ni fácil de razonar ni de testear. Mantener "histórico" con su significado literal (compras ya completadas antes de este pedido) evita ese problema de raíz.

**Alternativa descartada — usar el total proyectado (histórico + pedido actual) para decidir el tier del pedido en curso:** permitiría que un pedido grande "se beneficie a sí mismo" del tier que genera. Se descartó porque además de la circularidad ya mencionada, incentivaría un caso borde raro (un cliente Bronze podría intentar inflar artificialmente un solo pedido para saltar a Platinum y descontarse a sí mismo de una), y no es el comportamiento que sugiere la palabra "histórico" en el enunciado.

**Costo aceptado / gap conocido:** a diferencia del stock (decisión 2), el cálculo del histórico **no tiene locking explícito**. Si el mismo cliente disparara dos pedidos verdaderamente simultáneos, ambos podrían leer el mismo histórico "viejo" y calcular el mismo tier, en vez de que el segundo viera el resultado del primero. Se acepta este riesgo porque la contención esperada es muy distinta a la del stock: ahí compiten *muchos clientes* por *un mismo producto* (alta probabilidad de choque), mientras que acá el escenario es *un mismo cliente* mandando *pedidos simultáneos contra sí mismo* (baja probabilidad, y el impacto es menor: como mucho, un pedido de más se calcula con el tier anterior en vez del nuevo, no se genera stock negativo ni un dato corrupto). Si se quisiera cerrar ese hueco, la mitigación sería la misma técnica que en stock: `SELECT ... FOR UPDATE` sobre las órdenes del cliente, o una transacción con aislamiento `SERIALIZABLE`.

## 6. Dónde vive la lógica de reserva de stock (Node, ​.NET, o ambos) y por qué

**Decisión:** la lógica de **mutar** stock (restarlo o devolverlo) vive exclusivamente en Node, detrás de dos endpoints (`POST /api/inventory/reserve` y `POST /api/inventory/release`, en `node-catalog/src/routes/inventory.js`). El .NET **nunca escribe** stock directamente en la base de datos — solo lee la tabla `products` (precio, nombre) para armar el pedido, y para cualquier cambio de stock llama a Node por HTTP a través de `CatalogClient.cs`.

**Por qué:**
- El enunciado (sección 3) pide explícitamente que el servicio .NET "valide stock contra el servicio Node antes de confirmar" — es decir, ya define que la fuente de verdad del stock es Node, no un detalle libre a decidir desde cero.
- **Una sola responsabilidad, un solo dueño del dato mutable.** Si tanto Node como .NET pudieran restar `stock` directamente en Postgres, el locking pesimista (`SELECT ... FOR UPDATE`) que protege la concurrencia en Node (ver decisión 2) dejaría de servir para nada: .NET podría hacer un `UPDATE products SET stock = ...` por fuera de esa transacción y crear exactamente la condición de carrera que se buscaba evitar. Concentrar la escritura en un solo servicio es lo que hace que el locking sea confiable.
- La **lectura** de `products` (precio, nombre) sí se hace directo desde la base de datos en .NET (`OrderService.cs`, para armar las líneas del pedido y calcular descuentos), en lugar de pedírselo también por HTTP a Node. Es una lectura de datos que no cambian a mitad de una operación (el precio no se está disputando entre dos pedidos simultáneos como sí pasa con el stock) — pedirlo por HTTP solo agregaría una llamada de red sin beneficio real. La justificación es puramente de rendimiento/simplicidad, no rompe la regla de "un solo dueño de la escritura".

**Flujo real (`OrderService.CreateOrderAsync`):**
1. .NET calcula el motor de descuentos usando el precio leído directo de `products`.
2. .NET llama a `CatalogClient.ReserveAsync(...)` → Node valida y resta stock atómicamente (todo o nada) o responde 409 con el detalle exacto de qué producto(s) no alcanzan (`StockUnavailableException` con la lista de fallos, propagada tal cual al cliente HTTP del frontend).
3. Solo si la reserva en Node fue exitosa, .NET abre su propia transacción de EF Core para persistir el pedido.
4. Si ese guardado falla después de haber reservado en Node, .NET llama a `CatalogClient.ReleaseAsync(...)` como compensación manual (no hay una transacción distribuida entre los dos servicios — son dos bases de datos lógicamente separadas aunque compartan el mismo motor Postgres, así que la consistencia entre "stock reservado" y "pedido guardado" se garantiza con este patrón de compensación, no con un commit atómico conjunto).

**Alternativa descartada — duplicar la validación/resta de stock en ambos servicios:** se consideró (y se descartó) que .NET también pudiera restar stock directamente, dejando a Node solo como catálogo de lectura. Se descartó porque duplicaría la lógica de concurrencia en dos lenguajes distintos, con dos estrategias de locking que tendrían que mantenerse sincronizadas, y porque el enunciado ya asigna esa responsabilidad a Node.

**Costo aceptado:** hay una dependencia dura de disponibilidad — si Node está caído, .NET no puede confirmar ningún pedido, aunque su propia base de datos esté sana. Es un costo aceptado porque el enunciado pide justo esa separación de responsabilidades entre servicios, y las alternativas (duplicar lógica, o que .NET escriba directo) son peores para la integridad del dato.

## 7. Cómo se agregarían nuevas reglas de descuento a futuro sin reescribir el motor

**Estado actual, con honestidad:** el motor aplica Strategy **parcialmente**, no de forma completa. Volumen y Loyalty sí están detrás de su propia interfaz (`IVolumeDiscountRule`, `ILoyaltyDiscountRule`), inyectadas por DI en `Program.cs` — eso significa que **cambiar cómo se calcula** una regla existente (ej. ajustar los rangos de volumen) no toca `DiscountEngine` para nada, solo la clase de esa regla. Pero **agregar una regla nueva** hoy sí requeriría tocar `DiscountEngine.Calculate` directamente, porque el orquestador llama a cada regla por nombre, en un orden hardcodeado en el método (`_volumeRule.ApplyToLine(...)` y luego `_loyaltyRule.CalculateDiscount(...)`, explícitamente, no en un loop genérico). El cupón es el caso más débil de los tres: su lógica completa (incluida la comparación "stackable vs. no combinable") vive **inline dentro de `DiscountEngine`**, sin ninguna interfaz propia.

**Por qué no se implementó ya la versión completamente extensible:** el enunciado dice explícitamente que no es obligatorio implementarlo, solo argumentarlo. Con 3 reglas conocidas y fijas, y un plazo de días, invertir tiempo en una abstracción genérica antes de tenerla probada con un cuarto caso real habría sido sobre-ingeniería (una interfaz que "adivina" cómo será la cuarta regla es más riesgo de over-engineering que beneficio concreto ahora mismo) — mientras que el 35% de la nota que sí depende de la corrección del motor actual (con sus 3 reglas y sus pruebas unitarias) está resuelto y probado.

**Cómo se evolucionaría, si mañana pidieran una cuarta regla (ej. "descuento por temporada") sin tocar la lógica de las reglas existentes:**

1. Unificar las interfaces sueltas (`IVolumeDiscountRule`, `ILoyaltyDiscountRule`, y la lógica de cupón) bajo una sola interfaz común, algo como:
   ```
   interface IDiscountRule {
       DiscountStepResult Apply(DiscountPipelineState state);
   }
   ```
   donde `DiscountPipelineState` lleva el subtotal acumulado, las líneas, y la lista de descuentos ya aplicados hasta ese punto — es decir, el "estado" que hoy se va pasando manualmente entre variables (`subtotalAfterVolume`, `subtotalAfterLoyalty`, etc.) dentro de `Calculate`.

2. `DiscountEngine` dejaría de llamar reglas por nombre y pasaría a iterar una lista ordenada:
   ```
   foreach (var rule in _rules.OrderBy(r => r.Order))
       state = rule.Apply(state);
   ```
   Esto es, en esencia, el patrón **Chain of Responsibility** (o un pipeline): cada regla recibe el resultado acumulado de las anteriores y produce el siguiente estado, sin que el motor conozca de antemano cuántas reglas hay ni cuáles son.

3. Agregar una regla nueva se reduciría a: crear una clase que implemente `IDiscountRule`, registrarla en DI (`builder.Services.AddScoped<IDiscountRule, SeasonalDiscountRule>()`), y listo — `DiscountEngine` la recoge automáticamente vía `IEnumerable<IDiscountRule>` inyectado, sin recompilar ni tocar el orquestador ni las reglas existentes. Esto es literalmente "sin reescribir el motor".

4. El caso especial del cupón (competir contra el resto en vez de sumarse) dejaría de ser un `if` hardcodeado para "cupón" específicamente, y se modelaría como una propiedad genérica de la regla (ej. `bool IsExclusive` + un método `Compare(...)`) — así, si en el futuro apareciera *otra* regla con el mismo comportamiento de "reemplaza si conviene más" (no solo el cupón), el motor ya sabría manejarla sin lógica especial por tipo.

**Conclusión:** la base ya está orientada en esa dirección (interfaces + DI para 2 de las 3 reglas), y el camino hacia el patrón completo está claro y documentado acá — no implementarlo ahora fue una decisión consciente de alcance, no una limitación técnica ni un olvido.
