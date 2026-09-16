# Contexto — Prueba técnica SYC (Sistemas y Computadores S.A.)

## Quién soy y de dónde vengo

Soy Cristopher Buitrago, actualmente Desarrollador Full Stack en Campuslands (proyecto de gestión de contratos legales para el cliente Arenas Ochoa — Node.js/Express/TypeScript, React, Python/FastAPI/LangChain, MySQL). Estoy en proceso de renuncia/transición hacia **Sistemas y Computadores S.A. (SYC)**, empresa de Floridablanca (~846 empleados, 47+ años, outsourcing de TI para sector público y privado, línea fuerte en gestión documental). El cargo ofrecido es **"Desarrollador Novel"**.

Mi stack fuerte: **Node.js/Express/TypeScript, React, Java (Spring Boot), PHP (Laravel), MySQL/PostgreSQL, Sequelize**. **No tengo experiencia previa en .NET/C#** — es territorio nuevo.

## La prueba técnica (archivo original: `pruebasyc.pdf` en esta carpeta)

**Importante, primera señal de alarma:** el documento dice explícitamente **"Nivel: Semi Senior"**, no "Novel" como me dijeron que era el cargo. Pendiente confirmar con SYC si es un error de plantilla o si de verdad esperan ese nivel — no se ha resuelto todavía.

**Plazo:** el enunciado dice "48–72 horas desde la entrega", pero yo confirmé con SYC que el plazo real es **viernes a las 10:00 a.m.**

### Qué pide (resumen — el PDF completo tiene el detalle exacto, léelo si hace falta precisión)

Sistema de Gestión de Pedidos y Descuentos, 3 servicios independientes que se comunican entre sí + PostgreSQL, todo levantable con **Docker Compose** en un solo comando:

1. **Servicio .NET (obligatorio, no es opcional)** — .NET 8 / ASP.NET Core Web API (EF Core o Dapper). Es el "Orders & Discount Engine": crear/consultar/listar pedidos, aplicar el motor de descuentos, validar stock contra el servicio Node antes de confirmar, persistir todo en Postgres, manejo de errores de negocio con códigos HTTP claros.
2. **Servicio Node.js** — Catalog & Inventory. CRUD de productos, reservar/liberar stock, y **manejo de concurrencia** (que dos reservas simultáneas del mismo producto no dejen stock negativo — decidir y justificar: locking optimista, transacciones, etc.).
3. **Frontend React** — catálogo, creación de pedido (con cupón y desglose de descuentos visible), historial de pedidos, manejo visible de errores. **Ellos mismos dicen que NO evalúan diseño visual elaborado.**

### El núcleo real de la evaluación — Motor de Descuentos

Tres reglas que coexisten y pueden entrar en conflicto, en un solo cálculo:
1. **Volumen** (por línea de producto): 10-19 unidades → 5%; 20+ → 10%.
2. **Nivel de cliente** (histórico de compras acumuladas), sobre el subtotal ya con descuento de volumen aplicado: Bronze `<$500k` → 0%; Silver `$500k-$1.999.999` → 3%; Gold `$2M-$4.999.999` → 6%; Platinum `≥$5M` → 10%.
3. **Cupones de campaña**: monto mínimo, %, o monto fijo, fecha de expiración, flag `stackable`. Si `stackable=false`, el cupón reemplaza los otros descuentos **solo si es más beneficioso** para el cliente — la comparación y su justificación quedan a mi criterio, documentada por escrito.
4. Validación de stock antes de confirmar — si falla, informar exactamente qué línea(s).

**Debo responder por escrito en `DECISIONS.md`** (no opcional, es parte de la nota):
- Cómo se combinan/priorizan los 3 descuentos (¿se suman? ¿se aplica el mayor? ¿hay orden?)
- Qué pasa si el cupón `stackable=false` da un resultado peor que Volumen+Loyalty combinados
- Cómo se mantiene consistente el cálculo si el tier del cliente cambia justo después de comprar
- Dónde vive la lógica de reserva de stock (Node, .NET, o ambos) y por qué
- Cómo se agregarían nuevas reglas de descuento a futuro sin reescribir el motor (se valora si aplico Strategy/Chain of Responsibility — no es obligatorio implementarlo, sí argumentarlo)

### Modelo de datos sugerido (Postgres)
`customers`, `products`, `orders`, `order_items`, `discount_rules`/`coupons`, `applied_discounts` — libre de ajustar, pero justificando cambios relevantes.

### Entregables
Repo(s) Git con historial de commits real (no "final version" en un solo commit), `README.md` con instrucciones de `docker-compose up`, `DECISIONS.md`, colección Postman o Swagger/OpenAPI de ambas APIs, script(s) de migración de BD, **pruebas unitarias del motor de descuentos** (mínimo 3 escenarios de combinación de reglas).

### Rúbrica (peso real de la nota)
| Criterio | Peso |
|---|---|
| Correctitud y robustez del motor de descuentos (lógica, casos borde, concurrencia) | 35% |
| Calidad de las decisiones de diseño y su justificación escrita (`DECISIONS.md`) | 20% |
| Arquitectura y comunicación entre servicios | 15% |
| Modelado de base de datos | 10% |
| Calidad de código (legibilidad, separación de responsabilidades, tests) | 20% |

**55% de la nota es motor de descuentos + DECISIONS.md razonado.** El frontend explícitamente NO se evalúa por estética. Extras opcionales (mensajería asíncrona, JWT, cache, CI/CD, rate limiting) suman pero no son prioridad si el tiempo aprieta.

## Estrategia acordada hasta ahora

1. **Riesgo principal identificado: el servicio .NET.** Nunca he tocado C#/.NET — es lo que más tiempo puede comer si lo hago solo desde cero.
2. **Plan:** Claude arma el esqueleto funcional del servicio .NET (estructura de proyecto, endpoints, conexión a Postgres, EF Core básico) para no perder horas en boilerplate desconocido. Cristopher se enfoca en:
   - La lógica del motor de descuentos (backend .NET, es donde vive según el enunciado)
   - Escribir `DECISIONS.md` con criterio propio — esto NO se delega, es lo que más pesa y lo que revisan en la entrevista posterior ("¿por qué elegiste esto y no la alternativa X?")
   - El servicio Node.js (Catalog & Inventory) — aquí sí hay experiencia directa, debería avanzar más rápido solo.
3. **Nota del propio enunciado (sección 13):** en la entrevista de revisión posterior, preguntan el "por qué" de 2-3 decisiones del `DECISIONS.md` — ahí se nota quién razonó vs. quién copió. Cristopher tiene que poder defender cada decisión en voz propia, no solo tener el código.

## Pendiente / próximos pasos
- [ ] Confirmar con SYC si "Nivel: Semi Senior" en el documento es un error o el nivel real esperado.
- [ ] Empezar por el esqueleto del servicio .NET (mayor riesgo, mayor curva de aprendizaje).
- [ ] Diseñar el motor de descuentos (probablemente patrón Strategy/Chain of Responsibility, dado lo que pide la sección 6).
- [ ] Servicio Node.js de catálogo/inventario con manejo de concurrencia.
- [ ] Frontend React — última prioridad, funcional pero sin pulir.
- [ ] `DECISIONS.md` — ir escribiéndolo a medida que se toman decisiones, no dejarlo para el final.
