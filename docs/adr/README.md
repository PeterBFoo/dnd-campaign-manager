# Architecture Decision Records

Los ADR registran decisiones que afectan a la estructura, tecnología, seguridad, despliegue u operación del sistema.

## Convención

- Nombre: `NNNN-titulo-en-kebab-case.md`.
- Estados: `Propuesto`, `Aceptado`, `Sustituido` o `Rechazado`.
- Un ADR aceptado no se reescribe para cambiar la decisión: se crea otro ADR que lo sustituya.
- Las correcciones tipográficas o enlaces rotos pueden modificarse sin crear un ADR nuevo.

## Índice

| ADR | Estado | Decisión |
|---|---|---|
| [ADR-0001](0001-monorepositorio-y-monolito-modular.md) | Aceptado | Monorepositorio, Angular, ASP.NET Core, PostgreSQL y observabilidad |
| [ADR-0002](0002-identidad-invitaciones-y-correo-transaccional.md) | Aceptado | Alta exclusivamente por invitación, caducidad de siete días y correo transaccional con Brevo |
| [ADR-0003](0003-bootstrap-sesiones-y-flujo-de-invitaciones.md) | Aceptado | Bootstrap único, sesiones opacas y flujo funcional de invitaciones |
| [ADR-0004](0004-arquitectura-modular-cqrs-y-limites-de-dependencia.md) | Aceptado | Módulos de negocio, CQRS ligero, ownership del estado y límites arquitectónicos verificables |
