# Tareas 009: Encuentros e iniciativa de combate

- Estado: Completadas
- Especificación: [spec.md](spec.md)
- Plan: [plan.md](plan.md)
- Autorización: el usuario aceptó el spec y solicitó continuar con el plan el 2026-08-23

## T01 — Publicar la lectura de personajes para Combat

- [x] Añadir a Characters un contrato público mínimo para resolver personaje, nombre y CA dentro de una campaña.
- [x] Implementar el adaptador `AsNoTracking` sin exponer entidades, ownership, imagen o estado activo.
- [x] Cubrir personaje válido, inexistente y perteneciente a otra campaña.

## T02 — Crear el módulo backend Combat

- [x] Crear proyecto, fachada, capas internas y proyecto de pruebas.
- [x] Añadir resultados, puertos y métricas de cardinalidad acotada.
- [x] Incorporar Combat a la solución, host, Dockerfile y fitness functions con solo las dependencias aprobadas.

## T03 — Implementar el dominio de encuentros

- [x] Implementar `Encounter`, participantes de personaje/enemigo y validaciones.
- [x] Implementar iniciativa descendente, invalidación y resolución explícita de empates.
- [x] Implementar ciclo `Draft -> Active -> Finished`, ronda y turno actual.
- [x] Implementar daño, curación y límites de vida de enemigos.
- [x] Implementar versión del agregado y rechazo de operaciones fuera de estado.

## T04 — Persistir y proteger el estado de combate

- [x] Crear `CombatDbContext`, repositorio y factoría de diseño.
- [x] Generar la migración inicial del esquema `combat`, checks e índices.
- [x] Garantizar un solo encuentro activo y un personaje único por encuentro.
- [x] Verificar orden, instantáneas, control de versión y aislamiento sobre PostgreSQL real.

## T05 — Implementar casos de uso y proyecciones

- [x] Implementar listado y detalle exclusivos del DM.
- [x] Implementar creación, renombrado, alta de personajes/enemigos, iniciativa, retirada y desempate.
- [x] Implementar activación, avance, ajuste de vida y finalización.
- [x] Implementar la proyección activa segura para DM o jugador aceptado.
- [x] Mapear acceso, validación, inexistencia y conflictos sin efectos parciales.

## T06 — Exponer el contrato HTTP e integrar el host

- [x] Crear endpoints y DTO distintos para mesa de DM y vista activa segura.
- [x] Derivar actor y rol de la sesión y devolver `ProblemDetails` coherentes.
- [x] Registrar módulo, rutas y migraciones en el host.
- [x] Cubrir el recorrido HTTP, la ausencia de campos privados y la manipulación entre campañas.

## T07 — Crear el módulo frontend Combat

- [x] Añadir contratos separados y cliente HTTP con pruebas de URLs, métodos y cuerpos.
- [x] Añadir rutas lazy autenticadas y providers acotados.
- [x] Implementar listado, creación y estados de encuentro para el DM.
- [x] Implementar preparación con personajes, enemigos, iniciativas y desempates.
- [x] Integrar `Abrir encuentros` desde la portada de Campaigns.

## T08 — Implementar las mesas de DM y jugador

- [x] Implementar ronda, turno, avance y finalización en la mesa del DM.
- [x] Implementar vida actual/máxima y controles accesibles de daño y curación.
- [x] Implementar la proyección del jugador sin CA, vida ni controles.
- [x] Implementar sondeo cada 5 segundos sin solapamiento y cancelado al destruir.
- [x] Tratar estados vacío, carga, autorización, pérdida de acceso y conflicto con recarga autoritativa.

## T09 — Verificar calidad, privacidad y operación

- [x] Ejecutar pruebas de dominio, Application, PostgreSQL, HTTP y arquitectura .NET.
- [x] Ejecutar pruebas Angular, fitness functions y build de producción.
- [x] Construir imágenes API/web y validar Compose.
- [x] Verificar ausencia de secretos, datos privados de enemigos y contenido editorial concreto.
- [x] Actualizar migraciones, diagrama de componentes y observabilidad.

## T10 — Cerrar trazabilidad

- [x] Registrar comandos y resultados de verificación en este documento.
- [x] Actualizar spec, plan, índice y roadmap solo conforme a evidencia real.
- [x] Confirmar todos los criterios de aceptación o justificar cualquier descarte antes de marcar el incremento completado.

## Evidencias

- `dotnet build DndCampaign.slnx --configuration Release --no-restore`: compilación Release correcta, sin advertencias ni errores.
- `dotnet test DndCampaign.slnx --configuration Release --no-build`: 85 pruebas descubiertas; 72 superadas y 13 omitidas al no definir PostgreSQL local.
- `docker compose run --build --rm api-tests`: 85 de 85 pruebas superadas contra PostgreSQL 18 y Azurite, incluidas persistencia, contratos HTTP y arquitectura.
- `dotnet ef migrations has-pending-model-changes` sobre `CombatDbContext`: el modelo coincide con el snapshot de la migración inicial.
- `npm test -- --watch=false` desde `apps/web`: 27 archivos y 68 pruebas superadas.
- `npm run build` desde `apps/web`: build de producción correcto, incluido el chunk lazy `encounter-page`.
- `docker compose config --quiet` y `docker compose -f compose.deploy.yaml config --quiet`: configuraciones válidas.
- `docker compose build api web`: imágenes finales de API y web construidas correctamente con Combat incluido.
- La prueba HTTP confirma que la proyección activa del jugador omite `armorClass`, `currentHitPoints`, `maximumHitPoints` y `version`, y que un jugador recibe `403` al intentar avanzar.
- La suite de arquitectura confirma que Combat solo consume contratos públicos de Campaigns y Characters; la revisión documental y de código no incorpora contenido editorial concreto ni nuevos secretos.
