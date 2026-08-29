# Operación del catálogo de módulos de aventura

El módulo `AdventureCatalog` registra metadatos en el esquema PostgreSQL `adventure_catalog` y guarda portadas en el contenedor privado `adventure-module-images`. Las portadas se sirven únicamente a administradores mediante la API; no se publican URLs directas de Blob.

## Diagnóstico

Las métricas `adventure_catalog.operations` y `adventure_catalog.operation.duration` incluyen las etiquetas de operación (`list`, `detail`, `create`, `update`, `delete`, `cover_read`) y resultado (`success`, `validation`, `conflict`, `not_found`, `forbidden`, `failure`). No incluyen nombres, textos, fuentes, claves de Blob ni identificadores.

Si una escritura relacional falla después de subir una portada, el handler elimina el objeto provisional. Para detectar residuos históricos, comparar periódicamente las claves bajo `adventure-modules/` con `CoverObjectKey` en PostgreSQL y retirar solo objetos sin referencia mediante una tarea operativa revisada.
