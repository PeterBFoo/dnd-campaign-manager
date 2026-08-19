# Despliegue en Oracle Cloud Always Free

- Estado: preparado; pendiente de credenciales y recursos de la tenancy
- Alcance: infraestructura productiva del ADR-0001
- Objetivo de coste: 0 €, dentro de las cuotas Always Free y Grafana Cloud Free

## Topología

Una VM ARM `VM.Standard.A1.Flex` ejecuta la aplicación mediante `compose.deploy.yaml`. Caddy es la única entrada pública y termina HTTPS. PostgreSQL conserva sus datos en un volumen Docker sobre el volumen de arranque de OCI. OpenTelemetry Collector envía telemetría a Grafana Cloud; Grafana no se expone desde la VM.

La plantilla Terraform limita la instancia a un máximo de 2 OCPU, 12 GB de RAM y un volumen de arranque de hasta 150 GB. Estos límites deben contrastarse con la consola antes de aplicar porque las cuotas gratuitas pueden cambiar.

## 1. Preparar las cuentas

1. Crear una cuenta OCI y elegir cuidadosamente la región principal.
2. Crear una cuenta gratuita de Grafana Cloud.
3. En Grafana Cloud, abrir la conexión **OpenTelemetry** y obtener:
   - URL del gateway OTLP;
   - instance ID;
   - Cloud Access Policy token con permisos de escritura para métricas, logs y trazas.
4. Crear una cuenta de servicio de Grafana con permiso para escribir dashboards y conservar su token.

Los tokens y credenciales no deben copiarse a archivos versionados.

## 2. Autenticar Terraform

Instalar OCI CLI y Terraform, y configurar el perfil local estándar de OCI. El provider utiliza ese perfil; las claves privadas y el archivo `terraform.tfvars` están ignorados por Git.

```sh
cp infra/oci/terraform.tfvars.example infra/oci/terraform.tfvars
terraform -chdir=infra/oci init
terraform -chdir=infra/oci plan
terraform -chdir=infra/oci apply
```

El CIDR de SSH debe ser la IPv4 pública actual con sufijo `/32`; la configuración rechaza `0.0.0.0/0`. Si el compartment es la raíz, `compartment_ocid` coincide con `tenancy_ocid`.

Cloud-init instala Docker, desactiva login SSH por contraseña, crea `/opt/dnd-campaign-manager` y genera localmente la contraseña inicial de PostgreSQL. Espera a que exista `/var/lib/cloud/instance/dnd-campaign-bootstrap-complete` antes de desplegar.

## 3. Hostname HTTPS gratuito

Con la IP devuelta por Terraform se puede comenzar sin comprar un dominio. Para `203.0.113.10`, `203-0-113-10.sslip.io` resuelve a esa IP. Se usa ese valor como `APP_HOST`; Caddy obtiene un certificado público mediante ACME.

`sslip.io` es una dependencia gratuita sin SLA. Un dominio propio puede sustituirlo sin cambiar la aplicación.

## 4. Entorno de GitHub

Crear el environment `production` en GitHub y configurar:

| Tipo | Nombre | Contenido |
|---|---|---|
| Variable | `OCI_USER` | `ubuntu` |
| Variable | `APP_HOST` | hostname público sin protocolo |
| Variable | `ACME_EMAIL` | correo operativo para ACME |
| Variable | `GRAFANA_CLOUD_OTLP_ENDPOINT` | URL OTLP terminada en `/otlp` |
| Variable | `GRAFANA_URL` | URL del stack Grafana |
| Secreto | `OCI_HOST` | IPv4 pública de la VM |
| Secreto | `OCI_SSH_PRIVATE_KEY` | clave privada de despliegue |
| Secreto | `OCI_SSH_KNOWN_HOSTS` | línea verificada de `known_hosts` |
| Secreto | `GHCR_PULL_USER` | usuario con lectura de GHCR |
| Secreto | `GHCR_PULL_TOKEN` | token limitado a `read:packages` |
| Secreto | `GRAFANA_CLOUD_OTLP_AUTHORIZATION` | `Basic ` seguido de base64 de `instance-id:token` |
| Secreto | `GRAFANA_SERVICE_ACCOUNT_TOKEN` | token para publicar dashboards |

Se recomienda exigir aprobación manual para el environment `production`. La contraseña de PostgreSQL no se almacena en GitHub: nace y permanece en la VM.

## 5. Publicar

Ejecutar manualmente el workflow `deploy-oci` sobre el commit aprobado. El workflow:

1. construye y publica imágenes ARM64 inmutables en GHCR;
2. copia únicamente la configuración operativa necesaria;
3. instala el secreto OTLP como archivo protegido;
4. ejecuta `docker compose pull` y `up --wait`;
5. comprueba web, liveness, readiness y conexión PostgreSQL por HTTPS;
6. publica los dashboards en Grafana Cloud.

El script de despliegue rechaza imágenes etiquetadas como `latest`.

## 6. Validación y operación

```sh
BASE_URL=https://APP_HOST sh scripts/smoke-test.sh
ssh ubuntu@OCI_HOST 'cd /opt/dnd-campaign-manager/release && docker compose --env-file ../config/production.env -f compose.deploy.yaml ps'
```

Después del primer tráfico, comprobar en Grafana Cloud:

- disponibilidad y latencia HTTP;
- métricas de runtime .NET;
- `pg_up` y conexiones PostgreSQL;
- trazas del servicio `dnd-campaign-api`;
- logs correlacionados por `trace_id`.

## Riesgos pendientes antes de datos reales

- Always Free no ofrece SLA y una instancia ociosa puede ser reclamada.
- La VM es un único dominio de fallo.
- Debe automatizarse una copia cifrada de PostgreSQL hacia almacenamiento externo al volumen antes de implementar datos de campaña.
- Debe probarse la restauración, no solo la generación de backups.
