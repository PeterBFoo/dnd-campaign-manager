# Tareas 013: Asignación de un módulo a una campaña

- Estado: En ejecución
- Especificación: [spec.md](spec.md)
- Plan: [plan.md](plan.md)

## Preparación y decisiones

- [x] Crear la rama específica `codex/spec-013-asignacion-modulo-campana`.
- [x] Auditar Campaigns, la web y el alcance propuesto de AdventureCatalog.
- [x] Redactar y aceptar el plan técnico del incremento.
- [x] Documentar en ADR-0008 la dirección de dependencias y la garantía `ON DELETE SET NULL`.
- [x] Integrar la implementación aceptada de la spec 012 antes de conectar el contrato intermodular.

## Dominio y datos

- [x] Añadir al agregado Campaign alta opcional, asignación, cambio y retirada idempotentes.
- [x] Cubrir las nuevas invariantes con pruebas unitarias.
- [x] Incorporar el token de concurrencia y su migración compatible.
- [x] Añadir la FK entre esquemas con `ON DELETE SET NULL`.
- [ ] Cubrir persistencia, concurrencia y borrado transversal con PostgreSQL real (pendiente de entorno `IDENTITY_TEST_DATABASE`).

## API y arquitectura

- [x] Consumir el contrato mínimo de lectura de AdventureCatalog sin exponer implementaciones.
- [x] Ampliar creación y proyecciones de campaña con módulo opcional y resumen seguro.
- [x] Implementar asignación, cambio y retirada con autorización DM y errores `404`/`409`.
- [x] Publicar el listado autenticado y minimizado de opciones desde AdventureCatalog.
- [x] Añadir métricas sin datos sensibles y actualizar las reglas de arquitectura.
- [x] Cubrir handlers y contratos HTTP básicos, incluido DM, jugador/ajeno y conflicto de versión en Application.

## Web

- [x] Publicar el cliente y contratos mínimos de opciones desde `adventure-catalog`.
- [x] Añadir selector opcional y estados independientes al alta de campaña.
- [x] Mostrar el resumen a participantes y controles de asociación solo al DM.
- [x] Implementar confirmación, bloqueo de doble envío, conflicto y recarga de opciones.
- [x] Cubrir clientes y páginas con pruebas Angular (75 pruebas web).

## Cierre

- Evidencia local: build .NET sin avisos; Campaigns 17/17; arquitectura 8/8; AdventureCatalog 5 pruebas, 2 integraciones omitidas sin PostgreSQL/Azurite; Angular 75/75 y build de producción correcto.

- [x] Ejecutar las suites disponibles, compilación .NET y build Angular.
- [ ] Verificar migraciones y Docker Compose con servicios reales (entorno externo pendiente).
- [x] Actualizar estados, índice y trazabilidad del roadmap con evidencia real.

## Evidencias

- `/Users/pereborras/.dotnet/dotnet build DndCampaign.slnx --no-restore -m:1`: compilación correcta, sin advertencias ni errores.
- Ejecución directa del runner xUnit de Campaigns: 17 pruebas correctas, 0 fallos.
- Ejecución directa del runner de arquitectura: 8 pruebas correctas, 0 fallos.
- Ejecución directa del runner de AdventureCatalog: 5 pruebas, 0 fallos; 2 integraciones omitidas por falta de `IDENTITY_TEST_DATABASE` y Azurite.
- `ng test --watch=false --no-progress`: 27 archivos y 75 pruebas correctas.
- `ng build --no-progress`: bundle de producción generado correctamente.
