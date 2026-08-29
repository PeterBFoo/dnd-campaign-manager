# ADR-0008: Asociación entre campañas y módulos de aventura

- Estado: Aceptado
- Fecha: 2026-08-29
- Specs relacionadas: [spec 012](../specs/012-libreria-modulos/spec.md) y [spec 013](../specs/013-asignacion-modulo-campana/spec.md)

## Contexto

Campaigns posee la asociación opcional de una campaña con un módulo, mientras AdventureCatalog posee el catálogo y elimina físicamente sus módulos. La solución debe validar asociaciones, proyectar metadatos mínimos y garantizar que el borrado de un módulo no deje referencias colgantes ni elimine campañas. Los módulos comparten una instancia PostgreSQL, pero mantienen proyectos, esquemas, contextos y migraciones propios.

Se evaluaron dos alternativas para el borrado transversal:

1. Una clave foránea entre esquemas con `ON DELETE SET NULL`.
2. Un evento de borrado consumido de forma idempotente por Campaigns.

El evento reduce el acoplamiento físico, pero introduce consistencia eventual, outbox/inbox, reintentos y un intervalo observable con referencias a contenido eliminado. Ese coste no aporta valor al monolito modular desplegado sobre una única base de datos.

## Decisión

Campaigns dependerá del contrato público mínimo de AdventureCatalog para validar existencia y obtener resúmenes. AdventureCatalog no referenciará Campaigns ni conocerá identificadores de campaña.

La columna `campaigns.campaigns.AdventureModuleId` tendrá una clave foránea hacia la tabla propietaria de AdventureCatalog con `ON DELETE SET NULL`. La migración pertenece a Campaigns y se aplica después de crear el esquema y tabla del catálogo. El borrado del módulo y la desasociación de todas las campañas ocurren atómicamente en PostgreSQL.

La aplicación seguirá validando el módulo antes de una escritura para devolver un error funcional útil. La FK es la última defensa frente a carreras. Campaigns usará concurrencia optimista para no sobrescribir silenciosamente un cambio de asociación.

Los contratos intermodulares solo expondrán identificador, nombre y URL autorizada de portada. No expondrán entidades, `DbContext`, repositorios, procedencia ni contenido editorial.

## Consecuencias

- No existe un estado confirmado con una referencia a un módulo borrado.
- Eliminar un módulo no requiere que AdventureCatalog llame a Campaigns y no crea un ciclo de ensamblados.
- Campaigns adquiere una dependencia de compilación hacia la superficie contractual de AdventureCatalog y una dependencia de migración hacia su tabla.
- El despliegue debe aplicar primero la migración base de AdventureCatalog y después la migración de Campaigns que crea la FK.
- Un rollback que retire AdventureCatalog debe eliminar primero la FK de Campaigns; los runbooks deben preservar ese orden.
- Si los módulos se separan en servicios o bases de datos diferentes, será necesario sustituir la FK por coordinación durable y aceptar o esconder la consistencia eventual.
