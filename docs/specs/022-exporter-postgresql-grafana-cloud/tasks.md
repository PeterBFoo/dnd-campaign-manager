# Tareas 022: métricas de PostgreSQL en Grafana Cloud

- Estado: Implementado; validación de CI/despliegue pendiente
- Especificación: [spec.md](spec.md)
- Plan: [plan.md](plan.md)

## Pipeline de métricas

- [x] Configurar `postgres-exporter` como destino local de scrape.
- [x] Configurar Alloy para convertir y enviar métricas PostgreSQL por OTLP HTTPS.
- [x] Construir la imagen Alloy con configuración versionada e inmutable.

## Azure y despliegue

- [x] Aprovisionar la Container App sin ingress con los dos contenedores.
- [x] Referenciar DSN y autorización mediante secretos.
- [x] Actualizar GitHub Actions y el script de despliegue.

## Documentación y cierre

- [x] Actualizar secretos, despliegue, arquitectura y dashboards.
- [x] Validar sintaxis disponible de Compose/workflows, Terraform fmt, scripts y dashboards.
- [ ] Validar Compose y Alloy con Docker, y Terraform validate, en CI.
- [x] Registrar cómo comprobar `pg_up` y la actividad de PostgreSQL en Grafana Cloud.
