# Spec 013: Asignación de un módulo a una campaña

- Estado: Propuesta
- Fecha: 2026-08-29
- Tipo: incremento funcional vertical
- Roadmap: [roadmap funcional](../../roadmap/product-roadmap.md)
- Requisitos principales: RF-010, RF-011, RF-014, RF-015 y RF-017
- Dependencias: [spec 004](../004-creacion-campanas/spec.md) y [spec 012](../012-libreria-modulos/spec.md)

## Problema

Campaigns ya conserva `AdventureModuleId` opcional, pero todas las campañas nacen sin módulo y no existe una operación que valide, asocie, cambie o retire esa referencia. Tampoco está resuelto qué ocurre con las campañas cuando un administrador elimina un módulo compartido.

## Objetivo

Permitir que el DM seleccione opcionalmente un módulo al crear una campaña y que posteriormente pueda asociar, cambiar o retirar el módulo sin alterar la identidad ni el resto del estado de la campaña.

## Actores

- **Usuario registrado:** consulta una lista minimizada de módulos porque cualquier cuenta puede crear una campaña y seleccionar uno opcionalmente.
- **DM:** administra la asociación de su campaña.
- **Jugador aceptado:** ve los metadatos básicos del módulo asociado, pero no puede cambiarlo ni consultar su contenido de dirección.
- **Administrador de plataforma:** conserva las operaciones del spec 012; eliminar un módulo desasocia todas las campañas que lo utilicen.
- **Usuario ajeno:** no puede consultar ni modificar la asociación.

## Alcance funcional

- La campaña admite `0..1` módulo; nunca exige uno para existir.
- El formulario de creación ofrece `Sin módulo` y los módulos existentes.
- El DM puede asociar un módulo después, sustituirlo por otro o retirarlo.
- Cambiar la referencia no crea ni copia contenido y no modifica nombre, DM, miembros, personajes, bitácora, misiones o encuentros.
- La campaña muestra identificador, nombre y portada del módulo actual; no solo su identificador técnico.
- Un mismo módulo puede estar asociado simultáneamente con varias campañas independientes.
- Eliminar una campaña nunca afecta al módulo.
- Eliminar un módulo retira todas sus referencias y conserva las campañas como `Sin módulo`.
- No habrá un estado observable en el que una campaña entregue contenido de un módulo ya eliminado.

## Reglas y consistencia

- Solo el DM autoritativo puede cambiar la asociación.
- El módulo seleccionado debe existir en AdventureCatalog en el momento de confirmar la escritura.
- Repetir la asignación del mismo módulo es idempotente.
- Retirar una asociación inexistente es idempotente.
- La respuesta de campaña incluye un resumen opcional `adventureModule`; el campo provisional `adventureModuleId` podrá mantenerse durante una transición contractual.
- Cambiar de módulo se permite porque todavía no hay progreso del módulo. El spec 019 define la limpieza del estado de visibilidad de NPC cuando este aparezca.
- La desasociación provocada por el borrado debe quedar protegida por persistencia, no por una búsqueda previa en el frontend. El plan y un ADR asociado decidirán el mecanismo intermodular, evaluando expresamente una clave foránea entre esquemas con `ON DELETE SET NULL` frente a coordinación idempotente mediante evento.

## Recorrido web

- El alta de campaña incorpora un selector opcional con estado vacío y error de carga independiente.
- El detalle de una campaña DM ofrece `Asignar módulo`, `Cambiar módulo` o `Retirar módulo`.
- El jugador ve el resumen del módulo asociado, sin controles de edición.
- Cambiar o retirar exige confirmación y explica que no elimina el módulo.
- Si el módulo desaparece mientras el formulario está abierto, la interfaz recarga opciones y conserva la campaña sin cambios.

## Contrato HTTP funcional

- `GET /api/v1/adventure-modules/options`: listado minimizado disponible para un usuario autenticado que esté creando o dirigiendo una campaña.
- `POST /api/v1/campaigns`: amplía la creación con `adventureModuleId?`.
- `PUT /api/v1/campaigns/{campaignId}/adventure-module`: asigna o cambia mediante `adventureModuleId`.
- `DELETE /api/v1/campaigns/{campaignId}/adventure-module`: retira la referencia.
- `GET /api/v1/campaigns/{campaignId}`: devuelve el resumen opcional del módulo.

La lista de opciones contiene solo identificador, nombre y portada; no expone descripción editorial, capítulos ni recursos. Los errores usan `401`, `403`, `404`, `409` y `ProblemDetails` de acuerdo con los contratos existentes.

## Ownership técnico

- `apps/api/Modules/Campaigns` conserva la referencia, los comandos de asignación y la autorización del DM. Consume un contrato mínimo de AdventureCatalog para validar y proyectar módulos.
- `apps/api/Modules/AdventureCatalog` conserva existencia y metadatos, sin guardar `CampaignId` ni consultar tablas de Campaigns.
- `apps/web/src/app/modules/campaigns` posee el selector y la administración desde campaña; `adventure-catalog` publica únicamente el cliente o contratos mínimos necesarios.

La colaboración no expondrá entidades, `DbContext`, repositorios o consultas internas. El ADR evitará ciclos de ensamblados y definirá la garantía de borrado transversal.

## Observabilidad

Se medirán carga de opciones, creación con módulo, asociación, cambio, retirada y desasociación por borrado. No se usarán identificadores ni nombres como etiquetas o mensajes de log.

## Criterios de aceptación

1. Un usuario crea una campaña sin módulo y queda como su único DM.
2. Un usuario crea una campaña seleccionando un módulo existente y recibe su resumen asociado.
3. El DM de una campaña existente asocia un módulo y la campaña conserva identidad y estado previo.
4. El DM cambia a otro módulo y solo se modifica la referencia.
5. Retirar el módulo deja la campaña como `Sin módulo` y no elimina el catálogo.
6. Dos campañas pueden seleccionar el mismo módulo sin compartir estado de campaña.
7. Un jugador ve el resumen asociado, pero recibe `403` al intentar asignar, cambiar o retirar.
8. Un identificador de módulo inexistente o eliminado no puede quedar persistido.
9. Eliminar un módulo utilizado por varias campañas deja todas ellas sin módulo y no elimina ninguna campaña.
10. Eliminar una campaña no modifica el módulo ni las asociaciones de otras campañas.
11. Peticiones concurrentes no dejan una referencia colgante ni aplican silenciosamente una versión obsoleta.
12. Las pruebas web, API, PostgreSQL, contratos intermodulares y arquitectura demuestran el recorrido completo.

## Fuera de alcance

- Progreso, capítulo actual, recursos descubiertos o personalización por campaña.
- Consulta de capítulos, mapas, localizaciones o NPC.
- Historial de módulos usados o restauración automática de una asociación anterior.
- Cambiar permisos de catálogo o permitir que el DM edite el módulo compartido.
