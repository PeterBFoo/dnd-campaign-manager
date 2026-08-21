## Context

Ver `proposal.md`. Problema real: **endpoint gordo** + **Application → Api** + **servicios/controllers monolíticos en el plan anterior**.

Restricciones revisadas: sin Mediator; persistencia sin abstraer (deuda documentada); contratos HTTP idénticos; tests sin mocks de EF.

## Goals / Non-Goals

### Goals

- Application no referencia `DndCampaign.Api.Api`.
- Descomponer el endpoint gordo en adaptadores HTTP finos (controllers MVC — elección pragmática del proyecto, no mandato anti-Minimal-API).
- Servicios Application **uno por vertical**, no un `InvitationService` omnisciente.
- Controllers **uno por vertical**, no un `InvitationController` omnisciente.
- Operaciones que mutan estado (p. ej. `MarkExpired` en preview/list) modeladas como **commands**, nunca como queries.
- `IExceptionHandler` centralizado en este cambio.
- Tipos Application solo cuando aportan separación real respecto al contrato HTTP (evitar CQRS ceremonial).
- Tests de integración/component para Application; controller tests con servicios mockeados.

### Non-Goals

- Mediator, `IAppDbContext`, repositorios (cambio posterior).
- Unificar `Invitation` domain con `InvitationRecord`.
- Angular, EF migrations, split de proyectos.
- Prescribir MVC vs Minimal API como regla global — solo exigir adaptadores finos.

## Decisions

### 1. El antipatrón es el endpoint gordo, no Minimal API

**Decisión:** Eliminar `IdentityInvitationEndpoints.cs` porque concentra 523 líneas con EF, autorización y HTTP. Reemplazar por controllers finos. Si en el futuro un endpoint encaja mejor como Minimal API delgado, es válido — la regla es **delgadez**, no el mecanismo de routing.

### 2. Controllers por vertical (Api)

| Controller | Ruta base | Endpoints |
|---|---|---|
| `IdentityController` | `/api/v1/identity` | bootstrap GET/POST, login, logout, me |
| `InvitationAcceptanceController` | `/api/v1/invitations` | preview, accept |
| `PlatformInvitationsController` | `/api/v1/platform/invitations` | list, issue, resend, revoke |
| `CampaignInvitationsController` | `/api/v1/campaigns/{campaignId}/invitations` | list, issue, resend, revoke |

Cada controller: inyecta **un** servicio Application, mapea Request→input, invoca servicio, mapea result→Response, devuelve `IActionResult`. Sin EF, sin LINQ, sin try/catch locales (delegado a `IExceptionHandler`).

### 3. Servicios Application por vertical (no God Service)

| Servicio | Responsabilidad | Persiste hoy vía |
|---|---|---|
| `IdentityService` | bootstrap, login, logout | `CampaignDbContext` (**deuda temporal**) |
| `InvitationAcceptanceService` | preview, accept | idem |
| `PlatformInvitationService` | list/issue/resend/revoke platform | idem |
| `CampaignInvitationService` | list/issue/resend/revoke campaign + autorización DM | idem |

**Deuda temporal explícita:** los cuatro servicios inyectan `CampaignDbContext` (Infrastructure). Esto viola Clean Architecture estricta pero es aceptado **solo en este cambio**; la abstracción `IAppDbContext`/repos es el siguiente incremento. Documentar en comentarios de clase o README interno del módulo si ayuda al apply.

`InvitationService` actual se **divide** o reemplaza; no se amplía.

### 4. Commands vs queries — sin side-effects en queries

Operaciones que hoy llaman `MarkExpired` durante preview o list **mutan persistencia**. No se nombran `*Query`.

| Operación actual | Tipo Application | Razón |
|---|---|---|
| Preview | `PreviewInvitationCommand` | puede persistir expiración |
| List platform | `ListPlatformInvitationsCommand` | idem |
| List campaign | `ListCampaignInvitationsCommand` | idem + check DM |
| Accept, issue, resend, revoke | `*Command` | ya mutan |
| GET me | _(sin servicio)_ | mapeo directo ClaimsPrincipal→`UserResponse` en controller |
| GET bootstrap status | `GetBootstrapStatus` en `IdentityService` | lectura pura, OK |

Comportamiento observable idéntico: preview/list siguen expirando invitaciones pendientes antes de responder — pero el nombre refleja la realidad (command).

### 5. Contratos HTTP — sin cajón de sastre

| Archivo | Contenido | Regla |
|---|---|---|
| `IdentityHttpContracts.cs` | `BootstrapRequest`, `LoginRequest`, `SessionResponse`, `UserResponse`, bootstrap status DTO | Dueño de `UserResponse` |
| `InvitationAcceptanceHttpContracts.cs` | preview/accept requests y responses | Importa/reutiliza `UserResponse` desde Identity contracts |
| `PlatformInvitationHttpContracts.cs` | issue request, `InvitationResponse` | Solo platform |
| `CampaignInvitationHttpContracts.cs` | reutiliza issue request + `InvitationResponse` si idénticos, o type alias | Solo campaign |

**No** crear `SharedHttpContracts.cs` genérico. Un tipo compartido vive en el contrato del dominio que lo define (`UserResponse` → Identity).

### 6. Tipos Application — pragmáticos, no ceremoniales

Introducir tipo Application **solo si**:

- el input HTTP difiere del input de caso de uso (p. ej. `AuthenticatedActor` extraído de `ClaimsPrincipal`), o
- el output interno no debe filtrarse al HTTP tal cual, o
- el caso de uso devuelve discriminated result (p. ej. `AcceptInvitationResult` con status enum).

**Omitir** capas redundantes:

| Endpoint | HTTP | Application | Notas |
|---|---|---|---|
| POST bootstrap | `BootstrapRequest` | `BootstrapAccountCommand` | campos idénticos — mapping trivial 1:1 |
| POST login | `LoginRequest` | `LoginCommand` | idem |
| POST accept | `AcceptInvitationRequest` + actor | `AcceptInvitationCommand` + `AuthenticatedActor` | actor justifica tipo aparte |
| POST preview | `InvitationTokenRequest` | `PreviewInvitationCommand` | token only — puede ser `record PreviewInvitationCommand(string? Token)` |
| GET me | — | — | controller mapea claims → `UserResponse` |

Outcomes (`UserProfile`, `LoginOutcome`, etc.) se mantienen donde el servicio no debe conocer JSON HTTP. No crear `Outcome` espejo de `Response` si el mapping es campo a campo sin lógica — evaluar inline en mapeador.

### 7. Mapeadores HTTP

- `IdentityHttpMapping` — Identity vertical
- `InvitationAcceptanceHttpMapping` — preview/accept
- `PlatformInvitationHttpMapping` — platform CRUD
- `CampaignInvitationHttpMapping` — campaign CRUD

Un mapeador por controller; evita helper monolítico.

### 8. `IExceptionHandler` — en este cambio

**Decisión:** Implementar `ApiExceptionHandler : IExceptionHandler` mapeando:

- `InvitationConflictException` → 409 (payload actual)
- `InvitationStateException` → 409
- `InvitationRateLimitException` → 429 con `retryAt`
- `ArgumentException` en issue → 400 validation problem (si no cubierto por factory existente)

Controllers eliminan try/catch duplicados de `IssueInvitationResultAsync` / `ResendInvitationResultAsync`. `IdentityValidationProblemFactory` se mantiene para validación de credenciales.

**Cambio observable aceptado:** el endpoint gordo respondía 429 con `Results.Json` (`application/json`). El handler centralizado emite `application/problem+json; charset=utf-8` para todos los errores mapeados, incluido 429. Status code y campos del cuerpo (`status`, `title`, `detail`, `retryAt`) se mantienen; solo cambia el content type, alineándolo con el resto de respuestas de error.

Registrar en `Program.cs`: `AddExceptionHandler<ApiExceptionHandler>()` antes de migrar controllers.

### 9. GET `/me` sin caso de uso Application

**Decisión:** `IdentityController.Me` construye `UserResponse` desde `ClaimsPrincipal` (como hoy el handler `Me`). No introducir `GetCurrentUserQuery` — no hay orquestación ni persistencia.

### 10. Tests

**Application / casos de uso:**

- Extender o añadir tests de **integración** con PostgreSQL (patrón `InvitationFlowIntegrationTests`), uno por vertical de servicio.
- **Prohibido** mockear `DbContext`/`DbSet` en tests etiquetados como unitarios de Application.

**Api / controllers:**

- Tests con servicio Application **mockeado** (interface o clase con virtual methods si necesario — preferir extraer interfaces mínimas solo si DI lo exige).
- Verificar status codes y headers; no verificar EF.

**Regresión:** suite existente completa verde.

### 11. Sin Mediator

Servicios como entry points; commands como parámetros records.

## Flujo ejemplo (login)

```
IdentityController.Login(LoginRequest)
  → IdentityHttpMapping.ToCommand(request)
  → IdentityService.LoginAsync(LoginCommand)
  → IdentityHttpMapping.ToSessionResponse(outcome)
  → 200 OK
```

Excepciones no capturadas en controller → `ApiExceptionHandler`.

## Arquitectura objetivo (este cambio)

```
Controllers (4, finos)
  → Contracts (por vertical)
  → Mapping (por vertical)
  → Application services (4, acotados)  ──deuda──► CampaignDbContext
  → Domain
```

## Risks / Trade-offs

| Riesgo | Mitigación |
|---|---|
| Cuatro servicios + cuatro controllers = más archivos | Cada archivo pequeño y testeable; mejor que god classes |
| Side-effects en list/preview confunden lectura | Nombrado `*Command` + comentario; refactor futuro a job de expiración |
| Application→Infrastructure persiste | Documentado como deuda; siguiente change `IAppDbContext` |
| Split de `InvitationService` rompe DI | Actualizar `Program.cs` registrations en mismo PR |
| Tests más lentos (integración) | Aceptable; más fiables que mocks EF |

## Migration Plan

1. `IExceptionHandler` + registro.
2. Contracts + types Application pragmáticos.
3. Split servicios; migrar lógica desde endpoint gordo.
4. Crear 4 controllers; wire DI.
5. Eliminar `IdentityInvitationEndpoints`.
6. Tests integración + controller.
7. `dotnet build && dotnet test`.

## Open Questions

_(ninguna)_
