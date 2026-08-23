# Tareas 007: Bitácora compartida de campaña

- Estado: Completadas
- Especificación: [spec.md](spec.md)
- Plan: [plan.md](plan.md)
- Autorización: el usuario aprobó el plan y solicitó expresamente crear las tareas e implementarlas el 2026-08-23

## T01 — Publicar la consulta mínima de personaje activo

- [x] Añadir el contrato público de Characters para resolver el personaje activo por campaña y usuario.
- [x] Implementar la consulta sin tracking y registrarla en la fachada del módulo.
- [x] Cubrir contrato, proyección minimizada y límites públicos con pruebas.

## T02 — Crear el módulo backend Journal

- [x] Crear proyecto, fachada, capas internas y proyecto de pruebas.
- [x] Añadir el agregado `JournalEntry` con contenido, autoría original y fechas.
- [x] Implementar resultados, puertos y métricas sin datos de alta cardinalidad.

## T03 — Persistir y paginar entradas

- [x] Crear `JournalDbContext`, repositorio, codec de cursor y factoría de diseño.
- [x] Generar la migración inicial del esquema `journal` y sus índices.
- [x] Verificar keyset pagination, orden descendente y aislamiento por campaña sobre PostgreSQL.

## T04 — Implementar casos de uso y contrato HTTP

- [x] Implementar listado para miembros autorizados.
- [x] Implementar creación exclusiva de jugador con personaje activo.
- [x] Implementar edición de cualquier entrada por cualquier jugador aceptado.
- [x] Implementar eliminación exclusiva por el jugador que introdujo la entrada.
- [x] Exponer endpoints y mapear `ProblemDetails`, permisos y cursores.

## T05 — Integrar host y gobierno arquitectónico

- [x] Registrar Journal, rutas y migraciones en el host.
- [x] Añadir proyectos a solución, Dockerfile y grafo de dependencias aprobado.
- [x] Actualizar fitness functions backend para Journal y los contratos de Characters.

## T06 — Crear el módulo frontend Journal

- [x] Añadir contratos y cliente HTTP con pruebas.
- [x] Añadir ruta lazy autenticada y providers acotados.
- [x] Implementar listado, carga de páginas, estados vacío/carga/error y autoría visible.
- [x] Implementar alta, edición colaborativa y eliminación confirmada según permisos.
- [x] Integrar el enlace desde la portada de Campaigns.

## T07 — Verificar frontend y accesibilidad

- [x] Cubrir rol DM, jugador con/sin personaje activo, permisos y mutaciones.
- [x] Cubrir texto plano, saltos de línea, validación y tratamiento de errores.
- [x] Mantener límites modulares, routing y build de producción verdes.

## T08 — Verificación integrada y operativa

- [x] Ejecutar suites .NET, integración PostgreSQL y pruebas arquitectónicas.
- [x] Ejecutar suite Angular y build de producción.
- [x] Construir imágenes y validar Compose cuando el entorno lo permita.
- [x] Comprobar que no se incorporan secretos ni contenido editorial concreto.

## T09 — Cerrar trazabilidad

- [x] Actualizar orden de migraciones, diagramas y observabilidad afectados.
- [x] Registrar evidencias de pruebas en este documento.
- [x] Marcar spec, índice y roadmap conforme a la evidencia final.

## Evidencias

- `docker compose run --build --rm api-tests`: 65 pruebas correctas sobre PostgreSQL y Azurite; 0 fallos y 0 omisiones.
- `pnpm --filter @dnd/web test --watch=false`: 57 pruebas correctas en 23 archivos.
- `pnpm --filter @dnd/web build`: build de producción correcto y Journal cargado mediante chunk lazy.
- `dotnet build DndCampaign.slnx --no-restore`: compilación completa sin advertencias ni errores.
- `docker compose build api web`: imágenes finales construidas correctamente.
- `docker compose config --quiet` y `docker compose -f compose.deploy.yaml config --quiet`: configuraciones válidas.
