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
| [ADR-0005](0005-frontend-modular-por-capacidades.md) | Aceptado | Frontend modular por capacidades, APIs públicas y límites TypeScript verificables |
| [ADR-0006](0006-campanas-acceso-e-invitaciones.md) | Aceptado | Campaigns posee campañas y DM; Access conserva invitaciones, jugadores y selección de usuarios elegibles |
| [ADR-0007](0007-imagenes-privadas-de-personajes.md) | Aceptado | Imágenes privadas en Azure Blob/Azurite, metadatos relacionales y entrega autorizada por API |
| [ADR-0008](0008-asociacion-campanas-modulos.md) | Aceptado | Contrato dirigido Campaigns → AdventureCatalog y desasociación atómica entre esquemas |
