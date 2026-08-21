# ADR-0004: Arquitectura modular, CQRS ligero y límites de dependencia

- Estado: Aceptado
- Fecha: 2026-08-21
- Decisores: equipo del proyecto
- Alcance: estructura interna del backend, ownership del estado, persistencia, pruebas y gobierno arquitectónico
- Depende de: ADR-0001, ADR-0002 y ADR-0003

## Contexto

ADR-0001 decidió mantener el backend como un monolito modular y definió cuatro responsabilidades lógicas: Domain, Application, Infrastructure y Endpoints. La primera implementación funcional de identidad e invitaciones ha permitido validar el recorrido completo desde HTTP hasta PostgreSQL, pero también ha mostrado que esas responsabilidades están separadas principalmente mediante carpetas y namespaces, no mediante límites compilables.

Actualmente `apps/api` se compila en un único ensamblado. Los endpoints de identidad e invitaciones contienen binding HTTP, autorización, validación, consultas EF Core, transacciones, creación y mutación de entidades, métricas y mapeo de errores. Al mismo tiempo, clases situadas en Application dependen directamente de `CampaignDbContext`, EF Core, registros de persistencia, hosted services y telemetría.

La invitación está representada dos veces:

- `Domain.Invitations.Invitation` contiene emisión, aceptación, revocación y expiración;
- `Infrastructure.Persistence.InvitationRecord` vuelve a implementar el estado persistido y sus transiciones.

La implementación utiliza el modelo de dominio para emitir el token, lo convierte en un registro de persistencia y después ejecuta la aceptación, revocación y expiración sobre el registro. Esto crea dos fuentes de verdad y permite que los tests del dominio validen un comportamiento distinto del recorrido productivo.

También existen consultas que producen escrituras al marcar invitaciones expiradas, y el worker de outbox combina el ciclo de vida del host, resolución de dependencias, acceso EF Core y entrega de correo. La adquisición de un mensaje se realiza mediante lectura y actualización separadas, por lo que no constituye un claim atómico ante varios workers.

La solución y sus requisitos crecerán con campañas, módulos de aventura, NPC, bitácora, misiones y encuentros. Continuar añadiendo funcionalidad a capas globales aumentaría el acoplamiento y permitiría que cualquier caso de uso accediera a las entidades y tablas de cualquier área. Separar ahora el sistema en microservicios, bases de lectura o buses distribuidos añadiría complejidad operativa que el producto no necesita.

Necesitamos una estructura que:

- conserve un único proceso, despliegue y PostgreSQL;
- haga explícito quién es propietario de cada regla y cada dato;
- facilite implementar casos de uso nuevos sin ampliar endpoints o servicios generales;
- permita verificar las dependencias en compilación y en CI;
- preserve las garantías transaccionales del flujo de acceso e invitaciones;
- deje una vía de evolución futura sin anticipar microservicios.

## Decisión

### 1. Módulos de negocio antes que capas globales

El backend se organizará primero por módulos de negocio. Dentro de cada módulo se aplicarán los límites de Domain, Application, Infrastructure y Api.

El código existente de usuarios, credenciales, sesiones, bootstrap, invitaciones, outbox y concesiones de acceso a campañas formará inicialmente un único módulo **Access**. Identidad e invitaciones no se separarán todavía en módulos independientes porque aceptar una invitación puede crear una cuenta, emitir una sesión y conceder acceso a una campaña dentro de una misma operación transaccional.

La pertenencia de una capacidad a un módulo se decidirá por sus invariantes, transacciones y vocabulario, no porque exista una entidad con el mismo nombre. No se crearán módulos de una sola entidad ni un proyecto nuevo por cada tarea.

Los módulos funcionales futuros se incorporarán cuando exista comportamiento suficiente que justifique su frontera. Los candidatos iniciales son:

- `Campaigns`;
- `AdventureCatalog`;
- `Journal`;
- `Missions`;
- `Encounters`.

Esta lista es orientativa. La creación y responsabilidad final de cada módulo requerirá concretar sus invariantes y dependencias en la especificación o ADR correspondiente.

### 2. Proyectos y dirección de dependencias

Cada módulo funcional será un único proyecto y un único ensamblado. Las capas son límites internos del módulo, no proyectos independientes:

```text
Api.Host (composition root)
└── DndCampaign.Modules.Access
    ├── Api
    ├── Application
    ├── Domain
    └── Infrastructure
```

La estructura lógica será:

```text
apps/
  api/
    Program.cs
    Modules/Access/
      DndCampaign.Modules.Access/
        Domain/
          Accounts/
          Sessions/
          Invitations/
          CampaignAccess/
        Application/
          Bootstrap/
          Identity/
          Invitations/
          Ports/
        Infrastructure/
          Authentication/
          Email/
          Outbox/
          Persistence/
        Api/
          Controllers/
          Contracts/

tests/
  DndCampaign.ArchitectureTests/
  Modules/Access/DndCampaign.Modules.Access.Tests/
```

Las responsabilidades serán:

| Límite | Responsabilidad | No puede asumir |
|---|---|---|
| `Api.Host` | Middleware, configuración y composición de módulos | Reglas de negocio, consultas EF o contratos funcionales propios |
| `Access/Api` | Rutas, contratos HTTP, binding y traducción de resultados a HTTP | Transacciones, EF Core o mutación directa de agregados |
| `Access/Application` | Casos de uso, autorización funcional, coordinación y puertos | ASP.NET Core, EF Core, proveedores externos o telemetría concreta |
| `Access/Domain` | Agregados, value objects, políticas, invariantes y eventos de dominio | Persistencia, HTTP, logging o configuración del host |
| `Access/Infrastructure` | EF Core, PostgreSQL, autenticación técnica, correo, outbox y observabilidad | Decidir reglas funcionales o exponer tipos persistentes a otros módulos |

`Api.Host` será el composition root global, pero solo conocerá la fachada pública de cada módulo (`AddAccessModule`, `MapAccessModule` y operaciones de ciclo de vida expresamente publicadas). No referenciará namespaces internos, `DbContext`, controladores ni adaptadores concretos. Los controladores solo dependerán de Application.

Las clases serán `internal` por defecto. El ensamblado expondrá únicamente su fachada y los contratos intermodulares deliberados. `InternalsVisibleTo` se limitará al proyecto de tests del propio módulo. Como las capas comparten ensamblado, sus límites se protegerán mediante fitness functions estáticas por módulo; la compilación protegerá la frontera entre módulos.

### 3. Contratos entre módulos

Un módulo no podrá utilizar entidades, repositorios, `DbContext`, tablas ni namespaces internos de otro módulo. La comunicación se realizará mediante:

- contratos públicos de aplicación para colaboración síncrona dentro del proceso;
- DTO inmutables que no expongan entidades de dominio;
- eventos de integración y outbox cuando se admita consistencia eventual.

Los contratos intermodulares vivirán en un namespace público `Contracts` dentro del ensamblado del módulo propietario y solo se publicarán cuando otro módulo necesite consumirlos. No se creará un proyecto adicional de contratos ni un `SharedKernel` general para acumular tipos comunes. Si algún contrato compartido se necesita, contendrá únicamente DTO inmutables, identificadores tipados o eventos de integración, nunca entidades o repositorios funcionales.

Si una operación necesita modificar dos módulos de forma atómica, se revisará primero el ownership: una invariante transaccional suele indicar que los datos pertenecen al mismo módulo. No se utilizará un `DbContext` global para ocultar una frontera incorrecta. Cuando la consistencia eventual sea válida, se usarán eventos persistidos en outbox. Una transacción compartida entre módulos será excepcional y deberá documentarse explícitamente.

Las concesiones de acceso a campaña permanecerán inicialmente en Access para conservar la atomicidad del flujo de invitación. La futura introducción de Campaigns deberá decidir si Access conserva una proyección de autorización o si parte de ese estado cambia de propietario.

### 4. Ownership de datos y persistencia

Cada módulo será propietario de su modelo de escritura, configuración EF Core y migraciones. Al existir un segundo módulo persistente se utilizará un `DbContext` y un esquema PostgreSQL por módulo, por ejemplo:

```text
access.users
access.sessions
access.invitations
access.invitation_outbox

campaigns.campaigns
```

Esto no implica separar servidores ni cadenas de conexión: todos los módulos continuarán utilizando la misma instancia de PostgreSQL y el mismo despliegue. No se permitirán consultas directas a las tablas de otro módulo. Las relaciones intermodulares se representarán mediante identificadores y contratos; cualquier foreign key entre esquemas deberá evaluarse expresamente por el acoplamiento que introduce.

La primera refactorización podrá conservar temporalmente nombres de tablas y migraciones para reducir el riesgo. El cambio de esquema se hará mediante una migración posterior, una vez estabilizados los límites de Access.

Application dependerá de puertos específicos, no de un repositorio genérico ni de EF Core. Se distinguirán:

- repositorios de escritura que cargan y guardan agregados;
- read stores que proyectan directamente modelos de lectura;
- una unidad de trabajo o decorador transaccional que confirma una vez por comando;
- adaptadores externos, como correo, reloj y protección de secretos.

Las restricciones que protegen invariantes concurrentes también se expresarán en PostgreSQL. Las comprobaciones previas en Application no sustituyen índices únicos, control optimista o una estrategia transaccional verificable.

### 5. CQRS ligero por caso de uso

Se aplicará CQRS lógico dentro de cada módulo:

- cada operación de escritura será un command con un handler;
- cada consulta será una query con un handler;
- comandos y queries podrán usar la misma base de datos;
- no se introducirán event sourcing, read replicas ni un bus distribuido;
- no se exige MediatR ni otra librería de mediator.

Las abstracciones mínimas podrán ser equivalentes a:

```csharp
internal interface ICommandHandler<in TCommand, TResult>
{
    Task<TResult> HandleAsync(TCommand command, CancellationToken cancellationToken);
}

internal interface IQueryHandler<in TQuery, TResult>
{
    Task<TResult> HandleAsync(TQuery query, CancellationToken cancellationToken);
}
```

Los casos de uso iniciales de Access se separarán como mínimo en:

```text
CompleteBootstrapCommand
LoginCommand
LogoutCommand
IssueInvitationCommand
AcceptInvitationCommand
ResendInvitationCommand
RevokeInvitationCommand

GetBootstrapStatusQuery
GetCurrentUserQuery
PreviewInvitationQuery
ListInvitationsQuery
```

Cada acción de un controlador MVC se limitará a:

1. recibir y validar la forma básica del contrato HTTP;
2. construir el command o query con la identidad del actor;
3. invocar su handler;
4. traducir el resultado de aplicación a status code, cabeceras y `ProblemDetails`.

Application devolverá resultados y errores funcionales tipados. No devolverá `IResult`, `ProblemDetails`, códigos HTTP ni excepciones específicas del transporte.

Access utilizará controladores MVC con attribute routing. No mezclará Minimal APIs y controladores para los casos de uso del módulo. El documento OpenAPI y Swagger UI se generarán con Swashbuckle y solo se expondrán en el entorno `Development`.

Los command handlers cargarán agregados, comprobarán autorización funcional, invocarán comportamiento de dominio y confirmarán una única transacción. La política transaccional podrá implementarse mediante decoradores para evitar abrir transacciones manuales en cada handler.

Los query handlers usarán lectura sin tracking y proyectarán DTO. No mutarán agregados, publicarán eventos ni llamarán a `SaveChanges`. El estado efectivo de una invitación caducada podrá calcularse durante la lectura. Si el estado debe materializarse, se hará mediante un comando o job explícito, idempotente y observable.

### 6. Agregados como propietarios del estado

No se crearán servicios generales como `EntityService<T>` ni servicios CRUD que concentren todas las operaciones de una entidad.

La única representación funcional de una invitación será el agregado `Invitation`. EF Core lo mapeará directamente desde Infrastructure. `InvitationRecord` se eliminará una vez migrado el flujo completo.

La distribución de responsabilidades será:

- `Invitation` será propietaria de sus transiciones `Accept`, `Revoke`, `Expire` y del resto de invariantes locales;
- el handler coordinará autorización, repositorios, agregados relacionados y persistencia;
- una política o servicio de dominio se utilizará solo para reglas que requieran información que no pertenece naturalmente a una instancia, como los límites de reenvío basados en el historial;
- un servicio de aplicación no conservará estado de entidad entre peticiones.

Los tests unitarios se ejecutarán contra el mismo agregado que se persiste y utiliza en producción. No existirán modelos de dominio y registros de persistencia con comportamiento duplicado.

### 7. Outbox y procesos en segundo plano

El hosted service del outbox pertenecerá a Infrastructure y se limitará al polling y al ciclo de vida del proceso. El procesamiento funcional de un mensaje se expondrá como un caso de uso de Application.

La creación de la invitación y de su mensaje de outbox se confirmará en una misma transacción. El procesamiento cumplirá:

- claim atómico compatible con varias instancias, por ejemplo mediante bloqueo de fila y `SKIP LOCKED` o una actualización condicional equivalente;
- entrega idempotente o deduplicable;
- lease con recuperación tras caída;
- reintentos y descarte explícitos;
- distinción entre fallos transitorios, payload inválido y configuración;
- ausencia de tokens, destinatarios u otros secretos en logs y telemetría.

La métrica y el logging concretos se implementarán en adaptadores o decoradores. Domain y Application podrán producir eventos o información de resultado, pero no dependerán de OpenTelemetry.

### 8. Autorización

La autenticación técnica y las políticas HTTP permanecerán en Api e Infrastructure. La autorización sobre un recurso concreto también se comprobará en el handler de Application, usando puertos de acceso del módulo propietario.

No se considerará suficiente ocultar rutas en Angular, aplicar únicamente `RequireAuthorization` ni comprobar un rol sin el identificador de campaña. Los commands y queries incluirán la identidad del actor necesaria para verificar pertenencia, rol, campaña y, cuando corresponda, personaje activo.

### 9. Estrategia de adopción

La migración será incremental y conservará contratos HTTP y comportamiento observable:

1. añadir pruebas de caracterización, concurrencia y arquitectura;
2. crear el proyecto de Access, su fachada pública y sus capas internas;
3. mover el modelo de dominio y mapear `Invitation` directamente con EF Core;
4. extraer endpoints en vertical slices, empezando por casos sencillos y terminando por aceptación de invitaciones;
5. sustituir dependencias EF de Application por puertos especializados;
6. separar hosting, claim y procesamiento del outbox;
7. retirar tipos y código duplicados;
8. utilizar Access como referencia para el siguiente módulo funcional.

Las rutas `/api/v1`, formatos JSON y semántica pública de errores no cambiarán como efecto de esta reorganización. Cualquier modificación del contrato requerirá una decisión funcional independiente.

## Estrategia de pruebas

### Tests unitarios de dominio

Validarán agregados, value objects y políticas sin ASP.NET, EF Core ni red. Incluirán:

- ciclo de vida completo de invitaciones;
- aceptación única, caducidad exacta y revocación;
- normalización de identidad y política de contraseñas;
- emisión y revocación de sesiones;
- política de reenvío y sus límites temporales;
- invariantes de concesiones de acceso.

### Tests unitarios de Application

Validarán cada handler con puertos falsos controlados. Comprobarán:

- autorización y aislamiento por campaña;
- coordinación y orden de efectos;
- ausencia de commit ante errores;
- resultados funcionales y errores tipados;
- generación de outbox o eventos cuando corresponda.

No se simulará `DbContext` ni `DbSet`. El comportamiento de EF se comprobará mediante integración con PostgreSQL.

### Tests de integración de persistencia

Utilizarán PostgreSQL real y cubrirán:

- mapeo EF Core y conversiones;
- índices, foreign keys y restricciones únicas;
- repositorios y read stores;
- aislamiento y rollback transaccional;
- migración de una base vacía;
- actualización desde la versión anterior soportada;
- adquisición concurrente, lease, reintentos e idempotencia del outbox.

### Tests de componente/API

Utilizarán `WebApplicationFactory`, PostgreSQL real y adaptadores externos falsos. Validarán rutas completas desde HTTP hasta persistencia:

- autenticación, autorización y rate limiting;
- contratos JSON y `ProblemDetails`;
- bootstrap, login y logout;
- emisión, preview, aceptación, reenvío y revocación;
- manipulación de `campaignId` y acceso cruzado entre campañas;
- cabeceras de correlación y ausencia de información sensible.

### Tests de contrato de adaptadores

El adaptador de correo se probará contra un servidor HTTP controlado o handler falso para verificar payload, autenticación, timeouts, códigos de error y no exposición de información sensible. Estos tests no decidirán reglas del ciclo de vida de la invitación.

### Tests end-to-end

Se mantendrá un conjunto pequeño para los recorridos de mayor valor:

- bootstrap y login;
- invitación de plataforma y creación de cuenta;
- invitación de campaña a usuario existente y nuevo;
- acceso autorizado y rechazo de acceso a otra campaña.

No se duplicarán en end-to-end todas las combinaciones ya cubiertas por tests unitarios y de componente.

### Casos de concurrencia obligatorios

La suite de integración comprobará expresamente:

- dos bootstrap simultáneos, con un único administrador creado;
- dos aceptaciones simultáneas del mismo token, con un único éxito;
- dos invitaciones simultáneas a la misma identidad y contexto;
- límites de reenvío bajo concurrencia;
- dos workers intentando adquirir el mismo mensaje, sin doble entrega;
- consistencia entre invitación aceptada, cuenta y concesión de acceso.

## Fitness functions y pipeline

Existirán dos niveles de fitness functions que se ejecutarán en cada pull request:

- `DndCampaign.ArchitectureTests`, global, comprobará referencias y ciclos entre módulos, acceso del host únicamente a fachadas y ausencia de dependencias de un módulo sobre la implementación de otro;
- `DndCampaign.Modules.Access.Tests/Architecture`, propio de Access, comprobará las dependencias permitidas entre Api, Application, Domain e Infrastructure dentro del ensamblado.

Sus reglas harán fallar la build cuando:

- Domain referencie ASP.NET Core, EF Core, OpenTelemetry, Api o Infrastructure;
- Application referencie ASP.NET Core, EF Core, tipos HTTP, Api o Infrastructure;
- un endpoint referencie un `DbContext`, configuración EF, entidad de persistencia o `SaveChanges`;
- un módulo consuma internals, repositorios, entidades o tablas de otro;
- existan ciclos entre módulos;
- un query handler dependa de repositorios de escritura o unidad de trabajo;
- Infrastructure sea consumida fuera del composition root o tests de integración autorizados;
- el `DbContext` de un módulo mapee entidades propiedad de otro;
- un módulo exponga públicamente tipos que no forman parte de sus contratos.

Las reglas se implementarán mediante tests de arquitectura sobre ensamblados y, cuando una regla no pueda expresarse así, mediante analizadores o comprobaciones estáticas pequeñas mantenidas en el repositorio. Las fronteras importantes no dependerán únicamente de revisión manual.

El pipeline de API ejecutará, como mínimo y en este orden lógico:

1. restore reproducible y build con warnings tratados como errores;
2. tests unitarios y de arquitectura;
3. tests de persistencia y componente con PostgreSQL real;
4. migración desde cero y desde la versión anterior soportada;
5. comprobación de compatibilidad del contrato OpenAPI cuando se publique;
6. análisis de dependencias vulnerables y secretos.

Una suite obligatoria no podrá considerarse correcta si todos sus tests se omiten. El pipeline continuará aprovisionando PostgreSQL como servicio, pero se separarán proyectos o categorías para que la ausencia accidental de los tests de integración resulte visible y haga fallar CI.

Además de los límites estructurales, se mantendrán budgets revisables para impedir la reaparición de componentes generales desproporcionados. Estos budgets señalarán endpoints o handlers con demasiadas dependencias o responsabilidades, pero no sustituirán una revisión del diseño por métricas arbitrarias de líneas.

## Alternativas consideradas

### Mantener carpetas globales por capa

Requiere menos cambios inmediatos, pero las dependencias continúan siendo convenciones dentro de un único ensamblado. No impide que Application use Infrastructure ni que un caso de uso acceda a datos de cualquier área. Se descarta.

### Crear únicamente proyectos globales Domain, Application e Infrastructure

Haría compilables las reglas entre capas, pero no protegería los límites entre Access, Campaigns, Journal o Encounters. Con el crecimiento, cada capa se convertiría en un conjunto global de entidades y servicios. Se descarta en favor de capas dentro de módulos.

### Un proyecto por cada capa del módulo

Protege parte de la dirección de dependencias por compilación, pero multiplica proyectos y hace que la unidad física deje de coincidir con la unidad funcional. Se descarta: el proyecto es el módulo y las capas internas se protegen mediante tests arquitectónicos específicos del módulo.

### Servicios CRUD por entidad

Facilitan operaciones simples, pero tienden a generar modelos anémicos, servicios generales y transacciones implícitas. Tampoco representan bien casos de uso que coordinan cuenta, invitación, sesión y acceso. Se descartan como patrón principal.

### CQRS completo, event sourcing o base de lectura separada

Ofrecen independencia de modelos y escalado, pero añaden sincronización, versionado de eventos, reconstrucción de estado y operación distribuida sin una necesidad medida. Se adopta únicamente la separación lógica de commands y queries.

### Microservicios

Aislarían despliegues y datos, pero obligarían a resolver consistencia distribuida, mensajería, observabilidad y operación antes de validar el dominio. Contradicen la decisión vigente de monolito modular. Se descartan.

## Consecuencias

### Positivas

- Las fronteras dejan de depender solo de nombres de carpetas.
- Cada tarea nueva encuentra un módulo y un caso de uso concretos donde implementarse.
- Los endpoints dejan de ser coordinadores de persistencia y dominio.
- El agregado probado es el mismo que se ejecuta y persiste.
- Las consultas son predecibles y no producen efectos laterales.
- PostgreSQL continúa ofreciendo transacciones locales sin introducir infraestructura distribuida.
- Los módulos pueden evolucionar y, si algún día existe una necesidad real, extraerse con contratos ya definidos.
- El pipeline detecta degradaciones arquitectónicas antes de integrar cambios.

### Costes

- Cada módulo necesita mantener fitness functions internas porque sus capas comparten ensamblado.
- La migración inicial requiere mover código sin aportar funcionalidad visible.
- Será necesario diseñar puertos y resultados de aplicación explícitos.
- Los tests de integración y concurrencia consumirán más tiempo de CI.
- El equipo deberá mantener reglas de arquitectura y revisar el ownership al introducir módulos.

### Riesgos

- Crear demasiados módulos o abstracciones antes de entender sus invariantes.
- Convertir commands y queries en clases ceremoniales que sigan delegando en servicios generales.
- Utilizar contratos compartidos como vía para reconstruir un modelo global acoplado.
- Introducir consistencia eventual donde el requisito necesita atomicidad.
- Mantener temporalmente ambos diseños durante demasiado tiempo y generar rutas paralelas.

Estos riesgos se mitigarán con una migración vertical e incremental, eliminación temprana de código duplicado y architecture tests obligatorios.

## Decisiones excluidas

Este ADR no decide:

- la transferencia del DM ni otras reglas funcionales todavía pendientes;
- la frontera definitiva de Campaigns y las concesiones de acceso;
- una librería concreta de mediator o architecture testing;
- la extracción de microservicios;
- un cambio de proveedor de base de datos, correo u observabilidad;
- modificaciones de rutas o contratos públicos.

## Confirmaciones de aceptación

Al aceptar este ADR, el equipo confirma:

1. que Access es el módulo inicial correcto y conserva usuarios, sesiones, invitaciones y concesiones de acceso;
2. que Access es un único proyecto, con capas internas y una fachada pública mínima para el host;
3. que se adopta CQRS ligero sin exigir mediator, event sourcing ni bases separadas;
4. que `Invitation` será el único modelo funcional persistido;
5. que las suites de integración, concurrencia y arquitectura serán obligatorias en CI;
6. que la migración conservará temporalmente los contratos HTTP existentes.
