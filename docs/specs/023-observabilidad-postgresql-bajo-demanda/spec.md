# Spec 023: observabilidad PostgreSQL bajo demanda

- Estado: En implementación; despliegue y verificación productiva pendientes
- Fecha: 2026-08-30
- Tipo: incremento técnico vertical de coste y observabilidad
- Roadmap: [roadmap funcional](../../roadmap/product-roadmap.md), infraestructura transversal sin ampliación funcional
- Requisitos relacionados: no amplía requisitos `RF-*`; conserva la observabilidad operativa de las capacidades existentes
- Dependencias: [ADR-0001](../../adr/0001-monorepositorio-y-monolito-modular.md), [ADR-0009](../../adr/0009-broker-eventos-y-observabilidad-grafana.md), [ADR-0010](../../adr/0010-observabilidad-postgresql-bajo-demanda.md), [spec 021](../021-broker-eventos-correo/spec.md) y [spec 022](../022-exporter-postgresql-grafana-cloud/spec.md)

## Problema

El pipeline productivo de métricas PostgreSQL vive en una Container App sin ingress que contiene `postgres-exporter` y Grafana Alloy. Como no dispone de un activador, mantiene una réplica mínima para funcionar y reserva `0.5` vCPU y `1 GiB` las 24 horas, incluso cuando la API está escalada a cero.

Azure Cost Management confirmó el 2026-08-30 que Container Apps era el origen del incremento de coste. La API ya puede escalar a cero gracias al broker push del spec 021, pero la topología del spec 022 conserva un coste fijo independiente de la actividad funcional.

## Objetivo

Conservar las métricas PostgreSQL durante las ventanas en que la aplicación está operativa y eliminar su cómputo permanente, haciendo que ASP.NET Core, `postgres-exporter` y Alloy compartan una réplica de Container Apps que se activa por tráfico HTTP o Event Grid y escala conjuntamente a cero.

## Estado actual verificado

- `dnd-campaign-api` usa Consumption, ingress HTTPS, regla HTTP, `minReplicas = 0` y `maxReplicas = 1`.
- Event Grid entrega invitaciones mediante push HTTPS al endpoint interno de la API y puede reactivarla desde cero.
- `dnd-postgres-observability` no tiene ingress, contiene exporter y Alloy y mantiene `minReplicas = 1`.
- Alloy scrapea `127.0.0.1:9187` cada quince segundos y envía las métricas por OTLP HTTPS a Grafana Cloud.
- La API exporta su propia telemetría directamente a Grafana Cloud; no necesita Alloy para atender peticiones.
- La topología local de Compose usa un exporter separado dentro de la red local y no genera coste Azure.

## Alcance

### Unidad productiva

- Añadir `postgres-exporter` y Alloy como contenedores auxiliares de la revisión de `dnd-campaign-api`.
- Mantener ingress exclusivamente sobre el contenedor ASP.NET Core y conservar la regla HTTP con mínimo cero y máximo uno.
- Mantener inicialmente `0.25` vCPU y `0.5 GiB` por contenedor, con un total de `0.75` vCPU y `1.5 GiB` por réplica.
- Retirar la Container App independiente solo después de una verificación productiva satisfactoria.

### Despliegue e infraestructura

- Hacer que Terraform describa la topología final y deje de aprovisionar el recurso independiente, su variable y su output.
- Actualizar el despliegue para instalar los cuatro grupos de secretos necesarios en una única Container App y publicar una revisión coherente con las tres imágenes.
- Conservar etiquetas inmutables por commit para API y Alloy y fijar explícitamente la versión del exporter.
- Definir una secuencia de migración y rollback que evite una ventana sin API o sin capacidad de diagnóstico.

### Observabilidad y coste

- Conservar el scrape de loopback, el envío OTLP y las series requeridas por `dnd-postgresql`.
- Representar los huecos de métricas durante escala a cero como estado esperado, sin convertirlos automáticamente en caída de PostgreSQL.
- Verificar en Azure Monitor las transiciones de réplica y en Cost Management la desaparición del consumo atribuido al recurso retirado.
- Documentar un umbral de revisión si el tiempo activo conjunto vuelve a hacer más conveniente otra topología.

## Ownership y superficies

- `infra/azure` posee la plantilla multi-contenedor, secretos de plataforma, límites de escala y retirada del recurso antiguo.
- `infra/observability` conserva la configuración Alloy, los dashboards y la semántica de ausencia de series.
- `scripts/deploy-azure.sh` y `.github/workflows/deploy-azure.yml` poseen la publicación atómica de la revisión y la transición productiva.
- `apps/api` no cambia su código ni sus contratos HTTP. Su imagen sigue siendo el contenedor de ingress, pero su unidad productiva incorpora dos auxiliares que no participan en reglas de negocio ni readiness. Esta ausencia de cambios de código es deliberada.
- `apps/web` no cambia: no consume métricas, no recibe configuración nueva y sus contratos con la API permanecen estables. Esta ausencia es deliberada y debe verificarse manteniendo su build y pruebas verdes.
- PostgreSQL no cambia de esquema ni datos; el exporter continúa usando acceso de monitorización mediante TLS.

## Reglas operativas y de seguridad

1. Ningún endpoint de `postgres-exporter` o Alloy será accesible mediante ingress público.
2. El DSN y la autorización de Grafana solo se referenciarán como secretos de Container Apps.
3. El exporter no registrará el DSN y el workflow no imprimirá secretos ni plantillas renderizadas que los contengan.
4. La API seguirá atendiendo cuando Grafana Cloud no esté disponible; las sondas de los auxiliares no bloquearán readiness funcional.
5. El dashboard solo considerará `pg_up == 0` una señal de fallo cuando exista una réplica activa y se esperen scrapes.
6. La coexistencia temporal no se prolongará después de validar la nueva revisión; se etiquetarán las series lo suficiente para detectar duplicados durante la migración.
7. La eliminación productiva del recurso independiente requerirá evidencia previa de API saludable, activación por Event Grid y recepción de métricas.

## Migración y rollback

1. Preparar Terraform, plantilla de revisión, secretos y workflow sin retirar aún `dnd-postgres-observability` de Azure.
2. Desplegar la revisión conjunta y verificar health, webhook autenticado, envío de invitación y métricas PostgreSQL.
3. Confirmar que la revisión escala a cero y vuelve a activarse sin intervención manual.
4. Retirar la Container App independiente y limpiar sus variables y outputs después de conservar evidencia de la validación.
5. Si falla la revisión conjunta, restaurar la revisión anterior de la API y mantener o recrear el recurso de observabilidad separado antes de continuar.

## Criterios de aceptación

1. Terraform describe una sola Container App para API, exporter y Alloy, con ingress dirigido solo a ASP.NET Core, `minReplicas = 0` y `maxReplicas = 1`.
2. La revisión reserva inicialmente `0.25` vCPU y `0.5 GiB` por contenedor y Azure acepta la combinación total de Consumption.
3. Una petición web activa desde cero los tres contenedores y la API responde correctamente tras el arranque en frío.
4. Un evento de invitación entregado por Event Grid activa desde cero la misma revisión, procesa el correo y conserva las garantías del spec 021.
5. Durante una ventana activa, Alloy alcanza `127.0.0.1:9187`, Grafana Cloud recibe `pg_up` y el dashboard muestra las series PostgreSQL esperadas.
6. Después del periodo de enfriamiento sin tráfico, Azure Monitor muestra cero réplicas y deja de acumular uso de exporter y Alloy.
7. La indisponibilidad de Grafana Cloud o un fallo de exportación no hace fallar liveness, readiness ni los contratos funcionales de la API.
8. No existe ingress para exporter o Alloy y ningún secreto aparece en Git, imágenes, logs o salida del workflow.
9. La migración conserva una ruta de rollback y no elimina `dnd-postgres-observability` hasta completar la prueba productiva de la revisión conjunta.
10. Tras la retirada, Azure no contiene la Container App independiente y Cost Management deja de atribuirle nuevos cargos una vez superado el retraso normal de ingestión.
11. Los dashboards y runbooks distinguen escala a cero, fallo del exporter y fallo real de PostgreSQL; no alertan por ausencia esperada de series.
12. Terraform, scripts, workflow, Compose, configuración Alloy, dashboards, pruebas de API y pruebas/build web superan sus validaciones aplicables.
13. No cambian endpoints públicos, modelos de datos, permisos, comportamiento de invitaciones ni experiencia Angular.

## Fuera de alcance

- Cambiar de Neon, Grafana Cloud, Event Grid o Brevo.
- Introducir métricas continuas mientras toda la aplicación está escalada a cero.
- Rediseñar el dashboard PostgreSQL o añadir nuevas métricas funcionales.
- Cambiar el código de negocio, el esquema PostgreSQL o los contratos HTTP.
- Aumentar `maxReplicas` por encima de uno o diseñar la agregación de métricas para varias réplicas.
- Implementar un Container Apps Job, un scaler personalizado o un servicio externo de monitorización.
- Modificar la topología local de Docker Compose salvo ajustes necesarios para mantener sus validaciones.

## Condiciones de revisión

La decisión se revisará si la API permanece activa de forma sostenida, si los huecos impiden diagnosticar incidentes de base de datos, si el arranque conjunto incumple el objetivo operativo, si los auxiliares afectan a la disponibilidad funcional o si el coste medido deja de ser inferior al de una alternativa aislada o administrada.
