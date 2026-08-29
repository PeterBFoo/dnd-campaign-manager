# Spec 019: Visibilidad de NPC independiente por campaña

- Estado: Propuesta
- Fecha: 2026-08-29
- Tipo: incremento funcional vertical
- Roadmap: [roadmap funcional](../../roadmap/product-roadmap.md)
- Requisitos principales: RF-014, RF-021 a RF-024 y RF-026
- Requisito relacionado pendiente: RF-025, hasta que exista un modelo de estadísticas de NPC
- Dependencias: [spec 013](../013-asignacion-modulo-campana/spec.md) y [spec 018](../018-npc-modulo/spec.md)

## Problema

El catálogo base de NPC es compartido por todas las campañas de un módulo, pero su descubrimiento no puede compartirse. El DM necesita controlar qué NPC conoce su grupo y los jugadores requieren una proyección que nunca revele notas privadas ni NPC bloqueados.

## Objetivo

Introducir estado mínimo de visibilidad por campaña: todos los NPC del módulo comienzan bloqueados para jugadores y el DM puede desbloquearlos de forma irreversible mientras se mantenga la asociación. Los jugadores consultarán únicamente los campos públicos de los NPC desbloqueados en su campaña.

## Actores y proyecciones

- **DM:** ve el catálogo completo con estado `locked/unlocked` y puede desbloquear.
- **Jugador aceptado:** ve solo NPC desbloqueados, sin necesitar personaje activo.
- **Administrador:** administra contenido base mediante el spec 018; no desbloquea en campañas por ser administrador.
- **Usuario ajeno:** no ve catálogo ni estado.

La API construye contratos separados:

- proyección DM: campos públicos, notas privadas, relaciones y estado de visibilidad;
- proyección jugador: identificador, nombre, descripción pública e imagen autorizada;
- nunca se utiliza el mismo DTO eliminando campos en el frontend.

## Reglas funcionales

- El estado pertenece a la pareja campaña-NPC e incluye el identificador del módulo para evitar reutilización accidental.
- Un NPC empieza bloqueado implícitamente; no se crea una fila por cada combinación hasta desbloquear.
- Solo el DM de la campaña puede desbloquear y la operación repetida es idempotente.
- No existe operación de volver a bloquear en este incremento.
- Desbloquear en una campaña no afecta a otra aunque ambas usen el mismo módulo.
- El efecto está disponible para todos los jugadores autorizados en la siguiente consulta; no se exige tiempo real.
- Editar después los campos públicos del NPC actualiza lo que ven los jugadores; las notas de DM siguen ocultas.
- Eliminar un NPC elimina sus estados de visibilidad sin afectar a la campaña.
- Cambiar o retirar el módulo elimina los estados del módulo anterior. Volver a asociarlo comienza con todos los NPC bloqueados.
- Eliminar el módulo elimina sus estados y deja la campaña sin módulo conforme al spec 013.
- La lectura de imagen del jugador exige simultáneamente membresía aceptada, módulo actual y NPC desbloqueado.

## Recorrido web

- La librería DM del spec 018 añade estado y acción `Desbloquear para jugadores` con confirmación.
- La portada o librería de campaña del jugador ofrece un apartado NPC con búsqueda sobre los desbloqueados y estado vacío explícito.
- Tras desbloquear, el DM ve el nuevo estado y el jugador lo obtiene al recargar o en la siguiente consulta.
- El jugador nunca recibe enlaces a NPC bloqueados ni mensajes que confirmen su existencia.

## Contrato HTTP funcional

- `GET /api/v1/campaigns/{campaignId}/library/npcs`: devuelve proyección DM completa o colección pública según rol.
- `GET /api/v1/campaigns/{campaignId}/library/npcs/{npcId}`: detalle diferenciado por rol.
- `PUT /api/v1/campaigns/{campaignId}/library/npcs/{npcId}/visibility`: desbloquea mediante `{ "visibleToPlayers": true }`.
- `GET /api/v1/campaigns/{campaignId}/library/npcs/{npcId}/image`: aplica la misma política de proyección.

Para un jugador, un NPC bloqueado responde `404` sin revelar si existe; un usuario sin acceso a la campaña conserva la semántica `403` establecida para Campaigns. Intentar enviar `visibleToPlayers: false` devuelve validación.

## Ownership técnico

- `apps/api`: un módulo `Library` posee el estado por campaña, sus comandos, persistencia y proyecciones públicas. Consume contratos mínimos de Campaigns para acceso/módulo actual y de AdventureCatalog para datos base; no consulta tablas ajenas.
- `apps/web`: un módulo `library` posee la experiencia diferenciada de DM y jugador. `adventure-catalog` conserva exclusivamente la autoría base.

Campaigns y AdventureCatalog publican operaciones idempotentes para limpiar estado al cambiar o eliminar módulo. El plan debe resolver esta coordinación sin una transacción distribuida ni ciclos de ensamblados, mediante contratos y eventos persistidos si la consistencia inmediata entre módulos no puede garantizarse.

## Persistencia y consistencia

- Una restricción única sobre campaña, módulo y NPC impide dos desbloqueos distintos.
- La fila captura `unlockedAt` y el actor DM para auditoría interna, sin exponer su identificador a jugadores.
- Las consultas siempre validan el módulo actual; una fila huérfana nunca concede visibilidad.
- La limpieza por cambio o borrado es idempotente y observable. Mientras se completa un reintento, la validación contra el módulo actual mantiene cerrada la proyección.

## Observabilidad y privacidad

Se medirán listados DM/jugador, desbloqueo, lectura de imagen y limpieza por resultado. Logs y telemetría no incluyen nombres, búsquedas, identificadores ni el conjunto de NPC desbloqueados. Las respuestas públicas no serializan notas, relaciones privadas, procedencia interna o datos de auditoría.

## Criterios de aceptación

1. En dos campañas del mismo módulo, el mismo NPC comienza bloqueado para sus jugadores.
2. El DM de la primera lo desbloquea y todos sus jugadores lo ven en la siguiente consulta.
3. Los jugadores de la segunda campaña continúan sin verlo.
4. Un jugador no puede desbloquear ni obtener por identificador o imagen un NPC bloqueado.
5. La proyección pública contiene nombre, descripción e imagen, pero nunca notas de DM ni campos administrativos.
6. Repetir el desbloqueo no duplica estado ni falla.
7. Editar campos públicos actualiza ambas proyecciones autorizadas; editar notas no cambia el contrato público.
8. Cambiar, retirar o eliminar el módulo elimina el efecto del desbloqueo y no afecta a la campaña.
9. Volver a asociar el módulo no restaura estados anteriores.
10. Eliminar un NPC limpia su visibilidad sin eliminar capítulos, localizaciones o campañas.
11. Pruebas de contrato verifican explícitamente la ausencia de campos privados y el aislamiento entre campañas.
12. Pruebas Angular, API, PostgreSQL, integración intermodular y arquitectura mantienen verdes las suites existentes.

## Fuera de alcance

- Volver a bloquear, caducidad, visibilidad por jugador o grupos parciales.
- Estado vivo/muerto, reputación, inventario o personalización por campaña.
- Estadísticas de combate; cuando existan, serán exclusivas del DM conforme a RF-025.
- Referencias desde bitácora RF-033 y RF-034, que requerirán otro incremento sobre este contrato público.
- Notificaciones, WebSockets o sincronización instantánea.
