# Spec 014: Capítulos ordenados de un módulo

- Estado: Implementada; verificación .NET y PostgreSQL pendiente por entorno
- Fecha: 2026-08-29
- Tipo: incremento funcional vertical
- Roadmap: [roadmap funcional](../../roadmap/product-roadmap.md)
- Requisitos principales: RF-013, RF-015, RF-017, RF-060, RF-062 y RF-063
- Dependencias: [spec 012](../012-libreria-modulos/spec.md) y [spec 013](../013-asignacion-modulo-campana/spec.md)

## Problema

Los módulos no pueden organizar todavía el material de dirección en una secuencia navegable. El roadmap tampoco resuelve si los capítulos tienen progreso o desbloqueo, ni cómo evitar órdenes duplicados ante reordenaciones concurrentes.

## Objetivo

Permitir que el administrador cree, edite, ordene y elimine capítulos de un módulo, y que el DM los consulte en orden desde una campaña que tenga ese módulo asociado. Los capítulos serán una biblioteca completa, no estado de progreso.

## Actores y visibilidad

- **Administrador de plataforma:** CRUD y reordenación desde la autoría del módulo.
- **DM:** lectura completa desde una campaña propia con ese módulo.
- **Jugador:** no recibe capítulos, descripciones ni siquiera su existencia.
- **Usuario ajeno:** no puede acceder.

## Alcance funcional

- Un módulo contiene cero o más capítulos y puede editarse mientras esté asociado a campañas.
- Un capítulo tiene nombre, descripción opcional, posición y marcas temporales.
- El administrador crea, edita, elimina y reordena.
- El DM ve el índice ordenado y el detalle desde el contexto de su campaña.
- No existe capítulo actual, progreso, completado, bloqueo ni descubrimiento.
- Eliminar un capítulo elimina sus relaciones con recursos presentes o futuros, pero nunca elimina mapas, localizaciones o NPC.
- Los recursos se incorporan en specs posteriores; este incremento no crea asociaciones hacia conceptos aún inexistentes.

## Reglas y validación

- Nombre obligatorio, texto plano, normalizado, entre 2 y 120 caracteres.
- Descripción opcional, texto plano, hasta 20.000 caracteres.
- La posición es única y densa dentro del módulo, comenzando en uno.
- Crear añade al final. Eliminar compacta posiciones en la misma transacción.
- Reordenar envía exactamente una vez todos los identificadores vigentes del módulo y una versión esperada del índice; omisiones, duplicados, capítulos ajenos o versión obsoleta producen conflicto sin cambios parciales.
- El nombre no necesita ser único dentro del módulo; la posición proporciona la identidad visual.
- Las relaciones y consultas siempre resuelven simultáneamente módulo y capítulo.
- Un DM solo puede leer por la ruta de una campaña cuyo módulo actual coincida.
- Editar el capítulo actualiza inmediatamente su proyección en todas las campañas asociadas.

## Recorrido web

- `/admin/adventure-modules/:moduleId/chapters` ofrece índice, alta, edición, eliminación y reordenación accesible por teclado.
- `/campaigns/:campaignId/adventure/chapters` ofrece al DM un índice de solo lectura y detalle.
- La campaña sin módulo muestra una explicación y enlace de retorno; el jugador no recibe enlace ni carga el contenido.
- Las interfaces representan carga, módulo vacío, error, conflicto de versión y pérdida de autorización.

## Contrato HTTP funcional

- `GET|POST /api/v1/admin/adventure-modules/{moduleId}/chapters`
- `GET|PUT|DELETE /api/v1/admin/adventure-modules/{moduleId}/chapters/{chapterId}`
- `PUT /api/v1/admin/adventure-modules/{moduleId}/chapters/order`
- `GET /api/v1/campaigns/{campaignId}/adventure/chapters`
- `GET /api/v1/campaigns/{campaignId}/adventure/chapters/{chapterId}`

Los endpoints administrativos incluyen versión de concurrencia y procedencia editorial. La proyección de campaña omite auditoría y datos de administración.

## Ownership técnico

- `apps/api/Modules/AdventureCatalog` posee capítulos, orden, persistencia, endpoints y proyección de lectura. Consume un contrato mínimo de Campaigns para comprobar que el actor es DM y resolver el módulo actual.
- `apps/web/src/app/modules/adventure-catalog` posee autoría y lectura DM. Campaigns enlaza a su ruta pública sin importar internals.

Ambas superficies cambian para entregar autoría y consumo autorizados de extremo a extremo.

## Persistencia y observabilidad

- Una restricción impide posiciones duplicadas dentro del módulo.
- Alta, eliminación y reordenación confirman todas las posiciones afectadas en una transacción.
- Métricas acotadas cubren listado, detalle, alta, edición, reordenación y eliminación, sin texto ni identificadores en etiquetas o logs.

## Criterios de aceptación

1. El administrador abre un módulo vacío y crea tres capítulos que aparecen en orden 1, 2 y 3.
2. Editar nombre o descripción conserva identidad y posición y se refleja en la lectura DM.
3. Una reordenación válida cambia el índice completo atómicamente.
4. Una lista con omisiones, duplicados, capítulos de otro módulo o versión obsoleta se rechaza sin alterar el orden.
5. Eliminar el segundo capítulo compacta el tercero a la posición dos y no elimina recursos relacionados.
6. El DM consulta todos los capítulos del módulo desde su campaña, sin estado de progreso.
7. Un jugador, otro DM o una campaña con distinto módulo no pueden consultar el índice ni el detalle.
8. Cambiar o retirar el módulo de la campaña impide seguir consultando sus capítulos.
9. La API y la base de datos impiden posiciones duplicadas ante concurrencia.
10. Las pruebas Angular, API, PostgreSQL, autorización y arquitectura cubren los recorridos.

## Fuera de alcance

- Mapas, localizaciones, NPC y sus asociaciones.
- Progreso, capítulo activo, desbloqueo, publicación o versiones por campaña.
- Contenido enriquecido, adjuntos, comentarios o edición colaborativa en tiempo real.
- Consulta de capítulos por jugadores.
