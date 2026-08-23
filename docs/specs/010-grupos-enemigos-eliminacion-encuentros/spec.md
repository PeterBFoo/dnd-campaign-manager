# Spec 010: Grupos de enemigos y eliminación de encuentros

- Estado: Aceptada; implementación iniciada
- Fecha: 2026-08-23
- Tipo: incremento funcional vertical
- Roadmap: [roadmap funcional](../../roadmap/product-roadmap.md)
- Requisitos principales: RF-050 a RF-057
- Dependencias: [spec 009](../009-encuentros-iniciativa/spec.md)

## Problema

El spec 009 representa cada enemigo como un participante y, por tanto, como un turno independiente. Esto obliga al DM a duplicar manualmente criaturas que comparten iniciativa y no permite expresar que un grupo completo, por ejemplo ocho criaturas del mismo tipo, actúa en un único turno. Tampoco existe una operación para eliminar encuentros preparados o ya finalizados.

## Objetivo

Permitir que el DM cree un grupo enemigo indicando su cantidad. El grupo ocupará una sola posición y un solo turno de iniciativa, mientras cada integrante conservará identidad y vida individual. Además, el DM podrá eliminar encuentros que no estén activos.

## Decisiones funcionales aceptadas

El usuario confirmó estas reglas el 2026-08-23:

1. Un grupo enemigo comparte nombre, iniciativa y CA y ocupa una única fila del orden de iniciativa.
2. Todos los integrantes del grupo actúan durante el mismo turno.
3. Cada integrante conserva y modifica su vida de manera independiente.
4. La vida máxima introducida al crear el grupo se aplica inicialmente a cada integrante.
5. Un enemigo individual es un grupo de cantidad uno; no existe un modelo paralelo.
6. La cantidad se fija al preparar el encuentro y queda congelada al activarlo.
7. El DM puede eliminar encuentros en borrador o finalizados. Un encuentro activo debe finalizarse antes y nunca se elimina implícitamente.
8. Al avanzar turno se omiten los grupos enemigos derrotados, entendiendo por derrotado que todos sus integrantes están a 0. El tratamiento de personajes a 0 queda pendiente porque Combat no dispone todavía de vida de personajes.

## Alcance funcional

- Al crear un enemigo, el DM indica una cantidad entre 1 y 100.
- La API crea un participante enemigo y tantos integrantes de grupo como indique la cantidad, en una única operación atómica.
- Cada integrante recibe un identificador estable, un número ordinal y vida actual igual a la máxima.
- Daño y curación se aplican a un integrante concreto y se limitan entre 0 y su vida máxima.
- Llegar a 0 no elimina al integrante ni reduce automáticamente la cantidad.
- El DM ve la cantidad y una lista de integrantes con su vida y controles propios.
- El jugador ve el nombre y la cantidad del grupo en una única fila, sin vida, CA, identificadores de integrantes ni controles.
- Los desempates se resuelven entre participantes o grupos, nunca entre integrantes del mismo grupo.
- El DM puede eliminar un encuentro no activo con confirmación visual y control de versión.
- Al avanzar, el orden salta grupos con todos sus integrantes a 0; un grupo con al menos un integrante vivo mantiene su turno.
- El borrado elimina en cascada sus participantes e integrantes y queda aislado por campaña.

## API funcional

- `POST /api/v1/campaigns/{campaignId}/encounters/{encounterId}/enemies` incorpora `quantity` junto a nombre, iniciativa, CA y vida máxima individual.
- `POST /api/v1/campaigns/{campaignId}/encounters/{encounterId}/enemies/{participantId}/members/{memberId}/hit-points` aplica daño o curación a un integrante concreto.
- `DELETE /api/v1/campaigns/{campaignId}/encounters/{encounterId}?expectedVersion={version}` elimina un encuentro en borrador o finalizado y devuelve `204 No Content`.

La mesa del DM devuelve integrantes y vida. La proyección activa segura solo añade `quantity` al participante y continúa omitiendo CA, vida, versión e identificadores internos.

## Ownership técnico

- `apps/api/Modules/Combat` conserva ownership del grupo, integrantes, vida, borrado, migración y endpoints.
- `apps/web/src/app/modules/combat` incorpora cantidad, controles individuales y eliminación.

Ambas superficies cambian; Campaigns y Characters no requieren ampliar sus contratos.

## Persistencia y consistencia

- Se añade `combat.enemy_group_members`, dependiente del participante enemigo y con borrado en cascada.
- La migración convierte cada enemigo existente en un grupo de un integrante usando su vida actual y máxima; después retira esos campos del participante.
- El ordinal es único dentro de cada grupo y la forma de personaje/enemigo queda protegida con checks y relaciones.
- La versión del encuentro protege creación de grupos, ajustes individuales y eliminación frente a escrituras obsoletas.

## Criterios de aceptación

1. Crear un grupo de ocho enemigos produce una sola posición de iniciativa y ocho vidas independientes.
2. Avanzar el turno entra y sale del grupo una sola vez, sin recorrer sus integrantes.
3. Dañar o curar un integrante no modifica la vida de los otros siete.
4. Un enemigo con cantidad uno conserva el comportamiento funcional anterior.
5. La respuesta del jugador muestra cantidad ocho pero no contiene integrantes, CA ni vida.
6. Cantidades fuera de 1 a 100 se rechazan sin cambios parciales.
7. El DM elimina un borrador o finalizado y deja de poder consultarlo.
8. El jugador no puede eliminar; un usuario ajeno no conoce el encuentro.
9. El intento de eliminar un activo o usar una versión obsoleta devuelve conflicto y conserva el encuentro.
10. La migración, API, dominio y componentes web quedan cubiertos por pruebas y las suites existentes permanecen verdes.

## Fuera de alcance

- Cantidades variables después de crear el grupo, refuerzos o división/fusión de grupos.
- Eliminar automáticamente integrantes a 0, reanimación especial, daño de área o aplicación masiva de vida.
- Iniciativa o CA diferente por integrante del mismo grupo.
- Recuperar encuentros eliminados, papelera, archivado o auditoría visible.
- Eliminar un encuentro activo directamente.
