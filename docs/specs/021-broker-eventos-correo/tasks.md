# Tareas 021: Broker de eventos y entrega asíncrona de correo

- Estado: Implementación completada; verificación externa pendiente
- Especificación: [spec.md](spec.md)
- Plan: [plan.md](plan.md)
- ADR: [ADR-0009](../../adr/0009-broker-eventos-y-observabilidad-grafana.md)

## Contrato y Access

- [x] Añadir el contrato CloudEvents `access.invitation-email.requested.v1` y su serialización segura.
- [x] Añadir el puerto y adaptador de publicación Event Grid con identidad administrada.
- [x] Reutilizar el ledger del outbox por `CloudEvent.id` y adaptar los handlers de emisión/reenvío.
- [x] Extraer la entrega del worker a un caso de uso invocable por HTTP.
- [x] Añadir autenticación Entra y endpoint interno de entrega.
- [x] Eliminar `InvitationOutboxWorker`, `BackgroundService` y `Email__OutboxWorkerEnabled`.

## Azure y despliegue

- [x] Aprovisionar custom topic Basic, suscripción filtrada, dead letter y roles mínimos.
- [x] Documentar la configuración de App Registration/rol de aplicación para el webhook.
- [x] Ajustar Container Apps a escala mínima cero y actualizar Compose/scripts.
- [x] Mantener pendientes del outbox como ledger, ofrecer replay único durante la transición y documentar rollback.

## Observabilidad

- [x] Añadir métricas, spans y logs acotados sin datos sensibles.
- [x] Crear y provisionar dashboard `dnd-event-broker` en Grafana.
- [x] Documentar la configuración de Azure Monitor como datasource de lectura y alertas/runbook.

## Pruebas y cierre

- [ ] Cubrir contrato, concurrencia, duplicados, fallos, autenticación y migración.
- [ ] Ejecutar suites API/frontend, builds, Terraform, Compose y smoke Azure.
- [x] Actualizar documentación de operación, arquitectura y evidencias del incremento.
