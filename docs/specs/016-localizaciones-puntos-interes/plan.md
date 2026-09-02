# Plan 016: Localizaciones y puntos de interés sobre mapas

- Estado: Implementado; verificaciones de infraestructura pendientes
- Fecha: 2026-09-02
- Especificación: [spec.md](spec.md)
- Dependencias: [spec 014](../014-capitulos-modulo/spec.md) y [spec 015](../015-mapas-modulo/spec.md)
- Decisión arquitectónica reutilizada: [ADR-0011](../../adr/0011-lectura-de-contenido-de-campana-sin-ciclos-modulares.md)

## Estrategia

1. Incorporar `Location` como agregado versionado de AdventureCatalog y modelar sus POI, asociaciones con capítulos y placements como entidades dependientes sin estado de campaña.
2. Persistir la pertenencia al módulo en todas las relaciones mediante claves y FKs compuestas, con coordenadas normalizadas y unicidad protegidas por PostgreSQL.
3. Publicar CRUD y relaciones administrativas con concurrencia optimista, operaciones idempotentes y transacciones para los cambios que invalidan posiciones.
4. Extender el borrado de mapas para conservar localizaciones y POI, retirando atómicamente placements, referencias de mapa detallado y coordenadas dependientes.
5. Proyectar colección y detalle de solo lectura para el DM mediante el contexto de campaña existente, sin exponer recursos a jugadores ni datos administrativos.
6. Entregar autoría Angular con selectores visuales y alternativa numérica/teclado, además de consulta DM con imágenes y marcadores privados.
7. Verificar dominio, HTTP, autorización, PostgreSQL, contratos Angular, interacción accesible, límites modulares y efectos de borrado antes de cerrar trazabilidad.

## Backend, dominio y persistencia

- `Location` conserva identificador, módulo, nombre, descripción, mapa detallado opcional, auditoría y versión. Es la frontera de concurrencia para cambios de texto, POI, capítulos, mapa detallado y placements.
- `PointOfInterest` pertenece a una localización, conserva nombre, descripción y coordenadas opcionales. Las coordenadas se expresan como pareja completa: ambas nulas o ambas dentro de `[0,1]`.
- `LocationPlacement` conserva `ModuleId`, mapa, localización y su pareja de coordenadas. El par mapa-localización es único y un `PUT` repetido actualiza la posición existente.
- `LocationChapter` conserva la asociación muchos-a-muchos sin duplicar la localización.
- La configuración EF Core y la migración añaden tablas de localizaciones, POI, placements y asociaciones con capítulos; claves únicas y FKs compuestas con `ModuleId` impiden relacionar recursos de módulos distintos.
- La FK `Location.ModuleId → AdventureModule.Id` usa borrado en cascada. Los POI, placements y asociaciones con capítulos dependen a su vez de la localización con cascada, de modo que eliminar un módulo limpia todo el subgrafo de localizaciones en la propia base de datos y no depende de que la aplicación cargue sus hijos.
- Las restricciones `CHECK` protegen longitudes relevantes, versiones, parejas de coordenadas y el intervalo cerrado normalizado. La regla que permite posicionar POI solo con mapa detallado se valida en dominio y aplicación dentro de la misma transacción.
- Cambiar o retirar el mapa detallado conserva los POI y limpia sus coordenadas antes de confirmar. Sustituir únicamente el binario de un mapa no modifica ninguna coordenada.
- Eliminar una localización usa cascada para POI, placements y asociaciones, pero nunca para mapas o capítulos.
- El repositorio de mapas incorpora una operación transaccional de borrado que bloquea o valida la versión vigente, elimina placements, pone a nulo las referencias de detalle y las coordenadas de sus POI, y finalmente elimina el mapa. La eliminación del blob continúa como compensación posterior a la confirmación de datos.
- Las lecturas se proyectan sin tracking y evitan cargas por elemento: colección, capítulos, mapas, placements y POI se resuelven con consultas acotadas.

## API, autorización y contratos

- Se implementan los endpoints administrativos y de campaña definidos en la spec; los `PUT`/`DELETE` mutables reciben la versión esperada de la localización y devuelven `409` ante carreras.
- Las operaciones de asociación son idempotentes: añadir un capítulo existente o repetir un placement con las mismas coordenadas no crea duplicados; un placement existente con otras coordenadas se actualiza.
- Las validaciones diferencian `400` para forma o coordenadas inválidas, `403` para falta de rol, `404` para recursos ausentes o fuera del módulo y `409` para concurrencia.
- La lectura de campaña reutiliza `ICampaignAdventureContext`: resuelve siempre en servidor la campaña, el rol DM y su módulo actual. No se confía en identificadores de módulo enviados por el cliente.
- La colección DM incluye los datos necesarios para localizar recursos y el detalle incluye capítulos, mapa detallado, POI y placements con URLs de imagen bajo rutas de campaña. Procedencia, auditoría y versiones de autoría no se exponen al DM.
- Se amplía la proyección de mapas de campaña solo si es necesario para mostrar sus markers; no se crea ownership nuevo en Campaigns ni una dependencia inversa.

## Web

- Se añade un cliente tipado de localizaciones y rutas lazy para `/admin/adventure-modules/:moduleId/locations` y `/campaigns/:campaignId/adventure/locations`.
- La vista administrativa ofrece colección, alta, edición y eliminación, y enlaza desde el detalle del módulo.
- El editor de detalle permite seleccionar o retirar el mapa detallado, asociar capítulos, mantener POI y colocar la localización en uno o varios mapas.
- Un selector reutilizable representa coordenadas sobre la imagen con puntero y teclado. Los campos numéricos de `x` e `y`, con límites y paso explícitos, permiten completar todas las operaciones sin puntero.
- Los POI sin coordenadas se muestran como pendientes de recolocación; cambiar el mapa detallado actualiza inmediatamente ese estado local tras la respuesta del servidor.
- La vista de campaña es de solo lectura, solo se enlaza para el DM y muestra estados de carga, vacío, mapa sin imagen, POI sin posición, error y pérdida de autorización.
- Las imágenes se obtienen por los endpoints privados existentes; no se incorporan URLs públicas ni contenido editorial concreto en fixtures.
- Las pantallas reutilizan la UX consolidada en `main`: tokens oscuros compartidos, tipografía, paneles, botones, estados vacíos y layout de mapas; los estilos propios solo añaden el selector y los markers.

## Seguridad, observabilidad y despliegue

- Todos los endpoints exigen autenticación; la API aplica `platform-admin` para autoría y rol DM para lectura. Los guards y enlaces Angular son únicamente una ayuda de navegación.
- Las métricas de AdventureCatalog añaden operaciones de localización, POI y asociaciones con etiquetas acotadas de operación y resultado. No incluyen nombres, descripciones, coordenadas, IDs ni URLs.
- La migración se aplica después de `AdventureMaps`; su rollback elimina únicamente las cuatro estructuras nuevas y no altera mapas, capítulos, campañas ni blobs.
- No se necesita un ADR nuevo: el ownership permanece en AdventureCatalog y la lectura desde campaña sigue la composición establecida por ADR-0011. Cualquier desviación descubierta durante la implementación deberá documentarse antes de introducirla.

## Verificación y cierre

- Pruebas de dominio para normalización, límites de texto, coordenadas, POI sin posición, mapa detallado y semántica idempotente.
- Pruebas de aplicación y HTTP para CRUD, relaciones, concurrencia, pertenencia al módulo, autorización y proyección administrativa/DM.
- Pruebas PostgreSQL reales para FKs compuestas, unicidad, `CHECK`, cascadas y atomicidad al cambiar detalle o borrar mapas/localizaciones.
- Prueba PostgreSQL del borrado administrativo de un módulo poblado, comprobando que no sobreviven localizaciones, POI, placements ni asociaciones con capítulos.
- Pruebas Angular del cliente y componentes para formularios, selector por puntero, teclado, alternativa numérica, estados pendientes y lectura DM.
- Build y suites de API/web, reglas de arquitectura, reconciliación de migraciones, `docker compose config --quiet` y recorrido manual con PostgreSQL y Azurite reales.
- Al completar, actualizar estados y evidencias en spec, tareas, índice y trazabilidad del roadmap sin marcar verificaciones que no se hayan ejecutado.

## Secuencia de entrega

1. Modelo de dominio, configuración y migración.
2. Repositorios, transacciones y modificación segura del borrado de mapas.
3. Servicios, DTO, métricas y controladores administrativos/DM.
4. Cliente, rutas y navegación Angular.
5. Autoría visual y consulta DM.
6. Pruebas integradas, verificación manual, documentación y cierre.
