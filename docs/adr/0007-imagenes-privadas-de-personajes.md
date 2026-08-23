# ADR-0007: Imágenes privadas de personajes en almacenamiento de objetos

- Estado: Aceptado
- Fecha: 2026-08-23
- Spec relacionado: [005: personajes de campaña](../specs/005-personajes-campana/spec.md)

## Contexto

Los usuarios deben subir retratos de personaje. Guardar binarios en PostgreSQL acoplaría las copias de seguridad relacionales al volumen de imágenes; guardarlos en el filesystem de la Container App perdería datos al sustituir una revisión. Una URL pública o elegida por el usuario impediría aplicar el aislamiento de campaña de forma uniforme.

## Decisión

- Characters guarda los binarios en un contenedor privado compatible con Azure Blob y conserva en PostgreSQL únicamente clave de objeto, tipo detectado y tamaño.
- Producción usa Azure Blob Storage Standard LRS. La Container App accede con identidad administrada y `Storage Blob Data Contributor`; no recibe claves de cuenta ni SAS.
- Desarrollo y Compose usan Azurite con volumen persistente; los tests pueden usar Azurite o un doble en memoria a través de `ICharacterImageStore`.
- La API acepta JPEG, PNG y WebP hasta 5 MiB, valida firma binaria y genera claves aleatorias sin datos personales.
- El navegador descarga el retrato mediante un endpoint autenticado. El contenedor no es público y la respuesta aplica `nosniff` y caché privada.
- El alta carga primero el blob y lo elimina de forma compensatoria si falla la persistencia. Una sustitución o eliminación confirma primero PostgreSQL y después retira el blob anterior.
- El avatar por defecto es un SVG propio incluido en el build Angular; los SVG subidos por usuarios no se aceptan.

## Consecuencias

- El despliegue añade una cuenta de almacenamiento con coste variable y retención de borrados de siete días.
- Los metadatos y el binario no forman una transacción distribuida. Las compensaciones cubren fallos ordinarios, pero una interrupción del proceso puede dejar un objeto huérfano; una reconciliación automática queda fuera del incremento.
- Las imágenes no se pueden renderizar con un `<img>` apuntando directamente al blob: Angular las obtiene con `HttpClient` autenticado y crea una URL local temporal.
- La restauración completa necesita coordinar PostgreSQL y el contenedor de blobs.
