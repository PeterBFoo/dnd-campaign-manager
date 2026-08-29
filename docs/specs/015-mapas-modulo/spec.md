# Spec 015: Mapas reutilizables de un módulo

- Estado: Propuesta
- Fecha: 2026-08-29
- Tipo: incremento funcional vertical
- Roadmap: [roadmap funcional](../../roadmap/product-roadmap.md)
- Requisitos principales: RF-013, RF-015, RF-017, RF-018, RF-061, RF-062 y RF-063
- Dependencias: [spec 012](../012-libreria-modulos/spec.md), [spec 013](../013-asignacion-modulo-campana/spec.md) y [spec 014](../014-capitulos-modulo/spec.md)

## Problema

Los capítulos no disponen de mapas reutilizables. Guardar una copia por capítulo duplicaría imágenes, procedencia y cambios; además, entregar directamente archivos públicos impediría aplicar la visibilidad reservada al DM.

## Objetivo

Permitir que el administrador mantenga una colección única de mapas por módulo, cargue y sustituya su imagen y los asocie con uno o varios capítulos. El DM podrá consultarlos desde una campaña autorizada sin exponerlos a jugadores.

## Actores

- **Administrador de plataforma:** CRUD de mapas, imágenes, procedencia y asociaciones con capítulos.
- **DM:** lectura de los mapas del módulo activo desde su campaña.
- **Jugador y usuario ajeno:** sin acceso a metadatos ni binarios.

## Alcance funcional

- Un mapa pertenece exactamente a un módulo y tiene nombre, descripción opcional e imagen opcional.
- Puede existir antes de cargar la imagen, pero no admite posicionamiento ni cuadrícula hasta tenerla.
- Un mapa puede asociarse a varios capítulos del mismo módulo y un capítulo a varios mapas.
- Asociar de nuevo el mismo par no crea duplicados; retirar la relación no elimina ninguno de los dos recursos.
- Sustituir la imagen conserva la identidad y asociaciones. Las posiciones normalizadas que añada el spec 016 se conservarán.
- Eliminar un mapa borra imagen, asociaciones con capítulos y configuración dependiente; los capítulos permanecen.
- La vista DM ofrece colección y detalle desde la campaña, con la relación de capítulos como navegación contextual.

## Reglas y validación

- Nombre obligatorio, normalizado, entre 2 y 120 caracteres.
- Descripción opcional en texto plano, hasta 10.000 caracteres.
- JPEG, PNG o WebP de hasta 20 MiB y 50 megapíxeles, validados por firma y dimensiones; no se admiten SVG ni URL externas.
- Cada imagen conserva procedencia, fundamento de uso y atribución. Una sustitución crea su propio registro y no hereda silenciosamente el anterior.
- Solo pueden relacionarse mapa y capítulo pertenecientes al mismo módulo.
- Un mapa no contiene `ChapterId`; la asociación explícita es muchos a muchos y única por par.
- Toda escritura usa versión esperada. El contenido actualizado se proyecta inmediatamente a todas las campañas que usan el módulo.

## Recorrido web

- `/admin/adventure-modules/:moduleId/maps` ofrece listado, alta y estado sin imagen.
- El detalle permite editar, cargar/sustituir/retirar imagen y administrar capítulos mediante selección, no identificadores manuales.
- `/campaigns/:campaignId/adventure/maps` ofrece al DM colección y detalle de solo lectura.
- La interfaz no descarga la imagen hasta que se abre el recurso autorizado y representa explícitamente errores de carga.

## Contrato HTTP funcional

- `GET|POST /api/v1/admin/adventure-modules/{moduleId}/maps`
- `GET|PUT|DELETE /api/v1/admin/adventure-modules/{moduleId}/maps/{mapId}`
- `PUT|DELETE /api/v1/admin/adventure-modules/{moduleId}/maps/{mapId}/image`
- `PUT|DELETE /api/v1/admin/adventure-modules/{moduleId}/maps/{mapId}/chapters/{chapterId}`
- `GET /api/v1/campaigns/{campaignId}/adventure/maps`
- `GET /api/v1/campaigns/{campaignId}/adventure/maps/{mapId}`
- `GET /api/v1/campaigns/{campaignId}/adventure/maps/{mapId}/image`

Los endpoints de imagen vuelven a autorizar en cada lectura y nunca devuelven la clave de almacenamiento.

## Ownership técnico

- `apps/api/Modules/AdventureCatalog` posee mapas, imágenes, relaciones con capítulos, persistencia y proyecciones.
- `apps/web/src/app/modules/adventure-catalog` posee autoría y consulta DM.

No se crea una abstracción genérica `Resource`; mapa y capítulo conservan modelos y reglas explícitos.

## Persistencia, imágenes y seguridad

- Los metadatos viven en PostgreSQL y los binarios en Azure Blob/Azurite privado bajo un prefijo propio de AdventureCatalog.
- La API transmite el archivo con tipo detectado, `nosniff` y caché privada; no emite URL pública ni SAS al navegador.
- Escrituras fallidas compensan blobs nuevos. Eliminaciones y sustituciones usan limpieza idempotente para evitar huérfanos.
- Una restricción única impide relaciones capítulo-mapa duplicadas.
- Observabilidad cubre CRUD, imagen, asociación y lectura DM sin nombres, textos, claves o identificadores en telemetría.

## Criterios de aceptación

1. El administrador crea un mapa sin imagen y lo consulta en la colección del módulo.
2. Carga una imagen válida y el DM puede verla únicamente desde una campaña con ese módulo.
3. Sustituir o retirar la imagen conserva el mapa y sus asociaciones.
4. Un archivo inválido se rechaza sin perder la imagen vigente.
5. Un mapa se asocia a dos capítulos sin duplicar el binario; repetir una relación es idempotente.
6. No puede asociarse un capítulo de otro módulo.
7. Retirar una asociación conserva mapa y capítulo.
8. Eliminar el mapa elimina su blob y relaciones, pero no sus capítulos.
9. Un jugador, usuario ajeno o campaña con otro módulo no puede leer metadatos ni imagen aunque invoque directamente la API.
10. Las pruebas de almacenamiento, PostgreSQL, API, autorización, Angular y arquitectura mantienen verdes las suites existentes.

## Fuera de alcance

- Localizaciones, puntos de interés, marcadores, cuadrículas y cálculo de viaje.
- Tokens, movimiento, fog of war, anotaciones, capas o edición de imagen.
- Mapas visibles para jugadores o permisos configurables por mapa.
- Abstracción genérica para recursos del módulo.
