# Plan 008: Registro y gestión compartida de misiones

- Estado: Ejecutado; construcción adicional de imágenes finales pendiente
- Fecha: 2026-08-23
- Especificación: [spec.md](spec.md)
- ADR aplicables: [ADR-0004](../../adr/0004-arquitectura-modular-cqrs-y-limites-de-dependencia.md) y [ADR-0005](../../adr/0005-frontend-modular-por-capacidades.md)
- Dependencias: [spec 004](../004-creacion-campanas/spec.md), [spec 005](../005-personajes-campana/spec.md) y [spec 006](../006-resumen-personajes-activos/spec.md)
- Validación funcional: spec aceptado por el usuario el 2026-08-23 después de retirar las fechas funcionales y añadir eliminación

## Resultado esperado

La portada de campaña permitirá abrir un registro compartido de misiones. El DM y los jugadores aceptados podrán consultar las misiones con la principal primero, crear nuevas, editar su contenido y estado y cambiar la principal. Crear como jugador requerirá personaje activo y conservará una instantánea de esa autoría; el DM creará como dirección de campaña sin necesitar personaje.

Un jugador podrá eliminar solo las misiones que creó y el DM podrá eliminar cualquiera de su campaña. La API garantizará autorización, aislamiento y que nunca existan dos misiones principales, incluso ante escrituras concurrentes. Ningún formulario o contrato admitirá fecha de aceptación, fecha objetivo o recurrencia.

## Diagnóstico de partida

- Campaigns publica `ICampaignAccessReader`, que distingue campaña inexistente, DM, jugador aceptado y usuario sin acceso.
- Characters ya publica `IActiveCharacterReader` con identificador y nombre del personaje activo; Journal consume ese contrato sin acceder a internals.
- La API registra cada capacidad mediante una fachada, usa un proyecto y esquema PostgreSQL por módulo y aplica migraciones desde el host en orden explícito.
- Journal aporta patrones reutilizables para autoría histórica, permisos derivados, `ProblemDetails`, métricas y texto plano, pero Missions no dependerá de Journal ni compartirá sus tablas.
- Las pruebas arquitectónicas permiten únicamente aristas explícitas entre módulos y deben ampliarse para reconocer Missions.
- Angular está organizado por capacidades, con rutas lazy, providers acotados, clientes propios y fitness functions contra deep imports y ciclos.
- La portada de campaña ya conoce el rol efectivo, enlaza a Journal y compone Characters mediante su API pública.
- No existe módulo, esquema, endpoint, ruta, cliente ni interfaz de misiones.
- La API se despliega actualmente como una sola réplica, pero la unicidad de la misión principal debe quedar protegida por PostgreSQL y no depender de esa topología.

## Principios de ejecución

1. **Ownership local.** Missions será propietario de las misiones y no consultará tablas ni entidades de Campaigns o Characters.
2. **Contratos mínimos existentes.** Missions consumirá el acceso efectivo de Campaigns y la proyección de personaje activo de Characters sin ampliar esos contratos salvo que una prueba revele una carencia real.
3. **Autorización en Application.** Todos los casos de uso comprobarán campaña, rol y, para eliminación por jugador, creador. Controladores, flags y guards serán adaptadores o ayudas de UX.
4. **Sin fechas funcionales.** Solo existirán `CreatedAt` y `UpdatedAt` técnicos, asignados por el servidor; no se crearán columnas, DTO o controles para fechas aportadas por el usuario.
5. **Autoría original estable.** La creación capturará usuario creador y, cuando corresponda, personaje activo. Editar, cambiar estado o principal no sustituirá esos datos.
6. **Borrado deliberado.** `canDelete` se derivará en la API; la web pedirá confirmación y el backend repetirá el permiso antes de eliminar.
7. **Principal transaccional.** Dominio, índice único parcial y transacciones PostgreSQL cooperarán para mantener como máximo una principal activa por campaña.
8. **Texto sin interpretación.** Título y descripción se renderizarán por interpolación Angular, nunca como HTML aportado por el usuario.
9. **Verticales verdes.** Cada fase conservará compilación, límites modulares y suites existentes antes de habilitar la navegación.

## Estructura objetivo de API

```text
apps/api/Modules/Missions/DndCampaign.Modules.Missions/
  DndCampaign.Modules.Missions.csproj
  MissionsModule.cs
  Properties/
    AssemblyInfo.cs
  Api/
    MissionsController.cs
    InternalControllerFeatureProvider.cs
  Application/
    Abstractions/
      MissionResult.cs
    Missions/
      MissionHandlers.cs
    Ports/
      IMissionRepository.cs
      IMissionMetrics.cs
  Domain/Missions/
    Mission.cs
  Infrastructure/
    Observability/
      MissionMetrics.cs
    Persistence/
      MissionsDbContext.cs
      MissionRepository.cs
      MissionsDesignTimeDbContextFactory.cs
      Migrations/

tests/Modules/Missions/DndCampaign.Modules.Missions.Tests/
  Application/
  Architecture/
  Component/
  Domain/
  Infrastructure/
```

Los nombres exactos podrán simplificarse durante las tareas, pero no cambiarán el ownership, la dirección de dependencias ni la superficie pública sin revisar este plan.

## Dependencias entre módulos

Missions referenciará los proyectos Campaigns y Characters exclusivamente para consumir:

- `ICampaignAccessReader.GetAccessAsync(campaignId, userId)`;
- `IActiveCharacterReader.GetActiveAsync(campaignId, userId)`.

El segundo contrato solo se invocará al crear con rol `Player`. Una creación con rol `Dm` no dependerá de que el DM tenga personaje activo. Campaigns y Characters no referenciarán Missions, por lo que el grafo continuará acíclico:

```text
Missions ──────> Campaigns ───────> Access
    └──────────> Characters ──────> Campaigns + Access
```

Las pruebas arquitectónicas añadirán Missions a los conjuntos de módulos, permitirán únicamente esas dos aristas y prohibirán que el host utilice namespaces internos o `MissionsDbContext`.

## Modelo de dominio y persistencia

### Agregado `Mission`

La misión conservará:

- `Id`: identificador público no vacío;
- `CampaignId`: campaña propietaria;
- `CreatedByUserId`: usuario que la registró, privado en HTTP y usado para autorizar el borrado;
- `AuthorType`: `Dm` o `Player`;
- `AuthorCharacterId`: personaje activo al crear como jugador; `null` para DM;
- `AuthorCharacterName`: instantánea normalizada de hasta 80 caracteres para jugador; `null` para DM;
- `Title`: texto normalizado entre 2 y 120 caracteres;
- `Description`: texto plano opcional, normalizado y de hasta 5.000 caracteres;
- `Status`: `Active`, `Completed`, `Failed` o `Cancelled`;
- `IsMain`: indicador de principal, válido únicamente con estado `Active`;
- `CreatedAt`: instante UTC inmutable proporcionado por `TimeProvider`;
- `UpdatedAt`: instante UTC nullable hasta el primer cambio;
- una secuencia interna e inmutable para desempatar la ordenación sin exponer identificadores adicionales.

`CreateForPlayer` exigirá usuario, personaje y nombre; `CreateForDm` rechazará autoría de personaje. Ambos fijarán `Active`. `UpdateDetails` sustituirá título y descripción; `ChangeStatus` desmarcará la misión al pasar a un estado cerrado; `MarkAsMain` rechazará una cerrada y `ClearMain` será idempotente. Ningún método aceptará fechas funcionales o modificará la autoría.

La autorización de campaña y de borrado no se incluirá en la entidad: pertenece a Application y a los contratos intermodulares.

### Esquema PostgreSQL

Missions utilizará `MissionsDbContext`, historial de migraciones y esquema `missions`. La tabla `missions` no tendrá foreign keys hacia otros esquemas. Incluirá:

- clave primaria por `id`;
- longitudes, nullability y conversión de estados coherentes con el agregado;
- restricción que exige autor y personaje coherentes con `author_type`;
- restricción que impide `is_main = true` para un estado distinto de `Active`;
- índice único parcial por `campaign_id WHERE is_main = true`;
- índice de consulta por campaña, grupo de estado y marcas temporales;
- índice por `(campaign_id, created_by_user_id)` para el permiso de eliminación;
- secuencia interna o columna de identidad para desempates estables.

La ausencia de foreign keys intermodulares es deliberada: Campaigns y Characters validan el contexto al escribir, y la instantánea conserva la autoría legible si el personaje se renombra, reasigna o elimina.

### Orden de consulta

El repositorio devolverá una proyección `AsNoTracking` ordenada en SQL:

1. misión principal activa;
2. restantes activas por `CreatedAt DESC` y secuencia interna descendente;
3. cerradas por `COALESCE(UpdatedAt, CreatedAt) DESC` y secuencia interna descendente.

El primer incremento devolverá el registro completo de la campaña, conforme al contrato aceptado. No se añadirá paginación, filtros o búsqueda de forma implícita; si el volumen real lo exige se especificará aparte.

### Consistencia de la principal

Crear una misión como principal, marcar otra, cerrar la principal o eliminarla se ejecutará en una transacción local de Missions. El repositorio desmarcará la principal anterior antes de marcar la nueva y el índice único parcial actuará como última defensa.

Para carreras entre peticiones, cada promoción obtiene dentro de la transacción un advisory lock PostgreSQL derivado de la campaña. Las promociones de una misma campaña quedan serializadas, mientras el índice único parcial actúa como última defensa. Las pruebas con PostgreSQL real comprueban que dos promociones concurrentes completan sin dejar nunca dos principales.

## Casos de uso de Application

### Listar misiones

1. Resolver acceso con `ICampaignAccessReader`.
2. Devolver `404` si la campaña no existe y `403` si existe sin rol efectivo.
3. Consultar todas las misiones de la campaña con el orden definido.
4. Proyectar `canDelete = role == Dm || CreatedByUserId == actor`.
5. Mapear autor de jugador desde la instantánea guardada y autor DM como `Dirección de campaña`.

El identificador del usuario creador no abandonará Application.

### Crear misión

1. Exigir campaña existente y rol `Dm` o `Player`.
2. Para `Player`, resolver el personaje activo y devolver `409` si no existe.
3. Para `Dm`, crear autoría de dirección sin invocar Characters.
4. Validar título y descripción y asignar estado y marcas temporales en el servidor.
5. Si `isMain=true`, crear y sustituir la principal dentro de una única transacción.
6. Devolver `201 Created` con `Location` y permisos derivados.

El command no aceptará usuario, personaje, estado inicial ni fechas.

### Editar misión

1. Exigir campaña existente y cualquier rol efectivo.
2. Buscar por `CampaignId` y `MissionId`; devolver `404` si no coinciden.
3. Sustituir título, descripción y estado; conservar autoría y `CreatedAt`.
4. Si el nuevo estado es cerrado y la misión era principal, desmarcarla en la misma transacción.
5. Asignar `UpdatedAt` desde `TimeProvider` y devolver `200 OK`.

Dos ediciones simultáneas de contenido seguirán semántica de último `PUT` confirmado. Historial, fusión y control optimista quedan fuera del spec.

### Marcar o desmarcar principal

- Marcar exige misión existente en la campaña y estado `Active`; sustituye la principal anterior transaccionalmente y devuelve `200 OK`.
- Desmarcar exige misión existente en la campaña; si no era principal no cambia nada y devuelve `204 No Content`.
- Ambas operaciones admiten DM o jugador aceptado sin exigir personaje activo.

### Eliminar misión

1. Exigir campaña existente y rol efectivo.
2. Buscar la misión dentro de la campaña.
3. Autorizar cuando el actor sea DM o coincida con `CreatedByUserId`; otro jugador recibe `403`.
4. Eliminar definitivamente en una transacción local. Si era principal, su eliminación deja naturalmente la campaña sin principal.
5. Devolver `204 No Content`.

No habrá borrado en cascada desde Campaigns o Characters, restauración ni promoción automática de otra misión.

## Contrato HTTP

### Listado

`GET /api/v1/campaigns/{campaignId}/missions`

Respuesta `200 OK`:

```json
{
  "items": [
    {
      "id": "00000000-0000-0000-0000-000000000000",
      "campaignId": "00000000-0000-0000-0000-000000000000",
      "title": "Misión de campaña",
      "description": "Descripción genérica opcional.",
      "status": "active",
      "isMain": true,
      "authorType": "player",
      "authorCharacterId": "00000000-0000-0000-0000-000000000000",
      "authorDisplayName": "Personaje jugador",
      "createdAt": "2026-08-23T12:00:00Z",
      "updatedAt": null,
      "canDelete": true
    }
  ]
}
```

El ejemplo es genérico y no incorpora contenido editorial de ninguna aventura.

### Escrituras

- `POST /api/v1/campaigns/{campaignId}/missions` con `{ "title": "...", "description": "...", "isMain": false }`.
- `PUT /api/v1/campaigns/{campaignId}/missions/{missionId}` con `{ "title": "...", "description": "...", "status": "active" }`.
- `PUT /api/v1/campaigns/{campaignId}/missions/{missionId}/main` sin body.
- `DELETE /api/v1/campaigns/{campaignId}/missions/{missionId}/main` sin body.
- `DELETE /api/v1/campaigns/{campaignId}/missions/{missionId}` sin body.

Los DTO de escritura no tendrán propiedades de fecha, usuario, personaje o autor. Los controladores extraerán el usuario de la sesión, harán binding y mapearán resultados; no contendrán reglas de rol, ownership o principal.

Los errores utilizarán `ProblemDetails` o `ValidationProblemDetails`:

- `400`: título, descripción o estado inválido;
- `401`: ausencia de sesión válida;
- `403`: campaña sin acceso o borrado solicitado por un jugador distinto del creador;
- `404`: campaña inexistente o misión inexistente dentro de la campaña autorizada;
- `409`: creación de jugador sin personaje activo, intento de marcar una misión cerrada o conflicto concurrente agotado.

No se devolverán excepciones, `CreatedByUserId`, secuencias internas ni datos de otras campañas.

## Integración del host y límites backend

- Añadir Missions y su proyecto de tests a `DndCampaign.slnx`.
- Añadir referencias de Missions a Campaigns y Characters y del host a Missions.
- Actualizar el Dockerfile para restaurar, compilar y ejecutar los proyectos nuevos en los recorridos existentes.
- Registrar `AddMissionsModule`, `MapMissionsModule` y `ApplyMissionsMigrationsAsync` en `Program.cs`.
- Aplicar migraciones en orden `Access -> Campaigns -> Characters -> Journal -> Missions` cuando la bandera existente esté activa.
- Ampliar las fitness functions para reconocer Missions, su esquema y las únicas aristas aprobadas.
- Reutilizar la conexión PostgreSQL y la resolución de configuración actual.

No se requieren servicios externos, secretos, blobs, cambios de red ni recursos Terraform nuevos.

## Estructura objetivo de frontend

```text
apps/web/src/app/modules/missions/
  missions.routes.ts
  api/
    missions.client.ts
    mission.contracts.ts
    missions.client.spec.ts
  mission-page/
    mission.page.ts
    mission.page.html
    mission.page.scss
  mission-form/
    mission-form.component.ts
    mission-form.component.html
  missions.pages.spec.ts
```

No se exportarán internals de Missions. Campaigns añadirá un enlace por URL y no importará sus componentes, cliente o DTO. Missions consumirá `CampaignsClient`, `CharactersClient` y los datos de sesión solo desde las APIs públicas existentes para proyectar rol y personaje activo; la API repetirá esas comprobaciones autoritativamente.

### Routing y providers

- Añadir la ruta lazy autenticada `/campaigns/:campaignId/missions`.
- Proveer `MissionsClient`, `CampaignsClient` y `CharactersClient` en el ámbito de la ruta.
- Incorporar el entrypoint al composition root y adaptar la fitness function para reconocerlo.
- Añadir `Abrir misiones` en el detalle de campaña para ambos roles mediante `routerLink`.

No se añadirá un guard de rol: ambos roles pueden operar y la API decide cada permiso.

### Estado de página

La página usará signals y RxJS en el ámbito del recorrido para mantener:

- campaña, rol y personaje activo de presentación;
- colección recibida en el orden autoritativo;
- formulario de alta o edición y misión actualmente editada;
- operaciones de carga, guardado, cambio de principal y eliminación;
- mensajes de validación, autorización, conflicto y fallo.

Crear o editar sustituirá el estado con la respuesta y aplicará la misma función local de ordenación mientras se confirma con el servidor. Los cambios que afecten a principal recargarán el registro completo para incorporar tanto la misión promovida como la desmarcada. Eliminar retirará el elemento; si era principal, limpiará también la sección destacada. Ante `409` se recargará el registro autoritativo.

### Presentación y formularios

- Estado vacío con explicación y acción de alta.
- Sección destacada para la principal activa; secciones separadas para activas y cerradas.
- Formulario con título, descripción multilínea, contador y selector `Marcar como principal` solo en alta.
- Ningún campo, etiqueta o placeholder de fecha.
- Identificación visible del personaje autor o `Dirección de campaña`, además de marcas técnicas de creación y edición.
- Acción `Editar` para todos los miembros y controles de estado con etiquetas en español.
- Acción de marcar o desmarcar principal solo en misiones activas.
- Acción `Eliminar` únicamente con `canDelete=true` y confirmación explícita que advierta del borrado definitivo.
- Aviso y enlace a gestión de personajes cuando un jugador no tenga personaje activo; la edición y el borrado propio permanecen disponibles.
- Interpolación de texto y `white-space: pre-wrap`; no se usará `innerHTML`.
- Estados de botones, foco y errores comprensibles durante las peticiones y accesibles para tecnologías de asistencia.

## Observabilidad y privacidad

Missions añadirá contador y duración para `list`, `create`, `update`, `set_main`, `clear_main` y `delete`, con outcomes acotados como `success`, `validation`, `forbidden`, `not_found`, `conflict` y `failure`.

No se usarán como etiquetas ni mensajes:

- título o descripción;
- nombre del personaje;
- identificadores de misión, campaña, personaje o usuario.

Los logs conservarán correlación y tipo de operación. Las respuestas de error no reflejarán texto rechazado. El dashboard de plataforma incorporará operaciones agregadas de Missions sin dimensiones de alta cardinalidad.

## Estrategia de pruebas

### Dominio

- creación válida para jugador y DM, con normalización de título y descripción;
- rechazo de identificadores, autoría, texto o combinaciones de autor inválidas;
- estado inicial activo y ausencia total de fechas funcionales;
- edición conserva campaña, creador, autor y `CreatedAt`;
- cerrar una principal la desmarca y reabrir no la marca;
- una misión cerrada no puede marcarse como principal;
- operaciones idempotentes de desmarcado.

### Application

- listado permitido para DM y jugador con `canDelete` derivado correctamente;
- creación de DM sin consultar Characters;
- creación de jugador con personaje activo y `409` sin él;
- edición colaborativa para DM y cualquier jugador aceptado sin exigir personaje activo;
- marcado, sustitución y desmarcado de principal;
- eliminación propia por jugador, ajena rechazada y eliminación de cualquiera por DM;
- eliminación de principal sin promoción automática;
- distinción `403`/`404` y aislamiento por campaña;
- uso exclusivo de `TimeProvider` e instantánea de Characters.

### Persistencia PostgreSQL

- migración y esquema `missions` en una base efímera;
- longitudes, nullability, checks e índices esperados;
- índice único parcial para principal y check principal-activa;
- orden estable de principal, activas y cerradas;
- dos promociones concurrentes nunca producen dos principales;
- creación principal concurrente y reintentos sin cambios parciales;
- cerrar o eliminar la principal libera correctamente la unicidad;
- persistencia de autoría aunque el personaje ya no exista;
- edición y eliminación no afectan otra campaña.

### Contrato HTTP

- recorrido autenticado de crear, listar, editar, marcar y eliminar;
- autoría distinta de DM y jugador;
- ausencia de fechas funcionales y `CreatedByUserId` en JSON;
- jugador sin personaje recibe `409` al crear, pero puede editar y borrar una misión propia anterior;
- otro jugador puede editar pero recibe `403` al borrar;
- DM puede borrar cualquier misión;
- `401`, validación, `403`, `404`, `409` y manipulación de rutas;
- carreras de principal sobre PostgreSQL real.

### Frontend y arquitectura

- URLs, métodos y DTO de `MissionsClient` sin campos de fecha funcional;
- ruta lazy, guard autenticado y enlace desde detalle de campaña;
- principal, secciones activa/cerrada y orden recibido;
- estados vacío, carga y error;
- alta condicionada por personaje activo solo para jugador;
- edición colaborativa y autoría original conservada;
- cambio de estado y principal con recarga autoritativa;
- eliminación visible solo con `canDelete` y confirmación;
- ausencia de controles de fecha;
- validación de longitud y renderizado sin HTML;
- fitness functions sin deep imports ni ciclos.

## Fases de implementación propuestas

1. Crear el proyecto Missions, su fachada, capas internas, proyecto de tests y aristas arquitectónicas permitidas.
2. Implementar agregado, `MissionsDbContext`, repositorio, restricciones e índices y migración del esquema `missions`.
3. Implementar autorización y handlers de listado, creación por ambos roles, edición colaborativa y principal transaccional.
4. Implementar eliminación por creador o DM y completar pruebas de concurrencia y aislamiento con PostgreSQL real.
5. Exponer los endpoints, registrar host y migraciones y completar pruebas HTTP.
6. Crear el módulo Angular Missions, cliente, contratos, ruta lazy y estado de página.
7. Implementar secciones, formularios sin fechas, permisos visibles, confirmación de borrado y enlace desde Campaigns.
8. Completar pruebas frontend, fitness functions, observabilidad y verificación de privacidad.
9. Ejecutar suites, builds, imágenes, Compose y smoke tests; actualizar documentación, índice y trazabilidad del roadmap con evidencia.

Cada fase terminará con las suites aplicables verdes. La navegación no se habilitará hasta que los endpoints y su autorización estén integrados.

## Despliegue y reversibilidad

- La migración es aditiva: crea el esquema y tabla de Missions sin modificar datos existentes.
- El binario anterior ignora el nuevo esquema, por lo que un rollback de aplicación no destruye misiones; la tabla no se eliminará automáticamente.
- El orden documentado de migraciones pasará a ser `Access -> Campaigns -> Characters -> Journal -> Missions`.
- La API se desplegará antes o junto al frontend que expone el enlace.
- Antes de publicar se ejecutarán pruebas sobre PostgreSQL 18 efímero y copia de seguridad conforme al runbook vigente.
- Después se comprobarán readiness, registro vacío y un recorrido genérico de escritura en un entorno no productivo.
- No habrá migración destructiva de rollback; cualquier corrección de esquema se realizará mediante roll-forward.

## Riesgos y mitigaciones

| Riesgo | Mitigación |
|---|---|
| Missions consulta internals de otros módulos | Contratos existentes, referencias unidireccionales y fitness functions |
| El cliente suplanta autor o introduce fechas | DTO cerrados y autoría/tiempo derivados de sesión, Campaigns, Characters y `TimeProvider` |
| Dos peticiones dejan dos principales | Advisory lock transaccional por campaña, índice único parcial y pruebas concurrentes en PostgreSQL |
| Cerrar o borrar una principal deja estado incoherente | Operación transaccional y criterios explícitos sin promoción automática |
| Un jugador borra una misión ajena | `CreatedByUserId` privado, `canDelete` derivado y comprobación repetida en el command |
| El DM no puede corregir un alta errónea ajena | Permiso explícito de borrado global limitado a su propia campaña |
| Edición cambia u oculta autoría | Campos de autoría inmutables y DTO de escritura limitado |
| Contenido inyecta HTML o scripts | Texto plano, interpolación Angular, sin `innerHTML` y pruebas de renderizado |
| Fuga entre campañas | Toda consulta incluye `CampaignId`, acceso previo y escenarios manipulando rutas |
| El registro crece sin paginación | Consulta acotada por campaña, observación de volumen y futuro spec de paginación si existe evidencia |
| Migración bloquea el arranque | Migración aditiva, PostgreSQL efímero en CI, backup y estrategia roll-forward |

## Documentación que se actualizará al implementar

- `docs/operations/migraciones-de-base-de-datos.md` con Missions y el nuevo orden.
- `docs/architecture/diagrama-de-componentes.md` y el diagrama de despliegue si su inventario queda desactualizado.
- dashboard y documentación de observabilidad para las métricas de Missions.
- `docs/specs/README.md` y el estado agregado del roadmap cuando exista evidencia de criterios cumplidos.
- `tasks.md` con tareas y evidencias concretas, después de aprobar este plan.

No se propone un ADR nuevo: los límites de módulo, contratos, persistencia y frontend aplican ADR-0004 y ADR-0005; las decisiones funcionales de fechas, permisos, borrado y principal quedan registradas en el spec 008.

## Validación

El usuario aprobó el plan y solicitó expresamente crear `tasks.md` e implementar el incremento el 2026-08-23. La implementación pasa 77 pruebas .NET en Docker sobre PostgreSQL y Azurite, 63 pruebas Angular, build Angular de producción, compilación de solución y validación de ambas configuraciones Compose. La construcción adicional de las imágenes finales `api` y `web` no pudo iniciarse porque el sistema de aprobaciones alcanzó su límite de uso; el target de pruebas sí construyó y publicó la API en Release.
