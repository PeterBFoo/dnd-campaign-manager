# Migraciones de base de datos

- Estado: vigente
- ADR relacionados: [ADR-0004](../adr/0004-arquitectura-modular-cqrs-y-limites-de-dependencia.md) y [ADR-0006](../adr/0006-campanas-acceso-e-invitaciones.md)
- Alcance: migraciones de los módulos Access, Campaigns, Characters, Journal, Missions y Combat

## Orden de aplicación

La API comparte una conexión PostgreSQL, pero cada módulo mantiene su propio `DbContext` y esquema. Al arrancar con `Database__ApplyMigrations=true`, el host aplica siempre:

1. migraciones de Access;
2. migraciones de Campaigns;
3. migraciones de Characters;
4. migraciones de Journal;
5. migraciones de Missions;
6. migraciones de Combat.

Access mantiene identidad e invitaciones en el esquema `access`; Campaigns conserva campañas en `campaigns`; Characters persiste personajes y metadatos de imagen en `characters`; Journal conserva entradas y su autoría histórica en `journal`; Missions conserva misiones, autoría y principal única en `missions`; Combat conserva encuentros, participantes, iniciativa, turnos y vida de enemigos en `combat`. No hay foreign keys ni consultas directas entre esquemas de módulos.

## Antes de desplegar

1. Crear una copia de seguridad verificable de la base objetivo.
2. Confirmar que la versión instalada corresponde a una migración soportada por el binario nuevo.
3. Ejecutar `docker compose run --build --rm api-tests` contra la base efímera antes de publicar la imagen.
4. Desplegar una sola revisión con migraciones habilitadas y esperar a readiness antes de aumentar tráfico.

## Verificación posterior

- comprobar que existen los esquemas `access`, `campaigns`, `characters`, `journal`, `missions` y `combat`;
- comprobar que el historial contiene las migraciones esperadas;
- verificar `/health/ready`;
- crear y consultar una campaña de prueba no editorial en un entorno no productivo;
- crear, editar, listar y eliminar una entrada genérica de bitácora con los roles autorizados;
- crear, editar, marcar como principal y eliminar una misión genérica; comprobar que no existe más de una principal por campaña;
- crear un encuentro genérico, añadir participantes, activarlo y finalizarlo; comprobar que solo existe uno activo por campaña y que la proyección de jugador no contiene CA ni vida;
- revisar errores de migración y de PostgreSQL sin registrar payloads ni datos personales.

## Reversibilidad

El movimiento de tablas de Access hace que un binario anterior que espere el esquema por defecto no sea compatible. Después de aplicar esa migración, la estrategia ordinaria es **roll-forward** con una imagen corregida.

Solo se ejecutará un rollback de esquema si se ha ensayado sobre una copia, existe una copia de seguridad reciente y se ha detenido el tráfico de escritura. Nunca se debe desplegar el binario anterior sobre la base ya migrada asumiendo compatibilidad automática.
