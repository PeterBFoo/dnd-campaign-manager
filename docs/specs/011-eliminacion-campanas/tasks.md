# Tareas 011: Eliminación de campañas

- Estado: Completado
- Especificación: [spec.md](spec.md)
- Plan: [plan.md](plan.md)

## Dominio y datos

- [x] Añadir la transición de baja lógica y su invariante al agregado Campaign.
- [x] Filtrar campañas eliminadas en repositorios y contratos de acceso.
- [x] Crear y verificar la migración `DeletedAt` preservando datos existentes.

## API y seguridad

- [x] Implementar handler, autorización DM, endpoint `DELETE` y métricas.
- [x] Invalidar aceptación y gestión de invitaciones de campañas eliminadas.
- [x] Cubrir dominio, Application, PostgreSQL y contrato HTTP.

## Web

- [x] Añadir la operación al cliente Campaigns.
- [x] Mostrar confirmación y estado de petición solo al DM.
- [x] Cubrir cliente, roles, confirmación, éxito y error.

## Cierre

- [x] Ejecutar suites, builds y validación de Compose.
- [x] Actualizar estados, índice y trazabilidad del roadmap con evidencia real.

## Evidencias

- `docker compose run --build --rm api-tests`: 90 pruebas correctas, sin fallos ni omisiones, sobre PostgreSQL real.
- `pnpm test:web`: 73 pruebas correctas en 27 archivos.
- `dotnet build DndCampaign.slnx --no-restore -m:1`: compilación correcta, sin advertencias ni errores.
- `pnpm build`: build Angular de producción correcto.
- `docker compose build api web`: imágenes finales construidas.
- `docker compose config --quiet`: configuración válida.
