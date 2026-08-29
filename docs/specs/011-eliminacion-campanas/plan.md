# Plan 011: Eliminación de campañas

- Estado: Ejecutado
- Fecha: 2026-08-29
- Especificación: [spec.md](spec.md)
- Dependencia principal: [spec 004](../004-creacion-campanas/spec.md)

## Estrategia

1. Incorporar `DeletedAt` y la transición de baja al agregado Campaign.
2. Aplicar un filtro persistente a consultas, listados y contratos de acceso.
3. Añadir handler y endpoint autorizados exclusivamente para el DM, con telemetría existente.
4. Revalidar la existencia de la campaña durante la aceptación de invitaciones.
5. Añadir cliente, confirmación y navegación posterior en Angular solo para el rol `dm`.
6. Verificar dominio, Application, PostgreSQL, HTTP, Angular, builds y Compose.

## Seguridad y consistencia

- La autorización se realiza en Application; ocultar el botón no constituye una medida de seguridad.
- Los consumidores continúan consultando `ICampaignAccessReader` y reciben `Exists: false` para una baja.
- No se introducen referencias directas entre tablas ni transacciones distribuidas.
- La confirmación no incluye información editorial ni envía el nombre fuera del navegador.

## Datos y despliegue

La migración añade una columna nullable, por lo que todas las campañas existentes quedan activas. El despliegue aplica primero la migración; código previo ignora la columna y código nuevo filtra únicamente filas con un instante de baja.

## Verificación

- Tests de dominio y handlers de Campaigns.
- Test de persistencia/filtro y recorrido HTTP con PostgreSQL.
- Tests del cliente y página Angular para DM, jugador y confirmación.
- Suite completa, compilación .NET, build Angular y validación de Compose.
