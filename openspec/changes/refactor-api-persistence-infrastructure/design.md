## Context

Ver `proposal.md` — Why. El host es un solo `.csproj`; las carpetas son convención, no frontera de compilador. El puerto `ITransactionalEmailSender` ya es el patrón a copiar. Identidad se mapea como entidades de Domain; invitaciones tienen un segundo modelo (`InvitationRecord`) con la máquina de estados que producción realmente usa.

Componentes afectados: API (Application, Domain, Infrastructure, host) y `apps/api-tests`. No Angular, no esquema PostgreSQL, no Terraform, no Grafana.

## Goals / Non-Goals

**Goals:**

- Application depende solo de Domain y de ports propios.
- `CampaignDbContext` no sale de Infrastructure (salvo tests de integración de persistencia).
- Domain `Invitation` es la única máquina de estados de negocio; `InvitationRecord` es mapping.
- Outbox no vive en el mismo port que el agregado Invitation.
- Persistencia de login es explícita: no hay `SaveChanges` que “arrastre” entidades tracked por accidente.
- Worker de outbox es un adaptador Infrastructure que llama un caso de uso concreto.
- Status de plataforma no usa `CampaignDbContext` ni un port de Application para PostgreSQL.
- Un safeguard textual de namespaces evita regresiones obvias, sin fingir una frontera de compilador.

**Non-Goals:**

- `IAppDbContext`, `IRepository<T>`, UoW genérico, MediatR, CQRS, AutoMapper, nuevos proyectos.
- `IDatabaseAvailability` en Application.
- `IProcessInvitationOutbox` si solo serviría para DI/mock.
- Cambiar HTTP, JSON, reglas funcionales o migraciones.
- `xmin`/rowversion, locks distribuidos, split a cuatro ensamblados.
- Separar lectura/escritura: el listado actual persiste expiración.

## Decisions

### 1. Arquitectura actual (BEFORE)

```text
Api
 ↓
Application
 ↓
CampaignDbContext / InvitationRecord / EF Core
 ↓
PostgreSQL
```

Application también referencia `IdentityTelemetry` (Infrastructure) y el worker vive en Application como `BackgroundService`.

### 2. Arquitectura objetivo (AFTER)

```text
                 Domain
                   ↑
                   │
Api ───────→ Application
                  │
                  │ ports
                  ▼
      IIdentityStore
      IInvitationStore
      IInvitationOutboxStore
      ITransactionalBoundary
      ITransactionalEmailSender
                  ▲
                  │ implements
                  │
            Infrastructure
                  │
        CampaignDbContext
                  │
              EF Core
                  │
             PostgreSQL

Infrastructure Background
        ↓
Application ProcessInvitationOutbox (concreto)
        ↓
ports
```

Composition (`Program.cs` + extensiones del host) es el único sitio que conoce todas las capas para wiring. El endpoint de status usa `HealthCheckService`, no Application ni `CampaignDbContext`.

### 3. Dependency rules

| Desde | Puede | No puede |
|---|---|---|
| Domain | BCL | Application, Api, Infrastructure, EF, Npgsql |
| Application | Domain, ports propios, BCL (`System.Diagnostics.Metrics`) | Infrastructure, EF, Npgsql, `InvitationRecord`, `CampaignDbContext` |
| Infrastructure | Application (ports y casos de uso concretos), Domain, EF, Npgsql, SDK externos | Api (controllers/contracts) |
| Api | Application (casos de uso, commands, HTTP mapping) | EF, stores concretos, Domain salvo claims ya existentes |
| Composition / host | todas las capas; `HealthCheckService` | inyectar `CampaignDbContext` en endpoints |

`IPasswordHasher<UserAccount>` permanece en Application: es un helper de framework de hashing, no persistencia.

### 4. Por qué no `IAppDbContext` ni `IRepository<T>`

`IAppDbContext` con `DbSet<T>` deja Entity Framework en Application y no elimina `InvitationRecord`. Viola el objetivo explícito.

`IRepository<T>` / `IGenericRepository<T>` duplica `DbSet` en identidad y no cubre issue atómico invitation+outbox ni accept que cruza agregados.

Los ports son el mismo estilo que `ITransactionalEmailSender`: operaciones reales, sin `IQueryable`. Outbox es un port propio porque su ciclo de vida (lease, reintentos, ciphertext) no es el agregado Invitation.

### 5. `ITransactionalBoundary`

Existe porque bootstrap, issue, resend y accept ya usan `IsolationLevel.Serializable` y varios cruzan stores. No es un UoW: no expone `SaveChanges`, repositorios ni `DbSet`.

```csharp
public interface ITransactionalBoundary
{
    Task ExecuteSerializableAsync(
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken);

    Task<T> ExecuteSerializableAsync<T>(
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken);
}
```

Implementación Infrastructure: `database.Database.BeginTransactionAsync(IsolationLevel.Serializable)`. Application no ve `IsolationLevel`.

**Contrato de scope:** `CampaignDbContext`, stores e `ITransactionalBoundary` son `Scoped`. Los stores resueltos dentro de `action` usan el mismo DbContext que abrió la transacción. No crear scopes anidados dentro de `action`.

Los métodos mutadores de los stores llaman `SaveChangesAsync` internamente **solo sobre los argumentos que reciben**. Dentro de la transacción, eso no confirma SQL hasta `CommitAsync`.

Issue/resend:

```csharp
await transactions.ExecuteSerializableAsync(async ct =>
{
    await invitations.AddAsync(invitation, ct);
    await outbox.EnqueueAsync(invitation.Id, encryptedToken, now, ct);
}, cancellationToken);
```

Login y logout no usan el boundary (hoy tampoco abren transacción serializable).

### 6. `IIdentityStore`

Operaciones extraídas del código actual. Sin `FindById` de usuario: no se usa.

```csharp
public sealed record ActiveUserSession(UserAccount User, UserSession Session);

public interface IIdentityStore
{
    Task<bool> HasAnyUsersAsync(CancellationToken cancellationToken);

    Task<UserAccount?> FindByEmailAsync(
        string normalizedEmail,
        CancellationToken cancellationToken);

    Task AddUserAsync(UserAccount user, CancellationToken cancellationToken);

    Task AddSessionAsync(UserSession session, CancellationToken cancellationToken);

    Task PersistLoginAsync(
        Guid userId,
        string? rehashedPasswordHash,
        UserSession newSession,
        CancellationToken cancellationToken);

    Task<UserSession?> FindSessionByIdAsync(
        Guid sessionId,
        CancellationToken cancellationToken);

    Task SaveSessionAsync(UserSession session, CancellationToken cancellationToken);

    Task<ActiveUserSession?> FindActiveByTokenHashAsync(
        string tokenHash,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task<bool> IsCampaignDmAsync(
        Guid campaignId,
        Guid userId,
        CancellationToken cancellationToken);

    Task<bool> IsCampaignMemberAsync(
        Guid campaignId,
        Guid userId,
        CancellationToken cancellationToken);

    Task AddMembershipAsync(
        CampaignMembership membership,
        CancellationToken cancellationToken);
}
```

**Login — persistencia explícita y mínima.** Hoy un único `SaveChanges` escribe el rehash opcional de contraseña y la sesión nueva, sin transacción serializable. `PersistLoginAsync` reproduce exactamente eso, sin un UPDATE genérico del `UserAccount`:

- Application decide si hace falta rehash y crea la sesión.
- El store **inserta** `newSession`. Si `rehashedPasswordHash` no es null, **actualiza únicamente** `PasswordHash` de la fila `userId`. No escribe Email, DisplayName, IsPlatformAdmin, CreatedAt ni ningún otro escalar.
- Si `rehashedPasswordHash` es null, no hay UPDATE de usuario: solo la sesión.
- Un `SaveChanges` para ambos cambios. `AddSessionAsync` no actualiza usuarios.

`SaveSessionAsync` cubre revoke (logout): adjunta o actualiza **la sesión recibida** y persiste solo esa fila. No escribe otras entidades tracked del `DbContext` scoped.

Cada mutador de store aísla `SaveChangesAsync` a las entidades de esa operación. EF Core persiste todo lo tracked; los stores no asumen lo contrario. Las lecturas que entregan modelos a Application usan `AsNoTracking` para no dejar mutaciones accidentales en el tracker. `FindActiveByTokenHashAsync` ya era no-tracking. `PersistLoginAsync` recarga el usuario internamente y marca **solo** `PasswordHash` cuando hay rehash.

No se crean `DbContext` independientes por store: el contexto scoped compartido sigue siendo el de `ITransactionalBoundary`.

Accept de usuario nuevo: `AddUserAsync` inserta el usuario; `AddSessionAsync` inserta solo la sesión; ambos dentro del boundary serializable junto a membership e invitation.

Identidad **sigue** mapeada como entidad EF (constructores vacíos privados). No crear `UserAccountRecord` por simetría.

### 7. `IInvitationStore` (agregado Invitation)

Application trabaja con Domain `Invitation`. Nunca `InvitationRecord`.

```csharp
public sealed record InvitationListItem(
    Invitation Invitation,
    DateTimeOffset? LastSentAt);

public interface IInvitationStore
{
    Task<bool> HasPendingAsync(
        InvitationKind kind,
        Guid? campaignId,
        string recipientEmail,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task<Invitation?> FindByTokenHashAsync(
        string tokenHash,
        CancellationToken cancellationToken);

    Task<Invitation?> FindByIdAsync(
        Guid invitationId,
        CancellationToken cancellationToken);

    Task<Invitation?> FindByIdAsync(
        Guid invitationId,
        InvitationKind kind,
        Guid? campaignId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<DateTimeOffset>> ListRecentIssueTimesAsync(
        InvitationKind kind,
        Guid? campaignId,
        string recipientEmail,
        DateTimeOffset since,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<InvitationListItem>> ListAsync(
        InvitationKind kind,
        Guid? campaignId,
        CancellationToken cancellationToken);

    Task AddAsync(Invitation invitation, CancellationToken cancellationToken);

    Task SaveAsync(Invitation invitation, CancellationToken cancellationToken);

    Task SaveAllAsync(IReadOnlyCollection<Invitation> invitations, CancellationToken cancellationToken);

    Task MarkSentAsync(Guid invitationId, DateTimeOffset now, CancellationToken cancellationToken);
}
```

`SaveAsync` actualiza estado de negocio y no pisa `LastSentAt`/`SendCount`. `SaveAllAsync` aplica el mismo mapping a un conjunto y hace **un** `SaveChangesAsync` (listado de expiraciones). No es un repositorio genérico. `MarkSentAsync` actualiza esos campos de delivery en la fila de invitación. `ListAsync` no persiste expiración: Application llama `Expire` y `SaveAllAsync`.

`FindByIdAsync(Guid, CancellationToken)` existe porque `ProcessInvitationOutbox` solo tiene `ClaimedOutboxWork.InvitationId`. El overload con `kind` + `campaignId` no se puede reutilizar ahí sin inventar esos filtros o filtrarlos al work item del outbox, lo que mezclaría transporte con el agregado. Revoke/resend siguen usando el overload acotado para no cargar una invitación de campaña como de plataforma.

Revoke sobre invitación ya caducada: `Revoke` puede pasar el agregado a `Expired` en memoria; ese camino de conflicto **no** llama a `SaveAsync`. El listado posterior persiste la expiración en batch.

`LastSentAt` sale de la fila Invitation. El estado de entrega sale del outbox como `InvitationDeliveryStatus` (siguiente port), no como string.

### 8. `IInvitationOutboxStore`

Port del ciclo de vida del mensaje de outbox. Application ve `ClaimedOutboxWork`, nunca `InvitationOutboxMessage`.

```csharp
public enum InvitationDeliveryStatus
{
    Pending,
    Sent,
    Discarded,
    Failed,
}

public sealed record ClaimedOutboxWork(
    Guid OutboxId,
    Guid InvitationId,
    string EncryptedToken);

public interface IInvitationOutboxStore
{
    Task EnqueueAsync(
        Guid invitationId,
        string encryptedToken,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task<ClaimedOutboxWork?> TryClaimNextAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task MarkProcessedAsync(
        Guid outboxId,
        string providerMessageId,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task MarkDiscardedAsync(
        Guid outboxId,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task MarkFailedAsync(
        Guid outboxId,
        string errorCode,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, InvitationDeliveryStatus>> GetDeliveryStatusesAsync(
        IReadOnlyCollection<Guid> invitationIds,
        CancellationToken cancellationToken);
}
```

`InvitationDeliveryStatus` es un tipo de Application. `InvitationSummary.DeliveryStatus` usa el enum, no `string`. Los literales HTTP (`"pending"`, `"sent"`, `"discarded"`, `"failed"`) se mapean **solo** en `Api` (`*HttpMapping`). El store no devuelve strings de contrato.

`GetDeliveryStatusesAsync` proyecta el mensaje de outbox más reciente al enum. No es CRUD genérico; es la lectura que el caso de uso de list ya necesita.

`TryClaimNextAsync` encapsula el acquire actual (`LeaseUntil = now + 1 minute` + `SaveChanges`) para no cambiar el comportamiento de una réplica.

TOCTOU si hay dos réplicas: documentado (ADR-0003 opera con una). No `SKIP LOCKED`, no locks distribuidos. El port no impide añadir `FOR UPDATE SKIP LOCKED` más adelante solo en Infrastructure.

### 9. Mapping Invitation ↔ InvitationRecord

| Campo | Destino | Motivo |
|---|---|---|
| Id, Kind, RecipientEmail, CampaignId, TokenHash, IssuedAt, ExpiresAt, Status, AcceptedAt, RevokedAt | Domain `Invitation` | ciclo de vida |
| IssuedByUserId, AcceptedByUserId | Domain `Invitation` | auditoría del agregado; ya están en la tabla |
| LastSentAt, SendCount | solo `InvitationRecord` | delivery; `InvitationSummary.LastSentAt` sale de `InvitationListItem` |
| EncryptedToken, lease, attempts, provider id | `InvitationOutboxMessage` | transporte |

Mapping manual estático en Infrastructure (`InvitationPersistenceMapping`). Sin AutoMapper.

Domain gana:

- `Restore(...)` interno (mismo ensamblado) para reconstitución desde el record.
- `IssuePlatform` / `IssueCampaign` reciben `issuedByUserId`.
- `Accept(string token, Guid acceptedByUserId, DateTimeOffset now)` — producción tiene el token en el command; deja de usarse `InvitationRecord.MarkAccepted`.
- `Revoke` y `Expire` ya existen; se usan en producción.
- `InvitationRecord` pierde la máquina de estados de negocio. Puede conservar setters internos de mapping y `MarkSent` (delivery).

Tests de `InvitationTests` se actualizan a la firma de `Accept` con usuario.

### 10. Worker y `ProcessInvitationOutbox`

No se introduce `IProcessInvitationOutbox`. El único adaptador de entrada es el hosted service. Una interfaz serviría solo para DI o para mockear el worker; el valor de test está en el caso de uso con fakes de stores, no en abstraer la clase.

`ProcessInvitationOutbox` (Application, `sealed`) y **consistencia tras el envío**:

1. **Claim/lease** — `TryClaimNextAsync` confirma el lease y **cierra** la transacción SQL. El lease debe ser visible antes de hablar con Brevo.
2. **Descartar si ya no aplica** (caducada / no pending) — trabajo local: `Expire` + `Save` e `MarkDiscarded` dentro de `ITransactionalBoundary` (sin llamada externa).
3. **Envío** — `Compose` + `ITransactionalEmailSender.SendAsync`. **Ninguna transacción PostgreSQL abierta.** No se mantiene un `BeginTransaction` durante Brevo.
4. **Éxito local atómico** — cuando `SendAsync` termina bien, y solo entonces:

```csharp
await transactions.ExecuteSerializableAsync(async ct =>
{
    await outbox.MarkProcessedAsync(work.OutboxId, receipt.ProviderMessageId, now, ct);
    await invitations.MarkSentAsync(work.InvitationId, now, ct);
}, cancellationToken);
```

Ambas escrituras están en la misma BD; el boundary las hace atómicas. Serializable es más fuerte de lo necesario aquí: se reutiliza el único primitive transaccional del diseño, no se añade un segundo UoW. El tx cubre solo los dos UPDATEs, no el HTTP a Brevo.

5. **Fallo de proveedor** — `MarkFailedAsync` solo (outbox). No `MarkSent`.

At-least-once: si Brevo acepta el correo y el persist local posterior falla, un reintento puede reenviar. Eso ya es la semántica del outbox; no se “arregla” con una transacción distribuida. Lo que sí se evita es outbox processed sin `LastSentAt` (o al revés) cuando ambas filas se pueden escribir juntas.

`InvitationOutboxWorker` se mueve a `Infrastructure/Background/`. Crea un scope, resuelve `ProcessInvitationOutbox`, llama `ProcessNextAsync`. Solo: bucle, delay 5s/30s, catch de base de datos no disponible. Sin EF, sin `Expire`, sin componer correo.

Registro: `AddScoped<ProcessInvitationOutbox>()` (el worker es singleton y abre scope por iteración, como hoy).

### 11. Email composer

`InvitationEmailComposer.Compose` recibe Domain `Invitation` (o un DTO Application con `Kind` + `RecipientEmail`). Deja de importar `Infrastructure.Persistence`.

`ITransactionalEmailSender` / `BrevoEmailSender` / `BrevoOptions` no cambian. Esa interfaz sí es un output port real (proveedor sustituible, ADR-0002).

### 12. `SessionAuthenticationHandler`

Permanece en Infrastructure. **Reutiliza** `IIdentityStore.FindActiveByTokenHashAsync`: el join sesión+usuario ya no se duplica. Infraestructura → port de Application es legal. No se mueve a Application.

### 13. Telemetry

`IdentityTelemetry` se mueve a `Application/Identity/` conservando `MeterName` y nombres de instrumentos (`identity.bootstrap.completions`, `identity.login.*`, `identity.invitations.*`). `Program.cs` sigue registrando el mismo meter.

`ApiTelemetry` permanece junto al endpoint de status (host/Infrastructure). No se tocan dashboards Grafana.

### 14. `CampaignDbContext`

Sigue en `Infrastructure/Persistence/`. Lo usan: stores, `PostgresHealthCheck`, migraciones, tests de persistencia. No se inyecta en Application ni en el endpoint de status. Config fluent permanece en `OnModelCreating`.

Sin `AsNoTracking` indiscriminado. Las lecturas que **devuelven modelos a Application** (`FindByEmail`, `FindSessionById`, find/list de invitaciones, delivery statuses) no dejan esas instancias tracked. Los mutadores recargan o adjuntan explícitamente las filas de esa operación y aíslan `SaveChangesAsync`. `FindActiveByTokenHashAsync` y lecturas de claim de outbox que no mutan el agregado pueden no trackear. Flujos que mutan (login, expire, accept) persisten por operaciones explícitas, no por side-effects del tracker. Sin `xmin`/rowversion.

### 15. Transaction boundaries (uso)

| Operación | Boundary | Stores |
|---|---|---|
| Bootstrap | Serializable | `IIdentityStore` |
| Issue / resend | Serializable | `IInvitationStore` + `IInvitationOutboxStore` |
| Accept | Serializable | identity + invitation (+ membership) |
| Preview expire, list expire, revoke, logout | ninguno (como hoy) | el store que corresponda; list usa `SaveAllAsync` |
| Login | ninguno; `PersistLoginAsync` = un SaveChanges aislado a hash + sesión | `IIdentityStore` |
| Outbox claim (lease) | ninguno; SaveChanges corto, **antes** de Brevo | `IInvitationOutboxStore` |
| Outbox descarte local | Serializable | outbox `MarkDiscarded` + invitation `Save` |
| Outbox éxito tras Brevo | Serializable **después** de `SendAsync`; nunca durante Brevo | `MarkProcessed` + `MarkSent` |
| Outbox fallo de proveedor | ninguno | `MarkFailed` |

### 16. DI / composition root

No hay capa Composition. Carpeta `apps/api/Composition/` en el host:

- `AddApplication()` — servicios de caso de uso (incluido `ProcessInvitationOutbox` concreto), protector, composer.
- `AddPersistence()` — DbContext `UseNpgsql`, `IIdentityStore`, `IInvitationStore`, `IInvitationOutboxStore`, `ITransactionalBoundary`, health check.
- `AddEmail()` — `BrevoOptions` + `ITransactionalEmailSender`.
- Connection string / secretos PostgreSQL salen de `Program.cs` a un tipo estático del host.

`IdentitySecurityOptions` permanece en Application; `FromConfiguration` pasa a composition.

`Program.cs` conserva pipeline HTTP, CORS, rate limiting, OTEL, auth scheme, `MapControllers`, health endpoints, status, flag de migraciones.

### 17. Platform status

No hay `IDatabaseAvailability` en Application: la conectividad de PostgreSQL es operacional.

El endpoint (host) inyecta `HealthCheckService` y evalúa el check `postgres` ya registrado con tag `ready` (`PostgresHealthCheck`). Mapeo:

- `HealthStatus.Healthy` → `dependencies.database = "connected"`, `status = "operational"`
- cualquier otro resultado o ausencia de la entrada → `"unavailable"` / `"degraded"`

JSON **idéntico** al actual (`service`, `status`, `environment`, `version`, `generatedAt`, `dependencies.database`, `dependencies.telemetry = "otlp"`). `PostgresHealthCheck` sigue usando `CampaignDbContext` porque pertenece a Infrastructure. El endpoint no inyecta `CampaignDbContext`.

### 18. Testing strategy

- **Unitarios Application:** fakes in-memory de `IIdentityStore`, `IInvitationStore`, `IInvitationOutboxStore`, `ITransactionalBoundary` (el fake serializable ejecuta el delegate y restaura estado si el delegate lanza). Cubren accept (éxito y rollback) y `ProcessInvitationOutbox` (discard, fallo de proveedor, processed+MarkSent atómicos). `PersistLoginAsync` del fake actualiza solo `PasswordHash` (si viene) e inserta la sesión. No mockear `DbContext`/`DbSet`.
- **Mapping:** `Invitation` ↔ `InvitationRecord` en tests de Infrastructure/integración. HTTP: `InvitationDeliveryStatus` → `"pending"`/`"sent"`/`"discarded"`/`"failed"` solo en `Api`.
- **Stores + boundary:** PostgreSQL real; un test de issue cubre invitation+outbox en la misma transacción y el rollback si `Enqueue` falla; un resend exitoso contra stores reales; un test de login cubre rehash+sesión en un SaveChanges **sin** cambiar Email/DisplayName; tests de aislamiento de `AddSessionAsync`/`SaveSessionAsync`; un test de outbox cubre discard, fallo de proveedor (sin `LastSentAt`), y processed+MarkSent juntos **después** del send (y rollback si `MarkSent` falla).
- **HTTP e InvitationFlow existentes:** verdes, sin cambio de contrato. Status: `/api/v1/platform/status` healthy y degraded; `/health/ready` healthy y unhealthy.
- Tests de integración pueden seguir resolviendo `CampaignDbContext` para seed/assert de outbox.

### 19. Architecture enforcement (safeguard temporal)

Un test en `api-tests` lee `*.cs` bajo `Application/` y `Domain/` y falla si aparecen `using` o nombres cualificados prohibidos. **No es una frontera de compilador:** todo vive en un ensamblado. Limitaciones: puede omitir `global::`, nombres fully-qualified sin `using`, código generado, strings/comentarios, y no ve referencias de ensamblado. Es un freno temporal a regresiones obvias, no un sustituto de proyectos separados o analizadores. Sin librería nueva (no NetArchTest).

Excepciones: ninguna en Application/Domain. `Composition/` y `Program.cs` quedan fuera de esas carpetas.

### 20. Movimientos de tipos

| Tipo | Acción |
|---|---|
| `IdentityTelemetry` | mover a Application |
| `ApiTelemetry` | mantener |
| `InvitationOutboxWorker` | mover a `Infrastructure/Background/` y adelgazar |
| `IdentitySecurityOptions.FromConfiguration` | composition |
| `Brevo*` | mantener |
| `PostgresHealthCheck` | mantener; el status lo consume vía `HealthCheckService` |
| `InvitationRecord` / outbox message | mantener, encapsular |
| `CampaignDbContext` | mantener, encapsular |
| `IdentityStore` / `InvitationStore` / `InvitationOutboxStore` / `TransactionalBoundary` | nuevos en Persistence |

No crear carpetas vacías ni tipos forwarding.

### 21. Riesgos y trade-offs

- [Stores que `SaveChanges` por operación] → EF persiste todo lo tracked. Mitigación: cada mutador modifica explícitamente sus entidades y aísla `SaveChanges`; tests de `AddSessionAsync`/`SaveSessionAsync`/`PersistLoginAsync`.
- [Brevo OK, persist local KO] → reenvío posible (at-least-once). Mitigación: no abrir tx SQL durante Brevo; processed+MarkSent atómicos después.
- [Issue en dos stores] → ambos deben compartir DbContext scoped. Mitigación: test de integración serializable invitation+outbox.
- [Domain `Accept` vs lookup por hash] → Application carga por hash y luego `Accept(token, userId, now)`. Mitigación: tests de token inválido en Domain.
- [Worker concreto] → menos mockeable; el worker es un bucle. Mitigación: tests del caso de uso con fakes de stores.
- [HealthCheckService vs CanConnect directo] → el check `postgres` ya usa `CanConnectAsync`. Mitigación: mapear Healthy/no-Healthy al JSON actual; cubrir con el test HTTP de status.
- [Safeguard textual] → falsos negativos. Mitigación: documentado; no se vende como enforcement real.
- [TOCTOU outbox] → deuda explícita; una réplica.

### 22. Deuda deliberadamente no solucionada

- Constructores vacíos EF en entidades de identidad.
- Un solo `.csproj` (el test de namespaces no lo cambia).
- Lease de outbox no atómico entre réplicas.
- `Database:ApplyMigrations` al arranque (ADR-0003).
- Extraer fluent config a `IEntityTypeConfiguration`.

### 23. Rollback y migración

Sin migración de esquema. Rollback = revertir el slice de git. Orden: ver `tasks.md`. No eliminar el camino viejo de un slice hasta que compile y sus tests estén verdes.

### 24. Orden incremental

1. `IIdentityStore` (con `PersistLoginAsync`) + impl + tests.
2. `ITransactionalBoundary` + bootstrap.
3. Migrar `IdentityService`; quitar DbContext de identity.
4. Mover `IdentityTelemetry`; extraer `FromConfiguration`.
5. `IInvitationStore` + `IInvitationOutboxStore` + mapping.
6. Migrar issue/resend (ambos stores + boundary), luego list/revoke/accept.
7. Eliminar `InvitationRecord` de Application; composer.
8. `ProcessInvitationOutbox` concreto; mover worker.
9. Carpetas Infrastructure; composition; status vía health checks; safeguard de namespaces.

## Risks / Trade-offs

Ver Decisions §21–22. Trade-off central: ports por contexto (identity, invitation, outbox) + boundary mínimo frente a `IAppDbContext`. Se elige la frontera real. Un segundo port de outbox evita un God Store; `GetDeliveryStatusesAsync` es la única lectura cruzada justificada.

## Migration Plan

Refactor interno por fases en `tasks.md`. Cada fase: build + tests del slice. Sin cambio de esquema ni de clientes. Rollback por revert de commits del slice.

## Open Questions

Ninguna que altere el enfoque: login persiste solo rehash+sesión; delivery es `InvitationDeliveryStatus` en Application; Brevo queda fuera de cualquier transacción SQL; processed+MarkSent son atómicos después del envío.
