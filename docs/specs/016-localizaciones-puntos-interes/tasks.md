# Tareas 016: Localizaciones y puntos de interés sobre mapas

- Estado: Implementada; PostgreSQL/Azurite y recorrido manual pendientes
- Especificación: [spec.md](spec.md)
- Plan: [plan.md](plan.md)

## Preparación

- [x] Revisar roadmap, flujo SDD, specs 014 y 015, ADR-0011 y código existente de AdventureCatalog.
- [x] Aceptar el alcance vertical y documentar el diseño de dominio, datos, API, web y verificación.
- [x] Confirmar que el árbol de trabajo no contiene cambios solapados antes de iniciar la implementación.

## Dominio y persistencia

- [x] Implementar el agregado versionado `Location`, sus validaciones de texto y la selección opcional de mapa detallado.
- [x] Implementar `PointOfInterest` con posición opcional y limpieza de coordenadas al cambiar o retirar el mapa detallado.
- [x] Implementar `LocationPlacement` y `LocationChapter` con semántica idempotente y pertenencia al módulo.
- [x] Añadir `DbSet`, configuración EF Core, índices únicos, restricciones `CHECK`, cascadas y FKs compuestas por `ModuleId`, incluida la cascada completa desde `AdventureModule` hasta localizaciones y todos sus dependientes.
- [x] Generar la migración `AdventureLocations` y revisar `Up`, `Down`, designer y snapshot sin modificar migraciones ya aplicadas.
- [x] Implementar el repositorio de localizaciones con lecturas acotadas, concurrencia optimista y transacciones para mutaciones dependientes.
- [x] Ampliar el repositorio/servicio de mapas con borrado transaccional que conserve localizaciones y POI, retire referencias y limpie posiciones.
- [x] Registrar repositorio y servicio de localizaciones en la composición de AdventureCatalog.

## API y observabilidad

- [x] Definir DTO y comandos administrativos para colección, detalle, mapa detallado, POI, capítulos y placements.
- [x] Implementar colección, detalle, alta, edición y eliminación administrativos con `400`, `403`, `404` y `409` coherentes.
- [x] Implementar `PUT|DELETE` de mapa detallado y verificar la recolocación pendiente de POI.
- [x] Implementar `POST|PUT|DELETE` de POI con posición opcional y versión esperada.
- [x] Implementar `PUT|DELETE` idempotentes de asociaciones con capítulos y placements sobre mapas.
- [x] Implementar colección y detalle DM resolviendo campaña, rol y módulo mediante `ICampaignAdventureContext`.
- [x] Incluir URLs privadas y markers necesarios en la proyección DM sin filtrar procedencia, auditoría ni contratos administrativos.
- [x] Añadir métricas acotadas para la creación y comprobar que logs y métricas no incluyen contenido ni coordenadas.

## Web

- [x] Crear contratos y cliente Angular tipados para todos los endpoints administrativos y DM de localizaciones.
- [x] Añadir rutas lazy, exports públicos y enlaces desde el detalle del módulo y la navegación de campaña para DM.
- [x] Implementar colección administrativa con estados de carga, vacío, error, conflicto, alta, edición y eliminación respetando los patrones UX de `main`.
- [x] Implementar el editor de detalle para mapa detallado, capítulos, POI y placements.
- [x] Implementar un selector de coordenadas reutilizable sobre imágenes con puntero y marcadores normalizados.
- [x] Añadir interacción por teclado y campos numéricos accesibles para `x` e `y`, sin exigir el uso de puntero.
- [x] Mostrar POI sin posición como pendientes de recolocación y conservar ese estado tras sustituir o retirar el mapa detallado.
- [x] Implementar colección y detalle DM de solo lectura con mapas, markers, POI y estados degradados.
- [x] Comprobar que imágenes y navegación respetan autenticación, rol DM y pérdida de autorización.

## Pruebas backend

- [x] Cubrir dominio: longitudes, normalización, intervalo `[0,1]`, parejas de coordenadas, mapa detallado y versiones.
- [ ] Cubrir servicios: CRUD, idempotencia, recursos de otro módulo, concurrencia y proyecciones admin/DM.
- [ ] Cubrir autorización HTTP para administrador, DM, jugador, usuario ajeno y no autenticado.
- [ ] Cubrir contratos HTTP y estados `200`, `201`, `204`, `400`, `401`, `403`, `404` y `409`.
- [ ] Cubrir en PostgreSQL FKs compuestas, unicidad de pares, restricciones de coordenadas y cascada al borrar localización (prueba preparada; ejecución omitida sin base).
- [ ] Verificar en PostgreSQL que borrar un módulo poblado elimina sus localizaciones, POI, placements y asociaciones con capítulos sin dejar filas huérfanas (prueba preparada; ejecución omitida sin base).
- [ ] Verificar en PostgreSQL que cambiar el mapa detallado conserva POI y limpia sus posiciones en una transacción.
- [ ] Verificar en PostgreSQL que borrar un mapa conserva localizaciones/POI, elimina placements y limpia detalle/coordenadas atómicamente.
- [ ] Verificar que sustituir el binario de un mapa conserva placements y posiciones normalizadas.
- [x] Añadir prueba de migración y reconciliación con el historial vigente de AdventureCatalog.

## Pruebas web y accesibilidad

- [x] Cubrir el cliente Angular y la serialización de versiones, coordenadas y asociaciones.
- [ ] Cubrir formularios de localización y POI, validación, errores y conflictos recuperables.
- [ ] Cubrir selector visual con puntero, teclado y entrada numérica equivalente.
- [ ] Cubrir placements independientes de una localización en dos mapas y actualización sin duplicados.
- [ ] Cubrir el estado pendiente de POI después de cambiar o retirar el mapa detallado.
- [x] Cubrir rutas, navegación reservada y vistas DM de colección/detalle.

## Verificación y cierre

- [x] Ejecutar formato, build y suite .NET completa sin errores ni advertencias nuevas (22 pruebas: 0 fallos, 4 omitidas por servicios).
- [x] Ejecutar suite y build de producción Angular (80 pruebas correctas; build correcto).
- [ ] Ejecutar pruebas PostgreSQL y Azurite reales; documentar cualquier omisión causada por el entorno.
- [ ] Ejecutar reglas de arquitectura y comprobar que Campaigns no adquiere ownership de localizaciones (no ejecutado como suite independiente; no hay cambios en Campaigns API).
- [ ] Validar `docker compose config --quiet` y el arranque saludable de la composición.
- [ ] Verificar manualmente el recorrido administrativo y DM, incluida la operación completa por teclado.
- [x] Ejecutar `git diff --check` y revisar que no haya contenido editorial concreto, secretos ni cambios ajenos.
- [x] Actualizar estado y evidencias en spec, plan, tareas, `docs/specs/README.md` y roadmap.

## Evidencias

- `dotnet build apps/api/Modules/AdventureCatalog/DndCampaign.Modules.AdventureCatalog/DndCampaign.Modules.AdventureCatalog.csproj --no-restore`: 0 errores, 0 warnings.
- `dotnet build tests/Modules/AdventureCatalog/DndCampaign.Modules.AdventureCatalog.Tests/DndCampaign.Modules.AdventureCatalog.Tests.csproj --no-restore -m:1`: 0 errores, 0 warnings.
- Runner xUnit directo: 22 pruebas, 0 errores, 0 fallos, 4 omitidas por falta de `IDENTITY_TEST_DATABASE` o Azurite.
- `pnpm --filter web test`: 29 archivos y 80 pruebas correctas.
- `pnpm --filter web build`: bundle de producción correcto, incluida la ruta lazy de localizaciones.
- `git diff --check`: parche sin errores de espacios.
