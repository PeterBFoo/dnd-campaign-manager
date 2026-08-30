# ADR-0010: Observabilidad PostgreSQL bajo demanda junto a la API

- Estado: Aceptado
- Fecha: 2026-08-30
- Decisores: equipo del proyecto
- Alcance: topología productiva de observabilidad PostgreSQL, escala de Azure Container Apps y coste operativo
- Especificación: [spec 023](../specs/023-observabilidad-postgresql-bajo-demanda/spec.md)
- Complementa: [ADR-0001](0001-monorepositorio-y-monolito-modular.md) y [ADR-0009](0009-broker-eventos-y-observabilidad-grafana.md)
- Revisa: [spec 022](../specs/022-exporter-postgresql-grafana-cloud/spec.md), únicamente en la unidad de despliegue y continuidad de las métricas PostgreSQL en producción

## Contexto

El spec 022 desplegó `postgres-exporter` y Grafana Alloy en la Container App privada `dnd-postgres-observability`. Los dos contenedores suman `0.5` vCPU y `1 GiB`, carecen de ingress y mantienen `minReplicas = 1` para publicar métricas PostgreSQL continuamente.

El análisis de Azure Cost Management del 2026-08-30 mostró que esa réplica permanece activa aunque la API esté escalada a cero. Tras consumirse la franquicia mensual compartida de Container Apps, el nuevo recurso generó aproximadamente `0,218 EUR` durante sus primeras horas. Con el patrón observado, su coste recurrente se estimó en `14–15 EUR/mes`; Blob Storage y Event Grid no explicaban el aumento.

El spec 021 ya eliminó el sondeo de correo y permite que `dnd-campaign-api` use `minReplicas = 0`. El tráfico web y el push HTTPS de Event Grid reactivan la API cuando existe trabajo. Mantener otra réplica durante todo el día contradice el objetivo de coste fijo cero de la topología serverless.

## Decisión

1. `postgres-exporter` y Grafana Alloy se ejecutarán como contenedores auxiliares de la misma Container App y la misma revisión que ASP.NET Core. Se retirará la Container App independiente `dnd-postgres-observability` después de verificar la revisión conjunta.
2. La Container App conservará ingress únicamente hacia ASP.NET Core, `minReplicas = 0`, `maxReplicas = 1` y la regla HTTP existente. Una petición de usuario o un push de Event Grid arrancará la réplica completa; al escalar la API a cero se detendrán también exporter y Alloy.
3. La asignación por réplica será inicialmente `0.25` vCPU y `0.5 GiB` para cada contenedor: `0.75` vCPU y `1.5 GiB` en total. Se revisará con métricas reales, sin reducir recursos a costa de inestabilidad o tiempos de arranque no aceptables.
4. `postgres-exporter` seguirá sin ingress y escuchará únicamente en loopback. Alloy conservará el scrape de `127.0.0.1:9187` y el envío OTLP HTTPS a Grafana Cloud.
5. El DSN de monitorización y la autorización de Grafana se trasladarán al almacén de secretos de `dnd-campaign-api`. No se incorporarán valores a Terraform, imágenes, logs, argumentos visibles del proceso ni al frontend.
6. La telemetría de ASP.NET Core continuará exportándose directamente a Grafana Cloud. El pipeline exporter → Alloy será complementario y no se convertirá en dependencia de liveness o readiness de la API. Una indisponibilidad de telemetría no debe impedir atender tráfico funcional.
7. Se acepta que las métricas de PostgreSQL tengan huecos cuando la aplicación esté escalada a cero. Los dashboards distinguirán `sin réplica` de `PostgreSQL no disponible` y no calcularán indisponibilidad durante intervalos deliberadamente inactivos.
8. La migración será en dos fases: primero se desplegará y validará la revisión conjunta manteniendo temporalmente el recurso anterior; después se retirará la Container App independiente y sus variables, outputs y secretos operativos. El rollback deberá poder restaurar la topología anterior hasta completar la verificación productiva.

## Alternativas consideradas

### Mantener la Container App independiente con una réplica mínima

Conserva métricas continuas y aísla fallos, pero mantiene el coste fijo que origina esta decisión. Se descarta para el volumen y los objetivos actuales.

### Poner la Container App independiente a cero sin añadir un activador

Al no tener ingress ni regla KEDA, la aplicación se apagaría y no recibiría ninguna señal para arrancar. No satisface el objetivo.

### Escalar una aplicación a partir de las réplicas de la otra

Requeriría una señal intermediaria, permisos y lógica operativa adicionales para observar el estado de la API. Añade una dependencia circular y más superficie de fallo que compartir la réplica. Se descarta.

### Ejecutar un Container Apps Job periódico

Eliminaría la réplica continua, pero convertiría un pipeline Prometheus de scrape en ejecuciones por lotes, introduciría huecos independientes del tráfico y requeriría rediseñar la recolección. Podrá reconsiderarse si se necesitan muestras periódicas durante la inactividad.

### Retirar las métricas PostgreSQL de producción

Es la opción de menor coste, pero elimina señales útiles de conexiones, bloqueos, transacciones y disponibilidad. Compartir el ciclo de vida conserva esas señales durante los periodos en que la aplicación realmente usa la base.

## Consecuencias

### Positivas

- Se elimina la reserva de cómputo permanente de observabilidad.
- API, exporter y Alloy comparten el mismo ciclo de actividad y escala a cero.
- El broker de eventos reactiva también la observabilidad antes de procesar la entrega.
- Se conserva el dashboard PostgreSQL y el transporte OTLP ya implementados.
- Desaparecen un recurso Azure, una variable de GitHub y un conjunto separado de secretos operativos.

### Costes y riesgos

- No habrá métricas PostgreSQL continuas mientras la API esté a cero.
- Cada activación reservará más CPU y memoria y puede aumentar el arranque en frío.
- La unidad de despliegue tendrá tres imágenes coordinadas y requerirá actualizar la plantilla completa de la revisión de forma atómica.
- Un fallo de descarga o configuración de un contenedor auxiliar puede degradar el despliegue conjunto, aunque no deba convertir la telemetría en requisito de readiness.
- Durante la migración coexistirán brevemente dos exporters; los dashboards deberán evitar interpretar series duplicadas como carga real.

## Verificación y revisión

La decisión se considerará aplicada cuando Azure muestre una sola Container App de aplicación, esta escale de cero a uno por tráfico HTTP y Event Grid, `pg_up` aparezca durante la ventana activa, vuelva a cero tras el enfriamiento y Cost Management deje de atribuir consumo a `dnd-postgres-observability`.

Se abrirá un ADR sustituto si la ausencia de métricas durante la inactividad oculta incidentes relevantes, si la API permanece activa la mayor parte del día, si los auxiliares deterioran de forma material el arranque o la disponibilidad, o si aparece una alternativa administrada con mejor coste y aislamiento.

## Referencias

- [Facturación de Azure Container Apps](https://learn.microsoft.com/azure/container-apps/billing)
- [Escalado de Azure Container Apps](https://learn.microsoft.com/azure/container-apps/scale-app)
- [Contenedores múltiples en Azure Container Apps](https://learn.microsoft.com/azure/container-apps/containers)
