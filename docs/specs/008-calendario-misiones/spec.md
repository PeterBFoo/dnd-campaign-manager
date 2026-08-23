# Spec 008: Registro y gestión compartida de misiones

- Estado: Implementada; construcción adicional de imágenes finales pendiente
- Fecha: 2026-08-23
- Tipo: incremento funcional vertical
- Roadmap: [roadmap funcional](../../roadmap/product-roadmap.md)
- Requisitos principales: RF-040, RF-041, RF-042, RF-043, RF-044 y RF-045
- Dependencias: [spec 004](../004-creacion-campanas/spec.md), [spec 005](../005-personajes-campana/spec.md) y [spec 006](../006-resumen-personajes-activos/spec.md)

## Problema

Los miembros de una campaña no disponen de un lugar compartido donde registrar las misiones que el grupo ha aceptado y conocer cuál guía actualmente la aventura. Esa información queda fuera del producto, pierde su relación con la campaña y no puede evolucionar de forma consistente entre jugadores y DM.

El roadmap exige además que exista como máximo una misión principal por campaña, pero no concreta los permisos de actualización y eliminación ni el significado mínimo de calendario. Este incremento resuelve esas decisiones sin imponer fechas a las misiones ni anticipar recordatorios o contenido editorial.

## Objetivo

Permitir que el DM y los jugadores registren misiones en un espacio compartido de su campaña, las consulten con una ordenación estable, actualicen su contenido y estado, eliminen altas erróneas y cambien de forma atómica qué misión activa es la principal.

La capacidad será utilizable de extremo a extremo desde Angular, persistida por un módulo propio de la API y autorizada de nuevo en cada operación del backend.

## Actores

- **Jugador con personaje activo:** consulta el registro, crea misiones atribuidas a su personaje activo, actualiza cualquier misión y elimina las que él mismo creó.
- **Jugador sin personaje activo:** consulta y actualiza las misiones compartidas, y puede eliminar las que creó anteriormente, pero no puede crear otra hasta activar un personaje.
- **DM de la campaña:** consulta, crea, actualiza y elimina cualquier misión de su campaña sin necesitar un personaje activo.
- **Usuario ajeno a la campaña:** no puede consultar ni modificar ninguna misión.

## Alcance funcional

### Registro y consulta

- Cada campaña dispone de un registro de misiones independiente y compartido por todos sus miembros aceptados. Este registro concreta el calendario de RF-040 sin asignar fechas funcionales a las misiones.
- La portada de campaña ofrece una acción `Abrir misiones` para DM y jugadores.
- Cada misión presenta título, descripción, estado, autor original, fecha técnica de creación y fecha de última actualización cuando haya sido editada.
- La misión principal activa ocupa una sección destacada y aparece antes que cualquier otra misión.
- Después se muestran las demás misiones activas de más reciente a más antigua. Las misiones cerradas aparecen en una sección separada, también por actualización más reciente.
- Los estados de carga, error y registro vacío son explícitos.

### Creación

- El DM puede crear una misión directamente en su campaña.
- Un jugador aceptado solo puede crear una misión cuando dispone de personaje activo en esa campaña.
- La API resuelve el personaje activo a partir del usuario autenticado y la campaña de la ruta. El cliente no elige ni envía el identificador del personaje autor.
- El formulario solicita título, descripción opcional y si la nueva misión debe ser principal.
- Una misión nueva nace en estado `Activa`.
- Si se crea como principal, la operación crea la misión y retira esa condición de la principal anterior en una única transacción.

### Evolución y colaboración

- Cualquier miembro aceptado de la campaña, DM o jugador, puede modificar el título, la descripción y el estado de cualquier misión de esa campaña.
- La edición conserva la campaña, la identidad de la misión, su autor original y su fecha técnica de creación.
- Los estados disponibles son `Activa`, `Completada`, `Fallida` y `Cancelada`.
- Una misión cerrada puede volver a `Activa`, pero no recupera automáticamente la condición de principal.
- Completar, fallar o cancelar la misión principal retira automáticamente esa condición en la misma operación.

### Eliminación

- Un jugador puede eliminar una misión que él mismo creó, aunque su personaje activo actual sea otro.
- El DM puede eliminar cualquier misión de su campaña para corregir altas erróneas o moderar el registro.
- Otro jugador no puede eliminar una misión ajena aunque tenga permiso para editarla.
- La interfaz exige confirmación explícita antes de eliminar.
- La eliminación es definitiva en este incremento. Si se elimina la misión principal, la campaña queda sin principal y ninguna otra se promociona automáticamente.

### Misión principal

- Solo una misión `Activa` puede marcarse como principal.
- Marcar una misión activa como principal desmarca la anterior dentro de la misma transacción.
- La principal actual puede dejar de serlo sin seleccionar otra; la campaña admite no tener misión principal.
- DM y jugadores aceptados pueden marcar o desmarcar la misión principal con los mismos permisos colaborativos de edición.
- La API y la persistencia garantizan la unicidad también ante peticiones concurrentes; el frontend no constituye el control autoritativo.

## Decisiones funcionales aceptadas

1. **Actualización colaborativa.** DM y jugadores aceptados pueden actualizar cualquier misión, incluida la selección de la principal. La autoría visible sigue identificando a quien la registró originalmente.
2. **Autoría de jugador mediante personaje activo.** Crear como jugador exige personaje activo y captura su identificador y nombre. Las ediciones posteriores no cambian esa autoría. Las creaciones del DM se muestran como `Dirección de campaña` y no se atribuyen a un personaje.
3. **Registro sin fechas funcionales.** El calendario de RF-040 se concreta como un registro ordenado de misiones. No se solicita fecha de aceptación, fecha objetivo ni recurrencia.
4. **Estados y eliminación cumplen propósitos distintos.** `Completada`, `Fallida` y `Cancelada` conservan la evolución normal; la eliminación definitiva permite corregir una misión creada por error.
5. **Borrado por creador y moderación del DM.** Un jugador elimina solo las misiones que creó. El DM puede eliminar cualquiera de su campaña. La autorización se basa en el usuario creador guardado, no en el personaje activo actual.
6. **Principal solo mientras está activa.** Cerrar la principal la desmarca; reabrirla no desplaza automáticamente a la principal vigente.
7. **Registro ordenado en lugar de vista temporal.** La experiencia inicial separa principal, activas y cerradas; no presenta una cuadrícula mensual o semanal.

El usuario corrigió y aceptó estas decisiones el 2026-08-23. En particular, confirmó que las misiones no requieren fecha de aceptación ni fecha objetivo y que deben poder eliminarse si se crean por error.

## Reglas y validación

- El título es obligatorio, se recortan sus espacios exteriores y contiene entre 2 y 120 caracteres.
- La descripción es opcional, conserva saltos de línea, se trata como texto plano y admite como máximo 5.000 caracteres.
- No existen fechas funcionales aportadas por el usuario. El servidor asigna `createdAt` y `updatedAt` como instantes UTC; el cliente no puede establecerlos.
- El servidor fija el estado inicial `Active`; el cliente no puede crear directamente una misión cerrada.
- Solo las misiones activas pueden ser principales.
- La ordenación es determinista: principal activa; restantes activas por `createdAt` descendente y un desempate inmutable; cerradas por la marca temporal de su última actualización descendente y el mismo desempate inmutable.
- La lectura exige pertenencia aceptada a la campaña.
- La creación por un jugador exige rol `Player` y personaje activo; la creación por el DM exige rol `Dm` y no consulta Characters.
- La actualización y el cambio de principal exigen rol `Dm` o `Player` aceptado, pero no personaje activo.
- La eliminación exige rol `Dm` o que el actor sea el jugador guardado como creador de la misión.
- Toda operación vuelve a comprobar usuario, campaña y rol en la API.
- Una misión de otra campaña nunca se resuelve únicamente por su identificador: campaña y misión deben coincidir con la ruta.
- El frontend oculta o deshabilita acciones no disponibles por experiencia de uso, pero no sustituye la autorización del backend.

## Recorrido web

- La ruta `/campaigns/:campaignId/missions` carga el registro autorizado de la campaña.
- La cabecera presenta la misión principal cuando existe y una acción para registrar otra misión.
- El formulario de alta y edición muestra validaciones de título y descripción. En alta permite solicitar que la misión sea principal.
- Cada misión activa ofrece acciones para editar, cambiar su estado y marcarla o desmarcarla como principal.
- Las misiones cerradas permiten consultar su resultado, editar sus datos y reabrirlas.
- La acción de eliminación aparece solo cuando la representación indique que el actor puede usarla y siempre solicita confirmación.
- Un jugador sin personaje activo puede consultar y editar, pero al intentar crear recibe una explicación y una acción hacia `/campaigns/:campaignId/characters`.
- La interfaz actualiza la misión principal y las listas solo después de recibir una respuesta correcta de la API; ante un conflicto recarga el estado autoritativo.

## Contrato HTTP funcional

- `GET /api/v1/campaigns/{campaignId}/missions`: devuelve el registro autorizado con la misión principal primero y las demás misiones en orden determinista.
- `POST /api/v1/campaigns/{campaignId}/missions`: crea una misión y devuelve `201 Created`.
- `PUT /api/v1/campaigns/{campaignId}/missions/{missionId}`: actualiza contenido o estado y devuelve `200 OK`.
- `PUT /api/v1/campaigns/{campaignId}/missions/{missionId}/main`: marca una misión activa como principal y devuelve la misión actualizada.
- `DELETE /api/v1/campaigns/{campaignId}/missions/{missionId}/main`: retira la condición de principal y devuelve `204 No Content`; es idempotente si la misión ya no era principal.
- `DELETE /api/v1/campaigns/{campaignId}/missions/{missionId}`: elimina definitivamente una misión autorizada y devuelve `204 No Content`.

La creación recibe `title`, `description?` e `isMain`. La edición recibe `title`, `description?` y `status`; la principal se cambia únicamente mediante sus operaciones específicas.

La representación pública incluye como mínimo `id`, `campaignId`, `title`, `description`, `status`, `isMain`, `authorType`, `authorCharacterId?`, `authorDisplayName`, `createdAt`, `updatedAt` y `canDelete`. No expone el identificador del usuario creador.

Los errores usan `ProblemDetails`: `400` para campos o estado inválidos; `401` sin sesión; `403` sin acceso, rol válido o permiso de eliminación; `404` cuando la misión no existe dentro de la campaña indicada; y `409` cuando un jugador intenta crear sin personaje activo, se intenta marcar como principal una misión cerrada o una escritura concurrente exige recargar el registro.

## Ownership técnico

- `apps/api`: un nuevo módulo `Missions` es propietario del agregado de misión, persistencia PostgreSQL, consultas, comandos, endpoints, métricas y unicidad de la principal. Consume el contrato público de Campaigns para comprobar acceso y rol, y el contrato público mínimo de Characters para resolver el personaje activo al crear como jugador. No consulta tablas, `DbContext`, repositorios ni entidades internas de otros módulos.
- `apps/web`: un nuevo módulo `missions` es propietario de rutas, cliente HTTP, contratos, registro y formularios. Campaigns solo enlaza a la ruta o compone una API pública mínima; no importa internals de Missions.

Ambas superficies cambian porque el incremento requiere una experiencia web completa y una fuente autoritativa persistida y autorizada en la API.

## Persistencia y consistencia

- Missions persiste sus datos en su propio esquema, con su propio `DbContext` y migración conforme a los límites modulares existentes.
- La persistencia impide que dos misiones de una campaña sean principales a la vez, además de que el dominio aplique la regla.
- Crear una nueva principal, cambiarla o cerrar la principal actualiza todas las filas afectadas en una única transacción.
- La referencia histórica a un personaje se conserva como identidad externa y nombre capturado, sin clave foránea hacia las tablas privadas de Characters.
- Renombrar, reasignar o eliminar posteriormente el personaje no cambia ni elimina la misión.
- Eliminar una misión no afecta a personajes ni a otra campaña.
- El estado de una campaña no se comparte con otra aunque ambas usen el mismo módulo de aventura.

## Observabilidad, privacidad y seguridad

- Se medirán listado, creación, edición, cambio de principal y eliminación, con resultado y duración de cardinalidad acotada.
- Los logs, trazas, métricas y errores no incluyen títulos, descripciones, nombres de personaje ni identificadores de misión, campaña, personaje o usuario.
- El contenido se trata como dato privado de campaña y solo se devuelve después de autorizar el acceso.
- Título y descripción se presentan como texto, sin interpretar HTML o Markdown aportado por el usuario.
- Los fallos de autorización, los intentos entre campañas y los conflictos de unicidad son observables mediante resultados agregados sin registrar datos privados.

## Criterios de aceptación

1. Un jugador aceptado con personaje activo abre un registro vacío, crea una misión indicando título y descripción y la consulta con estado activo, autor y marcas temporales asignadas por el servidor.
2. El DM registra una misión sin personaje activo y la autoría visible indica `Dirección de campaña` sin exponer su identificador de usuario.
3. Un jugador sin personaje activo puede consultar y editar misiones, pero no puede crear mediante la interfaz ni invocando directamente la API.
4. DM y jugadores aceptados pueden editar una misión creada por otro miembro; la misión conserva identidad, campaña, fecha técnica de creación y autor original.
5. Crear o marcar una nueva misión principal desmarca la anterior atómicamente y la nueva aparece primero tanto en la web como en la API.
6. Dos intentos concurrentes de marcar principales no dejan nunca más de una misión principal en la campaña.
7. Completar, fallar o cancelar la principal deja la campaña sin esa principal; reabrirla no desplaza a otra misión que se hubiera marcado después.
8. Un título vacío o una descripción superior al límite se rechazan sin cambios parciales; los contratos de escritura no aceptan fechas funcionales.
9. Las misiones activas aparecen tras la principal por creación descendente y las cerradas en su sección, con orden estable ante empates.
10. Un miembro puede desmarcar la principal sin elegir sustituta y repetir la operación sin error ni efectos laterales.
11. Un usuario ajeno no puede listar ni operar misiones alterando identificadores o rutas, y una misión de otra campaña no puede modificarse desde la ruta actual.
12. Un jugador elimina una misión propia después de confirmarlo, pero recibe `403` al intentar eliminar una creada por otro jugador; el DM puede eliminar cualquiera de su campaña.
13. Eliminar la principal deja la campaña sin principal y no promociona otra misión.
14. Renombrar, reasignar o eliminar el personaje autor no modifica ni elimina la misión ni su nombre de autor capturado.
15. Las pruebas de dominio, aplicación, persistencia PostgreSQL, contrato HTTP, aislamiento modular y componentes Angular demuestran estos recorridos; las suites y builds existentes permanecen verdes.

## Fuera de alcance

- Fechas funcionales de aceptación u objetivo, cuadrícula mensual o semanal, calendario ficticio configurable, zonas horarias, horas del día y sincronización con calendarios externos.
- Recurrencia, recordatorios, notificaciones, suscripciones o vencimientos automáticos.
- Papelera, recuperación de misiones eliminadas, archivado separado, historial de versiones o auditoría visible de cada cambio.
- Submisiones, dependencias, etiquetas, categorías, recompensas, responsables, votaciones o comentarios.
- Adjuntos, imágenes, enlaces enriquecidos, HTML, Markdown o texto enriquecido.
- Visibilidad privada, misiones secretas del DM o permisos configurables por misión.
- Presencia, bloqueo de edición y actualización en tiempo real.
- Integración con bitácora, NPC, capítulos, módulos de aventura, combates o contenido editorial concreto.
- Cambios en las reglas de personajes activos, ownership de personajes o acceso a campañas.

## Validación

El usuario aceptó el alcance funcional y pidió continuar con el plan el 2026-08-23, después de retirar la fecha de aceptación y la fecha objetivo y de incorporar la eliminación de misiones creadas por error. La implementación quedó verificada con 77 pruebas .NET en Docker sobre PostgreSQL y Azurite, 63 pruebas Angular, build web de producción, compilación de solución y configuración Compose válida. La construcción adicional de las imágenes finales queda pendiente por el límite del sistema de aprobaciones; la imagen de pruebas sí compiló la API en Release.
