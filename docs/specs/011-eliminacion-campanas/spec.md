# Spec 011: Eliminación de campañas

- Estado: Completada
- Fecha: 2026-08-29
- Tipo: incremento funcional vertical
- Roadmap: [roadmap funcional](../../roadmap/product-roadmap.md)
- Requisitos principales: RF-003, RF-009, RF-014 y RF-084
- Dependencias: [spec 004](../004-creacion-campanas/spec.md) y los módulos consumidores del contrato de acceso a campañas

## Problema

Una campaña creada permanece disponible indefinidamente. El DM no puede retirarla cuando la mesa termina o se creó por error, y los jugadores conservan acceso a sus pantallas y datos asociados.

## Objetivo

Permitir que el único DM elimine una campaña con confirmación explícita, revocando inmediatamente el acceso de todos los participantes y cerrando invitaciones y operaciones dependientes sin coordinar borrados destructivos entre módulos.

## Decisiones funcionales aceptadas

La solicitud de implementación del usuario acepta este incremento el 2026-08-29 con estas reglas:

1. Solo el DM de la campaña puede eliminarla; un jugador recibe `403`.
2. La web pide confirmación e informa de que la acción no se puede deshacer.
3. La eliminación es una baja lógica: Campaigns conserva el registro y los módulos conservan sus datos, pero el producto no ofrece restauración y ningún actor puede acceder a ellos.
4. Una campaña eliminada desaparece de los listados y se comporta como inexistente (`404`) en Campaigns y en los módulos que consumen su contrato de acceso.
5. Las invitaciones pendientes de la campaña dejan de poder gestionarse o aceptarse.
6. Repetir la eliminación responde `404`, porque la campaña ya no forma parte del conjunto activo.

## Alcance funcional

- Añadir `DELETE /api/v1/campaigns/{campaignId}` con respuestas `204`, `403`, `404` y `401` por la autenticación existente.
- Registrar el instante de eliminación en Campaigns y excluir bajas de todas sus consultas y contratos públicos.
- Impedir que una invitación pendiente conceda membresía después de la baja.
- Mostrar una zona de peligro solo al DM en el detalle de campaña.
- Confirmar la acción, bloquear dobles envíos durante la petición y volver al listado al completarla.
- Mostrar un error recuperable si la petición falla.

## Ownership técnico

- `apps/api/Modules/Campaigns` es propietario de la regla, el estado de baja, el endpoint, la autorización y la métrica.
- `apps/api/Modules/Access` vuelve a validar que la campaña siga activa antes de aceptar una invitación.
- `apps/web/src/app/modules/campaigns` es propietario del cliente y de la experiencia de confirmación.

Ambas superficies requeridas cambian. Characters, Journal, Missions y Combat no cambian porque ya autorizan cada operación mediante `ICampaignAccessReader`; al dejar Campaigns de publicar la baja como existente, fallan cerrados.

## Persistencia y consistencia

- `campaigns.campaigns` incorpora `DeletedAt` nullable.
- Las consultas ordinarias aplican un filtro global `DeletedAt IS NULL`.
- La escritura se confirma en la transacción local de Campaigns y no elimina filas de otros esquemas.
- Las membresías y datos relacionados retenidos no conceden acceso por sí mismos; Campaigns sigue siendo la fuente de existencia y rol efectivo.
- Una aceptación que compita exactamente con una eliminación podría conservar una membresía interna, pero nunca obtiene acceso a la campaña dada de baja. Si la baja ya es visible al validar, la invitación se rechaza como no disponible.

## Observabilidad

La operación usa la métrica existente `campaigns.operations` con operación `delete` y resultados `success`, `forbidden` o `not_found`. No registra nombre, identificadores de usuario ni contenido de campaña.

## Criterios de aceptación

1. El DM confirma la eliminación y recibe `204`; después vuelve al listado sin la campaña.
2. Un jugador no ve la acción y una petición manipulada recibe `403`.
3. Tras la baja, DM y jugadores reciben `404` al consultar la campaña y dejan de verla en sus listados.
4. Las operaciones de módulos dependientes fallan como campaña inexistente.
5. Una invitación pendiente no puede gestionarse ni aceptarse tras la baja.
6. Una segunda eliminación recibe `404` y no modifica el instante original.
7. La migración preserva campañas existentes como activas.
8. Dominio, Application, contrato HTTP y componentes web quedan cubiertos por pruebas; suites y builds permanecen verdes.

## Fuera de alcance

- Restaurar campañas, papelera, archivado o suspensión temporal.
- Borrado físico inmediato o programado de personajes, imágenes, bitácora, misiones, encuentros, membresías e invitaciones.
- Transferir el rol DM, abandonar la campaña o delegar su eliminación.
- Editar campañas o asociar módulos de aventura.
- Retención regulatoria, exportación o auditoría visible de campañas eliminadas.

## Validación

La implementación quedó verificada el 2026-08-29 mediante 90 pruebas API en Docker con PostgreSQL real, incluidas migración y contrato HTTP de eliminación; 73 pruebas frontend; compilación .NET sin advertencias; build Angular; construcción de las imágenes API y web; y validación de Docker Compose. El recorrido integrado demuestra `403` para jugador, `204` para DM, ocultación posterior, `404` transversal e invalidación de una invitación pendiente.
