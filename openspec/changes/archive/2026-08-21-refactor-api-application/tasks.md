## 1. Excepciones centralizadas (Api)

- [x] 1.1 Implementar `Api/Middleware/ApiExceptionHandler.cs` (`IExceptionHandler`) mapeando `InvitationConflictException`, `InvitationStateException`, `InvitationRateLimitException` y casos acordados en `design.md`; registrar en `Program.cs` y verificar que devuelve los mismos ProblemDetails que los endpoints actuales
- [x] 1.2 Eliminar try/catch duplicados de issue/resend al migrar controllers; verificar con test de controller o integración que 409/429 siguen iguales

## 2. Contratos HTTP por vertical (Api/Contracts)

- [x] 2.1 Crear `IdentityHttpContracts.cs` (`BootstrapRequest`, `LoginRequest`, `SessionResponse`, `UserResponse`, bootstrap status DTO) — **sin** archivo Shared genérico
- [x] 2.2 Crear `InvitationAcceptanceHttpContracts.cs` (preview/accept; reutiliza `UserResponse` desde Identity)
- [x] 2.3 Crear `PlatformInvitationHttpContracts.cs` y `CampaignInvitationHttpContracts.cs` (issue request + `InvitationResponse` compartido solo si campos idénticos)
- [x] 2.4 Verificar `dotnet build` tras mover records desde `IdentityInvitationEndpoints.cs`

## 3. Tipos Application pragmáticos

- [x] 3.1 Identity: `BootstrapAccountCommand`, `UserProfile`, `LoginCommand`, `LoginOutcome`, `LogoutCommand` en `Application/Identity/`
- [x] 3.2 Invitations: commands acotados por servicio — `PreviewInvitationCommand`, `AcceptInvitationCommand`, `AuthenticatedActor`, `AcceptInvitationResult`, `InvitationAcceptanceOutcome`; platform/campaign issue/resend/revoke/list como `*Command` (no `*Query` para list/preview)
- [x] 3.3 Refactorizar `AcceptInvitationResult` con namespace Application y sin tipos Api
- [x] 3.4 Confirmar `rg 'DndCampaign\.Api\.Api' apps/api/Application/` vacío al final del apply

## 4. Mapeadores HTTP (uno por controller)

- [x] 4.1 `IdentityHttpMapping.cs`
- [x] 4.2 `InvitationAcceptanceHttpMapping.cs`
- [x] 4.3 `PlatformInvitationHttpMapping.cs`
- [x] 4.4 `CampaignInvitationHttpMapping.cs`

## 5. Servicios Application — split (no God Service)

- [x] 5.1 **`IdentityService`:** refactor bootstrap (`BootstrapAccountCommand`→`UserProfile`); añadir `LoginAsync`, `LogoutAsync`; documentar deuda temporal `CampaignDbContext` en summary de clase
- [x] 5.2 **`InvitationAcceptanceService`:** extraer preview (`PreviewInvitationCommand`) y accept (`AcceptInvitationCommand`+`AuthenticatedActor`) desde endpoint gordo; sin `ClaimsPrincipal` en Application
- [x] 5.3 **`PlatformInvitationService`:** extraer list/issue/resend/revoke platform; list como `ListPlatformInvitationsCommand` (side-effect expiración)
- [x] 5.4 **`CampaignInvitationService`:** extraer list/issue/resend/revoke campaign + autorización DM; list como `ListCampaignInvitationsCommand`
- [x] 5.5 Retirar o vaciar `InvitationService` monolítico; actualizar DI en `Program.cs`

## 6. Controllers finos (4 verticales)

- [x] 6.1 **`IdentityController`:** bootstrap, login, logout; **GET me** mapea `ClaimsPrincipal`→`UserResponse` sin servicio; rate limiting y status codes sin cambio
- [x] 6.2 **`InvitationAcceptanceController`:** preview, accept (migrar desde `InvitationController` actual)
- [x] 6.3 **`PlatformInvitationsController`:** GET/POST/resend/DELETE platform; policy `platform-admin`
- [x] 6.4 **`CampaignInvitationsController`:** GET/POST/resend/DELETE campaign; `[Authorize]`
- [x] 6.5 Verificar controllers sin EF, sin LINQ, sin try/catch de invitaciones (delegado a `IExceptionHandler`)

## 7. Eliminar endpoint gordo

- [x] 7.1 Eliminar `IdentityInvitationEndpoints.cs` y `MapIdentityInvitationEndpoints()` de `Program.cs`; verificar `dotnet build`

## 8. Tests — integración/component (Application)

- [x] 8.1 Extender/añadir tests de integración para `IdentityService` (bootstrap, login, logout) con PostgreSQL — **sin** mocks de `DbContext`/`DbSet`
- [x] 8.2 Extender/añadir tests de integración para `InvitationAcceptanceService`, `PlatformInvitationService`, `CampaignInvitationService` (preview estados, accept, list con expiración, revoke 404/409, issue conflict, resend 429)
- [x] 8.3 Mantener `InvitationFlowIntegrationTests` e `IdentitySecurityTests` verdes

## 9. Tests — controllers (Api)

- [x] 9.1 `IdentityControllerTests` — servicios mockeados; status codes bootstrap/login/me
- [x] 9.2 `InvitationAcceptanceControllerTests` — preview, accept matrix (200/400/401/403/410)
- [x] 9.3 `PlatformInvitationsControllerTests` y `CampaignInvitationsControllerTests` — issue 202/409, resend 429, revoke 204/404

## 10. Verificación final

- [x] 10.1 `dotnet build` cero errores
- [x] 10.2 `dotnet test` completo en `apps/api-tests/` sin regresiones
- [x] 10.3 Rutas HTTP idénticas a `proposal.md`
