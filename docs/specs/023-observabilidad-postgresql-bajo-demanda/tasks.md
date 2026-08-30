# Tareas 023: observabilidad PostgreSQL bajo demanda

- Estado: Implementada en producción; seguimiento diferido pendiente
- Especificación: [spec.md](spec.md)
- Plan: [plan.md](plan.md)
- ADR: [ADR-0010](../../adr/0010-observabilidad-postgresql-bajo-demanda.md)

## Decisión y preparación

- [x] Registrar el diagnóstico de coste y aceptar la topología compartida en ADR-0010.
- [x] Aceptar el spec 023 y documentar plan, migración, rollback y criterios verificables.
- [x] Capturar inventario, configuración de réplicas y coste base inmediatamente antes de implementar.
- [x] Probar mediante CI y revisión productiva controlada el mecanismo Azure CLI para publicar una plantilla multi-contenedor completa.

## Terraform y plantilla de ejecución

- [x] Añadir a la Container App de la API los placeholders y referencias de secretos de PostgreSQL exporter y Alloy.
- [x] Añadir `postgres-exporter` y Alloy como auxiliares con versiones y recursos explícitos.
- [x] Mantener ingress exclusivo de ASP.NET Core, regla HTTP, mínimo cero y máximo uno.
- [x] Validar la combinación total `0.75` vCPU y `1.5 GiB` en Consumption.
- [x] Resolver y documentar el ownership de `template` y `secret` entre Terraform y GitHub Actions.
- [x] Retirar de la configuración final el recurso independiente, su variable y su output durante la fase B.

## Script y workflow

- [x] Trasladar los secretos de observabilidad a `dnd-campaign-api` sin imprimir valores.
- [x] Preparar la publicación de API, exporter y Alloy en una única revisión declarativa y coherente.
- [x] Mantener etiquetas inmutables por SHA para API y Alloy y versión fija del exporter.
- [x] Eliminar `AZURE_POSTGRES_EXPORTER_APP` del script y workflow.
- [x] Confirmar que `AZURE_POSTGRES_EXPORTER_APP` ya no existe en el environment GitHub `production`.
- [x] Añadir comprobaciones que fallen si falta un contenedor o límite de escala esperado.

## Salud, métricas y dashboards

- [x] Verificar estáticamente que exporter escucha solo en loopback y Alloy scrapea `127.0.0.1:9187`.
- [x] Confirmar que liveness y readiness no dependen de exporter, Alloy ni Grafana Cloud.
- [x] Etiquetar la nueva fuente para detectar series duplicadas durante la convivencia.
- [x] Ajustar dashboards para representar la ausencia esperada de datos durante escala a cero.
- [ ] Ajustar alertas para no tratar la ausencia esperada de series como una caída continua.
- [x] Documentar la interpretación de huecos y el procedimiento de diagnóstico.

## Validación automática

- [x] Ejecutar formato y validación de Terraform.
- [x] Validar sintaxis del script, renderizado de plantilla y workflows de despliegue.
- [x] Validar Compose local y de despliegue.
- [x] Validar los dashboards JSON.
- [x] Validar la configuración Alloy en CI mediante Docker.
- [x] Ejecutar build y pruebas de API.
- [x] Ejecutar build y pruebas web para confirmar que el contrato no cambia.

## Migración productiva: fase A

- [x] Publicar la revisión de tres contenedores manteniendo temporalmente `dnd-postgres-observability`.
- [x] Verificar health, contratos públicos y arranque en frío mediante una petición web.
- [ ] Verificar una entrega funcional completa mediante Event Grid; la activación `0 → 1` y el webhook autenticado quedaron probados, con un primer `503` cubierto por reintentos.
- [ ] Confirmar en Grafana `pg_up` y las series PostgreSQL de la nueva revisión.
- [x] Confirmar en Azure la transición `1 → 0 → 1` mediante enfriamiento y handshake de Event Grid.
- [x] Conservar evidencia y mantener el recurso anterior como rollback hasta superar la puerta de retirada.

## Migración productiva: fase B

- [x] Retirar de Azure y del estado Terraform local la Container App independiente después de aceptar la evidencia de fase A.
- [x] Eliminar variables, outputs y referencias operativas exclusivas del recurso retirado.
- [x] Confirmar que Azure conserva únicamente la Container App conjunta esperada.
- [x] Eliminar la segunda fuente del exporter retirando su Container App.
- [ ] Esperar la ingestión de Cost Management y confirmar que no aparecen cargos nuevos para el recurso retirado.

## Cierre

- [x] Actualizar el diagrama de despliegue, secretos, runbooks y documentación Azure con la topología efectiva.
- [x] Actualizar el spec 022 para indicar que su unidad productiva fue sustituida, conservando el pipeline de métricas.
- [ ] Marcar el spec 023 completado únicamente con evidencia de CI, producción, escala y coste.
- [x] Actualizar el índice de specs y la trazabilidad del roadmap con el resultado verificado.
