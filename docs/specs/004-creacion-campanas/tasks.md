# Tareas 004: Creación de campañas e invitación de usuarios existentes

- Estado general: Completado
- Plan: [plan.md](plan.md)
- ADR: [ADR-0006](../../adr/0006-campanas-acceso-e-invitaciones.md)

## Convención de estado

- `[ ]` Pendiente.
- `[-]` En curso.
- `[x]` Completada.
- `[~]` Descartada, con justificación escrita bajo la tarea.

## Resumen

| ID | Tarea | Depende de | Estado |
|---|---|---|---|
| CAM-001 | Caracterizar y fijar contratos de partida | — | Completada |
| CAM-002 | Publicar contratos intermodulares de Access | CAM-001 | Completada |
| CAM-003 | Crear el proyecto y los límites del módulo Campaigns | CAM-002 | Completada |
| CAM-004 | Implementar dominio y persistencia de Campaigns | CAM-003 | Completada |
| CAM-005 | Implementar creación, listado y detalle en API | CAM-004 | Completada |
| CAM-006 | Integrar autorización DM entre Campaigns y Access | CAM-002, CAM-005 | Completada |
| CAM-007 | Implementar usuarios elegibles en Access | CAM-006 | Completada |
| CAM-008 | Emitir invitaciones por `recipientUserId` | CAM-007 | Completada |
| CAM-009 | Adaptar migraciones y esquemas PostgreSQL | CAM-004, CAM-008 | Completada |
| CAM-010 | Completar pruebas API, persistencia y arquitectura | CAM-005 a CAM-009 | Completada |
| CAM-011 | Crear el módulo frontend Campaigns | CAM-005 | Completada |
| CAM-012 | Integrar selector de usuarios elegibles en Access web | CAM-007, CAM-008, CAM-011 | Completada |
| CAM-013 | Completar pruebas frontend y fitness functions | CAM-011, CAM-012 | Completada |
| CAM-014 | Verificar build, Docker y recorrido integrado | CAM-010, CAM-013 | Completada |
| CAM-015 | Actualizar roadmap, arquitectura y cerrar el incremento | CAM-014 | Completada |

## Tareas

### [x] CAM-001 — Caracterizar y fijar contratos de partida

- Inventariar contratos HTTP, autorización y persistencia actuales de invitaciones y membresías.
- Confirmar cobertura de emisión, aceptación y aislamiento antes de cambiar ownership DM.
- Registrar cualquier compatibilidad que deban conservar las nuevas rutas.

### [x] CAM-002 — Publicar contratos intermodulares de Access

- Crear contratos públicos mínimos para consultar campañas de jugador y autorizar invitaciones.
- Mantener DTO inmutables y no exponer EF, repositorios, entidades o transacciones.
- Añadir fitness functions para la dirección de referencias.

### [x] CAM-003 — Crear el proyecto y los límites del módulo Campaigns

- Crear el ensamblado, fachada, capas internas y proyecto de tests.
- Integrarlo en solución, host, Docker y pruebas arquitectónicas globales.

### [x] CAM-004 — Implementar dominio y persistencia de Campaigns

- Crear el agregado `Campaign` con nombre, DM, módulo opcional y fecha.
- Crear `CampaignsDbContext`, configuración, repositorios, read stores y migración inicial.
- Cubrir invariantes y round-trip PostgreSQL.

### [x] CAM-005 — Implementar creación, listado y detalle en API

- Implementar command/query handlers y controladores.
- Aplicar `401`, `403`, `404`, validación y `201 Location` definidos.
- Añadir observabilidad sin datos privados.

### [x] CAM-006 — Integrar autorización DM entre Campaigns y Access

- Implementar en Campaigns el puerto requerido por Access.
- Sustituir la autorización DM provisional de Access.
- Mantener la referencia acíclica y el fallo cerrado.

### [x] CAM-007 — Implementar usuarios elegibles en Access

- Crear query paginada, filtros de elegibilidad y correo enmascarado.
- Exigir DM, aplicar rate limiting y evitar telemetría de consultas.
- Cubrir búsqueda por nombre/correo, exclusiones y autorización.

### [x] CAM-008 — Emitir invitaciones por `recipientUserId`

- Extender el contrato de emisión manteniendo compatibilidad por correo.
- Revalidar cuenta, membresía e invitación pendiente dentro del command.
- Conservar invitación y outbox transaccionales.

### [x] CAM-009 — Adaptar migraciones y esquemas PostgreSQL

- Introducir los esquemas `access` y `campaigns` sin perder datos.
- Verificar base vacía y actualización desde la versión previa.
- Definir orden determinista de migraciones.

### [x] CAM-010 — Completar pruebas API, persistencia y arquitectura

- Cubrir dominio, Application, PostgreSQL, componente, concurrencia y contratos.
- Demostrar aislamiento, `403`, roles e integración completa de invitación.
- Mantener verde la suite global.

### [x] CAM-011 — Crear el módulo frontend Campaigns

- Crear clientes, contratos, rutas lazy y páginas de listado, creación y detalle.
- Integrar navegación autenticada y estados de UI.
- Consumir Access solo mediante navegación y APIs públicas.

### [x] CAM-012 — Integrar selector de usuarios elegibles en Access web

- Sustituir el campo libre principal por listado/búsqueda y selección.
- Implementar debounce, cancelación, datos enmascarados y recarga tras emitir.
- Conservar el cliente compatible por correo.

### [x] CAM-013 — Completar pruebas frontend y fitness functions

- Cubrir clientes, routing, páginas, formularios, selector y errores.
- Ampliar reglas de imports para Campaigns y evitar deep imports/ciclos.
- Mantener build de producción verde.

### [x] CAM-014 — Verificar build, Docker y recorrido integrado

- Ejecutar suites web y API, builds e imágenes.
- Verificar migraciones, Compose y recorrido creador/invitado.
- Corregir cualquier regresión antes del cierre.

### [x] CAM-015 — Actualizar roadmap, arquitectura y cerrar el incremento

- Actualizar estados, diagramas, README, ADR y runbooks afectados.
- Marcar tareas con evidencia y cerrar spec solo tras verificación completa.

## Evidencia de cierre

- `dotnet msbuild DndCampaign.slnx /t:Build /m:1`: solución completa compilada.
- `docker compose run --build --rm api-tests`: 43 tests correctos, sin omisiones, sobre PostgreSQL 18 efímero.
- `pnpm test:web`: 45 tests correctos en 19 archivos.
- `pnpm build`: build Angular de producción correcto y rutas Campaigns emitidas como chunks lazy.
- `docker compose build api` y `docker compose build web`: imágenes construidas correctamente.
- `docker compose config --quiet`: configuración válida.
- El test `Existing_user_can_be_selected_invited_and_see_the_campaign_after_acceptance` demuestra el recorrido HTTP completo y la separación de roles.
