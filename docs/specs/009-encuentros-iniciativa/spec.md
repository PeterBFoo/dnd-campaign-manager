# Spec 009: Encuentros e iniciativa de combate

- Estado: Completada
- Fecha: 2026-08-23
- Tipo: incremento funcional vertical
- Roadmap: [roadmap funcional](../../roadmap/product-roadmap.md)
- Requisitos principales: RF-014 y RF-050 a RF-057
- Dependencias: [spec 004](../004-creacion-campanas/spec.md), [spec 005](../005-personajes-campana/spec.md) y [spec 006](../006-resumen-personajes-activos/spec.md)

## Problema

El DM no dispone de una herramienta asociada a la campaña para preparar encuentros, ordenar conjuntamente personajes y enemigos, controlar el turno y la ronda actuales ni conservar la vida de los enemigos durante el combate. Los jugadores tampoco pueden seguir el orden de actuación desde una proyección que oculte la información reservada del DM.

El roadmap ya reserva esta capacidad en RF-050 a RF-057, pero no concreta la preparación de varios encuentros, el ciclo para hacer visible uno de ellos, la CA obligatoria de los enemigos ni el mecanismo inicial de actualización para los jugadores.

## Objetivo

Permitir que el DM prepare varios encuentros independientes dentro de una campaña, añada personajes de esa campaña y enemigos locales, establezca el orden de iniciativa y active un único encuentro para dirigir sus turnos, rondas y puntos de vida.

Cuando exista un encuentro activo, los jugadores aceptados de la campaña podrán observar automáticamente una tabla segura con el orden y el turno actual, sin poder operarla ni acceder a la CA o los puntos de vida de los enemigos.

La capacidad será utilizable de extremo a extremo desde Angular, persistida y autorizada por un módulo propio de la API y aislada por campaña.

## Encaje con el roadmap y los incrementos existentes

| Petición | Encaje actual | Concreción de este spec |
|---|---|---|
| Tracker de iniciativa de compañeros y enemigos | RF-051, RF-053 y RF-054 | Un encuentro contiene participantes de personaje y de enemigo, ordenados por iniciativa, con turno y ronda actuales. |
| Enemigos creados en el encuentro con iniciativa, CA y vida | RF-051 y RF-052 ya exigen enemigos locales, nombre y vida actual/máxima; la CA y el valor de iniciativa no estaban explicitados | Nombre, iniciativa, CA y vida máxima son obligatorios; la vida actual comienza en el máximo. |
| Aumentar y disminuir vida | RF-055 | El DM aplica curación o daño y la API conserva el resultado entre 0 y la vida máxima. |
| Varios encuentros asociados a una campaña | RF-014 y el modelo `Campaña -> Combate` | Una campaña admite varios borradores y encuentros finalizados, con estado completamente independiente de otras campañas. |
| El DM avanza el turno | RF-050 y RF-054 | El avance identifica al siguiente participante y aumenta la ronda al volver al primero. |
| Los jugadores ven la tabla al activar un encuentro | RF-057 ya exige orden y turno, pero no define activación ni actualización | Solo el encuentro activo tiene proyección para jugadores y la web la refresca periódicamente mientras permanece abierta. |

El spec 005 ya persiste nombre, CA e iniciativa de los personajes. Combat los consume mediante un contrato público mínimo de Characters y guarda una instantánea propia al incorporarlos; no consulta ni modifica la persistencia de Characters. El spec 006 determina qué personaje está activo para cada jugador, pero RF-051 permite al DM elegir cualquier personaje de la campaña, por lo que la plantilla del encuentro no queda limitada al resumen de personajes activos.

## Actores

- **DM de la campaña:** prepara, modifica, activa y finaliza encuentros; añade o retira participantes; resuelve empates; avanza turnos y modifica la vida de enemigos.
- **Jugador aceptado:** solo puede consultar la proyección del encuentro activo de su campaña. No necesita tener un personaje activo para observarla.
- **Usuario ajeno:** no puede conocer ni consultar encuentros de la campaña.

## Alcance funcional

### Preparación de encuentros

- La portada de campaña ofrece una acción `Abrir encuentros` para DM y jugadores.
- El DM puede crear varios encuentros indicando un nombre y los consulta separados por estado.
- Un encuentro nace como `Borrador` y no es visible para los jugadores.
- En un borrador, el DM puede cambiar su nombre, añadir o retirar participantes y corregir sus iniciativas.
- El DM puede añadir cualquier personaje que pertenezca a la misma campaña, incluido uno sin propietario. Un personaje no puede aparecer dos veces en el mismo encuentro.
- Al añadir un personaje, Combat valida su pertenencia mediante Characters y captura su identificador, nombre y CA. El DM introduce el valor de iniciativa de ese encuentro, sin modificar la ficha del personaje.
- Los enemigos se crean exclusivamente dentro del encuentro y no pasan a ser personajes, NPC ni elementos reutilizables de una librería.
- Cada enemigo exige nombre, iniciativa, CA y vida máxima. Su vida actual se inicializa con la máxima.
- Se permiten varios enemigos con el mismo nombre porque cada uno conserva una identidad independiente dentro del encuentro.

### Orden e inicio

- El orden se calcula por iniciativa descendente.
- Si dos o más participantes empatan, el DM fija su orden relativo antes de activar el encuentro. La API conserva esa decisión de forma determinista.
- Un borrador solo puede activarse si tiene al menos un participante y todos los empates están resueltos.
- Como máximo existe un encuentro `Activo` por campaña. Mientras haya uno activo, intentar activar otro produce un conflicto y no cambia ninguno.
- Al activar, comienza la ronda 1 y el primer participante ordenado se convierte en el turno actual.
- La activación congela el nombre, los participantes, sus iniciativas y su orden. Durante el combate solo cambian el turno, la ronda y la vida actual de los enemigos.

### Dirección del combate

- Solo el DM puede avanzar al turno siguiente.
- Al avanzar desde el último participante se vuelve al primero y la ronda aumenta en uno.
- El turno siempre referencia a un participante existente y el avance no reordena la iniciativa.
- El DM puede aplicar a un enemigo una cantidad positiva de daño o curación.
- El daño nunca reduce la vida actual por debajo de 0 y la curación nunca la eleva por encima de la vida máxima.
- Llegar a 0 puntos de vida no elimina automáticamente al enemigo, no salta su turno y no revela su estado a los jugadores.
- El DM puede finalizar el encuentro activo. El encuentro pasa a `Finalizado`, conserva su último estado para consulta del DM y deja de ser visible para los jugadores.
- Un encuentro finalizado es de solo lectura y no se reactiva en este incremento.

### Vista segura del jugador

- Si no existe un encuentro activo, el jugador ve un estado vacío sin conocer borradores ni encuentros finalizados.
- Cuando el DM activa uno, el jugador ve su nombre, ronda, participante actual y la tabla completa en orden.
- Por cada participante, la proyección muestra nombre, tipo `Personaje` o `Enemigo`, iniciativa y si tiene el turno actual.
- La proyección no incluye CA, vida actual, vida máxima, controles, estados internos ni datos de preparación de los enemigos.
- La vista se vuelve a consultar automáticamente cada 5 segundos mientras está abierta. Esta actualización acotada satisface la observación de turnos sin introducir comunicación en tiempo real en el primer incremento.
- El jugador no puede alterar el encuentro mediante la interfaz ni invocando directamente la API.

## Decisiones funcionales aceptadas

Estas decisiones concretan aspectos no cerrados por el roadmap y fueron aceptadas por el usuario el 2026-08-23:

1. **Ciclo lineal.** Los estados son `Borrador`, `Activo` y `Finalizado`; finalizar es irreversible y no existe pausa ni reapertura inicial.
2. **Un activo sin sustitución implícita.** Para activar otro encuentro, el DM debe finalizar expresamente el actual. No se desactiva ni finaliza uno como efecto secundario de activar otro.
3. **Plantilla congelada durante el combate.** El elenco, las iniciativas y el orden solo se editan en borrador. Refuerzos, retirada de participantes y corrección del orden durante un encuentro activo quedan para una ampliación.
4. **Iniciativa por encuentro.** El DM introduce el total de iniciativa de cada personaje y enemigo. El valor guardado en la ficha del personaje no se sobrescribe.
5. **Empates resueltos por el DM.** El sistema no inventa un desempate con atributos que el modelo actual no posee; exige y conserva una decisión explícita antes de activar.
6. **Vida acotada.** El enemigo comienza con vida actual igual a la máxima y los ajustes quedan limitados al intervalo entre 0 y el máximo, sin puntos temporales.
7. **Consulta automática por sondeo.** La vista del jugador se refresca cada 5 segundos; WebSockets, presencia y sincronización instantánea quedan fuera del primer incremento.
8. **Instantáneas históricas.** El encuentro conserva los datos capturados del personaje. Renombrar, editar o eliminar posteriormente el personaje no modifica un encuentro preparado o finalizado.

## Reglas y validación

- El nombre del encuentro es obligatorio, se recortan sus espacios exteriores y contiene entre 2 y 120 caracteres.
- El nombre del enemigo es obligatorio, se recortan sus espacios exteriores y contiene entre 2 y 80 caracteres.
- CA de enemigo: entero entre 0 y 40, alineado con Characters.
- Iniciativa del encuentro: entero entre -20 y 30, alineado con Characters.
- Vida máxima: entero entre 1 y 100.000. La vida actual siempre permanece entre 0 y ese máximo.
- Una operación de daño o curación exige una cantidad entera entre 1 y 100.000.
- Un encuentro pertenece exactamente a una campaña y nunca cambia de campaña.
- Un participante pertenece exactamente a un encuentro. Los identificadores siempre se resuelven junto a campaña y encuentro, no de forma global.
- Solo el DM autorizado puede listar todos los encuentros o ejecutar escrituras.
- Un jugador aceptado solo puede consultar la proyección segura del encuentro activo.
- Toda operación vuelve a comprobar usuario, campaña y rol en la API; los controles visuales no sustituyen esa autorización.
- La base de datos garantiza como máximo un encuentro activo por campaña también ante peticiones concurrentes.

## Recorrido web

- La ruta `/campaigns/:campaignId/encounters` carga la experiencia autorizada según el rol.
- El DM ve el listado de encuentros, puede crear un borrador y abrir su mesa de preparación.
- La mesa de preparación permite seleccionar personajes de la campaña, crear enemigos y resolver el orden antes de activar.
- La mesa activa presenta ronda y turno actual de forma destacada, el orden inmutable, controles de daño/curación por enemigo y acciones para avanzar o finalizar.
- El jugador ve únicamente la proyección activa o un estado vacío. El cambio detectado por el sondeo actualiza ronda, turno y tabla sin recargar la página completa.
- Los estados de carga, error, vacío, conflicto de activación y pérdida de acceso son explícitos.

## Contrato HTTP funcional

- `GET /api/v1/campaigns/{campaignId}/encounters`: lista los encuentros para el DM.
- `POST /api/v1/campaigns/{campaignId}/encounters`: crea un borrador y devuelve `201 Created`.
- `GET /api/v1/campaigns/{campaignId}/encounters/{encounterId}`: devuelve la mesa completa al DM.
- `PUT /api/v1/campaigns/{campaignId}/encounters/{encounterId}`: cambia el nombre de un borrador.
- `POST /api/v1/campaigns/{campaignId}/encounters/{encounterId}/characters`: añade un personaje con su iniciativa de encuentro.
- `POST /api/v1/campaigns/{campaignId}/encounters/{encounterId}/enemies`: crea un enemigo local.
- `PUT /api/v1/campaigns/{campaignId}/encounters/{encounterId}/participants/{participantId}/initiative`: corrige la iniciativa de un participante en borrador.
- `PUT /api/v1/campaigns/{campaignId}/encounters/{encounterId}/initiative-order`: confirma el orden completo y resuelve los empates del borrador.
- `DELETE /api/v1/campaigns/{campaignId}/encounters/{encounterId}/participants/{participantId}`: retira un participante del borrador.
- `PUT /api/v1/campaigns/{campaignId}/encounters/{encounterId}/active`: activa un borrador.
- `POST /api/v1/campaigns/{campaignId}/encounters/{encounterId}/turns/advance`: avanza un turno.
- `POST /api/v1/campaigns/{campaignId}/encounters/{encounterId}/enemies/{participantId}/hit-points`: aplica daño o curación.
- `PUT /api/v1/campaigns/{campaignId}/encounters/{encounterId}/finished`: finaliza el encuentro activo.
- `GET /api/v1/campaigns/{campaignId}/encounters/active`: devuelve a cualquier miembro aceptado la proyección activa segura; el DM obtiene su mesa completa mediante el endpoint de detalle.

Los errores usan `ProblemDetails`: `400` para campos, cantidades o transiciones inválidas; `401` sin sesión; `403` sin acceso o rol suficiente; `404` cuando campaña, encuentro, participante o personaje no existe en el contexto indicado; y `409` ante otro encuentro activo, personaje duplicado, empates pendientes o escritura concurrente que exija recargar.

El plan podrá separar DTO de DM y jugador o ajustar la forma concreta de las rutas sin reducir estas operaciones ni mezclar proyecciones privadas y públicas.

## Ownership técnico

- `apps/api`: un nuevo módulo `Combat` será propietario de encuentros, participantes, instantáneas, orden, turnos, rondas, vida de enemigos, persistencia PostgreSQL, endpoints y métricas. Consumirá el contrato público de Campaigns para autorización y un contrato público mínimo de Characters para validar y capturar personajes de la campaña. No consultará tablas, repositorios, entidades ni `DbContext` ajenos.
- `apps/web`: un nuevo módulo `combat` será propietario de rutas, clientes HTTP, contratos, listado del DM, preparación, mesa activa y proyección del jugador. Campaigns solo enlazará a su ruta pública.

Ambas superficies cambian porque la capacidad requiere una experiencia diferenciada para DM y jugadores y una fuente autoritativa que persista y proteja el estado del combate.

## Persistencia y consistencia

- Combat utiliza su propio esquema, `DbContext` y migraciones, siguiendo los límites modulares existentes.
- Una restricción parcial garantiza como máximo un encuentro activo por campaña.
- Activar, avanzar turno, cambiar de ronda, ajustar vida y finalizar se confirman de forma atómica por operación.
- Los participantes de personaje guardan el identificador externo y los campos capturados necesarios, sin clave foránea hacia Characters.
- Las bajas o cambios posteriores en Characters no provocan cascadas ni reescriben la historia del encuentro.
- Dos campañas nunca comparten encuentros, participantes, orden ni puntos de vida, aunque utilicen el mismo módulo de aventura.

## Observabilidad, privacidad y seguridad

- Se medirán listado, creación, edición de plantilla, activación, avance, ajuste de vida, consulta activa y finalización con resultado y duración de cardinalidad acotada.
- Los logs, trazas, métricas y errores no incluyen nombres de encuentro, personaje o enemigo ni identificadores de campaña, encuentro, participante o usuario.
- La API construye expresamente una proyección de jugador que omite todos los campos privados; no se limita a ocultarlos mediante CSS.
- El sondeo se detiene al abandonar la página y no continúa cuando la sesión pierde acceso.
- Los nombres se presentan como texto y no se interpreta HTML o Markdown.

## Criterios de aceptación

1. El DM crea dos encuentros en la misma campaña y ambos permanecen en borrador sin ser visibles para los jugadores.
2. En un borrador, el DM añade personajes de la campaña con su iniciativa de encuentro y crea enemigos indicando nombre, iniciativa, CA y vida máxima.
3. La API rechaza un personaje de otra campaña, un personaje repetido, valores fuera de rango y toda escritura de un jugador o usuario ajeno sin producir cambios parciales.
4. Los participantes se ordenan por iniciativa descendente y un empate impide activar hasta que el DM fija su orden relativo.
5. Al activar un encuentro válido, comienza la ronda 1 en el primer participante y no puede existir un segundo encuentro activo en la campaña.
6. Cada avance mueve el turno exactamente al participante siguiente; al pasar del último al primero incrementa la ronda una sola vez y no altera el orden.
7. Aplicar daño o curación conserva la vida del enemigo, limitada entre 0 y su máximo, y no afecta a otro enemigo aunque comparta nombre.
8. Un jugador aceptado ve automáticamente el nombre del encuentro activo, ronda, turno, orden, tipos e iniciativas, pero la respuesta y la interfaz no contienen CA ni vida de enemigos ni controles.
9. Un jugador sin personaje activo puede observar el encuentro; un usuario ajeno no puede consultar siquiera la proyección activa.
10. Los borradores y finalizados nunca aparecen en la vista del jugador.
11. Finalizar conserva el último estado para el DM, retira inmediatamente la proyección del jugador y permite activar después otro borrador.
12. Editar o eliminar en Characters un personaje ya capturado no altera el encuentro, mientras que un personaje inexistente o ajeno no puede añadirse de nuevo.
13. Las pruebas de dominio, aplicación, persistencia PostgreSQL, contrato HTTP, aislamiento modular y componentes Angular demuestran estos recorridos; las suites y builds existentes permanecen verdes.

## Fuera de alcance

- Tiradas de dados, cálculo automático del total de iniciativa o integración con modificadores de característica.
- Puntos de vida, daño, curación, condiciones, concentración, recursos o acciones de personajes jugadores.
- Puntos de vida temporales, resistencias, vulnerabilidades, inmunidades, muerte automática o retirada automática de enemigos.
- Añadir, retirar o reordenar participantes después de activar; pausar, reabrir, duplicar o archivar encuentros.
- Turno anterior, deshacer, historial de cambios, auditoría visible o reproducción del combate.
- NPC, bestiario, plantillas reutilizables, importación de criaturas o estadísticas distintas de nombre, iniciativa, CA y vida.
- Mapas, cuadrículas, fichas, tokens, movimiento, alcance, línea de visión o contenido editorial concreto.
- Chat, notificaciones, presencia, WebSockets, Server-Sent Events o sincronización instantánea.
- Permisos configurables, varios DM o control del encuentro por jugadores.

## Validación

La petición del usuario del 2026-08-23 coincide con el módulo Combat previsto por el roadmap y amplía su concreción con encuentros preparados, CA obligatoria para enemigos y activación como condición de visibilidad. El usuario aceptó las decisiones funcionales, solicitó continuar con el plan e inició la implementación el mismo día.

El incremento está implementado de extremo a extremo en `apps/api` y `apps/web`. La evidencia final incluye 85 pruebas .NET con PostgreSQL/Azurite, 68 pruebas Angular, builds de producción, imágenes API/web y validación de las dos configuraciones Compose. Los criterios de aceptación quedan cubiertos por las suites de dominio, Application, persistencia, contrato HTTP, arquitectura, cliente y componentes registradas en [tasks.md](tasks.md).
