# Plan 004: Creación de campañas e invitación de usuarios existentes

- Estado: Ejecutado
- Fecha: 2026-08-23
- Especificación: [spec.md](spec.md)
- ADR: [ADR-0006](../../adr/0006-campanas-acceso-e-invitaciones.md)
- Dependencias: completar o estabilizar las tareas de spec 002 que afectan a contratos, persistencia y pruebas de Access
- Validación: aprobada explícitamente por el usuario el 2026-08-23

## Resultado esperado

Una persona autenticada podrá crear una campaña indicando solo su nombre, verla inmediatamente como su único DM y abrir desde ella la gestión de invitaciones. Access permitirá listar o buscar usuarios activos elegibles, seleccionar uno sin exponer su correo completo y emitir la invitación existente mediante su identificador. Cuando el destinatario acepte, verá la campaña como jugador.

Campaigns y Access mantendrán modelos de escritura independientes. Campaigns será propietario de la campaña y `DmUserId`; Access conservará usuarios, invitaciones y concesiones `Player`. La colaboración será síncrona mediante contratos públicos, sin consultas de tablas ajenas ni transacciones compartidas.

## Diagnóstico de partida

- Solo existe el módulo API Access; el host y las fitness functions ya contemplan crecimiento por ensamblados.
- Access persiste `CampaignMembership` con roles `Dm` y `Player`, pero los DM solo se preparan desde tests de componente.
- Los handlers de invitaciones autorizan al DM mediante `ICampaignAccessRepository.IsDmAsync`.
- La aceptación crea una membresía `Player` dentro de la misma transacción de Access.
- La emisión recibe un correo y ya soporta destinatarios registrados o no registrados.
- Angular ya tiene la ruta `/campaigns/:campaignId/invitations`, pero no puede llegar a ella desde una campaña productiva.
- No existen `Campaign`, endpoints de campañas, catálogo de módulos, listado de usuarios ni módulo frontend `campaigns`.
- Las tablas actuales de Access siguen en el esquema por defecto; ADR-0004 exige activar esquemas separados al aparecer el segundo módulo persistente.

## Principios de ejecución

1. **Una sola fuente por invariante.** Campaigns decide campaña y DM; Access decide invitación, elegibilidad y concesión de jugador.
2. **Contratos antes que integración.** Se fijarán los contratos intermodulares y sus tests antes de cambiar los handlers actuales.
3. **Compatibilidad de invitaciones.** El nuevo flujo por `recipientUserId` se añade sin retirar el contrato por correo de personas sin cuenta.
4. **Datos minimizados.** La búsqueda solo devuelve datos necesarios para seleccionar una cuenta y nunca registra sus términos.
5. **Autorización en Application.** Controladores y guards no sustituyen la comprobación de actor, campaña y rol en handlers.
6. **Migraciones antes de consumidores.** Los esquemas y la compatibilidad de datos se validan antes de depender de Campaigns en recorridos productivos.
7. **Incrementos verticales verdes.** Cada fase compila, conserva los flujos de Access y deja un único camino productivo.

## Estructura objetivo de API

```text
apps/api/Modules/
  Access/DndCampaign.Modules.Access/
    Contracts/
      CampaignAccess/
        ICampaignInvitationContext.cs
        IPlayerCampaignAccessReader.cs
    Application/
      Users/
        SearchEligibleUsers.cs
      Invitations/
        InvitationHandlers.cs
    Domain/
      CampaignAccess/
        CampaignMembership.cs

  Campaigns/DndCampaign.Modules.Campaigns/
    CampaignsModule.cs
    Contracts/
    Api/
      Controllers/
    Application/
      Abstractions/
      Campaigns/
        CreateCampaign.cs
        GetCampaign.cs
        ListCampaigns.cs
    Domain/
      Campaigns/
        Campaign.cs
    Infrastructure/
      Persistence/
        CampaignsDbContext.cs
        Configurations/
        Migrations/
      Access/
        CampaignInvitationContext.cs

tests/Modules/Campaigns/DndCampaign.Modules.Campaigns.Tests/
  Architecture/
  Application/
  Component/
  Domain/
  Infrastructure/
```

Los nombres de archivos podrán ajustarse durante las tareas, pero no el ownership ni la dirección de dependencias.

## Modelo funcional y persistencia

### Campaigns

`Campaign` será un agregado con:

- `Id` no vacío;
- `Name` normalizado, entre 3 y 100 caracteres;
- `DmUserId` obligatorio e inmutable durante este incremento;
- `AdventureModuleId` nullable y siempre `null` en el recorrido inicial;
- `CreatedAt` proporcionado por `TimeProvider`.

Los nombres no serán únicos. Campaigns tendrá su propio `CampaignsDbContext`, historial de migraciones y esquema `campaigns`. No existirá foreign key de `DmUserId` hacia Access; la validez del actor procede de la sesión autenticada emitida por Access.

### Access

Las concesiones `Player` continuarán en Access y conservarán unicidad por `(CampaignId, UserId)`. Las filas DM provisionales dejarán de autorizar operaciones. La migración decidirá entre retirarlas o conservarlas como datos legados ignorados, después de comprobar el estado real de bases soportadas.

Las tablas de Access se moverán al esquema `access` con una migración explícita que preserve nombres, datos, índices, constraints e historial. Se probará una base vacía y una base en la versión previa soportada.

## Contratos entre Campaigns y Access

Access publicará dos contratos deliberados:

- `ICampaignInvitationContext`: puerto que los handlers de Access usan para comprobar que una campaña existe y que el actor es su DM.
- `IPlayerCampaignAccessReader`: consulta de identificadores de campaña concedidos a un jugador, sin exponer entidades ni `IQueryable`.

Campaigns referenciará el ensamblado Access y:

- implementará `ICampaignInvitationContext` consultando su propia proyección de campaña;
- consumirá `IPlayerCampaignAccessReader` para combinar campañas dirigidas y campañas jugadas;
- registrará el adaptador mediante `AddCampaignsModule`.

Access no referenciará Campaigns. Si Campaigns no registra el puerto requerido, la aplicación fallará al arrancar o al validar servicios; nunca asumirá permiso ni devolverá una lista global.

Las fitness functions comprobarán:

- `Campaigns -> Access` como única referencia funcional permitida;
- ausencia de `Access -> Campaigns`;
- host limitado a las fachadas públicas;
- contratos sin EF Core, ASP.NET, repositorios, entidades ni tipos mutables;
- ausencia de consultas o mapeos hacia tablas del otro esquema.

## Casos de uso de Campaigns

### Crear campaña

`CreateCampaignCommand` incluirá actor y nombre. El handler validará la identidad autenticada, creará el agregado y confirmará una transacción de Campaigns. La respuesta contendrá el resumen con rol `dm` y `adventureModule: null`.

No se llamará a Access durante la escritura: `DmUserId` del agregado basta para la invariante y evita efectos parciales intermodulares.

### Listar campañas

`ListCampaignsQuery` obtendrá del contrato de Access los identificadores concedidos como jugador y proyectará, sin tracking:

- campañas cuyo `DmUserId` coincide con el actor;
- campañas cuyos identificadores están en sus concesiones `Player`.

La proyección devolverá el rol efectivo y eliminará duplicados de forma defensiva. No modificará estado ni ejecutará `SaveChanges`.

### Consultar campaña

`GetCampaignQuery` distinguirá:

- `404` cuando no existe el identificador;
- `403` cuando existe, pero el actor no es DM ni jugador aceptado;
- `200` con resumen minimizado cuando tiene acceso.

La comprobación se realizará en Application y tendrá cobertura frente a manipulación de identificadores.

## Búsqueda y selección de usuarios en Access

Se añadirá `SearchEligibleCampaignUsersQuery` con `CampaignId`, actor, texto opcional y cursor o página. Antes de consultar usuarios, el handler usará `ICampaignInvitationContext` para exigir que el actor sea DM.

La consulta:

- devolverá una primera página ordenada de forma estable cuando no haya texto;
- buscará de forma case-insensitive por nombre normalizado o correo normalizado;
- exigirá al menos dos caracteres cuando exista texto;
- limitará cada página a 20 resultados y nunca aceptará más de 50;
- excluirá actor, membresías existentes e invitaciones pendientes vigentes;
- proyectará `userId`, `displayName` y `maskedEmail`;
- usará lectura `AsNoTracking` y no materializará expiraciones ni otro estado.

El endpoint tendrá rate limiting por actor y campaña. El texto de búsqueda no formará parte de logs, spans, métricas, errores ni etiquetas.

La noción inicial de activo coincide con una cuenta registrada capaz de autenticarse. No se añadirá un flag sin un caso de uso de suspensión que defina sus transiciones.

## Emisión por usuario existente

`IssueInvitationRequest` admitirá `recipientUserId` además de `email`, exigiendo exactamente uno. El cliente nuevo usará `recipientUserId`; el flujo por correo seguirá disponible para personas no registradas.

El command handler de Access:

1. autorizará al actor como DM mediante `ICampaignInvitationContext`;
2. cargará la cuenta destinataria y comprobará que sigue activa;
3. rechazará actor, miembro existente o invitación pendiente;
4. resolverá internamente el correo normalizado;
5. creará invitación y outbox en la transacción actual de Access.

Las comprobaciones del buscador solo mejoran UX; el command repetirá todas las reglas para evitar carreras o peticiones manipuladas. Reenvío, revocación, preview y aceptación conservarán sus contratos y comportamiento.

## Contrato HTTP

### Campaigns

- `GET /api/v1/campaigns`
- `POST /api/v1/campaigns`
- `GET /api/v1/campaigns/{campaignId}`

`POST` recibirá inicialmente `{ "name": "..." }` y devolverá `201 Created` con cabecera `Location`. Los resúmenes incluirán `id`, `name`, `role`, `adventureModule: null` y `createdAt`.

### Access

- `GET /api/v1/campaigns/{campaignId}/eligible-users?query=...&cursor=...`
- rutas existentes bajo `/api/v1/campaigns/{campaignId}/invitations`

Los resultados elegibles tendrán datos minimizados y metadatos de paginación. La emisión aceptará `{ "recipientUserId": "..." }` o el contrato compatible `{ "email": "..." }`, nunca ambos.

Todos los errores usarán el mapeo `ProblemDetails` existente. Se fijarán mediante tests los códigos `400`, `401`, `403`, `404` y `409` relevantes.

## Estructura objetivo de frontend

```text
apps/web/src/app/modules/
  campaigns/
    public-api.ts
    campaigns.routes.ts
    api/
      campaigns.client.ts
      campaign.contracts.ts
    campaign-list/
      campaign-list.page.*
    campaign-create/
      campaign-create.page.*
    campaign-detail/
      campaign-detail.page.*

  access/
    api/
      invitations.client.ts
      invitation.contracts.ts
    invitation-management/
      campaign-invitations.page.*
      eligible-user-search.*
```

`app.routes.ts` cargará `CAMPAIGNS_ROUTES` de forma diferida. Campaigns será propietario de:

- `/campaigns` para listado y estado vacío;
- `/campaigns/new` para crear con nombre;
- `/campaigns/:campaignId` para resumen y acciones permitidas.

La ruta de invitaciones continuará en `ACCESS_ROUTES`. La página de detalle navegará mediante URL sin importar internals de Access. Los alias y la fitness function se ampliarán para prohibir deep imports y ciclos.

La página de invitaciones sustituirá el campo libre como recorrido principal por:

- primera página de usuarios elegibles;
- búsqueda con debounce, cancelación de peticiones obsoletas y longitud mínima;
- nombre y correo enmascarado;
- selección de un resultado y confirmación de envío;
- estados de carga, sin resultados, error, conflicto y éxito;
- recarga de elegibles e invitaciones después de emitir.

El contrato por correo se conservará en el cliente para compatibilidad, aunque el recorrido principal de este spec seleccione cuentas existentes.

## Observabilidad y privacidad

Campaigns añadirá métricas de creación y consulta con resultados acotados (`success`, `validation`, `forbidden`, `not_found`, `failure`) y duración. Access medirá búsquedas y emisiones por identificador sin usar nombres, correos, consultas o IDs como dimensiones.

Los logs incluirán correlación y tipo de operación, pero no nombre de campaña, texto buscado, resultados, correo, token ni datos de sesión. Se revisarán `ProblemDetails`, trazas HTTP y logs de EF para evitar que query strings o payloads sensibles se registren en producción.

## Estrategia de pruebas

### Unitarios de dominio y Application

- normalización de nombre e invariante del DM;
- creación sin módulo;
- listado combinado con rol correcto;
- `404` frente a `403` en detalle;
- autorización de búsqueda;
- filtros de elegibilidad y enmascarado;
- emisión por usuario, exclusiones y carreras relevantes.

### Integración PostgreSQL

- mapeo y migraciones del esquema `campaigns`;
- migración de Access al esquema `access` desde la versión soportada;
- unicidad de concesiones de jugador;
- consulta paginada y case-insensitive con datos reales;
- transacción de invitación y outbox por `recipientUserId`;
- ausencia de escrituras en queries.

### Componente HTTP

- crear, listar y consultar como DM;
- listar y consultar como jugador aceptado;
- `401`, `403`, `404`, validación y manipulación de identificadores;
- listado/búsqueda solo para DM;
- datos minimizados y exclusiones;
- emisión, aceptación y aparición posterior de la campaña;
- compatibilidad del contrato de invitación por correo.

### Frontend y arquitectura

- clientes HTTP y DTO;
- routing lazy, guards y navegación entre Campaigns y Access;
- listado, vacío, formulario, detalle y errores;
- debounce, cancelación, selección y conflictos del buscador;
- fitness functions de imports para el nuevo módulo;
- build de producción e imagen web.

## Fases de implementación

1. Caracterizar contratos actuales de membresías e invitaciones y cerrar las tareas de spec 002 que sean prerrequisito.
2. Crear contratos intermodulares y fitness functions que fijen la dirección `Campaigns -> Access`.
3. Crear el módulo Campaigns, agregado, persistencia, migraciones y pruebas internas.
4. Implementar creación, listado y detalle de campañas en API.
5. Migrar la autorización DM de invitaciones al puerto implementado por Campaigns y retirar el uso productivo del rol DM provisional de Access.
6. Implementar elegibilidad, búsqueda paginada y emisión por `recipientUserId` en Access.
7. Crear el módulo frontend Campaigns y sus recorridos de listado, creación y detalle.
8. Integrar el selector de usuarios elegibles en la página Access de invitaciones.
9. Completar escenarios end-to-end, concurrencia, migración, observabilidad y seguridad.
10. Verificar Docker, CI, documentación, roadmap, diagramas y ADR; después cerrar el incremento.

Cada fase debe terminar con build y suites aplicables verdes. No se habilitará la navegación productiva hacia Campaigns hasta que la autorización de invitaciones use la fuente DM real.

## Despliegue y reversibilidad

La topología continúa siendo un monolito modular, una aplicación Angular y una instancia PostgreSQL. La imagen API incorporará ambos ensamblados y aplicará migraciones de Access antes de Campaigns en un orden determinista.

La migración de esquema será compatible hacia delante y se validará sobre una copia representativa sin secretos. El rollback de aplicación solo se considerará seguro mientras el binario anterior pueda resolver las tablas movidas; si no es posible, el despliegue requerirá roll-forward y copia de seguridad previa. Esta restricción se documentará en el runbook antes de publicar.

El frontend podrá desplegarse después de que los endpoints estén disponibles. Mientras no lo estén, no se expondrán enlaces a rutas incompletas.

## Riesgos y mitigaciones

| Riesgo | Mitigación |
|---|---|
| Ciclo entre Access y Campaigns | Puerto propiedad de Access implementado por Campaigns, referencia única `Campaigns -> Access` y fitness functions |
| Campaña sin DM | `DmUserId` obligatorio dentro del agregado y transacción local de Campaigns |
| Duplicidad entre DM provisional y real | Retirar su uso productivo, migrar datos deliberadamente y probar autorización contra Campaigns |
| Enumeración de usuarios | Contexto de campaña, autorización DM, paginación, rate limit, datos enmascarados y ausencia de logs |
| Carrera entre búsqueda e invitación | Revalidación autoritativa dentro del command transaccional |
| Regresión del alta de personas sin cuenta | Mantener payload por correo y pruebas de compatibilidad |
| `403` revela existencia | UUID no predecible, respuesta sin datos y decisión de riesgo explícitamente aceptada |
| Migración de esquema rompe despliegue | Pruebas desde versión previa, backup, orden determinista y estrategia roll-forward |

## Validación

El usuario aprobó expresamente el plan el 2026-08-23, incluida la división `DmUserId` en Campaigns / concesiones `Player` en Access, la migración a esquemas separados y el buscador contextual con correos enmascarados. La ejecución y sus evidencias quedan registradas en [tasks.md](tasks.md).
