# Spec 007: Bitácora compartida de campaña

- Estado: Completada
- Fecha: 2026-08-23
- Tipo: incremento funcional vertical
- Roadmap: [roadmap funcional](../../roadmap/product-roadmap.md)
- Requisitos principales: RF-030, RF-031, RF-032 y RF-035
- Requisitos aplazados: RF-033 y RF-034
- Dependencias: [spec 004](../004-creacion-campanas/spec.md), [spec 005](../005-personajes-campana/spec.md) y [spec 006](../006-resumen-personajes-activos/spec.md)

## Problema

Los miembros de una campaña no disponen de un lugar común donde conservar las pistas, sucesos y demás información que descubren durante la aventura. La información queda fuera del producto y no mantiene relación verificable con la campaña ni con el personaje que la aportó.

El roadmap prevé una bitácora por campaña, pero la librería de NPC de la que dependerán las referencias opcionales todavía no existe. Este incremento debe entregar una bitácora útil sin anticipar el modelo de NPC ni acoplarse a contenido editorial futuro.

## Objetivo

Permitir que los jugadores de una campaña creen entradas de texto mediante su personaje activo y que todos los miembros autorizados las consulten de la más reciente a la más antigua. Cualquier jugador aceptado de la campaña podrá editar cualquier entrada, mientras que la eliminación corresponderá al jugador que la introdujo; el DM conservará acceso de solo lectura.

La capacidad será utilizable de extremo a extremo desde Angular, persistida por un módulo propio de la API y autorizada nuevamente en cada operación del backend.

## Actores

- **Jugador con personaje activo:** consulta la bitácora, crea entradas, edita cualquier entrada y elimina las que él mismo introdujo.
- **Jugador sin personaje activo:** consulta y edita cualquier entrada, pero no puede crear una nueva hasta disponer de un personaje activo; puede eliminar las que introdujo anteriormente.
- **Otro jugador de la campaña:** consulta y edita las entradas compartidas, pero no elimina las introducidas por otro jugador.
- **DM de la campaña:** consulta la bitácora completa, sin crear, editar ni eliminar entradas.
- **Usuario ajeno a la campaña:** no puede consultar ni modificar ninguna entrada.

## Alcance funcional

### Consulta

- Cada campaña dispone de una bitácora independiente y compartida por todos sus miembros autorizados.
- La vista muestra las entradas por fecha de creación descendente: primero la más reciente y después las anteriores.
- La ordenación es estable cuando dos entradas comparten la misma fecha de creación.
- La consulta está paginada y permite cargar entradas anteriores sin invertir ni duplicar el orden visible.
- Cada entrada presenta su contenido, el nombre del personaje autor, la fecha de creación y, cuando corresponda, que fue editada.
- Los estados de carga, error y bitácora vacía son explícitos.

### Creación

- Solo un jugador con membresía aceptada y personaje activo en la campaña puede crear una entrada.
- La API obtiene el personaje activo a partir del usuario autenticado y de la campaña de la ruta. El cliente no elige ni envía el identificador del personaje autor.
- La entrada conserva la campaña, el jugador creador, el personaje activo, una instantánea del nombre de ese personaje y la fecha de creación asignada por el servidor.
- Tras crearla, la nueva entrada aparece al principio de la vista.
- Un jugador sin personaje activo ve una explicación y una acción para gestionar sus personajes; la API rechaza igualmente una creación directa.

### Edición

- Cualquier jugador con membresía aceptada en la campaña puede editar el contenido de cualquier entrada de esa campaña; no necesita ser su creador ni disponer de personaje activo para editar.
- La edición no cambia la campaña, el jugador que introdujo la entrada, el personaje autor, su nombre conservado ni la fecha de creación.
- La API registra la fecha de última edición y la interfaz identifica la entrada como editada.
- Editar una entrada no la mueve al principio: el orden continúa determinado por la fecha de creación.

### Eliminación

- Solo el jugador que creó la entrada puede eliminarla, aunque su personaje activo actual sea otro.
- La interfaz solicita confirmación antes de eliminar.
- La eliminación es definitiva en este incremento y la entrada deja de aparecer en consultas posteriores.

## Decisiones funcionales aceptadas

1. **Edición colaborativa entre jugadores.** Cualquier jugador aceptado puede editar cualquier entrada de su campaña. El DM mantiene acceso de solo lectura.
2. **Eliminación por el jugador que introdujo la entrada.** Solo ese jugador puede eliminarla. La autorización se basa en el usuario que la creó, no en quién posea actualmente el personaje.
3. **Bitácora siempre compartida.** No hay borradores, entradas privadas ni visibilidad por subconjuntos de jugadores en este incremento.
4. **Autoría original visible y estable.** La entrada muestra quién la introdujo y conserva el identificador y una instantánea del nombre de su personaje activo en el momento de creación. Las ediciones posteriores no sustituyen esa autoría. Renombrar, reasignar o eliminar posteriormente el personaje tampoco reescribe ni elimina la entrada.
5. **Orden por creación.** Una edición no altera la posición cronológica de la entrada.
6. **Texto plano.** El contenido admite saltos de línea, se almacena y presenta como texto, y no interpreta HTML ni Markdown aportado por el usuario.
7. **Sin referencias a NPC todavía.** RF-033 y RF-034 se implementarán después de que exista la librería de NPC y pueda verificarse su visibilidad por campaña.

Estas decisiones fueron aceptadas expresamente por el usuario el 2026-08-23.

## Reglas y validación

- El contenido es obligatorio, se recortan los espacios exteriores y debe contener entre 1 y 5.000 caracteres.
- Los saltos de línea interiores se conservan.
- El servidor asigna `createdAt`; `updatedAt` permanece vacío hasta la primera edición. El cliente no puede establecer ni modificar esas fechas.
- `createdAt` es inmutable. `updatedAt` cambia únicamente al editar el contenido.
- La lista se ordena por `createdAt` descendente y usa un segundo criterio inmutable para resolver empates de forma determinista.
- La paginación emplea un cursor opaco; el tamaño predeterminado es 20 y el máximo aceptado es 50 entradas.
- La pertenencia a la campaña autoriza la lectura. Solo el rol `Player` con personaje activo autoriza la creación.
- La edición exige una membresía aceptada con rol `Player` en la campaña, pero no exige ser el creador ni tener un personaje activo.
- La eliminación exige que el usuario autenticado coincida con el jugador creador guardado en la entrada.
- El DM no puede crear una entrada aunque controle o llegue a controlar un personaje en la campaña.
- Toda lectura y escritura vuelve a comprobar usuario, campaña, rol y, para creación, personaje activo en la API.
- Una entrada de otra campaña nunca se resuelve únicamente por su identificador: campaña e entrada deben coincidir con la ruta.
- El frontend oculta o deshabilita acciones no disponibles por experiencia de uso, pero estos controles no sustituyen la autorización del backend.

## Recorrido web

- La portada de una campaña incorpora una acción visible `Abrir bitácora` para DM y jugadores.
- La ruta `/campaigns/:campaignId/journal` carga la bitácora de esa campaña.
- La página contiene el listado cronológico y la acción para registrar una entrada cuando el actor es jugador.
- La creación y edición usan un formulario de texto multilínea con contador y mensajes de validación.
- Todos los jugadores ven la acción de edición en todas las entradas; la acción de eliminación aparece únicamente en las que introdujo el usuario actual.
- El DM ve el estado de solo lectura de forma comprensible.
- Un jugador sin personaje activo puede leer y encuentra una acción hacia `/campaigns/:campaignId/characters` para crear o activar uno.
- El listado permite cargar entradas anteriores cuando la respuesta indique que existe otra página.

## Contrato HTTP funcional

- `GET /api/v1/campaigns/{campaignId}/journal/entries?cursor={cursor}&limit={limit}`: devuelve una página autorizada de entradas ordenadas de más recientes a más antiguas.
- `POST /api/v1/campaigns/{campaignId}/journal/entries`: crea una entrada para el personaje activo del jugador autenticado y devuelve `201 Created`.
- `PUT /api/v1/campaigns/{campaignId}/journal/entries/{entryId}`: permite a cualquier jugador aceptado sustituir el contenido de una entrada de su campaña y devuelve `200 OK`.
- `DELETE /api/v1/campaigns/{campaignId}/journal/entries/{entryId}`: elimina una entrada propia y devuelve `204 No Content`.

La creación y edición reciben JSON con un único campo `content`. La representación pública incluye como mínimo `id`, `campaignId`, `authorCharacterId`, `authorCharacterName`, `content`, `createdAt`, `updatedAt`, `canEdit` y `canDelete`. La autoría mostrada identifica siempre al personaje que introdujo la entrada, aunque otro jugador la haya editado. No se expone el identificador del usuario creador.

La respuesta de listado incluye `items` y `nextCursor`; el cursor no revela identificadores ni datos personales. Los errores usan `ProblemDetails`: `400` para contenido, límite o cursor inválido; `401` sin sesión; `403` cuando el actor no tiene acceso, su rol no permite crear o editar, o intenta eliminar una entrada ajena; `404` cuando la entrada no existe dentro de la campaña indicada; y `409` cuando un jugador autorizado intenta crear sin disponer de personaje activo.

## Ownership técnico

- `apps/api`: un nuevo módulo `Journal` es propietario del agregado de entrada, persistencia PostgreSQL, consultas, comandos, endpoints, métricas y reglas de autoría. Consume el contrato público de Campaigns para comprobar acceso y rol, y un contrato público mínimo de Characters para resolver el personaje activo del jugador. No consulta tablas, `DbContext`, repositorios ni entidades internas de otros módulos.
- `apps/api/Modules/Characters`: expone únicamente la proyección necesaria del personaje activo para colaboración intermodular. Characters continúa siendo la fuente autoritativa de selección, ownership y pertenencia del personaje.
- `apps/web`: un nuevo módulo `journal` es propietario de rutas, cliente HTTP, contratos, listado y formularios. Campaigns solo enlaza a la ruta o compone una API pública mínima; no importa internals de Journal.

Ambas superficies cambian porque el incremento requiere una experiencia web completa y una fuente autoritativa persistida y autorizada en la API.

## Persistencia y ciclo de vida

- Journal persiste sus datos en su propio esquema y mediante su propio `DbContext` y migración, conforme a los límites modulares existentes.
- La referencia histórica al personaje se almacena como identidad externa y nombre capturado, sin dependencia relacional directa sobre las tablas privadas de Characters.
- Eliminar un personaje no elimina entradas de bitácora ni deja su autoría sin representación legible.
- Eliminar una entrada sí elimina definitivamente su registro. Archivado, papelera y recuperación quedan fuera de alcance.
- El contenido de una campaña no se comparte con ninguna otra campaña, aunque ambas usen el mismo módulo de aventura.

## Observabilidad, privacidad y seguridad

- Se medirán listado, creación, edición y eliminación, con resultado y duración de cardinalidad acotada.
- Los logs, trazas, métricas y errores no incluyen contenido, nombre del personaje, identificadores de entrada, campaña, personaje o usuario, ni cursores de paginación.
- El contenido se trata como dato privado de campaña y solo se devuelve después de autorizar el acceso.
- El texto se representa sin interpretación de marcado para impedir que una entrada inyecte HTML o scripts.
- Los fallos de autorización y los intentos entre campañas son observables mediante resultados agregados sin registrar datos privados.

## Criterios de aceptación

1. Un jugador aceptado con personaje activo abre una bitácora vacía, registra una pista y la consulta con contenido, personaje autor y fecha asignada por el servidor.
2. Al registrar varias entradas, la web y la API las presentan de más recientes a más antiguas; cargar otra página mantiene el orden y no duplica entradas.
3. Un jugador sin personaje activo puede leer la bitácora, pero no puede crear mediante la interfaz ni invocando directamente la API.
4. El DM puede leer todas las entradas de su campaña, pero no puede crear, editar ni eliminar ninguna.
5. Un jugador distinto del creador edita el contenido y la entrada conserva como autor visible al personaje que la introdujo, mantiene su fecha de creación, muestra su fecha de edición y no cambia de posición.
6. El jugador creador confirma y elimina su entrada; una consulta posterior ya no la devuelve.
7. Otro jugador de la misma campaña puede leer y editar la entrada, pero recibe `403` al intentar eliminarla directamente.
8. Un usuario ajeno no puede listar la bitácora ni operar sobre una entrada alterando identificadores o rutas.
9. Cambiar de personaje activo no cambia la autoría de entradas existentes ni impide que cualquier jugador de la campaña las edite; las entradas nuevas se asocian al nuevo personaje activo y solo su jugador creador puede eliminarlas.
10. Renombrar, reasignar o eliminar el personaje autor no modifica ni elimina la entrada, que conserva la identidad y el nombre capturados al crearla.
11. El contenido vacío, superior a 5.000 caracteres o un cursor inválido se rechaza sin producir cambios parciales.
12. Las pruebas de dominio, aplicación, persistencia PostgreSQL, contrato HTTP, aislamiento modular y componentes Angular demuestran estos recorridos; las suites y builds existentes permanecen verdes.

## Fuera de alcance

- Referencias a NPC, validación de NPC visibles y protección asociada de RF-033 y RF-034.
- Adjuntos, imágenes, enlaces enriquecidos, etiquetas, categorías, búsqueda o filtros.
- HTML, Markdown, menciones o edición de texto enriquecido.
- Borradores, entradas privadas, permisos configurables o publicación diferida.
- Edición o eliminación por el DM, y eliminación por otros jugadores o por el propietario actual del personaje si no es el jugador creador.
- Restauración, archivado, historial de versiones o auditoría visible del contenido anterior.
- Comentarios, reacciones, notificaciones, presencia o actualización en tiempo real.
- Integración con misiones, calendario, capítulos, combates o contenido editorial.
- Cambios en las reglas de personajes activos, ownership de personajes o acceso a campañas.

## Validación

El usuario aprobó expresamente el alcance y las decisiones funcionales el 2026-08-23, incluida la edición colaborativa por cualquier jugador, la autoría original visible y la eliminación reservada al jugador que introdujo la entrada.

El usuario aprobó posteriormente el plan y autorizó crear `tasks.md` e implementar el incremento el 2026-08-23.

La implementación quedó verificada el mismo día con 65 pruebas .NET en Docker sobre PostgreSQL y Azurite, 57 pruebas Angular, build de producción web, compilación de la solución, construcción de imágenes API/web y validación de Compose. RF-033 y RF-034 permanecen aplazados conforme al alcance aprobado.
