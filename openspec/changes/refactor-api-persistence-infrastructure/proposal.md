## Why

Application orquesta casos de uso inyectando `CampaignDbContext`, tipos de persistencia (`InvitationRecord`, `InvitationOutboxMessage`) y telemetría de Infrastructure. Eso invierte Clean Architecture: la capa de casos de uso conoce EF Core, PostgreSQL y el modelo tabular. El cambio HTTP archivado (`refactor-api-application`) dejó esta deuda explícita; hay que saldarla ahora, de forma incremental y sin cambiar el comportamiento observable.

## What Changes

- Introducir ports de Application acotados a operaciones reales: `IIdentityStore`, `IInvitationStore` (agregado Invitation), `IInvitationOutboxStore` (ciclo de vida del outbox) e `ITransactionalBoundary` (un método serializable, no un Unit of Work genérico).
- Issue/resend persisten Invitation + Outbox atómicamente llamando a ambos stores dentro de `ITransactionalBoundary`.
- Persistencia de login explícita (`PersistLoginAsync`): un `SaveChanges` para rehash opcional + sesión nueva, sin depender del change tracker de EF como efecto colateral.
- Implementar los ports en Infrastructure con el `CampaignDbContext` scoped compartido.
- Hacer de Domain `Invitation` la fuente de verdad de las transiciones; dejar `InvitationRecord` como mapping interno. Application deja de conocer records EF.
- Mover `InvitationOutboxWorker` a Infrastructure como adaptador fino que invoca el caso de uso concreto `ProcessInvitationOutbox` (sin interfaz solo para DI/mock). El lease/SQL permanece en `IInvitationOutboxStore`.
- Mover `IdentityTelemetry` a Application. Extraer `IdentitySecurityOptions.FromConfiguration` al composition root.
- Extraer wiring de persistencia, email y stores desde `Program.cs` a extensiones del host (carpeta `Composition/` opcional; no es una capa nueva).
- Quitar `CampaignDbContext` de `GET /api/v1/platform/status` reutilizando `HealthCheckService` / `PostgresHealthCheck`. No añadir un port de Application para disponibilidad de PostgreSQL.
- Añadir tests unitarios con fakes de ports, tests de mapping/store contra PostgreSQL, y un safeguard temporal de namespaces (sin librería nueva; no es frontera de compilador).
- Documentar el TOCTOU del lease de outbox en multi-réplica; no introducir locks distribuidos ni `xmin`/`rowversion`.

**Sin cambios funcionales.** No hay **BREAKING** changes. Rutas, status codes, JSON, reglas de negocio, esquema PostgreSQL y migraciones existentes permanecen idénticos. Angular no se toca.

**No se introduce:** `IRepository<T>`, `IGenericRepository<T>`, `IAppDbContext` con `DbSet`, Unit of Work genérico, MediatR, CQRS artificial, `IDatabaseAvailability`, `IProcessInvitationOutbox`, nuevos `.csproj`, AutoMapper.

`skip_specs: true` — refactor interno; el comportamiento del sistema no cambia. Este marcador debe figurar en `.openspec.yaml` del cambio (el CLI lo exige para un delta vacío).

## Capabilities

### New Capabilities

_(ninguna — refactor interno sin cambio de comportamiento observable)_

### Modified Capabilities

_(ninguna)_

## Impact

| Componente | Alcance |
|---|---|
| **API (Application)** | Ports `IIdentityStore`, `IInvitationStore`, `IInvitationOutboxStore`, `ITransactionalBoundary`; caso de uso concreto `ProcessInvitationOutbox`; servicios dejan EF; `InvitationEmailComposer` deja `InvitationRecord`; meters de identity; options sin `FromConfiguration` |
| **API (Domain)** | `Invitation` gana reconstitución, emisor/aceptante y transiciones usadas en producción; identidad sigue mapeada por EF |
| **API (Infrastructure)** | Stores (identity, invitation, outbox), mapping, boundary transaccional, worker en `Background/`, `CampaignDbContext` encapsulado, `PostgresHealthCheck` |
| **API (host)** | Extensiones de DI; status vía `HealthCheckService`; migraciones y Npgsql solo en composición |
| **Tests** | Fakes de ports; mapping/store contra PostgreSQL; tests HTTP e integración existentes verdes; safeguard textual de namespaces |
| **Angular / PostgreSQL schema / Terraform / Grafana** | Sin cambios (nombres de métricas se conservan) |

**Riesgos principales:** mapping Invitation duplicado mal (doble máquina de estados); transacciones que no envuelven ambos stores si el DbContext no es scoped compartido; worker que deje de reclamar trabajo; tests de integración que aún tocan `CampaignDbContext` (permitido) frente a Application que no debe.

**Rollback:** revertir el commit del slice; no hay migración de esquema que deshacer. Cada fase debe compilar y dejar tests verdes antes de la siguiente.
