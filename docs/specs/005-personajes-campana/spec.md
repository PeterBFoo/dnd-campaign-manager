# Spec 005: Personajes de campaña y personaje activo

- Estado: Completada
- Fecha: 2026-08-23
- Requisitos del roadmap: RF-005, RF-007, RF-008, RF-012 y RF-020
- Dependencias: [spec 004](../004-creacion-campanas/spec.md)

## Problema y objetivo

Un jugador aceptado en una campaña necesita crear sus identidades de juego antes de participar. El incremento permitirá crear varios personajes propios en una campaña, consultar el elenco de esa campaña y mantener exactamente uno de los personajes del jugador como activo cuando exista al menos uno.

La selección activa se persistirá por usuario y campaña para que pueda consumirse más adelante desde combate y otras herramientas sin depender del estado local del navegador.

## Alcance funcional

- Un jugador con membresía aceptada puede crear personajes en una campaña.
- El DM puede crear personajes en su campaña y dejarlos sin propietario o vincularlos a un jugador con membresía aceptada.
- Nombre, CA e iniciativa son obligatorios. La imagen es un archivo opcional subido por el usuario; cuando se omite se devuelve el avatar local por defecto `/images/default-character.svg`.
- Un jugador puede crear varios personajes propios en la misma campaña.
- El jugador puede editar nombre, CA, iniciativa e imagen de cualquiera de sus personajes.
- Puede retirar una imagen subida para volver al avatar por defecto.
- El jugador puede eliminar cualquiera de sus personajes.
- El DM puede editar y eliminar cualquier personaje de su campaña, incluidos los vinculados a jugadores.
- El primer personaje del jugador en una campaña queda activo automáticamente.
- Los personajes posteriores nacen inactivos y el jugador puede activar cualquiera de los suyos.
- Activar un personaje desactiva al anterior del mismo usuario y campaña en una única operación transaccional.
- Al eliminar el personaje activo, el personaje restante más antiguo se activa en la misma transacción. Eliminar el último deja al jugador sin personaje activo.
- DM y jugadores aceptados pueden consultar los personajes de la campaña. La respuesta identifica el propietario y cuál está activo, sin exponer datos de cuenta.
- La interfaz del DM obtiene una lista minimizada de jugadores aceptados (`userId`, `displayName`) para asignar el propietario sin introducir identificadores manualmente.
- La web integra listado, formulario de alta y acción de activación dentro del recorrido de la campaña.

## Reglas y validación

- Cada personaje pertenece a una única campaña y puede pertenecer a un único usuario responsable o quedar sin propietario.
- Una membresía `Player` aceptada autoriza crear personajes propios y activar únicamente los propios.
- El DM autoriza alta, edición y eliminación en su campaña, pero no selecciona el personaje activo de un jugador.
- Un `ownerUserId` enviado por el DM debe corresponder a una membresía `Player` aceptada de esa campaña. Un jugador no puede elegir ni cambiar el propietario.
- Los personajes sin propietario nunca están activos. Al vincular el primer personaje de un jugador queda activo; al desvincular el activo se activa el personaje restante más antiguo del jugador.
- Un jugador solo puede activar personajes propios y de la campaña indicada en la ruta.
- Un jugador solo puede editar o eliminar personajes propios y de la campaña indicada en la ruta.
- Nombre: entre 2 y 80 caracteres tras recortar espacios.
- CA: entero entre 0 y 40.
- Iniciativa: entero entre -20 y 30.
- Imagen subida: JPEG, PNG o WebP de hasta 5 MiB. La API comprueba tamaño y firma binaria y no admite SVG aportado por usuarios.
- Toda consulta y escritura vuelve a comprobar campaña, usuario y rol en la API.
- La base de datos impide que existan dos personajes activos para el mismo usuario y campaña.

## Contrato HTTP

- `GET /api/v1/campaigns/{campaignId}/characters`
- `POST /api/v1/campaigns/{campaignId}/characters`
- `PUT /api/v1/campaigns/{campaignId}/characters/{characterId}`
- `PUT /api/v1/campaigns/{campaignId}/characters/{characterId}/active`
- `DELETE /api/v1/campaigns/{campaignId}/characters/{characterId}`
- `GET /api/v1/campaigns/{campaignId}/characters/{characterId}/image`

Alta y edición reciben `multipart/form-data` con `name`, `armorClass`, `initiative`, `image?`, `ownerUserId?` y, solo en edición, `removeImage`. `ownerUserId` solo es efectivo para el DM; la imagen nueva y `removeImage=true` son mutuamente excluyentes. El alta devuelve `201 Created`, la edición y activación `200`, y el borrado `204`. Las representaciones incluyen `id`, `campaignId`, `ownerUserId`, `ownerDisplayName`, `name`, `armorClass`, `initiative`, `imageUrl`, `isActive` y `createdAt`.

Los errores de validación usan `400`; ausencia de autenticación `401`; falta de acceso o ownership `403`; campaña o personaje inexistente `404`; y conflictos de unicidad concurrentes `409`.

## Criterios de aceptación

1. Un jugador aceptado entra en una campaña sin personajes, ve ese estado y puede abrir el formulario de creación.
2. Al crear con nombre, CA e iniciativa y sin imagen, recibe el avatar por defecto y el primer personaje queda activo.
3. Al crear un segundo personaje, ambos permanecen asociados al mismo jugador y campaña, pero solo el primero está activo.
4. Al activar el segundo, la operación devuelve el segundo activo y una consulta posterior muestra el primero inactivo.
5. Otro jugador no puede activar un personaje ajeno ni crear en una campaña sin membresía aceptada.
6. El DM puede consultar el elenco completo, crear personajes vinculados o no y administrarlos, pero no elegir el activo de un jugador.
7. El propietario puede editar estadísticas y sustituir o retirar la imagen sin alterar la identidad ni el estado activo.
8. Al eliminar un personaje se elimina también su blob; si era el activo, se activa el personaje restante más antiguo.
9. Otro jugador no puede editar ni eliminar personajes ajenos; el DM autorizado sí puede administrarlos.
10. Un DM no puede vincular un personaje a una cuenta que no sea jugadora aceptada de esa campaña.
11. La API, la migración PostgreSQL, la interfaz Angular y sus pruebas quedan verdes.

## Ownership técnico

- `apps/api`: el nuevo módulo `Characters` es propietario del agregado, metadatos de imagen, persistencia, endpoints y selección activa. Consume contratos públicos de Campaigns y Access para acceso efectivo y jugadores aceptados; no consulta tablas ajenas.
- `apps/web`: el nuevo módulo `characters` es propietario del cliente, contratos, rutas y páginas. Campaigns solo enlaza por URL a la capacidad.

## Almacenamiento de imágenes

- PostgreSQL solo conserva `ImageObjectKey`, `ImageContentType` y `ImageSizeBytes`; nunca el binario ni una URL externa elegida por el usuario.
- Producción usa un contenedor privado de Azure Blob Storage. La clave no predecible sigue el patrón `characters/{campaignId}/{characterId}/{random}.{extension}` y no contiene nombres de usuario ni personaje.
- La Container App accede con identidad administrada y rol mínimo `Storage Blob Data Contributor`; no se guarda una clave de cuenta en la aplicación.
- Desarrollo y tests usan Azurite mediante el mismo puerto de almacenamiento. Los tests unitarios pueden sustituirlo por memoria.
- El navegador nunca recibe acceso directo al contenedor. `GET .../characters/{characterId}/image` vuelve a autorizar la campaña y transmite el blob con su tipo seguro, `nosniff` y caché privada.
- Si el personaje no tiene imagen, `imageUrl` apunta al SVG genérico versionado con Angular. El SVG no procede de una subida.
- Si el blob se escribe pero falla la transacción de metadatos, la API intenta eliminarlo de forma compensatoria. Las copias de seguridad y retención del contenedor se documentan como operación de producción.

## Observabilidad y privacidad

Se medirán creación, listado, activación, carga y lectura de imagen con resultados de cardinalidad acotada. No se incluirán nombres, claves de objeto, identificadores de personaje, usuario o campaña en etiquetas ni mensajes de error.

## Fuera de alcance

- Recorte, edición, moderación automática o transformación de imágenes.
- Archivado o recuperación de personajes eliminados.
- Fichas completas, clases, atributos, inventario, puntos de golpe o estadísticas adicionales.
- Selección global entre campañas o expiración por sesión.
- Integración con combate, bitácora, misiones o NPC.

## Validación

El alcance inicial y sus ampliaciones —subida privada de imágenes, edición/eliminación y administración de personajes vinculados o no por el DM— fueron solicitados por el usuario el 2026-08-23. La implementación quedó verificada con 52 tests .NET en Docker sobre PostgreSQL y Azurite reales, 49 tests Angular, build de producción web, construcción de imágenes API/web, validación de Compose y validación Terraform.
