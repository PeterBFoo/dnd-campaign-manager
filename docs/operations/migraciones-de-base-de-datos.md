# Migraciones de base de datos

- Estado: vigente
- ADR relacionados: [ADR-0004](../adr/0004-arquitectura-modular-cqrs-y-limites-de-dependencia.md) y [ADR-0006](../adr/0006-campanas-acceso-e-invitaciones.md)
- Alcance: migraciones de los módulos Access y Campaigns

## Orden de aplicación

La API comparte una conexión PostgreSQL, pero cada módulo mantiene su propio `DbContext` y esquema. Al arrancar con `Database__ApplyMigrations=true`, el host aplica siempre:

1. migraciones de Access;
2. migraciones de Campaigns.

Access mueve sus tablas existentes al esquema `access` preservando datos, índices y restricciones. Campaigns crea su tabla en el esquema `campaigns`. No hay foreign keys ni consultas directas entre ambos esquemas.

## Antes de desplegar

1. Crear una copia de seguridad verificable de la base objetivo.
2. Confirmar que la versión instalada corresponde a una migración soportada por el binario nuevo.
3. Ejecutar `docker compose run --build --rm api-tests` contra la base efímera antes de publicar la imagen.
4. Desplegar una sola revisión con migraciones habilitadas y esperar a readiness antes de aumentar tráfico.

## Verificación posterior

- comprobar que existen los esquemas `access` y `campaigns`;
- comprobar que el historial contiene las migraciones esperadas;
- verificar `/health/ready`;
- crear y consultar una campaña de prueba no editorial en un entorno no productivo;
- revisar errores de migración y de PostgreSQL sin registrar payloads ni datos personales.

## Reversibilidad

El movimiento de tablas de Access hace que un binario anterior que espere el esquema por defecto no sea compatible. Después de aplicar esa migración, la estrategia ordinaria es **roll-forward** con una imagen corregida.

Solo se ejecutará un rollback de esquema si se ha ensayado sobre una copia, existe una copia de seguridad reciente y se ha detenido el tráfico de escritura. Nunca se debe desplegar el binario anterior sobre la base ya migrada asumiendo compatibilidad automática.
