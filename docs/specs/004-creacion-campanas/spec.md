# Especificación 004: Creación de campañas e invitación de usuarios existentes

- Estado: Completada
- Fecha: 2026-08-23
- Tipo: incremento funcional vertical
- Roadmap: [roadmap funcional](../../roadmap/product-roadmap.md)
- Requisitos principales: RF-002 a RF-004, RF-009 a RF-011, RF-014, RF-073 a RF-075 y RF-077 a RF-083
- Dependencias: comportamiento de Access definido por [ADR-0002](../../adr/0002-identidad-invitaciones-y-correo-transaccional.md), [ADR-0003](../../adr/0003-bootstrap-sesiones-y-flujo-de-invitaciones.md) y el [spec 002](../002-modularizacion-access/spec.md); arquitectura frontend del [spec 003](../003-modularizacion-frontend/spec.md)

## Problema

El producto ya permite crear cuentas, iniciar sesión y gestionar invitaciones de campaña, pero una persona no puede crear una campaña real. Los endpoints de invitación solo resultan utilizables en pruebas que preparan previamente una membresía de DM.

Faltan una fuente real de campañas, la asignación del creador como su único DM, una vista de las campañas accesibles y la integración del recorrido existente de invitaciones con esas campañas.

## Objetivo

Permitir que cualquier usuario registrado cree una campaña aunque todavía no tenga un módulo de aventura asociado, quede asignado atómicamente como su único DM y seleccione a otro usuario activo para invitarlo. Tras aceptar explícitamente la invitación con la cuenta destinataria, el nuevo jugador podrá ver la campaña entre sus campañas accesibles.

El resultado debe ser utilizable de extremo a extremo desde Angular y estar autorizado por la API; no basta con crear tablas, contratos aislados o controles exclusivamente visuales.

## Actores

- **Usuario registrado:** puede consultar sus campañas y crear una nueva.
- **DM creador:** usuario registrado que crea una campaña y se convierte en su único DM.
- **Usuario invitado:** cuenta ya existente cuyo correo coincide con el destinatario de una invitación de campaña.
- **Jugador:** usuario invitado que ha aceptado la invitación y ha obtenido acceso a la campaña.

El administrador de plataforma no obtiene privilegios sobre una campaña por su rol global. Solo actúa como DM cuando crea la campaña como usuario.

## Recorridos incluidos

### 1. Consultar las campañas accesibles

- Un usuario autenticado podrá abrir una vista con las campañas en las que es DM o jugador aceptado.
- Cada resultado mostrará como mínimo identificador, nombre, módulo seleccionado cuando exista y rol del usuario actual.
- Una invitación pendiente no hará aparecer la campaña en la lista del destinatario.
- Un usuario sin campañas verá un estado vacío y una acción para crear una.

### 2. Crear una campaña

- Cualquier usuario autenticado podrá iniciar el recorrido de creación.
- El formulario solicitará un nombre. La asociación a un módulo de aventura será opcional y la campaña podrá crearse con el valor `Sin módulo`.
- Este incremento no cargará un catálogo ni permitirá asociar posteriormente un módulo. Esa evolución pertenecerá a un spec independiente.
- Al completarse la operación, la campaña existirá con el creador como su único DM y aparecerá inmediatamente en su listado.
- La creación no generará personajes, NPC, capítulos, misiones, entradas de bitácora ni combates.

### 3. Invitar a un usuario existente

- Desde una campaña propia, el DM podrá abrir la gestión de invitaciones ya existente.
- Access ofrecerá al DM una lista paginada y un buscador de usuarios activos elegibles para esa campaña.
- El DM seleccionará una cuenta por su identificador estable. La interfaz mostrará nombre visible y correo enmascarado para distinguir homónimos sin entregar un directorio de correos completos.
- La búsqueda admitirá nombre visible y correo, exigirá autenticación y autorización como DM de la campaña, y devolverá un conjunto paginado y acotado.
- Se considerará activa una cuenta registrada que pueda autenticarse. Mientras no exista un ciclo de desactivación de cuentas, todas las cuentas registradas cumplen esta condición.
- Los resultados excluirán al propio DM, miembros ya incorporados y destinatarios con una invitación pendiente para la misma campaña.
- Se reutilizará el ciclo de vida actual de Access: emisión, correo, previsualización, aceptación explícita, caducidad, revocación y reenvío.
- El destinatario existente deberá autenticarse con la cuenta asociada al correo invitado antes de aceptar.
- Al aceptar, se incorporará exclusivamente como jugador y la campaña aparecerá en su listado.
- La capacidad ya existente para que una persona sin cuenta se registre mediante una invitación de campaña no se elimina ni cambia, pero ese recorrido no se amplía ni constituye el objetivo de aceptación de este incremento.

## Reglas funcionales

1. Toda operación requiere una sesión válida salvo la previsualización y los pasos públicos ya autorizados del flujo de aceptación.
2. El nombre de campaña es obligatorio, se normaliza retirando espacios exteriores y contiene entre 3 y 100 caracteres.
3. Los nombres de campaña no son globalmente únicos; grupos distintos pueden utilizar el mismo nombre.
4. Una campaña puede no tener módulo de aventura o referenciar como máximo uno. En este incremento se creará sin módulo; la asociación posterior queda fuera de alcance.
5. La campaña y la asignación de su creador como DM constituyen un único resultado funcional: nunca quedará una campaña creada sin DM ni con dos DM.
6. Crear una campaña no convierte al usuario en jugador ni crea una membresía de jugador duplicada.
7. Solo el DM de la campaña puede emitir, listar, reenviar o revocar sus invitaciones.
8. Una invitación nunca concede acceso antes de su aceptación ni concede el rol DM.
9. Un usuario que ya pertenece a la campaña no puede obtener una segunda membresía. El propio DM, los miembros existentes y quienes ya tengan una invitación pendiente no aparecerán como elegibles; una petición directa producirá un conflicto sin efectos parciales.
10. Un usuario solo puede listar o consultar campañas en las que sea DM o jugador aceptado.
11. La API aplicará el aislamiento aunque se modifiquen manualmente rutas, identificadores o payloads.
12. Dos campañas que seleccionen el mismo módulo conservarán identificadores y estado completamente independientes.

## Alcance de frontend

El frontend incorporará un módulo `campaigns` con ownership de:

- rutas para listar, crear y consultar el resumen de una campaña;
- clientes HTTP y contratos propios de campañas;
- estado local de listado, creación y detalle;
- estados de carga, vacío, validación, error y éxito;
- navegación hacia `/campaigns/:campaignId/invitations` sin importar internals de Access.

El módulo `access` conservará la página de invitaciones e incorporará en ella el listado, búsqueda y selección de destinatarios elegibles. `campaigns` solo navegará hacia su ruta pública y no importará clientes, contratos ni componentes internos de Access.

Ambos módulos usarán rutas lazy y APIs públicas mínimas conforme a ADR-0005. Los guards son ayudas de experiencia; no sustituyen la autorización de la API.

## Alcance de API

La API incorporará un módulo funcional `Campaigns`, con un único proyecto y límites internos equivalentes a los establecidos para Access:

- agregado y persistencia de campaña;
- consulta de campañas accesibles y de un resumen individual;
- command de creación con asignación del DM;
- contratos intermodulares deliberados para comprobar acceso y colaborar con Access;
- `DbContext`, migraciones y esquema PostgreSQL propios cuando lo exija ADR-0004;
- autorización por usuario, campaña y rol dentro de los handlers;
- métricas, logs y trazas sin nombres de campaña ni correos.

Campaigns no accederá directamente a entidades, repositorios, `DbContext` o tablas de Access. Access tampoco consultará directamente la persistencia de Campaigns.

Access continuará siendo propietario de usuarios, invitaciones y concesiones de acceso derivadas de estas. Incorporará la consulta paginada de destinatarios elegibles, la emisión por `recipientUserId` y la validación autoritativa de que el destinatario continúa activo y es elegible al emitir la invitación.

## Contrato HTTP funcional

El plan concretará los DTO sin alterar estas operaciones públicas:

- `GET /api/v1/campaigns`: campañas accesibles para el usuario actual.
- `POST /api/v1/campaigns`: creación de campaña; devuelve `201 Created` y su resumen.
- `GET /api/v1/campaigns/{campaignId}`: resumen autorizado de una campaña.
- `GET /api/v1/campaigns/{campaignId}/eligible-users`: listado o búsqueda paginada de usuarios activos que Access permite invitar.
- Se conservan las rutas actuales de `GET`, `POST`, reenvío y revocación bajo `/api/v1/campaigns/{campaignId}/invitations`.

La emisión para usuarios existentes aceptará `recipientUserId`; Access resolverá internamente el correo de destino. El contrato actual por correo se conservará para no romper el recorrido de personas sin cuenta.

Los errores se expresarán mediante `ProblemDetails`. La API devolverá `401` sin sesión, `403` cuando la campaña exista pero el actor no tenga autorización, validación para entradas incorrectas y conflicto cuando la operación choque con una membresía o invitación existente.

## Observabilidad

- Contador y duración de creaciones de campaña, distinguiendo éxito, validación, conflicto y fallo.
- Contador de consultas de listado y detalle, sin etiquetar identificadores de usuario o campaña.
- Contador y duración de búsquedas de usuarios elegibles, sin registrar consultas, nombres, correos ni resultados.
- Correlación de la creación con la concesión de DM y de la invitación con la membresía aceptada.
- Logs estructurados sin nombre de campaña, correo, token de invitación ni otros datos personales.
- Los fallos parciales o reintentos de colaboración entre módulos deben ser detectables.

## Criterios de aceptación

1. Un usuario registrado crea una campaña indicando solo el nombre, recibe `201` y la ve en su listado como DM y con estado `Sin módulo`.
2. Inmediatamente después de crearla, el creador es su único DM y puede abrir la gestión de invitaciones; no existe ningún estado observable de campaña sin DM.
3. Otro usuario registrado no ve la campaña antes de aceptar una invitación dirigida a su correo.
4. El DM lista o busca usuarios elegibles, selecciona una cuenta activa y emite la invitación desde la campaña creada; el destinatario inicia sesión con la cuenta correcta, acepta y ve la campaña como jugador.
5. Aceptar una invitación de campaña nunca crea otro DM, sustituye al actual ni genera una cuenta duplicada.
6. Un jugador o un usuario ajeno no puede gestionar invitaciones aunque invoque directamente la API con el identificador de la campaña.
7. Un usuario ajeno no puede listar ni consultar la campaña alterando rutas o identificadores y recibe `403` cuando solicita directamente una campaña existente.
8. Un jugador, un usuario ajeno o una petición sin campaña autorizada no puede listar ni buscar usuarios elegibles.
9. La búsqueda no devuelve correos completos y excluye al DM, miembros e invitaciones pendientes; intentar invitarlos directamente devuelve conflicto sin crear otra invitación ni membresía.
10. Dos campañas sin módulo se crean como instancias independientes y no comparten identidad ni estado.
11. Los recorridos Angular cubren carga, estado vacío, validación, error, creación, búsqueda y selección de usuario, navegación a invitaciones y aparición tras la aceptación.
12. Las pruebas unitarias, de integración con PostgreSQL, de componente HTTP, de arquitectura y frontend demuestran los criterios anteriores y se ejecutan en CI.

## Decisiones confirmadas

1. Access conserva usuarios, invitaciones y concesiones de acceso relacionadas con invitaciones. Campaigns es propietario de la campaña y colabora con Access mediante contratos públicos, sin consultar sus tablas o internals.
2. La campaña puede crearse sin módulo. Este incremento no crea `AdventureModules`; el catálogo y la asociación posterior se especificarán por separado.
3. Una campaña existente pero no autorizada responde `403`, priorizando la coherencia contractual sobre la ocultación de existencia.
4. Access ofrece un listado y buscador de usuarios activos elegibles, limitado al DM y a la campaña, y emite la invitación usando el identificador del usuario seleccionado.

Estas decisiones se registran en [ADR-0006](../../adr/0006-campanas-acceso-e-invitaciones.md).

## Fuera de alcance

- crear, editar, importar o publicar contenido editorial de módulos de aventura;
- crear un directorio global o una API de usuarios accesible fuera del contexto autorizado de invitación de una campaña;
- cambiar el registro mediante invitación de personas sin cuenta;
- crear o asignar personajes al aceptar una invitación;
- editar, archivar o eliminar campañas;
- transferir el rol DM, incorporar un segundo DM o abandonar una campaña;
- expulsar jugadores o permitir que abandonen la campaña;
- NPC, capítulos, bitácora, misiones, calendario, iniciativa o cualquier otro estado de juego;
- sincronización en tiempo real, notificaciones dentro de la aplicación o presencia;
- modificar el proveedor de correo, las sesiones o la política de tokens de invitación.

## Validación

El usuario aceptó el alcance y resolvió expresamente las decisiones funcionales el 2026-08-23. La implementación quedó verificada el mismo día mediante 43 tests API en Docker con PostgreSQL real, 45 tests frontend, compilación .NET, build Angular, construcción de ambas imágenes y validación de Compose. El recorrido de componente crea una campaña real, busca una cuenta existente con datos enmascarados, invita por `recipientUserId`, exige aceptación y comprueba que después aparece con rol `player`.
