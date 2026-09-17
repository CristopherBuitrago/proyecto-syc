# DECISIONS.md

Acá explico por qué tomé cada decisión importante del proyecto. No es el "qué" (eso ya está en el código), es el "por qué".

---

## 1. Todo en un solo repositorio

Metí los tres servicios y la base de datos en un solo repo, cada uno en su carpeta, en vez de tres repos separados. Es una prueba técnica con plazo corto, no un producto con equipos separados por servicio, no tenía sentido complicarme coordinando tres remotos distintos. Además así un solo `docker-compose.yml` en la raíz levanta todo con un comando, que es literalmente lo que pide el enunciado.

## 2. Node sin ORM, SQL directo con `pg`

En `node-catalog` no usé Sequelize ni ningún ORM, hago las queries directo con `pg`. La razón es la concurrencia: el punto que más pesa de este servicio es que dos reservas de stock al mismo tiempo no dejen el número en negativo, y para eso uso `SELECT ... FOR UPDATE` dentro de una transacción manual. Prefería ver y controlar exactamente qué SQL se ejecuta y en qué momento del lock, en vez de confiar en cómo un ORM lo traduciría por debajo. El servicio además es chiquito (dos rutas de negocio y un CRUD simple), así que tampoco perdía mucho no teniendo ORM.

En el `.NET` sí usé EF Core, porque ahí el modelo tiene más tablas relacionadas entre sí y no necesito ese control tan fino de locking, es una decisión que tomé para ese servicio puntual, no una regla general de "nunca usar ORM".

## 3. Cómo se combinan los tres descuentos

Los aplico en cascada, uno encima del resultado del anterior, no cada uno por separado sumando porcentajes:

1. Primero **volumen**, por línea de producto, sobre el precio de esa línea.
2. Después **loyalty**, una sola vez por todo el pedido, sobre el subtotal que ya quedó con el descuento de volumen aplicado
3. Al final el **cupón**: si es `stackable` (combinable), se resta encima de lo que ya quedó con volumen + loyalty. Si no es `stackable`, no se suma, compite: calculo cuánto pagaría el cliente solo con el cupón (sobre el precio sin ningún descuento) contra cuánto pagaría con volumen + loyalty juntos, y se queda el que le sale más barato al cliente.

El cupón va de último porque para poder compararlo necesito saber primero cuánto valen los otros dos combinados, si lo evaluara primero no tendría con qué compararlo.

## 4. Si el cupón no combinable pierde la comparación

Simplemente no se aplica. No es un error, no rechazo el pedido, el cliente no se entera de que "falló" nada, el pedido sigue con volumen + loyalty como si nunca hubiera puesto el cupón. La idea es que el cliente nunca termine pagando más por haber probado un cupón; si el cupón no le convenía, pues no se usa y ya.

Lo único que dejé como pendiente honesto: hoy el sistema no le avisa "tu cupón no se aplicó porque no era la mejor opción", simplemente no aparece en el desglose. Sería una mejora de UX, no algo que bloquee la entrega.

## 5. Consistencia si el cliente cambia de tier justo después de comprar

No guardo un campo `tier` en la tabla de clientes. Lo calculo en vivo, sumando el total de todos sus pedidos confirmados, y ese cálculo se hace **antes** de contar el pedido que se está creando en ese momento, o sea que el pedido nunca se cuenta a sí mismo para decidir su propio descuento.

Eso significa que si un pedido es el que hace que el cliente suba de nivel, ese pedido paga con el nivel viejo, y el nivel nuevo aplica desde el siguiente pedido para adelante. No hay ambigüedad de cuál nivel le tocaba.

Preferí esto en vez de guardar un campo `tier` fijo porque un campo guardado se puede desincronizar (si cancelas un pedido y se me olvida actualizarlo, por ejemplo). Calculándolo en vivo desde los pedidos confirmados, el dato siempre refleja la realidad sin que yo tenga que acordarme de actualizar nada aparte.

Un hueco que dejo claro: esto no tiene lock como sí tiene el stock. Si el mismo cliente lograra mandar dos pedidos exactamente al mismo tiempo, ambos podrían leer el mismo histórico viejo. Lo acepté porque es un caso muy distinto al del stock, ahí compiten muchos clientes por un mismo producto todo el tiempo, acá tendría que ser el mismo cliente compitiendo consigo mismo, mucho menos probable y el daño es menor (como mucho un pedido de más se calcula con el tier anterior).

## 6. Dónde vive la reserva de stock

Vive solo en Node. El `.NET` nunca resta stock directo en la base de datos, solo lee `products` para el precio y el nombre; para reservar o liberar siempre le pega por HTTP a Node.

La razón principal es que si dos servicios pudieran restar stock cada uno por su lado, el lock que puse en Node (`FOR UPDATE`) no serviría de nada .NET podría hacer un update por fuera de esa transacción y meter la misma condición de carrera que estoy tratando de evitar. Un solo dueño de la escritura es lo que hace que el lock realmente funcione. Y el enunciado ya lo pide así de todos modos (que .NET valide contra Node antes de confirmar).

Como no hay una transacción que una a los dos servicios, si .NET logra reservar en Node pero después falla guardando el pedido en su propia base, tiene que llamar a `/release` para devolver lo que había reservado. Eso lo manejo con un try/catch alrededor de la transacción de EF Core.

## 7. Cómo agregaría una regla de descuento nueva sin reescribir todo

Siendo honesto: hoy el motor no está listo del todo para eso. Volumen y Loyalty sí están cada una en su propia clase detrás de una interfaz, inyectadas por DI, así que cambiar cómo calcula una de esas dos no toca el resto. Pero el cupón está metido directo adentro de `DiscountEngine`, sin su propia clase, y agregar una regla completamente nueva hoy sí tocaría el método `Calculate`.

No lo dejé 100% genérico a propósito: con solo 3 reglas conocidas y el tiempo que tenía, armar una abstracción tipo Strategy completa (una interfaz común para las tres, un loop que las recorra sin saber cuántas hay) me parecía más riesgo de sobre-ingeniería que beneficio real ahora mismo. Si tuviera que hacerlo, el camino sería: una interfaz única `IDiscountRule` con un método `Apply` que reciba el estado acumulado (subtotal, descuentos ya aplicados) y devuelva el siguiente estado, y el motor simplemente recorre una lista de reglas inyectadas por DI sin conocerlas de antemano, ahí sí, agregar una regla nueva sería solo crear la clase y registrarla, sin tocar nada más. Ya tengo la mitad del camino hecho (Volumen y Loyalty ya funcionan así), me faltaría mover el cupón al mismo esquema.

## 8. (Extra) El frontend simula sesión de cliente, no deja elegir cliente por pedido

En vez de que cada pedido pregunte "¿de qué cliente es?", como si fuera un panel de administrador armando pedidos para otros, el usuario elige una vez quién es (con un selector arriba) y desde ahí toda la app actúa como ese cliente: crear pedido no vuelve a preguntar, e historial filtra automático por ese cliente.

No es login de verdad, no hay contraseñas ni tokens, es una simulación con lo que ya tenía (`GET /api/customers`). No implementé autenticación real porque el enunciado la lista como extra opcional, y con el tiempo que tenía no valía la pena meterle horas a JWT cuando el 55% de la nota está en el motor de descuentos y en este documento.
