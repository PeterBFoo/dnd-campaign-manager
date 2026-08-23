# Plan 010: Grupos de enemigos y eliminación de encuentros

- Estado: En ejecución
- Fecha: 2026-08-23
- Especificación: [spec.md](spec.md)
- Dependencia: [spec 009](../009-encuentros-iniciativa/spec.md)

## Estrategia

1. Modelar `EnemyGroupMember` como entidad hija del participante enemigo y trasladar a ella la vida actual y máxima.
2. Mantener un participante por grupo para que iniciativa, orden y turno no cambien con la cantidad.
3. Crear una migración que preserve enemigos existentes como grupos de cantidad uno.
4. Extender DTO y endpoint de alta con cantidad y dirigir los ajustes de vida por `memberId`.
5. Añadir eliminación autorizada y versionada para estados `Draft` y `Finished`.
6. Adaptar la mesa del DM, la proyección segura y el cliente Angular.
7. Verificar dominio, Application, PostgreSQL, HTTP, arquitectura, Angular, builds e imágenes.

## Seguridad y privacidad

- La proyección de jugador solo incorpora la cantidad agregada.
- Los integrantes, sus identificadores y vida permanecen en el DTO exclusivo del DM.
- Todas las escrituras vuelven a comprobar DM, campaña, encuentro, participante, integrante y versión.
- El borrado activo se rechaza y no se implementan cascadas fuera del agregado Combat.

## Despliegue

La migración se aplica después de la inicial de Combat. El backfill precede a la retirada de las columnas antiguas para conservar la vida de encuentros ya persistidos. El rollback ordinario seguirá siendo roll-forward; no se eliminará el esquema manualmente.
