# Spec 016: Localizaciones y puntos de interés sobre mapas

- Estado: Implementada; verificaciones PostgreSQL/Azurite pendientes
- Fecha: 2026-08-29
- Tipo: incremento funcional vertical
- Roadmap: [roadmap funcional](../../roadmap/product-roadmap.md)
- Requisitos principales: RF-013, RF-015, RF-017, RF-018, RF-061, RF-062 y RF-063
- Dependencias: [spec 014](../014-capitulos-modulo/spec.md) y [spec 015](../015-mapas-modulo/spec.md)

## Problema

Un módulo puede tener mapas, pero no puede representar lugares reutilizables, mostrar un mismo lugar en varios mapas ni describir puntos relevantes dentro de su mapa detallado. Guardar la posición en la localización impediría reutilizarla en contextos distintos.

## Objetivo

Permitir que el administrador mantenga localizaciones del módulo, las relacione con capítulos, coloque cada una sobre uno o varios mapas y gestione puntos de interés en su mapa detallado. El DM consultará esta estructura desde su campaña.

## Modelo funcional

- Una `Location` pertenece exactamente a un módulo y tiene nombre y descripción.
- Puede usar `0..1` mapa existente del mismo módulo como mapa detallado; un mapa puede reutilizarse como detalle de varias localizaciones.
- `LocationPlacement` relaciona un mapa con una localización y posee coordenadas normalizadas propias.
- `PointOfInterest` pertenece a una localización y tiene nombre, descripción y posición opcional sobre su mapa detallado.
- Localizaciones, placements y POI no contienen estado de campaña.

## Actores y alcance

- El administrador crea, edita y elimina localizaciones y POI, administra mapa detallado, placements y asociaciones con capítulos.
- El DM consulta colección, detalle, mapas, markers y POI desde una campaña con el módulo.
- Los jugadores no reciben estos recursos de dirección.
- Una localización puede relacionarse con varios capítulos y un capítulo con varias localizaciones, sin duplicar la entidad.

## Reglas y validación

- Nombre de localización o POI: texto plano obligatorio de 2 a 120 caracteres.
- Descripción opcional: texto plano, hasta 10.000 caracteres para localización y 5.000 para POI.
- Coordenadas `x` e `y` decimales normalizadas en el intervalo cerrado `[0,1]`, independientes de píxeles y resolución.
- Un par mapa-localización tiene como máximo un placement; la misma localización sí puede aparecer en mapas diferentes.
- Mapa, localización, capítulo y POI relacionados deben pertenecer al mismo módulo.
- Un POI puede existir sin posición. Solo puede posicionarse cuando la localización tiene mapa detallado.
- Cambiar o retirar el mapa detallado conserva los POI, pero elimina atómicamente sus posiciones anteriores. La interfaz los presenta como pendientes de recolocación.
- Sustituir el binario de un mapa conserva posiciones normalizadas.
- Eliminar una localización elimina sus POI, placements y relaciones con capítulos; no elimina mapas ni capítulos.
- Eliminar un mapa retira placements, referencias como mapa detallado y posiciones de POI; conserva localizaciones y POI.
- Eliminar un módulo elimina en cascada todas sus localizaciones y, con ellas, sus POI, placements y relaciones con capítulos; ninguna fila dependiente puede quedar huérfana.
- Los cambios se reflejan inmediatamente en todas las campañas asociadas al módulo.

## Recorrido web

- `/admin/adventure-modules/:moduleId/locations` ofrece colección, alta y edición.
- El detalle permite seleccionar mapa detallado, administrar POI, capítulos y placements mediante un selector visual sobre la imagen.
- `/campaigns/:campaignId/adventure/locations` ofrece al DM colección y detalle de solo lectura con mapas y marcadores autorizados.
- Teclado y formularios numéricos ofrecen una alternativa al posicionamiento mediante puntero.

## Contrato HTTP funcional

- `GET|POST /api/v1/admin/adventure-modules/{moduleId}/locations`
- `GET|PUT|DELETE /api/v1/admin/adventure-modules/{moduleId}/locations/{locationId}`
- `PUT|DELETE /api/v1/admin/adventure-modules/{moduleId}/locations/{locationId}/detail-map`
- `POST /api/v1/admin/adventure-modules/{moduleId}/locations/{locationId}/points-of-interest`
- `PUT|DELETE /api/v1/admin/adventure-modules/{moduleId}/locations/{locationId}/points-of-interest/{poiId}`
- `PUT|DELETE /api/v1/admin/adventure-modules/{moduleId}/maps/{mapId}/locations/{locationId}`
- `PUT|DELETE /api/v1/admin/adventure-modules/{moduleId}/locations/{locationId}/chapters/{chapterId}`
- `GET /api/v1/campaigns/{campaignId}/adventure/locations`
- `GET /api/v1/campaigns/{campaignId}/adventure/locations/{locationId}`

El plan podrá agrupar cambios de asociaciones en comandos de reemplazo completo si conserva atomicidad, validación y semántica.

## Ownership técnico

- `apps/api/Modules/AdventureCatalog` posee localizaciones, POI, relaciones, posiciones y proyección DM.
- `apps/web/src/app/modules/adventure-catalog` posee autoría visual y consulta DM.

No se crea `LocationMap` como tipo de mapa ni una jerarquía genérica de recursos.

## Persistencia y observabilidad

- Restricciones únicas protegen relaciones capítulo-localización y mapa-localización.
- Restricciones de base de datos acotan coordenadas y garantizan que los hijos pertenezcan a la identidad correcta dentro del módulo.
- Cambiar mapa detallado, retirar posiciones y borrar relaciones dependientes se confirma en una transacción.
- Se miden CRUD y relaciones con cardinalidad acotada, sin nombres, descripciones, coordenadas o identificadores en logs y métricas.

## Criterios de aceptación

1. El administrador crea una localización y la asocia con dos capítulos sin duplicarla.
2. Selecciona un mapa detallado y crea dos POI posicionados mediante coordenadas normalizadas.
3. Coloca la misma localización en dos mapas generales con posiciones independientes.
4. Repetir el mismo par mapa-localización actualiza su posición y no crea duplicados.
5. No se puede relacionar ningún recurso de otro módulo ni usar coordenadas fuera de rango.
6. Cambiar el mapa detallado conserva los POI, elimina sus posiciones antiguas y permite recolocarlos.
7. Sustituir la imagen conserva placements y posiciones normalizadas.
8. Eliminar una localización borra POI y relaciones sin borrar mapas o capítulos.
9. Eliminar un mapa conserva localizaciones y POI, pero retira las referencias y posiciones que dependían de él.
10. El DM consulta el resultado desde una campaña asociada; jugadores y usuarios ajenos reciben `403`.
11. Las pruebas web, API, PostgreSQL y autorización cubren interacción visual, alternativa por teclado y efectos de borrado.
12. Eliminar un módulo con localizaciones elimina también todos sus POI, placements y relaciones con capítulos, sin registros huérfanos.

## Fuera de alcance

- Jerarquías padre-hijo entre localizaciones, rutas o navegación automática.
- Cuadrículas, distancia, coste de terreno, tiempo de viaje o pathfinding.
- POI compartidos entre localizaciones o visibles para jugadores.
- Tokens, movimiento, capas, fog of war o edición de mapas.
