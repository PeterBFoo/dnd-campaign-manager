## Why

La capa Application importa tipos del namespace `DndCampaign.Api.Api`, invirtiendo la dirección de dependencias que exige Clean Architecture. Además, `IdentityInvitationEndpoints.cs` (523 líneas) es un **endpoint gordo**: mezcla lógica de negocio, acceso EF Core y contratos HTTP en un solo archivo — el antipatrón no es Minimal API en sí, sino la concentración de responsabilidades. Este cambio corrige el límite Application↔Api, **descompone** ese archivo en adaptadores HTTP finos y servicios Application acotados, y centraliza el mapeo de excepciones.

## What Changes

- Eliminar la dependencia Application → Api; Application define inputs/outputs de casos de uso propios (sin duplicación ceremonial Request+Command+Outcome+Response cuando no aporta).
- Descomponer `IdentityInvitationEndpoints` en adaptadores HTTP finos por vertical:
  - `IdentityController` — bootstrap, login, logout, me
  - `InvitationAcceptanceController` — preview, accept
  - `PlatformInvitationsController` — CRUD platform admin
  - `CampaignInvitationsController` — CRUD campaign DM
- Eliminar el archivo gordo `IdentityInvitationEndpoints.cs` y su registro en `Program.cs`.
- Dividir lógica Application en servicios acotados (no ampliar `InvitationService` con todo):
  - `IdentityService`
  - `InvitationAcceptanceService`
  - `PlatformInvitationService`
  - `CampaignInvitationService`
- Renombrar operaciones con side-effects (`MarkExpired` en lecturas) como **commands**, no queries.
- Contratos HTTP en `Api/Contracts/` por vertical; `UserResponse` solo donde nace (Identity), reutilizado por referencia desde Invitations.
- Añadir `IExceptionHandler` centralizado para excepciones de identidad/invitaciones (eliminar mapeo duplicado en controllers).
- Tests de integración/component (PostgreSQL real o patrón existente); **no** mockear `DbContext`/`DbSet`. Controller tests mockean servicios Application, no persistencia.
- Mantener tests de integración existentes verdes.

**Deuda explícita (temporal, no arquitectura final):** Application seguirá usando `CampaignDbContext` directamente hasta un cambio posterior de abstracción de persistencia.

**Sin cambios funcionales.** Rutas, status codes, cuerpos JSON y reglas de negocio permanecen idénticos.

**Fuera de alcance:** Mediator, `IAppDbContext`/repositorios, Angular, migraciones EF, split en proyectos separados.

## Capabilities

### New Capabilities

_(ninguna — refactor interno sin cambio de comportamiento observable)_

### Modified Capabilities

_(ninguna)_

`skip_specs: true` — no cambia requisitos de sistema.

## Impact

| Componente | Alcance |
|---|---|
| **API** | `Api/Contracts/`, 4 controllers, `IExceptionHandler`, mapeadores HTTP, eliminar `IdentityInvitationEndpoints.cs`, `Program.cs` |
| **Application** | 4 servicios acotados; commands/results propios; sin `using DndCampaign.Api.Api`; **deuda:** acceso directo a `CampaignDbContext` |
| **Tests** | Integración/component extendidos; controller tests con servicios mockeados |
| **Angular / Infra** | Sin cambios |

**Endpoints migrados:**

| Ruta | Método | Controller |
|---|---|---|
| `/api/v1/identity/*` | GET/POST | `IdentityController` |
| `/api/v1/invitations/preview`, `/accept` | POST | `InvitationAcceptanceController` |
| `/api/v1/platform/invitations/*` | GET/POST/DELETE | `PlatformInvitationsController` |
| `/api/v1/campaigns/{campaignId}/invitations/*` | GET/POST/DELETE | `CampaignInvitationsController` |
