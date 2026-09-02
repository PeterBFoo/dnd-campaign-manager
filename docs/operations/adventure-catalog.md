# Operación del catálogo de módulos de aventura

El módulo `AdventureCatalog` registra metadatos en el esquema PostgreSQL `adventure_catalog` y guarda portadas y mapas en el contenedor privado `adventure-module-images`. Las portadas administrativas y los mapas se sirven mediante la API; los mapas de campaña vuelven a comprobar en cada lectura que el actor sea el DM y que el módulo siga asociado. No se publican URLs directas de Blob.

## Diagnóstico

Las métricas `adventure_catalog.operations` y `adventure_catalog.operation.duration` incluyen etiquetas acotadas de operación y resultado. Cubren catálogo y mapas sin incluir nombres, textos, fuentes, claves de Blob ni identificadores.

Si una escritura relacional falla después de subir una portada o mapa, el handler elimina el objeto provisional. Una sustitución confirma primero los metadatos y después retira el objeto anterior de forma idempotente. Para detectar residuos históricos, comparar periódicamente las claves bajo `adventure-modules/` con `CoverObjectKey` e `ImageObjectKey` en PostgreSQL y retirar solo objetos sin referencia mediante una tarea operativa revisada.

Los mapas aceptan JPEG, PNG o WebP de hasta 20 MiB y 50 megapíxeles. Un `404` al leer un binario con metadatos presentes indica una inconsistencia de almacenamiento; un `403` en la ruta de campaña puede indicar que cambió el DM o el módulo asociado.

## Compatibilidad de migraciones de capítulos y mapas

La migración `20260830230000_AdventureChapters` puede encontrar `adventure_chapters` sin entrada equivalente en `__EFMigrationsHistory` si la base arrancó durante el desarrollo provisional de mapas. En ese caso adopta la tabla sin borrarla, añade las columnas, restricciones e índice que falten y conserva sus filas. No se debe eliminar manualmente la tabla para resolver el error PostgreSQL `42P07`; basta con desplegar esta versión y volver a iniciar la aplicación con migraciones habilitadas.

La migración `20260902111111_AdventureMaps` aplica la misma reconciliación a `adventure_maps` y `adventure_map_chapters`. Esto cubre bases que llegaron a crear las tablas con una versión provisional cuyo identificador no coincide con la migración definitiva. Ambas tablas y sus datos se conservan mientras se completan columnas, restricciones e índices ausentes.
