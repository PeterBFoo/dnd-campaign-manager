# Secretos de despliegue

- Estado: preparado para Oracle Cloud y GitHub Environments
- Alcance: credenciales de infraestructura del ADR-0001

Los valores de `.env` son únicamente para desarrollo local. Producción no copia ese archivo ni proporciona secretos como argumentos de build, variables Angular o valores versionados.

PostgreSQL utiliza la contraseña de entorno solo durante la primera inicialización del volumen. Modificar el secreto con un volumen existente no rota la credencial de la base de datos; la rotación debe ejecutarse explícitamente dentro de PostgreSQL antes de actualizar sus consumidores.

## Secretos del host OCI

Se almacenan bajo `/opt/dnd-campaign-manager/secrets`, propiedad del usuario de despliegue, directorio `0700` y archivos `0600`:

| Archivo | Consumidor | Origen |
|---|---|---|
| `postgres_password` | PostgreSQL, API y exporter | generado en la VM por cloud-init |
| `grafana_cloud_otlp_authorization` | OpenTelemetry Collector | instalado desde el environment protegido de GitHub |

`grafana_cloud_otlp_authorization` contiene el valor completo del header: `Basic ` seguido del base64 de `instance-id:token`. El Collector lo resuelve desde el archivo mediante su provider de configuración y no lo incorpora a `compose.deploy.yaml`.

## Secretos y variables de GitHub

El environment `production` conserva únicamente:

- acceso SSH a la VM y su `known_hosts` verificado;
- credencial de solo lectura para descargar imágenes privadas de GHCR;
- autorización de escritura OTLP en Grafana Cloud;
- token de cuenta de servicio para publicar dashboards.

Host, hostname público, correo ACME y URLs de Grafana son configuración, no secretos. La contraseña de PostgreSQL nunca sale de OCI.

Los permisos deben ser mínimos:

- `GHCR_PULL_TOKEN`: solo `read:packages`;
- Cloud Access Policy: escritura de métricas, logs y trazas del stack seleccionado;
- cuenta de servicio Grafana: escritura de carpetas y dashboards, sin administración de usuarios;
- clave SSH exclusiva para despliegue, sin reutilizar la clave personal del operador.

## Invariantes

- No se registran valores de secretos en Actions, Compose, Terraform o logs de aplicación.
- No se guardan secretos en `terraform.tfvars` ni en el estado Terraform.
- Las imágenes se construyen sin secretos y solo reciben configuración al arrancar.
- El frontend no recibe tokens, credenciales de base de datos ni endpoints administrativos.
- `deploy/secrets/`, `*.tfvars`, estados Terraform y claves están ignorados por Git y Docker.

## Rotación

La contraseña de PostgreSQL exige coordinar `ALTER ROLE` con la actualización atómica del archivo y el reinicio de API/exporter. La autorización OTLP y el token de dashboards pueden rotarse de manera independiente desde Grafana Cloud y GitHub.

Después de cada rotación se ejecutan readiness, smoke test y una comprobación de recepción de telemetría. Las credenciales anteriores se revocan solo después de validar las nuevas.

Cuando se implemente autenticación de usuarios se añadirán secretos independientes para firma o cifrado de sesión. No se reutilizará ninguna credencial de infraestructura.
