# Spec 018: Catálogo de NPC del módulo y consulta del DM

- Estado: Propuesta
- Fecha: 2026-08-29
- Tipo: incremento funcional vertical
- Roadmap: [roadmap funcional](../../roadmap/product-roadmap.md)
- Requisitos principales: RF-013, RF-015, RF-017, RF-021 y RF-023
- Requisito relacionado pendiente: RF-025, hasta que exista un modelo de estadísticas de NPC
- Dependencias: [spec 014](../014-capitulos-modulo/spec.md) y [spec 016](../016-localizaciones-puntos-interes/spec.md)

## Problema

El módulo no puede representar NPC reutilizables ni relacionar una misma identidad con varios capítulos y localizaciones. Crear copias por aparición haría divergir nombre, imagen y descripción; exponer un único DTO también correría el riesgo de entregar notas de dirección a los jugadores.

## Objetivo

Permitir que el administrador cree, edite, busque y elimine NPC dentro de un módulo, administre sus relaciones y que el DM consulte el catálogo completo desde su campaña. La visibilidad para jugadores se incorpora separadamente en el spec 019.

## Modelo funcional

- Un NPC pertenece exactamente a un módulo.
- Tiene nombre, descripción pública, imagen opcional y notas privadas para el DM.
- Puede relacionarse con varios capítulos y localizaciones del mismo módulo sin duplicarse.
- No contiene `CampaignId`, estado vivo/muerto, descubrimiento, reputación ni estadísticas completas.
- Sus campos públicos se preparan para una proyección de jugador futura; las notas del DM nunca forman parte de ella.

## Actores

- **Administrador de plataforma:** CRUD, imagen, procedencia y relaciones.
- **DM:** lista, busca y consulta todos los NPC del módulo asociado desde su campaña.
- **Jugador:** no accede todavía a este catálogo.
- **Usuario ajeno:** sin acceso.

## Reglas y validación

- Nombre obligatorio, normalizado, entre 2 y 120 caracteres.
- Descripción pública opcional en texto plano, hasta 10.000 caracteres.
- Notas de DM opcionales en texto plano, hasta 20.000 caracteres.
- Imagen opcional JPEG, PNG o WebP de hasta 10 MiB, validada por firma y dimensiones seguras; sin SVG ni URL externas.
- Textos e imagen conservan procedencia y fundamento de uso verificables.
- Solo se relacionan NPC, capítulos y localizaciones del mismo módulo.
- Cada asociación es única por par e idempotente.
- Editar un NPC actualiza inmediatamente todas sus apariciones y campañas asociadas.
- Eliminarlo borra imagen y relaciones, pero no capítulos ni localizaciones. El spec 019 amplía el borrado para limpiar estados de visibilidad.
- La búsqueda DM y administrativa es paginada, acotada, no distingue mayúsculas ni acentos y busca inicialmente por nombre.

## Recorrido web

- `/admin/adventure-modules/:moduleId/npcs` ofrece listado, búsqueda, alta y edición.
- El detalle administra imagen, campos públicos, notas privadas, capítulos y localizaciones mediante selección.
- `/campaigns/:campaignId/library/npcs` ofrece al DM búsqueda y detalle completo del módulo activo.
- La campaña sin módulo presenta estado vacío; jugadores no reciben la ruta de detalle DM.

## Contrato HTTP funcional

- `GET|POST /api/v1/admin/adventure-modules/{moduleId}/npcs`
- `GET|PUT|DELETE /api/v1/admin/adventure-modules/{moduleId}/npcs/{npcId}`
- `PUT|DELETE /api/v1/admin/adventure-modules/{moduleId}/npcs/{npcId}/image`
- `PUT|DELETE /api/v1/admin/adventure-modules/{moduleId}/npcs/{npcId}/chapters/{chapterId}`
- `PUT|DELETE /api/v1/admin/adventure-modules/{moduleId}/npcs/{npcId}/locations/{locationId}`
- `GET /api/v1/campaigns/{campaignId}/library/npcs`
- `GET /api/v1/campaigns/{campaignId}/library/npcs/{npcId}`
- `GET /api/v1/campaigns/{campaignId}/library/npcs/{npcId}/image`

Los DTO administrativos y DM pueden incluir notas privadas; el contrato del spec 019 será un DTO público distinto y no una serialización condicional accidental del mismo objeto.

## Ownership técnico

- `apps/api/Modules/AdventureCatalog` posee NPC base, imágenes, relaciones, búsqueda y proyección DM.
- `apps/web/src/app/modules/adventure-catalog` posee autoría y consulta DM. La ruta de campaña puede presentarse bajo la librería sin trasladar ownership de los datos.

## Persistencia, imágenes y observabilidad

- PostgreSQL conserva metadatos y relaciones únicas; Azure Blob/Azurite privado conserva binarios.
- Cada lectura de imagen se vuelve a autorizar contra campaña, rol y módulo actual.
- Sustitución y borrado de imágenes usan compensación y limpieza idempotente.
- Se miden CRUD, búsqueda, relaciones y lectura de imagen con resultados de cardinalidad acotada, sin términos buscados, textos, nombres, claves o identificadores.

## Criterios de aceptación

1. El administrador crea un NPC con nombre, descripción pública y notas de DM y lo encuentra por nombre.
2. Puede añadir, sustituir y retirar una imagen válida sin alterar la identidad.
3. El mismo NPC se relaciona con varios capítulos y localizaciones sin copias ni asociaciones duplicadas.
4. No puede relacionarse con recursos de otro módulo.
5. El DM consulta y busca todos los NPC del módulo desde su campaña, incluidas notas privadas.
6. Cambiar o retirar el módulo impide continuar accediendo al catálogo anterior.
7. Un jugador o usuario ajeno no puede listar, buscar, consultar detalle ni leer imágenes.
8. Editar el NPC se refleja en todas sus relaciones y campañas que usan el módulo.
9. Eliminarlo borra relaciones e imagen sin eliminar capítulos o localizaciones.
10. Las pruebas de proyección, búsqueda, almacenamiento, autorización, PostgreSQL, Angular y arquitectura mantienen verdes las suites.

## Fuera de alcance

- Desbloqueo, bloqueo o cualquier estado por campaña.
- Vista de jugador y referencias desde bitácora.
- Vida, muerte, reputación, inventario, estadísticas de combate o iniciativa.
- Propiedad por campaña, variantes o copias personalizadas.
- Importación, fichas completas o bestiario externo.
