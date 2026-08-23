# Plan 007: Bitácora compartida de campaña

- Estado: Ejecutado
- Fecha: 2026-08-23
- Especificación: [spec.md](spec.md)
- ADR aplicables: [ADR-0004](../../adr/0004-arquitectura-modular-cqrs-y-limites-de-dependencia.md) y [ADR-0005](../../adr/0005-frontend-modular-por-capacidades.md)
- Dependencias: [spec 004](../004-creacion-campanas/spec.md), [spec 005](../005-personajes-campana/spec.md) y [spec 006](../006-resumen-personajes-activos/spec.md)
- Validación funcional: spec aprobado explícitamente por el usuario el 2026-08-23

## Resultado esperado

La portada de campaña permitirá abrir una bitácora compartida. Sus miembros autorizados leerán páginas de entradas ordenadas de más recientes a más antiguas. Un jugador con personaje activo podrá introducir una entrada; cualquier jugador aceptado podrá editar cualquier entrada de esa campaña; solo el jugador que la introdujo podrá eliminarla. El DM tendrá acceso completo de lectura y ninguna operación de escritura.

Cada entrada mostrará de forma permanente el personaje que la introdujo y su fecha de creación. Una edición actualizará el contenido y la fecha de edición, pero no la autoría ni la posición cronológica. La API será la fuente autoritativa de acceso, rol, personaje activo y permisos; el frontend solo proyectará esas capacidades.

## Diagnóstico de partida

- Campaigns publica `ICampaignAccessReader`, que distingue campaña inexistente, DM, jugador aceptado y usuario sin acceso.
- Characters mantiene el ownership y la unicidad del personaje activo por usuario y campaña, pero todavía no publica una consulta intermodular para resolverlo.
- Characters ya conserva el nombre, campaña, propietario y estado activo necesarios para producir una instantánea mínima de autoría.
- La API registra cada módulo mediante una fachada, usa un proyecto y esquema PostgreSQL por módulo y aplica migraciones desde el host en orden explícito.
- Angular está organizado por capacidades, con rutas lazy, providers acotados, clientes propios y fitness functions que prohíben deep imports y ciclos.
- La portada de campaña ya conoce el rol efectivo y compone Characters mediante su API pública.
- No existe módulo, esquema, endpoint, ruta, cliente ni interfaz de bitácora.
- Producción ejecuta una sola réplica de la API y no existe infraestructura de tiempo real; este incremento no añadirá sincronización, historial ni bloqueo colaborativo.

## Principios de ejecución

1. **Ownership local.** Journal será propietario de las entradas y no consultará tablas ni entidades de Campaigns o Characters.
2. **Contratos mínimos.** Journal consumirá el acceso efectivo existente de Campaigns y una nueva proyección de personaje activo publicada por Characters.
3. **Autorización en Application.** Todos los commands y queries comprobarán campaña y rol; controladores, flags y guards serán únicamente adaptadores o ayudas de UX.
4. **Autoría inmutable.** La creación capturará usuario creador, identificador de personaje y nombre visible; ninguna edición posterior modificará esos campos.
5. **Permisos separados.** `canEdit` se derivará del rol `Player`; `canDelete`, del rol y del usuario que introdujo la entrada.
6. **Paginación por clave.** La lectura no usará offset y mantendrá un orden estable aunque se creen entradas entre páginas.
7. **Texto sin interpretación.** El contenido se mostrará mediante interpolación de Angular y estilos que preserven saltos de línea, nunca mediante HTML inyectado.
8. **Verticales verdes.** Cada fase conservará compilación, límites modulares y suites existentes antes de habilitar la navegación.

## Estructura objetivo de API

```text
apps/api/Modules/
  Characters/DndCampaign.Modules.Characters/
    Contracts/ActiveCharacters/
      IActiveCharacterReader.cs
    Infrastructure/Access/
      ActiveCharacterReader.cs

  Journal/DndCampaign.Modules.Journal/
    DndCampaign.Modules.Journal.csproj
    JournalModule.cs
    Properties/
      AssemblyInfo.cs
    Api/
      JournalEntriesController.cs
      InternalControllerFeatureProvider.cs
    Application/
      Abstractions/
        JournalResult.cs
      Entries/
        JournalEntryHandlers.cs
      Ports/
        IJournalEntryRepository.cs
        IJournalMetrics.cs
        IJournalCursorCodec.cs
    Domain/Entries/
      JournalEntry.cs
    Infrastructure/
      Observability/
        JournalMetrics.cs
      Pagination/
        JournalCursorCodec.cs
      Persistence/
        JournalDbContext.cs
        JournalRepository.cs
        JournalDesignTimeDbContextFactory.cs
        Migrations/

tests/Modules/Journal/DndCampaign.Modules.Journal.Tests/
  Application/
  Architecture/
  Component/
  Domain/
  Infrastructure/
```

Los nombres exactos podrán simplificarse durante las tareas, pero no cambiarán el ownership, la dirección de dependencias ni la superficie pública sin revisar este plan.

## Contrato entre Characters y Journal

Characters publicará un contrato de solo lectura equivalente a:

- `IActiveCharacterReader.GetActiveAsync(campaignId, userId)`;
- resultado nullable con `CharacterId` y `Name` únicamente.

La implementación consultará con `AsNoTracking` un personaje que coincida simultáneamente con campaña, propietario y `IsActive`. No devolverá CA, iniciativa, imagen, entidades EF ni `IQueryable`.

Journal referenciará los proyectos Campaigns y Characters. Characters no referenciará Journal, por lo que el grafo continuará acíclico:

```text
Journal ───────> Campaigns ───────> Access
    └──────────> Characters ──────> Campaigns + Access
```

Las pruebas arquitectónicas permitirán exclusivamente esas dependencias y actualizarán la superficie pública de Characters para admitir su fachada y los tipos explícitos del contrato. El host seguirá utilizando solo las fachadas de los módulos.

## Modelo de dominio y persistencia

### Agregado `JournalEntry`

La entrada conservará:

- `Id`: identificador público no vacío;
- `CampaignId`: campaña propietaria;
- `CreatedByUserId`: usuario que la introdujo, privado en el contrato HTTP;
- `AuthorCharacterId`: personaje activo al crearla;
- `AuthorCharacterName`: instantánea normalizada de su nombre, máximo 80 caracteres;
- `Content`: texto normalizado entre 1 y 5.000 caracteres;
- `CreatedAt`: fecha inmutable proporcionada por `TimeProvider`;
- `UpdatedAt`: nullable hasta la primera edición;
- una clave interna e inmutable de paginación, distinta de los identificadores públicos.

`Create` validará identificadores, nombre, contenido y fecha. `UpdateContent` solo podrá sustituir el contenido y asignar `UpdatedAt`; nunca recibirá campos de autoría. El agregado no contendrá reglas de membresía, que pertenecen a los handlers y a los contratos intermodulares.

### Esquema PostgreSQL

Journal utilizará `JournalDbContext`, historial de migraciones y esquema `journal`. La tabla `journal_entries` no tendrá foreign keys hacia esquemas ajenos. Incluirá:

- clave primaria por `Id`;
- longitudes y nullability coherentes con el agregado;
- índice de consulta por `(CampaignId, CreatedAt DESC, PaginationKey DESC)`;
- índice por `(CampaignId, CreatedByUserId)` para autorización y evolución operativa.

La ausencia de foreign keys intermodulares es deliberada: el contrato de Characters valida la creación y la instantánea mantiene la entrada legible si el personaje se renombra, reasigna o elimina.

### Cursor y orden

El repositorio implementará keyset pagination. El cursor será una cadena Base64Url versionada que contenga únicamente `CreatedAt` y la clave interna de paginación; no incluirá `entryId`, `campaignId`, usuario ni personaje. El codec rechazará versión, longitud o forma inválidas mediante un resultado de validación, sin excepciones públicas.

La consulta aplicará orden descendente por ambos valores y pedirá `limit + 1` elementos para determinar `nextCursor`. El tamaño predeterminado será 20 y el máximo 50. Insertar una entrada nueva mientras se cargan páginas no duplicará ni desplazará las páginas antiguas ya recorridas.

## Casos de uso de Application

### Listar entradas

1. Resolver acceso con `ICampaignAccessReader`.
2. Devolver `404` si la campaña no existe y `403` si existe sin rol efectivo.
3. Validar límite y cursor.
4. Consultar una página `AsNoTracking` dentro de la campaña.
5. Proyectar `canEdit = role == Player` y `canDelete = role == Player && CreatedByUserId == actor`.

DM y jugadores reciben el mismo contenido y autoría. El identificador del usuario creador solo se usa internamente para derivar permisos.

### Crear entrada

1. Exigir campaña existente y rol `Player`.
2. Resolver el personaje activo del actor mediante `IActiveCharacterReader`.
3. Devolver `409` con un problema específico si no existe personaje activo.
4. Crear el agregado usando exclusivamente la instantánea devuelta por Characters y el reloj del servidor.
5. Persistir y devolver `201 Created` con `Location`, `canEdit=true` y `canDelete=true`.

El command no aceptará identificador o nombre de personaje ni fechas desde el cliente.

### Editar entrada

1. Exigir campaña existente y rol `Player`; el personaje activo no participa en esta autorización.
2. Buscar la entrada por `CampaignId` y `EntryId`.
3. Devolver `404` si no pertenece a esa campaña o no existe.
4. Validar y sustituir solo `Content`; conservar autoría y `CreatedAt` y asignar `UpdatedAt` desde `TimeProvider`.
5. Devolver la representación actualizada con permisos derivados para el actor.

Cualquier jugador aceptado podrá completar este command. El DM y usuarios sin rol efectivo recibirán `403`. Conforme al alcance aprobado, dos ediciones simultáneas seguirán semántica de último `PUT` confirmado; control optimista, historial y fusión quedan diferidos.

### Eliminar entrada

1. Exigir campaña existente y rol `Player`.
2. Buscar la entrada dentro de la campaña.
3. Exigir `CreatedByUserId == actor`; otro jugador recibirá `403` aunque pueda editarla.
4. Eliminar y confirmar una sola escritura local; devolver `204 No Content`.

No habrá borrado en cascada desde Characters ni restauración.

## Contrato HTTP

### Listado

`GET /api/v1/campaigns/{campaignId}/journal/entries?cursor={cursor}&limit={limit}`

Respuesta `200 OK`:

```json
{
  "items": [
    {
      "id": "00000000-0000-0000-0000-000000000000",
      "campaignId": "00000000-0000-0000-0000-000000000000",
      "authorCharacterId": "00000000-0000-0000-0000-000000000000",
      "authorCharacterName": "Personaje jugador",
      "content": "Una pista genérica de la campaña.",
      "createdAt": "2026-08-23T12:00:00Z",
      "updatedAt": null,
      "canEdit": true,
      "canDelete": false
    }
  ],
  "nextCursor": null
}
```

El ejemplo es genérico y no incorpora contenido editorial de ninguna aventura.

### Escrituras

- `POST /api/v1/campaigns/{campaignId}/journal/entries` con `{ "content": "..." }`.
- `PUT /api/v1/campaigns/{campaignId}/journal/entries/{entryId}` con `{ "content": "..." }`.
- `DELETE /api/v1/campaigns/{campaignId}/journal/entries/{entryId}` sin body.

Los controladores extraerán el usuario de la sesión, harán binding y mapearán resultados. No contendrán reglas de rol, ownership o personaje activo.

Los errores utilizarán `ProblemDetails` o `ValidationProblemDetails`:

- `400`: contenido, límite o cursor inválido;
- `401`: ausencia de sesión válida;
- `403`: campaña sin acceso, rol sin permiso de escritura o borrado por un jugador distinto del creador;
- `404`: campaña inexistente o entrada inexistente dentro de la campaña autorizada;
- `409`: creación por jugador sin personaje activo.

No se devolverán excepciones, `CreatedByUserId`, claves internas ni datos de otras campañas.

## Integración del host y límites backend

- Añadir Journal y su proyecto de tests a `DndCampaign.slnx`.
- Añadir referencias de Journal a Campaigns y Characters y del host a Journal.
- Actualizar el Dockerfile para restaurar y probar ambos proyectos nuevos.
- Registrar `AddJournalModule`, `MapJournalModule` y `ApplyJournalMigrationsAsync` en `Program.cs`.
- Aplicar migraciones en orden `Access -> Campaigns -> Characters -> Journal` cuando la bandera existente esté activa.
- Ampliar las fitness functions para reconocer Journal, su esquema y las únicas aristas aprobadas.
- Mantener una única conexión PostgreSQL y reutilizar la resolución de configuración actual; renombrar `ConnectionStrings:Campaigns` queda fuera de este incremento.

No se requieren servicios externos, secretos, blobs, cambios de red ni recursos Terraform nuevos.

## Estructura objetivo de frontend

```text
apps/web/src/app/modules/journal/
  journal.routes.ts
  api/
    journal.client.ts
    journal.contracts.ts
    journal.client.spec.ts
  journal-page/
    journal.page.ts
    journal.page.html
    journal.page.scss
  entry-form/
    journal-entry-form.component.ts
    journal-entry-form.component.html
  journal.pages.spec.ts
```

No se exportarán internals de Journal. Campaigns añadirá un enlace por URL y no importará sus componentes, cliente o DTO. Journal consumirá `CampaignsClient`, `CharactersClient` y `SessionStore` exclusivamente desde las APIs públicas existentes para proyectar el rol y detectar el personaje activo en la experiencia; la API repetirá esas comprobaciones de forma autoritativa.

### Routing y providers

- Añadir la ruta lazy autenticada `/campaigns/:campaignId/journal`.
- Proveer `JournalClient`, `CampaignsClient` y `CharactersClient` en el ámbito de la ruta.
- Incorporar el entrypoint de rutas al composition root y adaptar la fitness function para reconocerlo.
- Añadir `Abrir bitácora` en el detalle de campaña para ambos roles mediante `routerLink`.

No se añadirá un guard de rol: el rol se usa para presentación y la API decide cada permiso.

### Estado de página

La página usará signals y RxJS en el ámbito del recorrido para mantener:

- campaña, rol y personaje activo de presentación;
- `items` en el orden recibido;
- `nextCursor` y carga de páginas anteriores;
- contenido de creación o edición y entrada actualmente editada;
- operaciones de carga, guardado y eliminación;
- mensajes de validación, autorización, conflicto y fallo.

Al cargar otra página se anexarán elementos y se deduplicarán defensivamente por `id`. Crear insertará la respuesta al principio; editar sustituirá la representación en su posición; eliminar retirará la entrada sin recargar páginas ya obtenidas.

### Presentación y formularios

- Estado vacío con una explicación de uso de la bitácora.
- Formulario multilínea para jugadores con personaje activo, contador y máximo de 5.000 caracteres.
- Aviso y enlace a gestión de personajes cuando un jugador no tenga personaje activo.
- Identificación visible `Introducida por {authorCharacterName}` y fechas de creación/edición.
- Acción `Editar` en todas las entradas para jugadores; nunca para DM.
- Acción `Eliminar` solo cuando `canDelete` sea `true`, con confirmación explícita antes de enviar la petición.
- Botón `Cargar entradas anteriores` mientras exista `nextCursor`.
- Interpolación de texto y `white-space: pre-wrap`; no se usará `innerHTML`.
- Estados de botones y foco comprensibles durante peticiones, sin ocultar el error a tecnologías de asistencia.

El frontend confiará en `canEdit` y `canDelete` para dibujar acciones, pero no enviará usuario ni rol al backend.

## Observabilidad y privacidad

Journal añadirá contador y duración para `list`, `create`, `update` y `delete`, con outcomes acotados como `success`, `validation`, `forbidden`, `not_found`, `conflict` y `failure`.

No se usarán como etiquetas ni mensajes:

- contenido de la entrada;
- nombre del personaje;
- cursor;
- identificadores de entrada, campaña, personaje o usuario.

Los logs conservarán correlación y tipo de operación. Las respuestas de error no reflejarán el contenido rechazado. La telemetría HTTP no añadirá query strings de cursores y el dashboard existente incorporará las operaciones agregadas de Journal sin variables de alta cardinalidad.

## Estrategia de pruebas

### Dominio

- creación válida y normalización de contenido;
- rechazo de identificadores, autor, contenido o fechas inválidas;
- edición conserva campaña, creador, personaje, nombre y `CreatedAt`;
- primera y sucesivas ediciones actualizan `UpdatedAt`.

### Application

- listado permitido para DM y jugador, con flags distintos;
- creación exclusiva de jugador con personaje activo;
- `409` sin personaje activo y ausencia de escritura parcial;
- edición por el creador y por otro jugador;
- edición rechazada para DM;
- eliminación permitida solo al creador y rechazada para otro jugador o DM;
- distinción `403`/`404` y aislamiento por campaña;
- uso del reloj del servidor y de la instantánea de Characters.

### Persistencia PostgreSQL

- migración y esquema `journal` en una base efímera;
- longitudes, nullability e índices esperados;
- orden descendente estable, empates, primera página y páginas posteriores;
- creación concurrente entre páginas sin duplicados;
- rechazo de cursores inválidos;
- persistencia de la instantánea aunque no exista ya el personaje;
- actualización y eliminación sin afectar otra campaña.

### Contrato intermodular y componente HTTP

- Characters devuelve únicamente el personaje activo propio de la campaña indicada;
- creación HTTP ignora cualquier intento de inyectar autoría porque el DTO no admite esos campos;
- recorrido autenticado de crear, listar, editar por otro jugador y eliminar por el creador;
- DM con lectura y `403` en las tres escrituras;
- jugador sin personaje con `409` al crear, pero capaz de editar;
- `401`, validación, cursor, `403`, `404` y manipulación de rutas;
- ausencia de `CreatedByUserId` y claves de paginación en JSON.

### Frontend y arquitectura

- URLs, métodos, query params y DTO de `JournalClient`;
- ruta lazy y guard autenticado;
- enlace desde detalle de campaña;
- orden, carga adicional y deduplicación;
- estados vacío, carga y error;
- creación condicionada por personaje activo;
- edición visible para cualquier jugador y autoría original conservada tras responder;
- eliminación visible solo con `canDelete` y confirmación;
- vista DM de solo lectura;
- validación de longitud, saltos de línea y renderizado sin HTML;
- fitness functions sin deep imports ni ciclos.

## Fases de implementación propuestas

1. Caracterizar la consulta de personaje activo y fijar el nuevo contrato público de Characters con sus pruebas de límites.
2. Crear el proyecto Journal, fachada, capas internas, proyecto de tests y aristas arquitectónicas permitidas.
3. Implementar agregado, `DbContext`, repositorio, cursor keyset y migración del esquema `journal`.
4. Implementar autorización y handlers de listado, creación, edición colaborativa y eliminación por creador.
5. Exponer los cuatro endpoints, registrar host/migraciones y completar pruebas HTTP con PostgreSQL real.
6. Crear el módulo Angular Journal, cliente, contratos, ruta lazy y estados de página.
7. Implementar listado, paginación, formularios, permisos visibles, confirmación de borrado y enlace desde Campaigns.
8. Completar pruebas frontend, fitness functions, observabilidad y verificación de privacidad.
9. Ejecutar suites, builds, imágenes, Compose y smoke tests; actualizar documentación, diagramas, runbook, índice y trazabilidad del roadmap.

Cada fase terminará con las suites aplicables verdes. La navegación no se habilitará hasta que los endpoints y su autorización estén integrados.

## Despliegue y reversibilidad

- La migración es aditiva: crea el esquema y tabla de Journal sin modificar datos existentes.
- El binario anterior ignora el nuevo esquema, por lo que un rollback de aplicación no destruye entradas; la tabla no se eliminará automáticamente.
- El orden documentado de migraciones pasará a ser `Access -> Campaigns -> Characters -> Journal`.
- La API se desplegará antes o junto al frontend que expone el enlace.
- Antes de publicar se ejecutarán pruebas sobre PostgreSQL 18 efímero y copia de seguridad conforme al runbook vigente.
- Después se comprobarán readiness, listado vacío y un recorrido genérico de escritura en un entorno no productivo.
- No habrá migración destructiva de rollback; cualquier corrección de esquema se realizará mediante roll-forward.

## Riesgos y mitigaciones

| Riesgo | Mitigación |
|---|---|
| Journal consulta internals de Characters | Contrato mínimo, referencia unidireccional y fitness functions |
| Suplantación del personaje autor | Autor y fechas derivados exclusivamente de sesión, Campaigns, Characters y `TimeProvider` |
| Un jugador borra una entrada ajena | Separar `canEdit` de `canDelete` y repetir ownership en el command de borrado |
| Edición cambia u oculta la autoría | Campos de autoría sin setters de edición, DTO de escritura limitado a `content` y tests de regresión |
| Paginación duplica u omite entradas | Keyset estable con cursor versionado, límite acotado y tests con inserciones intermedias |
| Carrera entre ediciones colaborativas | Semántica explícita de último `PUT`; versionado e historial quedan para otro spec |
| Contenido inyecta HTML o scripts | Texto plano, interpolación Angular, sin `innerHTML` y tests de renderizado |
| Fuga entre campañas | Toda consulta incluye `CampaignId`, acceso previo y escenarios manipulando rutas |
| Crece el listado sin límite | Páginas de 20, máximo 50 y botón explícito para continuar |
| Migración bloquea el arranque | Migración aditiva, PostgreSQL efímero en CI, backup y estrategia roll-forward |

## Documentación que se actualizará al implementar

- `docs/operations/migraciones-de-base-de-datos.md` con Journal y el nuevo orden.
- `docs/architecture/diagrama-de-componentes.md` y diagrama de despliegue si su inventario de módulos queda desactualizado.
- dashboard y documentación de observabilidad para las métricas de Journal.
- `docs/specs/README.md` y el roadmap solo cuando exista evidencia de criterios cumplidos.
- `tasks.md` con evidencias concretas de pruebas y cierre, después de aprobar este plan.

No se propone un ADR nuevo: las decisiones de módulo, contratos, persistencia y frontend aplican directamente ADR-0004 y ADR-0005; los permisos y la autoría ya están aceptados en el spec 007.

## Validación

El usuario aprobó el plan y autorizó expresamente crear `tasks.md` e implementar el incremento el 2026-08-23.

## Evidencia de cierre

- `docker compose run --build --rm api-tests`: 65 pruebas correctas sobre PostgreSQL y Azurite, sin omisiones.
- `pnpm --filter @dnd/web test --watch=false`: 57 pruebas correctas en 23 archivos.
- `pnpm --filter @dnd/web build`: build Angular de producción correcto con chunk lazy de Journal.
- `dotnet build DndCampaign.slnx --no-restore`: solución completa sin advertencias ni errores.
- `docker compose build api web`: imágenes finales API y web construidas correctamente.
- `docker compose config --quiet` y variante deploy: configuración válida.
