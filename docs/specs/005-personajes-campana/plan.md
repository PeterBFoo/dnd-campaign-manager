# Plan 005: Personajes de campaña y personaje activo

- Estado: Ejecutado
- Especificación: [spec.md](spec.md)
- Validación: la petición de implementación del usuario acepta el alcance funcional descrito el 2026-08-23

## Resultado esperado

El jugador aceptado podrá listar el elenco de su campaña, crear, editar y eliminar varios personajes propios con nombre, CA, iniciativa e imagen opcional, y cambiar cuál está activo. El DM podrá crear personajes vinculados a un jugador aceptado o sin propietario y administrar todo el elenco, preparado para la futura integración con combates.

## Diseño

1. Publicar contratos mínimos que resuelvan acceso efectivo de campaña y jugadores aceptados con identidad minimizada.
2. Crear el módulo API `Characters`, con agregado y esquema `characters` independientes y un puerto de almacenamiento de objetos.
3. Proteger la unicidad activa mediante índice parcial `(CampaignId, OwnerUserId) WHERE IsActive` y efectuar los cambios de activo dentro de una transacción.
4. Validar imágenes por tamaño y firma, guardarlas en Azurite/Azure Blob privado y exponerlas mediante lectura autorizada.
5. Exponer listado, alta y edición multipart, eliminación y activación con autorización en Application y `ProblemDetails` en HTTP.
6. Crear el módulo Angular `characters` con rutas lazy, cliente, formulario reactivo, selector de archivo, listado y estados accesibles.
7. Añadir avatar SVG genérico propio, sin contenido editorial de aventuras.
8. Aprovisionar el contenedor privado y la identidad administrada en Azure; usar Azurite en Compose.
9. Verificar límites modulares, dominio, handlers, contratos HTTP, migración, componentes y builds.

## Verificación

- Tests .NET de dominio y aplicación.
- Tests de componente HTTP con PostgreSQL real dentro del recorrido Docker existente.
- Tests Angular de cliente, formulario, listado, activación, rutas y límites modulares.
- Compilación de la solución y build de producción Angular.

## Evidencia de cierre

- `docker compose run --build --rm api-tests`: 52 tests correctos sobre PostgreSQL y Azurite, sin omisiones.
- `pnpm --filter @dnd/web test --watch=false`: 49 tests correctos en 21 archivos.
- `pnpm --filter @dnd/web build`: build Angular de producción correcto con rutas lazy de Characters.
- `docker compose build api web`: ambas imágenes finales construidas.
- `docker compose config --quiet` y variante deploy: configuración válida.
- `terraform -chdir=infra/azure validate`: infraestructura válida con Storage, contenedor privado, identidad administrada y RBAC.
