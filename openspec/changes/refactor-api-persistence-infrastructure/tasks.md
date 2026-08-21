## 1. Application — `IIdentityStore`

- [x] 1.1 Añadir `IIdentityStore` y `ActiveUserSession` en Application con las firmas de `design.md` (`PersistLoginAsync(userId, rehashedPasswordHash, newSession)`; `AddSessionAsync` no actualiza usuarios) y verificar que `dotnet build apps/api` compila
- [x] 1.2 Implementar `IdentityStore` en `Infrastructure/Persistence/`: `PersistLoginAsync` actualiza **solo** `PasswordHash` cuando el hash no es null e inserta la sesión (un SaveChanges; ningún otro escalar de `UserAccount`) y verificar `dotnet build apps/api`
- [x] 1.3 Registrar `IIdentityStore` como scoped junto a `CampaignDbContext` en `Program.cs` y verificar que la API arranca en tests de integración existentes (`dotnet test apps/api-tests --filter Identity`)
- [x] 1.4 Añadir tests de integración PostgreSQL del store (HasAnyUsers, FindByEmail, AddUser, `PersistLoginAsync` con rehash+sesión en un SaveChanges **sin** cambiar Email/DisplayName, y sin rehash solo inserta sesión, DM/member) usando `PostgreSqlIntegrationTestHelper` y verificar que pasan

## 2. Application — `ITransactionalBoundary`

- [x] 2.1 Añadir `ITransactionalBoundary` en Application (overloads `Task` y `Task<T>`) y verificar `dotnet build apps/api`
- [x] 2.2 Implementar el boundary en Infrastructure con `BeginTransactionAsync` Serializable sobre el `CampaignDbContext` scoped y verificar `dotnet build apps/api`
- [x] 2.3 Registrar el boundary como scoped y añadir un test de integración que ejecute dos escrituras en el mismo scope (commit y rollback) y verificar que el test pasa
- [x] 2.4 Verificar `dotnet build apps/api` y que Application sigue compilando sin `IsolationLevel` ni EF en tipos nuevos (`rg IsolationLevel apps/api/Application` sin matches en el port)

## 3. Application — migrar `IdentityService`

- [x] 3.1 Cambiar `IdentityService` a `IIdentityStore` + `ITransactionalBoundary` para bootstrap (quitar `CampaignDbContext` de este método) y verificar tests de bootstrap
- [x] 3.2 Migrar login a `FindByEmail` + `PersistLoginAsync(userId, rehashedPasswordHash o null, session)` y logout a `FindSessionById`+`SaveSession`; verificar que login no hace UPDATE genérico de `UserAccount` y que los tests de login/logout pasan
- [x] 3.3 Quitar `using Microsoft.EntityFrameworkCore` y `Infrastructure.Persistence` de `IdentityService` y verificar `dotnet build apps/api`
- [x] 3.4 Apuntar `SessionAuthenticationHandler` a `FindActiveByTokenHashAsync` y verificar tests de sesión/autorización existentes

## 4. Application — telemetría y options de identity

- [x] 4.1 Mover `IdentityTelemetry` a `Application/Identity/` conservando nombres de meter e instrumentos y verificar que `Program.cs` sigue registrando `IdentityTelemetry.MeterName`
- [x] 4.2 Eliminar usos Application → `Infrastructure.Observability` en identity/invitaciones (invitaciones pueden seguir un `using` temporal hasta la fase 7) y verificar `dotnet build apps/api`
- [x] 4.3 Extraer `IdentitySecurityOptions.FromConfiguration` al host/composition; el tipo permanece en Application y verificar que el arranque de tests sigue validando bootstrap/outbox keys

## 5. Application — `IInvitationStore` e `IInvitationOutboxStore`

- [x] 5.1 Añadir `InvitationDeliveryStatus`, `IInvitationStore` + `InvitationListItem` e `IInvitationOutboxStore` + `ClaimedOutboxWork` en Application (`GetDeliveryStatusesAsync` devuelve el enum, no `string`) y verificar `dotnet build apps/api`
- [x] 5.2 Implementar `InvitationStore` (pending, token hash, id, recent issues, list, Add/Save/MarkSent) y `InvitationOutboxStore` (Enqueue, TryClaimNext, MarkProcessed/Discarded/Failed, GetDeliveryStatuses → enum) sobre `CampaignDbContext` y verificar `dotnet build apps/api`
- [x] 5.3 Registrar ambos stores scoped y añadir tests de integración PostgreSQL: find by hash; enqueue+claim; GetDeliveryStatuses; y verificar que pasan
- [x] 5.4 Verificar `dotnet test apps/api-tests --filter Invitation` sigue verde; no eliminar aún el uso de DbContext en los servicios

## 6. Domain / Infrastructure — mapping Invitation ↔ InvitationRecord

- [x] 6.1 Extender Domain `Invitation` (`Restore`, `issuedByUserId`/`acceptedByUserId`, `Accept(token, userId, now)`) y verificar que `InvitationTests` se actualizan y pasan
- [x] 6.2 Añadir mapping manual `InvitationPersistenceMapping` en Infrastructure y tests que redondean Issue → Record → Restore y verificar que pasan
- [x] 6.3 Vaciar la máquina de estados duplicada de `InvitationRecord` (`MarkAccepted`/`Revoke`/`MarkExpired` de negocio) dejando mapping + `MarkSent` y verificar `dotnet build apps/api`
- [x] 6.4 Conectar `InvitationStore` al mapping Domain (Add/Save/Find devuelven `Invitation`) y verificar tests del store

## 7. Application — migrar casos de uso de invitaciones

- [x] 7.1 Migrar `InvitationIssuanceCore` (issue/resend) a `IInvitationStore` + `IInvitationOutboxStore` + `ITransactionalBoundary` (`Add`/`Save` + `Enqueue` dentro del boundary) y verificar issue/resend en integración (invitation y outbox en la misma transacción)
- [x] 7.2 Migrar list/revoke/`IsCampaignDm` (list: `Expire`+`Save` en Application; `InvitationDeliveryStatus` vía `GetDeliveryStatusesAsync`) y verificar list/revoke
- [x] 7.3 Migrar `InvitationAcceptanceService` (preview/accept) a invitation store + identity store + boundary (`AddUser`/`AddSession` explícitos, no tracker) y verificar preview/accept
- [x] 7.4 Cambiar `InvitationSummary.DeliveryStatus` al enum; mapear a `"pending"`/`"sent"`/`"discarded"`/`"failed"` **solo** en `Api/*HttpMapping`; verificar JSON de list/issue/resend
- [x] 7.5 Migrar `PlatformInvitationService` y `CampaignInvitationService` (FindById/resend) y verificar tests HTTP de platform/campaign invitations
- [x] 7.6 Verificar `dotnet build apps/api` y `dotnet test apps/api-tests --filter Invitation`

## 8. Application — eliminar `InvitationRecord` de Application

- [x] 8.1 Eliminar todo `using` de `Infrastructure.Persistence` y tipos `InvitationRecord`/`InvitationOutboxMessage` bajo `apps/api/Application` y verificar `rg InvitationRecord apps/api/Application` vacío
- [x] 8.2 Eliminar `using Microsoft.EntityFrameworkCore` de Application (salvo que quede el worker, que se mueve en la fase 11) y verificar `dotnet build apps/api`
- [x] 8.3 Verificar tests de integración de invitaciones e identity verdes

## 9. Application — `InvitationEmailComposer`

- [x] 9.1 Cambiar `Compose` para recibir Domain `Invitation` (no `InvitationRecord`) y verificar `dotnet build apps/api`
- [x] 9.2 Verificar que el composer no importa Infrastructure y que los tests de correo/outbox existentes siguen pasando

## 10. Application / Infrastructure — outbox y caso de uso concreto

- [x] 10.1 Completar `IInvitationOutboxStore` (claim/processed/discarded/failed) si quedó algo de la fase 5 y documentar el TOCTOU multi-réplica en el store (sin `SKIP LOCKED` ni locks distribuidos); verificar tests de store de outbox
- [x] 10.2 Añadir `ProcessInvitationOutbox` concreto: claim (commit lease) → Brevo **fuera** de tx SQL → `ExecuteSerializableAsync` con `MarkProcessed`+`MarkSent`; descarte local atómico; `MarkFailed` sin MarkSent; verificar un test de integración de mensaje pending
- [x] 10.3 Registrar `AddScoped<ProcessInvitationOutbox>()` y añadir un test que demuestre que no hay transacción abierta durante el send (processed+MarkSent ocurren juntos después) y verificar `dotnet build apps/api`

## 11. Infrastructure — mover worker

- [x] 11.1 Mover `InvitationOutboxWorker` a `Infrastructure/Background/` como bucle/delay/catch que abre un scope y llama `ProcessInvitationOutbox.ProcessNextAsync` y verificar `dotnet build apps/api`
- [x] 11.2 Quitar EF, `Expire`, composer y `CampaignDbContext` del worker y verificar `rg CampaignDbContext apps/api/Application` vacío
- [x] 11.3 Actualizar el registro `AddHostedService` y verificar el flujo de outbox en `InvitationFlowIntegrationTests`

## 12. Infrastructure — reorganización de carpetas

- [x] 12.1 Dejar Persistence con DbContext, records, `IdentityStore`, `InvitationStore`, `InvitationOutboxStore`, boundary, mapping, migrations, health; Identity handler; Email; Background worker; Observability solo `ApiTelemetry` y verificar que no hay carpetas vacías
- [x] 12.2 Confirmar que ningún tipo se movió solo por estética (lista de archivos = `design.md` §20) y verificar `dotnet build apps/api`

## 13. Host — composition / DI

- [x] 13.1 Extraer connection string Npgsql y `AddDbContext` a `Composition/` (o extensions del host) y verificar tests de integración que levantan el host
- [x] 13.2 Extraer `AddPersistence` (tres stores, boundary, health), `AddApplication` (servicios, `ProcessInvitationOutbox`, protector, composer) y `AddEmail` y verificar que `Program.cs` ya no registra esos concretos uno a uno
- [x] 13.3 Dejar en `Program.cs` el pipeline HTTP, CORS, rate limits, OTEL, auth, controllers, health, flag de migraciones y verificar `dotnet build apps/api`

## 14. Host — platform status vía health checks

- [x] 14.1 Cambiar `GET /api/v1/platform/status` para inyectar `HealthCheckService` (check `postgres` / tag `ready`) en lugar de `CampaignDbContext` y verificar que no existe `IDatabaseAvailability` en Application (`rg IDatabaseAvailability apps/api` vacío)
- [x] 14.2 Mapear `HealthStatus.Healthy` → `connected`/`operational` y el resto → `unavailable`/`degraded` y verificar el JSON actual (service, status, dependencies.database, telemetry) con el test HTTP existente
- [x] 14.3 Verificar que `PostgresHealthCheck` sigue usando `CampaignDbContext` en Infrastructure y que `/health/ready` sigue verde en tests

## 15. Tests — safeguard temporal de namespaces

- [x] 15.1 Añadir un test en `api-tests` que lea fuentes de `Application/` y falle si aparecen `Infrastructure`, `Microsoft.EntityFrameworkCore` o `Npgsql`, con un comentario de que es safeguard temporal (un ensamblado, sin frontera de compilador, posibles falsos negativos) y verificar que el test pasa
- [x] 15.2 Añadir el análogo para `Domain/` (además Application y Api) y verificar que el test pasa
- [x] 15.3 No añadir NetArchTest ni otro paquete; verificar que el `.csproj` de tests no tiene nueva dependencia de architecture testing

## 16. Infrastructure — EF hardening seguro

- [x] 16.1 Revisar que `PersistLoginAsync` no escribe Email/DisplayName/otros escalares y que `AddSessionAsync` no actualiza usuarios; verificar tests de login/sesión
- [x] 16.2 No añadir `xmin`/rowversion ni `AsNoTracking` en list/preview/accept y verificar que no hay migración nueva (`ls apps/api/Infrastructure/Persistence/Migrations` igual)
- [x] 16.3 Verificar que `SaveChangesAsync` sigue ocurriendo solo dentro de los stores/boundary

## 17. Verificación backend completa

- [x] 17.1 Ejecutar `dotnet build apps/api` sin errores ni warnings nuevos y verificar éxito
- [x] 17.2 Ejecutar `dotnet test apps/api-tests` completo (unitarios, HTTP, PostgreSQL) y verificar éxito
- [x] 17.3 Confirmar ausencia de `IAppDbContext`, `IRepository<`, `IGenericRepository`, `IUnitOfWork`, `IDatabaseAvailability`, `IProcessInvitationOutbox` en `apps/api` (`rg` vacío) y verificar el resultado
- [x] 17.4 Confirmar `rg CampaignDbContext apps/api/Application` y `rg InvitationRecord apps/api/Application` vacíos; `CampaignDbContext` solo en Infrastructure, Composition/Program (migraciones) y tests de persistencia — no en el endpoint de status

## 18. Verificación de boundaries (criterios finales)

- [x] 18.1 `rg Microsoft.EntityFrameworkCore apps/api/Application` y `rg Npgsql apps/api/Application` vacíos y verificar
- [x] 18.2 `rg Microsoft.EntityFrameworkCore apps/api/Domain` y referencias Infrastructure/Application/Api en Domain vacías y verificar
- [x] 18.3 Worker en `Infrastructure/Background/` sin lógica de Expire/compose y con dependencia concreta de `ProcessInvitationOutbox`; verificar por lectura del archivo
- [x] 18.4 Contrato HTTP intacto: tests de controllers/flujo de invitaciones, identity y platform status verdes (ya cubiertos por 17.2) y verificar que no hay migración EF nueva

## 19. Correcciones de verificación

- [x] 19.1 Aislar `SaveChangesAsync` de `IdentityStore`, `InvitationStore` e `InvitationOutboxStore` a las entidades de cada operación; `SaveSessionAsync` adjunta/actualiza la sesión recibida; lecturas hacia Application no dejan mutaciones tracked accidentales
- [x] 19.2 `InvitationRecord` permanece interno de Infrastructure; Application/Domain no lo referencian; transiciones en Domain `Invitation`; mapping en `InvitationPersistenceMapping`
- [x] 19.3 Revoke de invitación expirada no persiste `Expired` en el conflict path
- [x] 19.4 Listado de expiraciones usa `SaveAllAsync` con un único `SaveChangesAsync`
- [x] 19.5 Tests HTTP de `/api/v1/platform/status` healthy/degraded y `/health/ready`
- [x] 19.6 Tests reales de atomicidad Invitation+Outbox (éxito y rollback de enqueue) y resend exitoso contra stores reales
- [x] 19.7 Tests de `ProcessInvitationOutbox`: discard, fallo de proveedor (sin `LastSentAt`), processed+MarkSent atómicos
- [x] 19.8 Tests HTTP del mapping Pending/Sent/Discarded/Failed
- [x] 19.9 Documentar `FindByIdAsync(Guid, CancellationToken)` en `design.md`
- [x] 19.10 Tests Application con fakes para accept y `ProcessInvitationOutbox`
