# Spec 012: Librería administrable de módulos de aventura

- Estado: Aceptada; implementación iniciada
- Fecha: 2026-08-29
- Tipo: incremento funcional vertical
- Roadmap: [roadmap funcional](../../roadmap/product-roadmap.md)
- Requisitos principales: RF-013, RF-016 y RF-017
- Dependencias: [spec 003](../003-modularizacion-frontend/spec.md) y [spec 004](../004-creacion-campanas/spec.md)

## Problema

Las campañas ya pueden existir sin módulo, pero no hay un catálogo autoritativo de módulos de aventura ni una experiencia segura para crear y mantener sus metadatos. Un módulo debe ser reutilizable, independiente de cualquier campaña y editable sin generar una copia por cada grupo.

## Objetivo

Permitir que un administrador de plataforma cree, liste, consulte, edite y elimine módulos de aventura desde Angular. La API persistirá el catálogo en un módulo funcional propio y protegerá los metadatos, la portada y la procedencia editorial.

## Actores y permisos

- **Administrador de plataforma:** administra todo el catálogo.
- **Usuario registrado sin administración:** no puede usar las operaciones de autoría. La consulta necesaria para seleccionar un módulo se incorpora en el spec 013.
- **Usuario no autenticado:** no puede consultar ni modificar el catálogo.

El rol global de administrador no se convierte en un rol de campaña. Un DM sin administración no obtiene por ello permisos para editar contenido compartido.

## Alcance funcional

- Un módulo nace sin relación con campañas y puede existir sin capítulos ni recursos.
- Sus metadatos iniciales son nombre, descripción opcional y portada opcional.
- Todo módulo guardado está disponible inmediatamente; no existen estados `Draft`, `Published` o `Locked`.
- El administrador puede sustituir o retirar la portada y editar nombre o descripción sin cambiar la identidad.
- Los cambios serán visibles para todos los consumidores futuros del módulo; no se crean copias ni versiones por campaña.
- El listado presenta nombre, portada o sustituto visual, fecha de actualización y acciones de administración, con estados de carga, vacío y error.
- El detalle prepara navegación hacia capítulos, mapas, localizaciones y NPC, pero no crea todavía esos recursos.
- El borrado es definitivo y elimina metadatos, portada y, cuando existan, contenido dependiente. El spec 013 añade la desasociación de campañas.

## Reglas y validación

- Nombre obligatorio, normalizado y de 3 a 120 caracteres.
- El nombre es único en el catálogo sin distinguir mayúsculas, minúsculas ni espacios exteriores.
- Descripción opcional, en texto plano, con un máximo de 5.000 caracteres.
- Portada JPEG, PNG o WebP de hasta 10 MiB, validada por tamaño y firma binaria; no se aceptan SVG ni URL externas.
- Cada alta y edición conserva `createdAt`, `updatedAt` y el identificador del actor que realizó la última modificación, sin exponerlo en contratos de consumo.
- Los textos y archivos editoriales conservan un registro de procedencia: tipo de origen, referencia cuando exista, fundamento de uso, atribución requerida y fecha de verificación. El contenido original puede declarar origen `Original` sin una fuente externa.
- Un identificador inexistente produce `404`; falta de sesión `401`; falta de administración `403`; validación `400`; nombre duplicado o escritura concurrente `409`.

## Recorrido web

- La ruta administrativa `/admin/adventure-modules` lista el catálogo y permite crear un módulo.
- `/admin/adventure-modules/:moduleId` muestra metadatos, procedencia, portada y accesos a la administración futura del contenido.
- El formulario permite editar, sustituir o retirar portada.
- El borrado exige confirmación explícita y explica que es definitivo. Cuando el spec 013 esté disponible, mostrará además que las campañas asociadas quedarán sin módulo.

## Contrato HTTP funcional

- `GET /api/v1/admin/adventure-modules`
- `POST /api/v1/admin/adventure-modules`
- `GET /api/v1/admin/adventure-modules/{moduleId}`
- `PUT /api/v1/admin/adventure-modules/{moduleId}`
- `DELETE /api/v1/admin/adventure-modules/{moduleId}`
- `GET /api/v1/admin/adventure-modules/{moduleId}/cover`

Alta y edición admiten `multipart/form-data` para metadatos, procedencia y portada. La representación incluye identificador, nombre, descripción, `coverUrl`, procedencia, `createdAt`, `updatedAt` y versión de concurrencia. El plan podrá separar la operación de portada sin reducir el comportamiento.

## Ownership técnico

- `apps/api`: un nuevo módulo `AdventureCatalog` posee módulos, metadatos editoriales, procedencia, almacenamiento de portada, persistencia, endpoints y observabilidad.
- `apps/web`: un nuevo módulo `adventure-catalog` posee rutas administrativas, cliente, contratos, formularios y navegación de autoría.

`AdventureCatalog` no contiene entidades, tablas ni colecciones de campañas. `Campaigns.AdventureModuleId` continúa perteneciendo a Campaigns.

## Persistencia e imágenes

- AdventureCatalog utiliza esquema, `DbContext` y migraciones propios.
- PostgreSQL guarda solo metadatos del archivo. El binario vive en Azure Blob/Azurite privado bajo claves no predecibles que no contienen el nombre del módulo.
- La API entrega la portada tras autorizar al actor y añade cabeceras de tipo seguro, `nosniff` y caché privada.
- Una escritura de blob seguida de fallo relacional intenta compensarse; una limpieza idempotente detecta huérfanos.
- La eliminación usa versión esperada y no puede borrar silenciosamente una edición concurrente.

## Observabilidad y seguridad

Se medirán listado, alta, detalle, edición, carga de portada y eliminación por resultado y duración. Logs, trazas, métricas y errores no contendrán nombres, descripciones, fuentes, claves de blob ni identificadores de usuario o módulo.

## Criterios de aceptación

1. Un administrador crea un módulo con nombre y sin portada, recibe `201` y lo ve en el catálogo.
2. Puede editar nombre y descripción, y la identidad y fecha de creación permanecen estables.
3. Puede subir, sustituir y retirar una portada válida; archivos de tipo, firma o tamaño inválidos se rechazan sin cambios parciales.
4. Un nombre equivalente a otro existente produce conflicto.
5. Un usuario autenticado sin administración y una petición anónima no pueden usar la API ni las rutas de autoría.
6. Un módulo puede permanecer vacío y aun así consultarse y editarse.
7. Eliminar un módulo borra sus metadatos y portada; una petición posterior devuelve `404`.
8. Una edición o eliminación con versión obsoleta devuelve conflicto y conserva la versión vigente.
9. Cada texto y portada conservan procedencia verificable sin exponer datos internos de auditoría al consumidor.
10. Pruebas API, PostgreSQL, almacenamiento, autorización, arquitectura y Angular cubren el recorrido y mantienen verdes las suites existentes.

## Fuera de alcance

- Asociación con campañas, selección durante la creación de campaña y consulta desde campaña.
- Capítulos, mapas, localizaciones, NPC o cuadrículas.
- Propiedad del módulo por usuarios, coautoría o permisos configurables.
- Versionado editorial, publicación, marketplace, importación o exportación.
- Moderación automática, transformación de imágenes o recuperación de módulos borrados.
