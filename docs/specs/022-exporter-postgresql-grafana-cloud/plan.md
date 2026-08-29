# Plan 022: métricas de PostgreSQL en Grafana Cloud

- Estado: Ejecutado; validación de CI/despliegue pendiente
- Fecha: 2026-08-29
- Especificación: [spec.md](spec.md)

## Estrategia

1. Añadir la configuración Alloy que scrapea el exporter local y publica métricas por OTLP HTTP.
2. Crear una imagen Alloy versionada en GitHub Actions.
3. Añadir a Terraform una Container App sin ingress con exporter y Alloy como sidecar.
4. Extender el script de despliegue para instalar secretos, derivar la cabecera de autorización y actualizar la imagen Alloy.
5. Actualizar Compose, documentación, diagramas y runbooks para distinguir topología local y productiva.
6. Validar configuración, sintaxis y referencias sin desplegar credenciales reales.

## Seguridad y operación

- El exporter y Alloy comparten red de loopback, pero no tienen endpoint externo.
- Terraform contiene solo valores placeholder para secretos; GitHub Actions los instala durante el despliegue.
- La configuración Alloy no contiene credenciales.
- La rotación vuelve a ejecutar el despliegue y fuerza una nueva revisión del agente.

## Verificación

- `docker compose config --quiet`.
- `docker compose -f compose.deploy.yaml config --quiet`.
- `terraform -chdir=infra/azure fmt -check` y `validate`.
- `alloy validate` sobre `config.alloy`, mediante la imagen versionada.
- `jq empty` sobre dashboards.
