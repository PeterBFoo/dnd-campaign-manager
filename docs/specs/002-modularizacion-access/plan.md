# Plan 002: Modularización de Access

- Estado: Aprobado
- Fecha: 2026-08-21
- Especificación: [spec.md](spec.md)
- ADR: [ADR-0004](../../adr/0004-arquitectura-modular-cqrs-y-limites-de-dependencia.md)

## Resultado esperado

La API seguirá siendo un único monolito desplegable y el código funcional actual residirá en un módulo Access compuesto por un único proyecto y ensamblado. El host solo configurará la aplicación; Api traducirá HTTP; Application ejecutará casos de uso; Domain será propietario de las invariantes; e Infrastructure implementará persistencia y adaptadores.

## Estructura objetivo

Se conservará `apps/api` como host para evitar cambios innecesarios en Docker y despliegue. El proyecto del módulo se ubicará fuera de su árbol de compilación automática:

```text
apps/
  api/                                      host ASP.NET Core

src/
  Modules/Access/
    DndCampaign.Modules.Access/
      Api/
      Application/
      Domain/
      Infrastructure/

tests/
  DndCampaign.ArchitectureTests/
  Modules/Access/
    DndCampaign.Modules.Access.Tests/
      Architecture/
      Application/
      Component/
      Domain/
      Infrastructure/
```

Los tests de una capacidad vivirán en el proyecto de tests de su módulo y se clasificarán por tipo. El proyecto global contendrá exclusivamente reglas entre módulos y sobre el host.

## Dirección de referencias

```text
Host -> Access (fachada pública)
Access.Api -> Access.Application
Access.Infrastructure -> Access.Application + Access.Domain
Access.Application -> Access.Domain
Access.Domain -> BCL
```

El host referenciará únicamente la fachada de Access. Un test arquitectónico del módulo verificará los límites internos y el proyecto global impedirá dependencias indebidas entre módulos o desde el host.

## Estrategia de implementación

La migración se hará mediante vertical slices y no mediante una reescritura completa:

1. fijar el comportamiento actual con pruebas de caracterización;
2. crear el proyecto del módulo, su fachada y las reglas arquitectónicas globales e internas;
3. trasladar el dominio y eliminar la duplicidad de Invitation;
4. establecer puertos de persistencia, resultados y handlers;
5. migrar bootstrap y sesiones;
6. migrar consultas de invitación y eliminar escrituras en lectura;
7. migrar comandos de invitación, dejando aceptación para el final;
8. separar y endurecer el outbox;
9. retirar el camino antiguo y endurecer CI.

Cada operación migrada se registrará mediante attribute routing en un controlador MVC y dejará de estar registrada en la clase Minimal API antigua en el mismo cambio. Los contratos HTTP se cubrirán antes y después mediante tests de componente.

## Dominio y persistencia

`Invitation` evolucionará para ser la entidad persistida por EF Core. Incorporará los campos funcionales que hoy solo tiene `InvitationRecord`, mantendrá sus invariantes y no expondrá setters públicos. La configuración EF residirá en Infrastructure.

Access tendrá un contexto propio. Durante este incremento se conservarán tablas, columnas y esquema actuales siempre que sea posible. Cualquier cambio de modelo tendrá migración y prueba de actualización. Las invariantes concurrentes se protegerán mediante constraints o control de concurrencia además de validaciones de Application.

Application definirá puertos específicos para cuentas, sesiones, invitaciones, acceso a campañas, lecturas y unidad de trabajo. No se creará un repositorio genérico. Las queries proyectarán DTO con lectura sin tracking; los commands cargarán agregados y confirmarán una sola transacción.

## API y errores

Los controladores se dividirán por responsabilidad y sus acciones por caso de uso. Api será responsable de contratos, autenticación HTTP, rate limiting y traducción a `ProblemDetails`. Application devolverá resultados y errores tipados, sin referencias a `IResult` ni status codes. Swagger/OpenAPI se expondrá únicamente en `Development`.

Las comprobaciones de permisos dependientes de campaña se ejecutarán dentro del handler además de las políticas HTTP. La identidad del actor formará parte del command o query cuando sea necesaria.

## Outbox

El hosted service se moverá a Infrastructure y solo realizará polling. Un caso de uso procesará cada mensaje. La adquisición será atómica y segura para varias instancias; la creación de invitación y mensaje se confirmará en la misma transacción; y el proveedor de correo seguirá aislado tras un puerto de Application.

## Observabilidad

Las métricas y logs se emitirán desde adaptadores o decoradores externos. Se conservarán los nombres útiles para dashboards cuando sea posible. Cualquier cambio de nombre deberá actualizar dashboards y documentación en la misma tarea. Ninguna señal incluirá tokens, direcciones privadas o contenido sensible.

## Verificación

- Unitarios de Domain para invariantes y estados.
- Unitarios de Application para handlers y autorización.
- Integración con PostgreSQL para EF, migraciones, constraints y concurrencia.
- Componente con `WebApplicationFactory` para compatibilidad HTTP.
- Contrato del adaptador Brevo mediante HTTP controlado.
- Architecture tests globales para módulos y específicos de Access para sus capas internas.
- Smoke tests y Docker Compose al finalizar.

## Despliegue y reversibilidad

No se modifica la topología. La imagen seguirá conteniendo un único host ASP.NET Core. Las migraciones deberán ser compatibles con el procedimiento actual. Antes de eliminar tipos antiguos se verificará que no forman parte de migraciones ya compiladas o que estas conservan las referencias necesarias.

Los cambios se integrarán en unidades pequeñas y reversibles. No se eliminará el camino anterior de una capacidad hasta que su slice nueva tenga cobertura equivalente y esté registrada en el host.

## Riesgos y mitigaciones

| Riesgo | Mitigación |
|---|---|
| Reescritura prolongada con dos diseños | Migración vertical y eliminación del endpoint antiguo en cada slice |
| Cambio accidental del contrato | Tests de caracterización y componente por endpoint |
| Pérdida de datos o migraciones rotas | Tests de base vacía y actualización desde la versión previa |
| Abstracciones ceremoniales | Puertos específicos y handlers centrados en casos de uso |
| Regresión de concurrencia | Tests PostgreSQL para bootstrap, invitaciones y outbox |
| Dependencias cruzadas reaparecen | Architecture tests obligatorios desde el inicio |
