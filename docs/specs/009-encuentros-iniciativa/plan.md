# Plan 009: Encuentros e iniciativa de combate

- Estado: Ejecutado
- Fecha: 2026-08-23
- Especificación: [spec.md](spec.md)
- ADR aplicables: [ADR-0004](../../adr/0004-arquitectura-modular-cqrs-y-limites-de-dependencia.md) y [ADR-0005](../../adr/0005-frontend-modular-por-capacidades.md)
- Dependencias: [spec 004](../004-creacion-campanas/spec.md), [spec 005](../005-personajes-campana/spec.md) y [spec 006](../006-resumen-personajes-activos/spec.md)
- Validación funcional: spec y decisiones funcionales aceptados por el usuario el 2026-08-23

## Resultado esperado

La portada de una campaña permitirá abrir una herramienta de encuentros. El DM podrá preparar varios borradores, incorporar personajes autorizados y enemigos locales, fijar iniciativas y desempates y activar exactamente uno. Durante el encuentro dirigirá turno, ronda y vida de enemigos hasta finalizarlo.

Los jugadores aceptados podrán abrir la misma capacidad y observar automáticamente cada 5 segundos una proyección del encuentro activo con orden y turno actual. El contrato de jugador no contendrá CA, vida, controles ni encuentros no activos. La API garantizará roles, aislamiento, ciclo de estado y consistencia aun cuando se manipulen rutas o existan peticiones concurrentes.

## Diagnóstico de partida

- Campaigns publica `ICampaignAccessReader`, que distingue campaña inexistente, DM, jugador aceptado y ausencia de acceso.
- Characters posee nombre, CA, iniciativa, campaña y estado activo de cada personaje, pero su contrato público actual solo permite resolver el personaje activo de un usuario.
- El cliente Angular de Characters ya lista el elenco autorizado con nombre, CA e iniciativa y puede consumirse desde otro módulo mediante su `public-api.ts`.
- La portada de Campaigns conoce el rol efectivo y ya enlaza por URL a Journal y Missions sin importar sus internals.
- Journal y Missions aportan patrones recientes para módulos con esquema propio, contratos intermodulares mínimos, `ProblemDetails`, métricas y pruebas sobre PostgreSQL real.
- El host registra módulos y migraciones explícitamente; las fitness functions backend enumeran proyectos y aristas permitidas.
- Angular usa rutas lazy, providers acotados, APIs públicas y una fitness function que debe reconocer cada nuevo entrypoint de rutas.
- La CORS existente ya admite `GET`, `POST`, `PUT` y `DELETE`; este incremento no necesita ampliar métodos o cabeceras.
- No existe proyecto, esquema, endpoint, contrato, ruta ni interfaz productiva de Combat.

## Principios de ejecución

1. **Ownership local.** Combat será propietario de encuentros, participantes, orden, turnos, rondas, instantáneas y vida de enemigos.
2. **Contratos mínimos.** Combat consumirá el acceso efectivo de Campaigns y una nueva lectura puntual de Characters; nunca consultará tablas, repositorios o entidades ajenas.
3. **Proyecciones separadas.** El DTO activo para jugadores será seguro por construcción. La mesa completa del DM se obtendrá mediante un contrato distinto y no mediante campos ocultos en CSS.
4. **Ciclo autoritativo.** Solo el dominio y Application ejecutarán `Borrador -> Activo -> Finalizado`; el frontend se limitará a solicitar transiciones.
5. **Orden estable.** La iniciativa descendente y el desempate confirmado producirán posiciones persistidas que no cambiarán después de activar.
6. **Escrituras consistentes.** Una versión del agregado y restricciones PostgreSQL evitarán avances perdidos, vida sobrescrita y dos encuentros activos.
7. **Instantáneas deliberadas.** Renombrar o eliminar un personaje después de incorporarlo no reescribirá encuentros preparados, activos o finalizados.
8. **Sondeo acotado.** Solo la vista del jugador consultará automáticamente cada 5 segundos y cancelará el sondeo al destruirse.
9. **Verticales verdes.** Cada fase conservará compilación, límites modulares y suites existentes antes de exponer la navegación.

## Estructura objetivo de API

```text
apps/api/Modules/Combat/DndCampaign.Modules.Combat/
  DndCampaign.Modules.Combat.csproj
  CombatModule.cs
  Properties/
    AssemblyInfo.cs
  Api/
    EncountersController.cs
    InternalControllerFeatureProvider.cs
  Application/
    Abstractions/
      CombatResult.cs
    Encounters/
      EncounterHandlers.cs
    Ports/
      IEncounterRepository.cs
      ICombatMetrics.cs
  Domain/Encounters/
    Encounter.cs
    EncounterParticipant.cs
  Infrastructure/
    Observability/
      CombatMetrics.cs
    Persistence/
      CombatDbContext.cs
      EncounterRepository.cs
      CombatDesignTimeDbContextFactory.cs
      Migrations/

tests/Modules/Combat/DndCampaign.Modules.Combat.Tests/
  Application/
  Architecture/
  Component/
  Domain/
  Infrastructure/
```

Los nombres podrán compactarse durante la implementación, pero no cambiarán el ownership, la dirección de dependencias ni la separación de proyecciones sin revisar el spec y este plan.

## Dependencias entre módulos

Combat referenciará Campaigns y Characters exclusivamente para consumir:

- `ICampaignAccessReader.GetAccessAsync(campaignId, userId)`;
- un nuevo `ICombatCharacterReader.GetAsync(campaignId, characterId)` que devolverá `CharacterId`, `Name` y `ArmorClass` cuando el personaje pertenezca a esa campaña.

El nuevo contrato vivirá bajo `Characters.Contracts.CombatParticipants` y su adaptador se implementará con una consulta `AsNoTracking` dentro de Characters. No devolverá propietario, imagen, estado activo ni internals de persistencia. Combat capturará la respuesta al añadir al participante y no volverá a resolverla al listar o dirigir el encuentro.

Characters no referenciará Combat. El grafo permanecerá acíclico:

```text
Combat ───────> Campaigns ───────> Access
   └──────────> Characters ──────> Campaigns + Access
```

Las pruebas arquitectónicas incorporarán Combat a los conjuntos globales, permitirán solo esas dos aristas y prohibirán que el host utilice namespaces internos o `CombatDbContext`.

## Modelo de dominio

### Agregado `Encounter`

El encuentro conservará:

- `Id` y `CampaignId` no vacíos;
- `Name` normalizado entre 2 y 120 caracteres;
- `Status`: `Draft`, `Active` o `Finished`;
- `Round` y `CurrentParticipantId`, nulos en borrador, inicializados al activar y conservados al finalizar;
- colección privada de participantes;
- `Version` creciente para control optimista de concurrencia;
- `CreatedAt`, `ActivatedAt` y `FinishedAt`, asignados por `TimeProvider`.

El agregado expondrá operaciones explícitas para renombrar, añadir personaje, añadir enemigo, cambiar iniciativa, retirar participante, confirmar el orden, activar, avanzar, ajustar vida y finalizar. Cada operación comprobará el estado permitido y aumentará `Version` solo cuando exista un cambio confirmado.

`Activate` exigirá al menos un participante, orden válido y ausencia de empates sin confirmar. Fijará ronda 1 y el primer participante. `AdvanceTurn` localizará la posición actual persistida, seleccionará la siguiente y aumentará la ronda únicamente al envolver. `Finish` solo aceptará un encuentro activo.

### Participantes

Cada participante conservará:

- `Id`, `EncounterId` y `Kind`: `Character` o `Enemy`;
- `SourceCharacterId` únicamente para personaje;
- `NameSnapshot`, `ArmorClass` e `InitiativeTotal`;
- `OrderPosition` persistida y una marca interna que indique si los empates vigentes están confirmados;
- `CurrentHitPoints` y `MaximumHitPoints` únicamente para enemigo;
- una secuencia de incorporación inmutable para presentar un orden provisional estable en borrador.

Un participante de personaje exige la instantánea validada por Characters. Un enemigo exige nombre, CA, iniciativa y vida máxima; su vida actual nace igual al máximo. Las combinaciones imposibles entre tipo, origen y vida se rechazarán tanto en dominio como mediante checks de base de datos.

### Iniciativa y empates

El agregado calculará un orden provisional por `InitiativeTotal DESC` y secuencia de incorporación. Cuando no existan iniciativas repetidas, ese orden estará listo para activarse.

Si una alta o corrección produce un empate, el agregado marcará el orden como pendiente. El command de confirmación recibirá la lista completa de identificadores en el orden visible, comprobará que:

- incluye cada participante exactamente una vez;
- conserva la iniciativa descendente;
- solo decide posiciones relativas dentro de iniciativas iguales.

Después persistirá `OrderPosition` contigua desde cero y marcará los empates como resueltos. Cualquier cambio posterior de elenco o iniciativa recalculará el orden y volverá a exigir confirmación si reaparece un empate.

### Vida de enemigos

La escritura recibirá `kind` igual a `damage` o `healing`, una cantidad positiva y la versión esperada. El dominio aplicará saturación entre 0 y el máximo. Llegar a cualquiera de los límites sigue siendo una operación válida; no elimina participantes ni altera el turno.

No existirá una operación equivalente para personajes jugadores en este incremento.

## Persistencia PostgreSQL

Combat utilizará `CombatDbContext`, historial de migraciones y esquema `combat`, sin foreign keys hacia otros esquemas.

La tabla `encounters` incluirá:

- clave primaria por `id`;
- estado, ronda, turno, versión y marcas temporales;
- checks de coherencia entre estado, ronda, turno y fechas;
- índice por `(campaign_id, status, created_at)` para el listado del DM;
- índice único parcial por `campaign_id WHERE status = 'Active'`.

La tabla `encounter_participants` incluirá:

- clave primaria por `id` y foreign key local con borrado en cascada hacia `encounters`;
- discriminador, instantánea, iniciativa, posición, vida y secuencia de incorporación;
- checks de rangos y coherencia de tipo;
- índice único por `(encounter_id, order_position)` cuando la posición esté confirmada;
- índice único parcial por `(encounter_id, source_character_id)` para impedir duplicar un personaje;
- índice de lectura por encuentro y orden.

Todas las escrituras del agregado se ejecutarán en una transacción local. `Version` participará en el predicado de actualización y una versión obsoleta devolverá conflicto. El índice parcial de encuentro activo será la última defensa ante activaciones concurrentes, además de la comprobación en Application.

No se eliminarán encuentros en este alcance. Por tanto, las instantáneas finalizadas permanecen disponibles para el DM y un rollback de aplicación no destruye información.

## Casos de uso de Application

### Listar y consultar como DM

1. Resolver acceso mediante Campaigns.
2. Devolver `404` si la campaña no existe y `403` si el actor no es su DM.
3. Listar resúmenes de borradores, activo y finalizados con nombre, estado, participantes, ronda, turno, marcas temporales y versión.
4. Consultar una mesa concreta por `CampaignId` y `EncounterId`, proyectando CA y vida únicamente en el DTO de DM.

La fila de personaje del DTO de DM incluirá su `characterId` para que la web pueda excluirlo del selector y dirigir correcciones sobre la identidad adecuada. Este identificador no aparecerá en la proyección del jugador.

### Consultar el activo de forma segura

1. Exigir rol `Dm` o `Player` aceptado en la campaña.
2. Buscar el único encuentro activo o devolver una representación explícita sin encuentro.
3. Proyectar solo identificador, nombre, ronda, nombre del turno actual y participantes con nombre, tipo, iniciativa, posición y turno actual.
4. No cargar ni serializar CA, vida, versiones de escritura, estado de borradores o controles.

Aunque el DM pueda llamar a esta consulta, la respuesta seguirá siendo la proyección segura. Su mesa completa se obtiene por el endpoint de detalle exclusivo del DM; así no existe un JSON dependiente del rol con campos opcionalmente privados.

### Crear y preparar un borrador

- Crear y renombrar exigen rol DM y valores válidos.
- Añadir personaje resuelve la instantánea puntual de Characters y devuelve `404` si no existe en la campaña indicada.
- Añadir enemigo valida y normaliza sus campos sin consultar Library ni NPC.
- Cambiar iniciativa, retirar participante y confirmar orden exigen borrador y versión vigente.
- Cada respuesta de escritura devolverá la mesa completa actualizada, incluida la nueva versión, para que la web sustituya su estado autoritativo.

### Activar y finalizar

- Activar exige DM, borrador válido, versión vigente y ausencia de otro activo; confirma la transición en una transacción.
- Un conflicto de unicidad concurrente se traduce a `409` sin finalizar ni modificar otro encuentro.
- Finalizar exige DM, encuentro activo y versión vigente; conserva ronda, turno y vida y retira la proyección activa en la misma confirmación.

### Avanzar y ajustar vida

- Avanzar exige DM, encuentro activo y versión vigente; devuelve la mesa con nueva ronda, turno y versión.
- Ajustar vida exige DM, encuentro activo, participante enemigo y versión vigente; aplica daño o curación y devuelve la mesa actualizada.
- Un personaje, participante ajeno, encuentro finalizado o versión obsoleta se rechaza sin efectos parciales.

## Contrato HTTP planificado

### Lecturas

- `GET /api/v1/campaigns/{campaignId}/encounters`: listado exclusivo del DM.
- `GET /api/v1/campaigns/{campaignId}/encounters/active`: proyección segura para DM o jugador aceptado; responde `200` con `encounter: null` cuando no existe uno activo.
- `GET /api/v1/campaigns/{campaignId}/encounters/{encounterId}`: mesa completa exclusiva del DM.

Las rutas literales se declararán de forma que `active` no pueda interpretarse como un identificador.

### Preparación

- `POST /api/v1/campaigns/{campaignId}/encounters` con `{ "name": "..." }`; devuelve `201 Created`.
- `PUT /api/v1/campaigns/{campaignId}/encounters/{encounterId}` con `{ "name": "...", "expectedVersion": 1 }`.
- `POST /api/v1/campaigns/{campaignId}/encounters/{encounterId}/characters` con `{ "characterId": "...", "initiative": 14, "expectedVersion": 1 }`.
- `POST /api/v1/campaigns/{campaignId}/encounters/{encounterId}/enemies` con `{ "name": "Enemigo", "initiative": 12, "armorClass": 15, "maximumHitPoints": 20, "expectedVersion": 2 }`.
- `PUT /api/v1/campaigns/{campaignId}/encounters/{encounterId}/participants/{participantId}/initiative` con `{ "initiative": 16, "expectedVersion": 3 }`.
- `DELETE /api/v1/campaigns/{campaignId}/encounters/{encounterId}/participants/{participantId}?expectedVersion=4`.
- `PUT /api/v1/campaigns/{campaignId}/encounters/{encounterId}/initiative-order` con `{ "participantIds": ["..."], "expectedVersion": 5 }`.

### Ejecución

- `PUT /api/v1/campaigns/{campaignId}/encounters/{encounterId}/active` con `{ "expectedVersion": 6 }`.
- `POST /api/v1/campaigns/{campaignId}/encounters/{encounterId}/turns/advance` con `{ "expectedVersion": 7 }`.
- `POST /api/v1/campaigns/{campaignId}/encounters/{encounterId}/enemies/{participantId}/hit-points` con `{ "kind": "damage", "amount": 5, "expectedVersion": 8 }`.
- `PUT /api/v1/campaigns/{campaignId}/encounters/{encounterId}/finished` con `{ "expectedVersion": 9 }`.

Las escrituras, excepto creación, devolverán `200 OK` con la mesa actualizada. Esto evita reconstrucciones parciales y entrega la versión necesaria para la operación siguiente.

Los errores usarán `ProblemDetails` o `ValidationProblemDetails`:

- `400`: nombres, rangos, cantidades, cuerpo o transición inválidos;
- `401`: ausencia de sesión;
- `403`: campaña sin acceso, jugador intentando operar o actor no DM en una lectura privada;
- `404`: campaña, encuentro, participante o personaje inexistente en la jerarquía indicada;
- `409`: versión obsoleta, personaje duplicado, empates pendientes u otro encuentro activo.

Los DTO de jugador y DM serán tipos distintos. Ninguna respuesta pública incluirá entidades EF, secuencias internas o datos de otra campaña; los identificadores de origen solo aparecerán en la mesa privada del DM cuando sean necesarios para operar el encuentro.

## Integración del host y límites backend

- Añadir Combat y su proyecto de tests a `DndCampaign.slnx`.
- Añadir referencias de Combat a Campaigns y Characters y del host a Combat.
- Actualizar el Dockerfile para restaurar y probar los proyectos nuevos.
- Registrar `AddCombatModule`, `MapCombatModule` y `ApplyCombatMigrationsAsync` en `Program.cs`.
- Aplicar migraciones en orden `Access -> Campaigns -> Characters -> Journal -> Missions -> Combat`.
- Ampliar las fitness functions para reconocer Combat, `CombatDbContext` y solo sus aristas aprobadas.
- Reutilizar la conexión PostgreSQL y la configuración actual.

No se requieren servicios externos, secretos, blobs, colas, cambios de CORS, recursos Azure ni Terraform nuevos.

## Estructura objetivo de frontend

```text
apps/web/src/app/modules/combat/
  combat.routes.ts
  api/
    combat.client.ts
    combat.contracts.ts
    combat.client.spec.ts
  encounter-list/
    encounter-list.page.ts
    encounter-list.page.html
  encounter-table/
    encounter-table.page.ts
    encounter-table.page.html
  combat.pages.scss
  combat.pages.spec.ts
```

La ruta base decidirá la experiencia tras cargar el rol de Campaigns. El DM consultará el listado y abrirá `/campaigns/:campaignId/encounters/:encounterId`; el jugador permanecerá en `/campaigns/:campaignId/encounters` con la proyección activa.

Combat consumirá `CampaignsClient` y `CharactersClient` únicamente desde sus APIs públicas. Campaigns añadirá un `routerLink` y no importará componentes, contratos ni clientes de Combat. Combat no exportará internals; su entrypoint de rutas se registrará en el composition root y en la fitness function.

### Estado del DM

La experiencia del DM mantendrá con signals:

- campaña y rol cargados;
- listado de encuentros y mesa seleccionada;
- formulario de nombre, selector de personaje y formulario de enemigo;
- iniciativa por participante y orden de desempate;
- ronda, turno, vida y versión autoritativa;
- operaciones de carga, guardado, avance, ajuste de vida y finalización;
- errores de validación, autorización y conflicto.

Cada escritura sustituirá la mesa por la respuesta completa. Ante `409`, la página recargará la mesa y explicará que el estado cambió. La activación o finalización recargará también el listado para reflejar los estados.

En borrador se mostrarán controles de preparación; en activo, únicamente avance, daño, curación y finalización; en finalizado, una mesa de solo lectura.

### Selección y orden

- El selector cargará el elenco mediante `CharactersClient` y excluirá los personajes ya añadidos.
- Para cada personaje se mostrará nombre y CA; su iniciativa de ficha podrá presentarse como referencia, pero el DM introducirá expresamente el total del encuentro.
- Los enemigos se crearán con campos numéricos y validaciones alineadas con la API.
- La tabla se ordenará por la posición devuelta por el servidor.
- Cuando existan empates pendientes, controles accesibles `Subir` y `Bajar` permitirán ordenar únicamente participantes empatados y confirmar el orden completo.
- La acción de activar permanecerá deshabilitada por experiencia mientras falten participantes o empates, aunque la API repetirá la validación.

### Mesa activa del DM

- Ronda y turno actual tendrán énfasis y anuncio accesible.
- Cada fila mostrará nombre, tipo, iniciativa y CA; los enemigos añadirán vida actual/máxima y controles de daño y curación.
- Los importes de vida exigirán cantidad positiva y conservarán foco y mensaje de resultado.
- `Siguiente turno` y `Finalizar encuentro` serán acciones distintas; finalizar exigirá confirmación explícita.
- Ningún enemigo se eliminará, ocultará o saltará automáticamente al llegar a 0.

### Proyección del jugador y sondeo

- La proyección mostrará estado vacío cuando `encounter` sea `null`.
- Cuando exista activo, mostrará nombre, ronda, turno y tabla con nombre, tipo e iniciativa.
- No habrá columna, binding o propiedad de CA o vida en el tipo TypeScript de jugador.
- El sondeo usará `timer(0, 5000)` con una estrategia que evite peticiones solapadas y `takeUntilDestroyed` para cancelar al abandonar la página.
- Un fallo aislado conservará el último estado visible y mostrará el error; `401` o `403` detendrá el sondeo y no seguirá enviando peticiones.
- Las pruebas usarán tiempo simulado para demostrar consulta inicial, intervalo, ausencia de solapamiento y cancelación.

## Observabilidad, privacidad y rendimiento

Combat añadirá contador y duración para `list`, `get`, `get_active`, `create`, `rename`, `add_character`, `add_enemy`, `update_initiative`, `resolve_order`, `remove_participant`, `activate`, `advance`, `adjust_hit_points` y `finish`.

Los outcomes serán de cardinalidad acotada: `success`, `validation`, `forbidden`, `not_found`, `conflict` y `failure`. No se incluirán como etiquetas ni mensajes nombres o identificadores de campaña, encuentro, participante, personaje, enemigo o usuario.

La consulta activa será `AsNoTracking`, recuperará solo la proyección necesaria y dispondrá de índices por campaña y estado. El sondeo de cinco segundos se limita a la página abierta; no se ejecutará desde la portada ni en segundo plano después de destruir el componente.

El dashboard de plataforma podrá incorporar tasas y conflictos agregados de Combat. No se expondrán puntos de vida o turnos como métricas.

## Estrategia de pruebas

### Dominio

- normalización y rangos de encuentro, personaje y enemigo;
- coherencia de tipo y vida de participantes;
- altas, retirada y cambio de iniciativa solo en borrador;
- orden descendente, aparición de empates, confirmación válida y rechazo de listas incompletas o mal ordenadas;
- activación solo con plantilla válida, ronda 1 y primer turno;
- avance normal y vuelta de ronda sin alterar posiciones;
- daño, curación y saturación en 0/máximo únicamente para enemigos activos;
- finalización irreversible y rechazo de cambios posteriores;
- incremento de versión solo con cambios confirmados.

### Application

- distinción `404` de campaña inexistente y `403` de actor sin rol suficiente;
- listado y detalle solo para DM;
- proyección activa accesible para DM y jugador aceptado, sin campos privados;
- personaje válido capturado una vez y personaje ajeno o inexistente rechazado;
- enemigo local sin dependencia de NPC o Library;
- todas las escrituras rechazadas para jugador;
- otro encuentro activo, empates y versión obsoleta traducidos a `409`;
- aislamiento jerárquico por campaña, encuentro y participante;
- uso de `TimeProvider` para todas las marcas.

### Persistencia PostgreSQL

- migración y esquema `combat` en una base efímera;
- checks, índices y combinaciones nullable esperadas;
- personaje único por encuentro y nombres de enemigo repetibles;
- orden persistido y lectura determinista;
- índice único parcial que impide dos activos ante activaciones concurrentes;
- control de versión que evita dos avances o ajustes perdidos;
- transición y ajuste de vida atómicos;
- instantánea preservada sin foreign key intermodular;
- consultas y escrituras sin mezcla entre campañas.

### Contrato HTTP

- recorrido DM de crear dos borradores, preparar, desempatar, activar, avanzar, ajustar vida y finalizar;
- segundo activo rechazado y activación posterior permitida tras finalizar;
- representación completa de DM con CA y vida;
- representación activa de jugador sin CA, vida, versión ni controles;
- estado vacío antes de activar y después de finalizar;
- `401`, validación, `403`, `404`, `409` y manipulación de rutas;
- ausencia de datos privados mediante aserciones sobre JSON, no solo sobre valores nulos.

### Frontend y arquitectura

- URLs, métodos, cuerpos y tipos separados de `CombatClient`;
- rutas lazy, providers y enlace desde Campaigns;
- listado y estados del DM;
- alta de personaje y enemigo, corrección de iniciativa y desempate;
- controles adecuados a borrador, activo y finalizado;
- avance, vuelta de ronda, daño, curación y confirmación de finalización;
- estado vacío y tabla segura del jugador;
- sondeo inicial y cada 5 segundos, sin solapamiento y cancelado al destruir;
- tratamiento de `409`, `401` y `403`;
- accesibilidad básica de tablas, foco, mensajes y turno actual;
- fitness functions sin deep imports ni ciclos.

## Fases de implementación propuestas

1. Ampliar el contrato público mínimo de Characters y crear el proyecto Combat, su fachada, capas, tests y aristas arquitectónicas.
2. Implementar agregado, participantes, iniciativa, empates, ciclo, vida y versión con pruebas de dominio.
3. Crear `CombatDbContext`, repositorio, checks, índices, concurrencia y migración del esquema `combat` con pruebas PostgreSQL.
4. Implementar casos de uso de preparación, acceso y proyecciones separadas.
5. Implementar activación, turnos, rondas, vida y finalización, incluida concurrencia y aislamiento.
6. Exponer endpoints, integrar host, solución, Dockerfile, migraciones y completar pruebas HTTP.
7. Crear módulo Angular, rutas, clientes, contratos, listado y preparación del DM.
8. Crear mesa activa, controles del DM y proyección segura del jugador con sondeo.
9. Completar pruebas frontend, fitness functions, observabilidad, documentación y verificación integrada.

Cada fase terminará con sus suites aplicables verdes. El enlace desde Campaigns no se habilitará hasta que los endpoints autorizados y la proyección segura estén integrados.

## Despliegue y reversibilidad

- La migración es aditiva: crea el esquema y tablas de Combat sin modificar los esquemas existentes.
- El binario anterior ignora el esquema `combat`; un rollback de aplicación no borra encuentros, aunque la funcionalidad quede temporalmente inaccesible.
- La API se desplegará antes o junto al frontend que expone el enlace.
- El orden documentado de migraciones pasará a `Access -> Campaigns -> Characters -> Journal -> Missions -> Combat`.
- Antes de publicar se ejecutarán pruebas sobre PostgreSQL 18 efímero y copia de seguridad conforme al runbook.
- Después se comprobarán readiness, creación de un borrador genérico, activación, avance, proyección segura y finalización en un entorno no productivo.
- No habrá rollback destructivo del esquema; cualquier corrección se realizará mediante roll-forward.

## Riesgos y mitigaciones

| Riesgo | Mitigación |
|---|---|
| La respuesta del jugador filtra CA o vida | DTO y query seguros independientes, pruebas sobre propiedades JSON ausentes y sin reutilizar la mesa del DM |
| Dos encuentros quedan activos | Comprobación de Application, transacción e índice único parcial por campaña |
| Dos acciones del DM pierden un turno o vida | `Version` esperada, actualización condicional y `409` con recarga autoritativa |
| El desempate cambia al añadir o editar | Invalidación explícita, confirmación completa y orden persistido antes de activar |
| Un personaje de otra campaña entra en el encuentro | Contrato puntual de Characters con `campaignId`, comprobación repetida y prueba de ruta manipulada |
| Cambiar o eliminar el personaje rompe el encuentro | Instantánea sin foreign key intermodular y pruebas de persistencia histórica |
| El sondeo produce peticiones solapadas o continúa oculto | Estrategia RxJS sin solapamiento y cancelación con `takeUntilDestroyed` |
| Un jugador opera mediante la API | Autorización de rol en cada handler y escenarios HTTP de todas las escrituras |
| Llegar a 0 elimina o revela al enemigo | Dominio conserva al participante y DTO de jugador carece de vida |
| La migración bloquea el arranque | Cambio aditivo, PostgreSQL efímero en CI, backup y estrategia roll-forward |

## Documentación que se actualizará al implementar

- `docs/operations/migraciones-de-base-de-datos.md` con Combat y el nuevo orden.
- `docs/architecture/diagrama-de-componentes.md` con frontend, backend, contratos y esquema Combat.
- dashboard y documentación de observabilidad con métricas agregadas.
- `docs/specs/README.md`, roadmap, spec, plan y tareas con estado y evidencias finales.

No se propone un ADR nuevo. La separación modular, los contratos públicos y la composición frontend ya están gobernados por ADR-0004 y ADR-0005; ciclo, desempate, privacidad y sondeo son decisiones acotadas y reversibles registradas en el spec 009.

## Validación

El usuario aceptó el spec 009, solicitó continuar con el plan e inició la implementación el 2026-08-23. Las nueve fases se ejecutaron y verificaron en API, PostgreSQL, Angular y Docker. Los comandos, resultados y cobertura final se registran en [tasks.md](tasks.md).
