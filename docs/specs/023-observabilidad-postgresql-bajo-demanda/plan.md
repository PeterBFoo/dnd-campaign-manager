# Plan 023: observabilidad PostgreSQL bajo demanda

- Estado: Ejecutado; seguimiento diferido de coste y entrega funcional
- Fecha: 2026-08-30
- Especificación: [spec.md](spec.md)
- ADR: [ADR-0010](../../adr/0010-observabilidad-postgresql-bajo-demanda.md)

## Estrategia

La migración separará la activación de la nueva topología de la eliminación del recurso anterior. Primero se publicará una revisión multi-contenedor verificable; únicamente después de demostrar arranque desde cero, entrega de Event Grid y recepción de métricas se retirará `dnd-postgres-observability`.

## 1. Plantilla productiva de Container Apps

- Incorporar a `azurerm_container_app.api` los secretos `postgres-dsn` y `grafana-cloud-authorization` con placeholders seguros para el primer aprovisionamiento.
- Incorporar los contenedores `postgres-exporter` y `alloy` a la plantilla de la API, conservando imágenes fijadas, variables actuales y comunicación por loopback.
- Mantener ASP.NET Core como único destino de ingress y conservar mínimo cero, máximo uno y la regla HTTP actual.
- Verificar que la suma `0.75` vCPU y `1.5 GiB` es válida en el perfil Consumption de Spain Central.
- Retirar de la configuración final el recurso `azurerm_container_app.postgres_exporter`, `postgres_exporter_name` y `postgres_exporter_container_app_name`.

Como Terraform ignora actualmente cambios de `template` y `secret` para permitir revisiones inmutables desde GitHub Actions, la implementación deberá auditar ese ownership. La configuración inicial y las actualizaciones no pueden quedar divididas entre dos fuentes que produzcan plantillas incompatibles.

## 2. Despliegue atómico de la revisión

- Cambiar `scripts/deploy-azure.sh` para instalar en `dnd-campaign-api` los secretos de aplicación y observabilidad.
- Sustituir las actualizaciones parciales de dos Container Apps por la publicación declarativa de una única plantilla de revisión con los tres contenedores.
- Mantener la imagen de API y la imagen Alloy etiquetadas con el mismo SHA; fijar la versión del exporter.
- Eliminar `AZURE_POSTGRES_EXPORTER_APP` del contrato del script y de GitHub Actions cuando la migración haya concluido.
- Evitar que comandos, errores o artefactos temporales impriman los valores de secretos.

Antes de implementar se elegirá y probará el mecanismo de Azure CLI que conserva la plantilla multi-contenedor completa —por ejemplo una plantilla YAML versionada con referencias a secretos—; no se encadenarán actualizaciones que puedan eliminar accidentalmente otro contenedor de la revisión.

## 3. Semántica de salud y telemetría

- Mantener liveness y readiness en ASP.NET Core sin dependencias nuevas hacia exporter, Alloy o Grafana Cloud.
- Conservar en Alloy el target `127.0.0.1:9187` y el pipeline OTLP HTTPS existente.
- Añadir etiquetas de recurso estables que permitan identificar la revisión y detectar la coexistencia temporal de exporters sin dimensiones funcionales ni cardinalidad no acotada.
- Ajustar el dashboard y sus consultas para distinguir:
  - réplica de API igual a cero;
  - réplica activa sin scrape del exporter;
  - `pg_up == 0` con réplica activa;
  - fallo de exportación a Grafana Cloud.
- Revisar alertas para que la ausencia deliberada de series no se interprete como indisponibilidad continua de PostgreSQL.

## 4. Seguridad y secretos

- Reutilizar `DATABASE_CONNECTION_STRING` para derivar el DSN no pooler sin persistir el valor derivado.
- Reutilizar `GRAFANA_CLOUD_OTLP_HEADERS` para derivar la autorización de Alloy durante el despliegue.
- Mantener TLS obligatorio hacia Neon y HTTPS hacia Grafana Cloud.
- Confirmar que exporter y Alloy no tienen ingress ni puertos adicionales en la configuración externa.
- Mantener el principio de privilegios mínimos para el usuario PostgreSQL de monitorización.

## 5. Superficies de aplicación y datos

- `apps/api`: no requiere cambios de código. Se ejecutarán sus pruebas y smoke tests porque cambia su unidad de despliegue y arranque.
- `apps/web`: no requiere cambios. Se mantendrán build y pruebas para demostrar estabilidad del contrato público.
- PostgreSQL: no requiere migración ni cambios de esquema.
- Compose local: conservará el exporter y LGTM como servicios locales; solo se tocará si una validación compartida exige alinear una referencia no funcional.

## 6. Migración productiva

### Fase A: convivencia controlada

1. Publicar imágenes inmutables.
2. Instalar secretos de observabilidad en la Container App de la API.
3. Publicar la revisión de tres contenedores sin borrar todavía `dnd-postgres-observability`.
4. Ejecutar smoke tests de API, health y webhook de Event Grid.
5. Confirmar en Grafana `pg_up`, series de actividad y etiquetas de la nueva revisión.
6. Dejar que la API escale a cero y confirmar su reactivación mediante tráfico ordinario y Event Grid.

La convivencia será breve y estará identificada para no confundir dos scrapes con duplicación de carga PostgreSQL.

### Fase B: retirada

1. Conservar evidencia de las comprobaciones de la fase A.
2. Retirar la Container App independiente, eliminarla del estado local y retirar sus referencias de Terraform.
3. Eliminar la variable de GitHub `AZURE_POSTGRES_EXPORTER_APP` y cualquier secreto exclusivo del recurso retirado.
4. Verificar el inventario Azure y esperar el retraso de Cost Management antes de confirmar ausencia de nuevos cargos.
5. Actualizar estados del spec 022 y 023, roadmap, diagramas y runbooks con evidencia de producción.

### Rollback

- Antes de la fase B, dirigir el tráfico a la revisión anterior de la API y mantener la Container App independiente.
- Después de la fase B, revertir la configuración y aplicar Terraform para recrear el recurso independiente si la pérdida de diagnóstico o la estabilidad lo exige.
- La reversión no modifica datos ni contratos de Event Grid, por lo que no necesita rollback de PostgreSQL.

## 7. Verificación

### Estática y CI

- `terraform -chdir=infra/azure fmt -check` y `validate`.
- Sintaxis de `scripts/deploy-azure.sh` y validación del workflow.
- `docker compose config --quiet` y equivalente para `compose.deploy.yaml`.
- `alloy validate` sobre `infra/observability/alloy/config.alloy`.
- Validación JSON de dashboards.
- Build y pruebas de .NET y Angular existentes.

### Productiva

- Inventario exacto de Container Apps antes y después de la retirada.
- Réplicas: `0 → 1 → 0` por petición web y `0 → 1` por entrega de Event Grid.
- Health y respuesta funcional después de arranque en frío.
- `pg_up` y series PostgreSQL presentes solo durante la ventana activa.
- Ausencia de ingress o endpoint público para exporter y Alloy.
- Ausencia de nuevos cargos para `dnd-postgres-observability` tras el retraso de Cost Management.

## 8. Documentación afectada

- ADR e índices de decisiones.
- Índice de specs y trazabilidad del roadmap.
- Diagrama de despliegue productivo.
- Despliegue Azure, secretos y dashboards de observabilidad.
- Runbook de coste, escala a cero, huecos de métricas y rollback.

## Riesgos de ejecución

- Publicar una actualización parcial que retire sin querer un sidecar de la revisión.
- Hacer que un fallo de Alloy o exporter impida el arranque funcional.
- Borrar el recurso independiente antes de observar métricas desde la nueva revisión.
- Interpretar la coexistencia como duplicación real de conexiones o actividad.
- Declarar ahorro antes de que Cost Management complete la ingestión.

## Resultado productivo

La migración se ejecutó los días 2026-08-30 y 2026-08-31. `dnd-campaign-api` contiene los tres contenedores y conserva `minReplicas = 0`; `dnd-postgres-observability` ya no existe. La identidad de la API heredó `Monitoring Reader` sobre el topic antes de retirar la identidad anterior. El arranque desde cero por Event Grid quedó demostrado, aunque la primera validación agotó su ventana y respondió `503`; la política de reintentos del broker cubre ese arranque en frío. Cost Management y una entrega funcional real permanecen como comprobaciones diferidas.
