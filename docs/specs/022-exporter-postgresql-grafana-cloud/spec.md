# Spec 022: métricas de PostgreSQL en Grafana Cloud

- Estado: Implementada; pendiente de verificación productiva
- Fecha: 2026-08-29
- Tipo: incremento técnico vertical de observabilidad
- Roadmap: [roadmap funcional](../../roadmap/product-roadmap.md), infraestructura de observabilidad de ADR-0001
- Requisitos principales: no amplía requisitos funcionales `RF-*`; cubre la observabilidad operativa de PostgreSQL
- Dependencias: [ADR-0001](../../adr/0001-monorepositorio-y-monolito-modular.md), [spec 011](../011-eliminacion-campanas/spec.md)
- Evolución aceptada: el [spec 023](../023-observabilidad-postgresql-bajo-demanda/spec.md) conserva este pipeline y sustituirá su Container App permanente por contenedores auxiliares de la API cuando complete su migración productiva

## Problema

La topología local tiene un `postgres-exporter` que Prometheus consulta dentro de Docker Compose, pero la topología productiva envía la telemetría de la API directamente a Grafana Cloud y no despliega ningún scraper ni exporter para la base PostgreSQL externa de Neon. Por ello, el dashboard PostgreSQL no recibe `pg_up`, estadísticas de bases de datos ni métricas de bloqueos.

## Objetivo

Publicar métricas operativas de Neon en Grafana Cloud de forma continua, privada y reproducible, sin exponer el endpoint Prometheus del exporter ni copiar credenciales al repositorio, al frontend o a una imagen.

## Decisiones aceptadas

1. Azure Container Apps ejecutará una Container App sin ingress con dos contenedores estrechamente acoplados: `postgres-exporter` y Grafana Alloy.
2. El exporter usará `DATA_SOURCE_NAME` referenciado desde un secreto de Container Apps. El valor es la misma URI PostgreSQL con TLS que ya consume la API.
3. Alloy consultará `127.0.0.1:9187`, convertirá las métricas Prometheus a OTLP y las enviará al endpoint OTLP HTTPS de Grafana Cloud.
4. La autorización de Alloy se derivará durante el despliegue del secreto existente `GRAFANA_CLOUD_OTLP_HEADERS`; nunca se escribe el valor derivado en Git, Terraform ni una imagen.
5. El contenedor Alloy se construirá con una configuración versionada y una etiqueta inmutable por commit. El exporter conserva una versión fija y actualizable de forma independiente.
6. El Compose local seguirá usando Prometheus LGTM para scrapear el exporter dentro de la red privada.

## Alcance

- Aprovisionar la Container App productiva del exporter y Alloy en el entorno Azure existente.
- Construir y publicar la imagen Alloy de observabilidad desde GitHub Actions.
- Instalar el DSN PostgreSQL y la autorización de Grafana como secretos de Container Apps.
- Configurar el pipeline scrape Prometheus → OTLP y conservar las etiquetas necesarias para el dashboard PostgreSQL.
- Añadir validaciones de formato de Alloy, Compose y Terraform, además de documentación operativa y de secretos.

## Ownership

- `infra/observability`: configuración del exporter, Alloy y dashboards.
- `infra/azure` y `scripts/deploy-azure.sh`: recurso productivo, secretos y actualización de revisiones.
- `.github/workflows/deploy-azure.yml`: publicación de la imagen Alloy y coordinación del despliegue.
- `apps/api`: no cambia su código; sigue siendo consumidor del mismo DSN y productor directo de telemetría OTLP. La nueva Container App comparte únicamente la frontera operativa de despliegue.
- `apps/web`: no cambia; no debe conocer ni consultar métricas de infraestructura.

## Seguridad y privacidad

- La Container App del exporter no tendrá ingress ni puertos publicados.
- El DSN y la autorización de Grafana solo se referencian como secretos en tiempo de ejecución.
- Neon se consulta con TLS obligatorio según la URI de producción.
- No se exportan nombres de campañas, usuarios, personajes ni contenido editorial; solo las métricas estándar de PostgreSQL.
- El usuario de monitorización debe tener privilegios mínimos de lectura cuando el proveedor lo permita.

## Criterios de aceptación

1. Terraform describe una Container App sin ingress con exporter y Alloy, ambos con una réplica mínima y recursos válidos para Consumption.
2. El exporter tiene un destino PostgreSQL explícito y Alloy tiene un target scrapeable `127.0.0.1:9187`.
3. Alloy reenvía métricas por OTLP HTTP con el endpoint y autorización proporcionados durante el despliegue.
4. El workflow publica la imagen Alloy con el SHA del commit y actualiza la Container App sin imprimir secretos.
5. Compose local y Compose de despliegue siguen siendo válidos; la configuración existente de Grafana local continúa operativa.
6. Tras el despliegue, Grafana Cloud puede consultar `pg_up` y las series usadas por `dnd-postgresql`, incluyendo `pg_stat_database_numbackends`.
7. La documentación identifica las variables nuevas, el flujo de rotación y la comprobación posterior en Grafana.

## Fuera de alcance

- Cambiar el esquema, consultas o migraciones de la aplicación.
- Crear una base PostgreSQL dentro de Azure o habilitar ingress para el exporter.
- Configurar alertas, SLO o sondas sintéticas de Grafana Cloud.
- Incorporar métricas del proveedor Neon que no estén expuestas por PostgreSQL exporter.

## Validación

La implementación queda verificada estáticamente con `terraform fmt`, sintaxis del script, YAML de Compose/workflows, JSON de dashboards y revisión de que el pipeline contiene el target y el exporter esperados. `alloy validate`, la validación efectiva de Compose/Terraform y la comprobación productiva de `pg_up` y `pg_stat_database_numbackends` quedan para CI/despliegue porque este entorno no tiene acceso al daemon Docker ni a `registry.terraform.io`.
