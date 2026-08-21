# Especificación 002: Modularización de la API y extracción de Access

- Estado: Aceptada
- Fecha: 2026-08-21
- Tipo: incremento técnico
- Decisión vinculante: [ADR-0004](../../adr/0004-arquitectura-modular-cqrs-y-limites-de-dependencia.md)

## Problema

La API actual compila Domain, Application, Infrastructure y Endpoints en un único ensamblado. Los endpoints coordinan HTTP, autorización, dominio, EF Core, transacciones y observabilidad; Application depende de Infrastructure; y la invitación tiene dos representaciones con comportamiento duplicado.

Esta estructura permite completar el flujo funcional actual, pero dificulta añadir capacidades sin aumentar el acoplamiento y no protege los límites definidos para el monolito modular.

## Objetivo

Extraer la funcionalidad actual como un único proyecto de módulo `Access`, con límites internos entre Api, Application, Domain e Infrastructure verificados por fitness functions, y reorganizar sus operaciones como commands y queries independientes sin cambiar el comportamiento público de la API.

## Alcance

- Usuarios, credenciales y bootstrap.
- Sesiones y autenticación bearer opaca.
- Invitaciones de plataforma y campaña.
- Concesiones actuales de acceso a campaña.
- Entrega de invitaciones mediante outbox y correo transaccional.
- Persistencia, migraciones y observabilidad de esas capacidades.
- Pruebas unitarias, integración, componente, concurrencia y arquitectura.
- Fitness functions del pipeline.

## Restricciones

- Se conserva un único proceso, despliegue y PostgreSQL.
- Las rutas `/api/v1`, formatos JSON y semántica HTTP se mantienen compatibles.
- No se introduce event sourcing, mediator obligatorio, bus distribuido ni base de lectura separada.
- Las migraciones existentes y los datos deben seguir siendo actualizables.
- Las reglas de seguridad y aislamiento entre campañas no pueden debilitarse.
- Cada incremento intermedio debe compilar y mantener verde la suite aplicable.

## Fuera de alcance

- Implementar Campaigns u otros módulos funcionales futuros.
- Cambiar autenticación, recuperación de contraseña o reglas funcionales de invitación.
- Cambiar Brevo, PostgreSQL u OpenTelemetry.
- Modificar el frontend salvo por ajustes imprescindibles para conservar el contrato.
- Separar procesos, despliegues o bases de datos.
- Renombrar o mover tablas a esquemas PostgreSQL por módulo; se pospone hasta que exista un segundo módulo persistente.

## Criterios de aceptación

1. Access está formado por un único proyecto y ensamblado, organizado internamente en Domain, Application, Infrastructure y Api con la dirección de dependencias definida en ADR-0004.
2. El host actúa únicamente como composition root y pipeline HTTP.
3. Los endpoints no dependen de EF Core, `DbContext` ni entidades persistentes.
4. Application no depende de ASP.NET Core, EF Core, Infrastructure ni OpenTelemetry.
5. `Invitation` es el único modelo funcional persistido y no existe comportamiento duplicado en `InvitationRecord`.
6. Cada operación pública de Access se implementa mediante un command o query handler específico.
7. Las queries no mutan estado ni ejecutan `SaveChanges`.
8. El outbox crea y adquiere mensajes de forma transaccional, segura ante concurrencia e idempotente.
9. Bootstrap, aceptación de invitación, emisión duplicada y adquisición del outbox tienen pruebas de concurrencia con PostgreSQL real.
10. Los flujos HTTP existentes conservan rutas, payloads, status codes y autorización.
11. Las migraciones funcionan sobre una base vacía y sobre la versión anterior soportada.
12. Los architecture tests globales entre módulos y los internos de Access se ejecutan en CI sin omisiones silenciosas.

## Definición de terminado

El incremento estará terminado cuando todas las tareas de `tasks.md` estén completadas, las suites sean verdes, la API mantenga compatibilidad, no permanezca código funcional duplicado y la documentación arquitectónica represente la implementación final.
