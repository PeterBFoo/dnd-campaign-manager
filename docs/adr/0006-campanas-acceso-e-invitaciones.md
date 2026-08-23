# ADR-0006: Campañas, acceso e invitaciones de usuarios activos

- Estado: Aceptado
- Fecha: 2026-08-23
- Decisores: equipo del proyecto
- Alcance: ownership de campañas, roles, búsqueda de usuarios e integración con invitaciones
- Depende de: ADR-0003, ADR-0004, ADR-0005 y [spec 004](../specs/004-creacion-campanas/spec.md)
- Sustituye parcialmente: la obligatoriedad de seleccionar módulo al crear una campaña descrita en ADR-0002

## Contexto

Access ya es propietario de usuarios, sesiones, invitaciones y concesiones de jugador creadas al aceptar una invitación. También contiene una representación provisional de membresía DM que solo puede prepararse desde tests porque todavía no existe una campaña productiva.

El primer incremento de Campaigns debe crear una campaña y su único DM sin estados parciales, permitir a DM y jugadores aceptados consultarla y hacer utilizables las rutas de invitaciones existentes. ADR-0004 exige que los módulos no consulten tablas o internals ajenos y dejó pendiente el ownership definitivo de las concesiones.

El usuario ha decidido además que una campaña pueda existir antes de que su módulo de aventura esté disponible, que la API responda `403` ante una campaña existente pero ajena y que el DM pueda seleccionar destinatarios desde usuarios activos de la plataforma.

## Decisión

### 1. Campaigns será propietario de la campaña y del DM único

El agregado `Campaign` contendrá como mínimo su identificador, nombre, `DmUserId`, `AdventureModuleId` opcional y fecha de creación. Crear el agregado y fijar su DM será una única transacción de Campaigns.

El DM no se duplicará como una nueva concesión escrita por Access. El campo obligatorio `DmUserId` será la fuente autoritativa de esa función, de modo que una campaña persistida no pueda quedar sin DM ni con dos DM. La transferencia o sustitución del DM continúa fuera de alcance.

### 2. Access conservará invitaciones y concesiones de jugador

Access continuará siendo propietario de:

- cuentas y elegibilidad de usuarios;
- emisión, entrega, reenvío, revocación y aceptación de invitaciones;
- validación de la identidad destinataria;
- concesión transaccional del acceso `Jugador` al aceptar;
- consultas necesarias para saber a qué campañas accede un jugador.

La aceptación conservará su transacción local en Access: invitación, cuenta cuando corresponda, sesión y concesión de jugador seguirán confirmándose juntas. No se trasladará ningún estado de invitación a Campaigns.

Las membresías DM provisionales de Access dejarán de ser fuente de autorización. Las migraciones conservarán compatibilidad y retirarán o transformarán ese estado solo cuando el plan pueda demostrar que no se pierden datos válidos.

### 3. La comunicación será por contratos públicos y sin ciclos

La dirección de referencia entre ensamblados será `Campaigns -> Access`. Access publicará contratos mínimos para consultar concesiones de jugador y declarará un puerto, propiedad del consumidor, que necesita para autorizar invitaciones contra una campaña.

Campaigns implementará ese puerto usando exclusivamente su modelo y registrará el adaptador mediante su fachada. Así Access podrá comprobar existencia y DM sin referenciar el ensamblado Campaigns, mientras Campaigns podrá combinar sus campañas dirigidas con los identificadores concedidos por Access.

Los contratos solo contendrán identificadores y DTO inmutables. Ningún módulo expondrá entidades, repositorios, `DbContext`, `IQueryable`, transacciones o tablas. No habrá una transacción compartida: crear campaña y aceptar invitación mantienen cada una sus invariantes dentro de su módulo propietario.

### 4. El módulo de aventura será opcional

`AdventureModuleId` será nullable. El primer recorrido creará campañas sin módulo y las representará como `Sin módulo`.

Este incremento no creará `AdventureModules`, catálogo, contenido editorial ni asociación posterior. Un spec futuro definirá el catálogo y el comando para asociar como máximo un módulo, sin modificar la identidad de la campaña.

### 5. Access ofrecerá destinatarios elegibles por campaña

La búsqueda no será un directorio global. Solo el DM autorizado podrá solicitar usuarios elegibles dentro de una campaña concreta.

La operación:

- admitirá una página inicial y búsqueda por nombre visible o correo;
- estará paginada y tendrá un límite máximo;
- excluirá al actor, miembros existentes e invitaciones pendientes del mismo contexto;
- devolverá `UserId`, nombre visible y correo enmascarado;
- no expondrá correos completos, estado administrativo, roles globales ni datos de sesión;
- no registrará el texto buscado ni resultados en logs o telemetría.

Hasta que exista un ciclo de desactivación, una cuenta activa es cualquier cuenta registrada que puede autenticarse. Una futura suspensión deberá incorporarse al mismo predicado de elegibilidad.

La emisión para una cuenta existente aceptará `recipientUserId`. Access volverá a comprobar elegibilidad y resolverá internamente el correo en la misma operación; nunca confiará solo en el resultado previo del buscador. El contrato por correo se mantendrá para el recorrido ya existente de personas sin cuenta.

### 6. Los recursos existentes pero ajenos responderán `403`

Campaigns distinguirá entre campaña inexistente y campaña existente sin autorización. La primera responderá `404`; la segunda, `403`.

Esta elección revela que un identificador corresponde a una campaña, riesgo aceptado expresamente a cambio de una semántica contractual uniforme. Las respuestas no incluirán nombre, DM, módulo ni otros datos de la campaña.

### 7. Cada módulo persistente tendrá contexto y esquema propios

Al introducir el segundo módulo persistente se activa la decisión diferida de ADR-0004: Access y Campaigns tendrán `DbContext`, migraciones y esquemas PostgreSQL propios. Seguirán compartiendo servidor y cadena de conexión, pero no foreign keys ni consultas entre esquemas.

## Consecuencias

### Beneficios

- La campaña y su DM se crean atómicamente sin coordinar dos transacciones.
- La aceptación de invitaciones conserva sus garantías actuales dentro de Access.
- No se duplican invitaciones, usuarios ni concesiones de jugador.
- La dirección de dependencias entre ensamblados permanece acíclica.
- Las campañas pueden empezar antes de disponer de contenido editorial.
- El DM puede seleccionar cuentas sin conocer o exponer su correo completo.

### Costes y riesgos

- El rol DM y las concesiones de jugador tienen propietarios distintos y requieren contratos explícitos para construir una vista conjunta.
- La búsqueda de usuarios aumenta la superficie de privacidad y exige rate limiting, paginación y minimización.
- `403` permite confirmar la existencia de una campaña a quien obtenga su identificador.
- Mover Access a un esquema propio requiere una migración de datos verificable.
- El puerto implementado por Campaigns debe tener un fallo de arranque claro si el módulo no se registra; no puede degradarse a una autorización permisiva.

## Alternativas consideradas

### Mantener también el DM en Access

Obligaría a coordinar la creación de una campaña y su concesión DM entre dos modelos de escritura. Una transacción compartida filtraría detalles de infraestructura y una compensación permitiría estados intermedios. Se descarta para este incremento.

### Mover invitaciones y membresías a Campaigns

Rompería la atomicidad actual entre invitación, cuenta, sesión y concesión, además de duplicar comportamiento ya establecido en Access. Se descarta.

### Crear primero AdventureModules

Impediría iniciar campañas propias mientras no exista un catálogo cargado. Se pospone: la asociación es opcional.

### Exponer un directorio global con correo completo

Simplificaría la selección, pero revelaría datos personales fuera del contexto que los necesita. Se sustituye por resultados elegibles, contextuales y minimizados.

### Responder `404` para ocultar recursos ajenos

Reduciría enumeración, pero el usuario ha priorizado distinguir de forma coherente inexistencia y falta de permiso. Se rechaza a favor de `403` sin detalles.

## Confirmaciones de aceptación

Al aceptar este ADR, el equipo confirma:

1. Campaigns posee la campaña y su DM único.
2. Access conserva usuarios, invitaciones y concesiones de jugador.
3. La colaboración usa contratos públicos, una dirección de referencia acíclica y ningún acceso directo a persistencia ajena.
4. Una campaña puede crearse sin módulo.
5. Solo el DM puede listar o buscar destinatarios elegibles, con datos minimizados.
6. La emisión por usuario revalida elegibilidad dentro de Access.
7. Una campaña existente pero ajena responde `403`.
