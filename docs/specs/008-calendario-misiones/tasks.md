# Tareas 008: Registro y gestión compartida de misiones

- Estado: Implementación completada; construcción adicional de imágenes finales pendiente
- Especificación: [spec.md](spec.md)
- Plan: [plan.md](plan.md)
- Autorización: el usuario aprobó el plan y solicitó expresamente crear las tareas e implementarlas el 2026-08-23

## T01 — Crear el módulo backend Missions

- [x] Crear proyecto, fachada, capas internas y proyecto de pruebas.
- [x] Añadir el agregado `Mission` con autoría estable, estados y principal.
- [x] Implementar resultados, puertos y métricas sin datos de alta cardinalidad.

## T02 — Persistir misiones y proteger la principal

- [x] Crear `MissionsDbContext`, repositorio y factoría de diseño.
- [x] Generar la migración inicial del esquema `missions`, checks e índices.
- [x] Garantizar una sola misión principal activa por campaña ante concurrencia.
- [x] Verificar orden estable y aislamiento por campaña sobre PostgreSQL.

## T03 — Implementar casos de uso y contrato HTTP

- [x] Implementar listado autorizado y `canDelete` derivado.
- [x] Implementar creación por DM y por jugador con personaje activo.
- [x] Implementar edición colaborativa y evolución de estado.
- [x] Implementar marcado y desmarcado transaccional de principal.
- [x] Implementar eliminación por creador o DM.
- [x] Exponer endpoints y mapear `ProblemDetails` sin fechas funcionales.

## T04 — Integrar host y gobierno arquitectónico

- [x] Registrar Missions, rutas y migraciones en el host.
- [x] Añadir proyectos a solución y Dockerfile.
- [x] Actualizar fitness functions backend para Missions y sus únicas dependencias aprobadas.

## T05 — Crear el módulo frontend Missions

- [x] Añadir contratos y cliente HTTP con pruebas.
- [x] Añadir ruta lazy autenticada y providers acotados.
- [x] Implementar principal, secciones activa/cerrada y estados vacío/carga/error.
- [x] Implementar alta y edición sin campos de fecha.
- [x] Implementar estados, cambio de principal y eliminación confirmada según permisos.
- [x] Integrar el enlace desde la portada de Campaigns.

## T06 — Verificar frontend y accesibilidad

- [x] Cubrir DM, jugador con/sin personaje activo, autoría y permisos.
- [x] Cubrir validación, texto plano, confirmación de borrado y tratamiento de errores.
- [x] Mantener límites modulares, routing y build de producción verdes.

## T07 — Verificación integrada y operativa

- [x] Ejecutar suites .NET, integración PostgreSQL y pruebas arquitectónicas.
- [x] Ejecutar suite Angular y build de producción.
- [ ] Construir las imágenes finales `api` y `web`; el sistema de aprobaciones agotó su límite antes de iniciar esta comprobación. La imagen `api-tests` sí compiló y publicó la API en Release, y ambas configuraciones Compose son válidas.
- [x] Comprobar ausencia de secretos, fechas funcionales y contenido editorial concreto.

## T08 — Cerrar trazabilidad

- [x] Actualizar orden de migraciones, diagramas y observabilidad afectados.
- [x] Registrar evidencias de pruebas en este documento.
- [x] Marcar spec, plan, índice y roadmap conforme a la evidencia final disponible.

## Evidencias

- `docker compose run --build --rm api-tests`: 77 pruebas correctas sobre PostgreSQL y Azurite; 0 fallos y 0 omisiones.
- `npm test -- --watch=false`: 63 pruebas Angular correctas en 25 archivos.
- `npm run build`: build Angular de producción correcto con chunk lazy `mission-page`.
- `dotnet build DndCampaign.slnx --no-restore --disable-build-servers`: solución completa sin advertencias ni errores.
- `docker compose config --quiet` y `docker compose -f compose.deploy.yaml config --quiet`: configuraciones válidas.
- `docker compose build api web`: no iniciado porque el sistema de aprobaciones alcanzó su límite de uso; requiere una nueva aprobación explícita cuando vuelva a estar disponible.
