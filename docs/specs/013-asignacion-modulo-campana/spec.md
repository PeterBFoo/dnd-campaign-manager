# Spec 013: Asignación de un módulo a una campaña

- Estado: Aceptada; implementación en curso
- Fecha: 2026-08-29
- Tipo: incremento funcional vertical
- Roadmap: [roadmap funcional](../../roadmap/product-roadmap.md)
- Requisitos principales: RF-010, RF-011, RF-014, RF-015 y RF-017
- Dependencias: [spec 004](../004-creacion-campanas/spec.md) y [spec 012](../012-libreria-modulos/spec.md)

## Problema y objetivo

Campaigns conserva una referencia opcional a `AdventureModule`, pero no existe un recorrido para validar, asociar, cambiar o retirar esa referencia. Este incremento permite seleccionar un módulo opcional al crear una campaña y administrarlo posteriormente sin alterar la identidad ni el resto del estado de la campaña.

## Actores y alcance

- Cualquier usuario autenticado puede consultar opciones minimizadas y crear una campaña con o sin módulo.
- Solo el DM puede asociar, cambiar o retirar el módulo de su campaña.
- Los jugadores aceptados ven únicamente `Id`, `Name` y `CoverUrl` del módulo asociado.
- El administrador conserva las operaciones de catálogo; borrar un módulo desasocia sus campañas mediante persistencia.
- Un módulo puede compartirse entre campañas independientes; borrar una campaña nunca borra el módulo.

Quedan fuera de alcance el progreso, capítulos, recursos, NPC, historial de módulos y edición del contenido del catálogo.

## Reglas y consistencia

- El módulo seleccionado debe existir al confirmar la escritura; un identificador inexistente no se persiste.
- Asignar el mismo módulo y retirar una asociación inexistente son operaciones idempotentes.
- Un cambio real incrementa `version`; una versión esperada obsoleta devuelve `409`.
- La FK entre esquemas usa `ON DELETE SET NULL`, preservando las campañas al eliminar un módulo.
- Las respuestas mantienen temporalmente `adventureModuleId` y añaden el resumen opcional `adventureModule` y `version`.

## Contrato HTTP

- `GET /api/v1/adventure-modules/options`: opciones minimizadas para usuarios autenticados.
- `POST /api/v1/campaigns`: acepta `adventureModuleId` nullable.
- `PUT /api/v1/campaigns/{campaignId}/adventure-module`: asigna o cambia con `adventureModuleId` y `expectedVersion`.
- `DELETE /api/v1/campaigns/{campaignId}/adventure-module`: retira con `expectedVersion`.
- `GET /api/v1/campaigns/{campaignId}` y el listado proyectan el resumen seguro.

Los errores respetan los contratos existentes (`401`, `403`, `404`, `409` y `ProblemDetails`). No se exponen entidades, `DbContext`, repositorios ni contenido editorial del catálogo.

## Ownership técnico

- `apps/api/Modules/Campaigns` conserva la referencia, la autorización DM, los comandos y la concurrencia.
- `apps/api/Modules/AdventureCatalog` conserva existencia y metadatos, y publica el contrato mínimo de lectura.
- `apps/web/src/app/modules/campaigns` posee el selector y la administración; `adventure-catalog` publica solo cliente y contratos mínimos.

## Criterios de aceptación

1. Se puede crear una campaña sin módulo o con un módulo existente y recibir su resumen.
2. El DM puede asociar, sustituir y retirar el módulo sin alterar identidad ni estado ajeno.
3. Jugadores ven el resumen, pero reciben `403` al modificar la asociación; usuarios ajenos no acceden.
4. Dos campañas pueden compartir el mismo módulo sin compartir estado.
5. Eliminar un módulo utilizado deja todas sus campañas como `Sin módulo`; eliminar una campaña no afecta al catálogo.
6. Versiones obsoletas devuelven `409` y las escrituras concurrentes no dejan referencias colgantes.
7. Las pruebas de dominio, API, persistencia PostgreSQL, contratos, arquitectura y web cubren el recorrido.

## Observabilidad

Se miden carga de opciones, creación con módulo, asociación, cambio, retirada y desasociación por borrado, sin usar identificadores ni nombres como etiquetas.
