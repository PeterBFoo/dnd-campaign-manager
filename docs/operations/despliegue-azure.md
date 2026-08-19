# Despliegue gratuito en Azure

- Estado: infraestructura aprovisionada; publicación automatizada mediante GitHub Actions
- ADR relacionado: [ADR-0001](../adr/0001-monorepositorio-y-monolito-modular.md)
- Alcance: infraestructura y plataforma sin datos funcionales ni contenido de campaña

## Topología y límites de coste

Terraform crea solamente un grupo de recursos, un entorno Azure Container Apps Consumption y una Container App Linux. La API queda limitada a `0.25` vCPU, `0.5 GiB`, cero réplicas en reposo y una como máximo. No se crea VM, Azure Container Registry, IP pública dedicada, Log Analytics ni PostgreSQL de Azure.

Angular se publica en GitHub Pages, PostgreSQL reside en Neon Free y la telemetría se envía a Grafana Cloud Free. Los tres proveedores aplican cuotas y los planes gratuitos no tienen SLA. Debe existir un presupuesto Azure con avisos antes de habilitar cualquier recurso adicional.

## 1. Requisitos de las cuentas

1. Activar una suscripción Azure y comprobarla con `az account show`.
2. Crear un proyecto Neon Free en una región europea y copiar su cadena de conexión con TLS obligatorio.
3. Crear un stack Grafana Cloud Free y una política con escritura OTLP para métricas, logs y trazas.
4. Crear una cuenta de servicio Grafana con rol `Editor` para publicar los dashboards versionados.
5. En GitHub, configurar Pages con **GitHub Actions** como origen.
6. Hacer público el paquete `dnd-campaign-api` de GHCR después de su primera publicación; la Container App no conserva un token de registro.

## 2. Aprovisionar Azure

```sh
az login
az account set --subscription '<subscription-id>'
az provider register --namespace Microsoft.App
terraform -chdir=infra/azure init
terraform -chdir=infra/azure plan -out=dnd-campaign-manager.tfplan
terraform -chdir=infra/azure apply dnd-campaign-manager.tfplan
terraform -chdir=infra/azure output
```

El estado y los planes son locales, contienen identificadores operativos y están ignorados por Git. Los secretos no son variables Terraform.

## 3. Identidad de GitHub Actions

Se crea una aplicación de Microsoft Entra con credencial federada limitada al environment GitHub `production`. Su principal recibe `Contributor` únicamente sobre `dnd-campaign-manager-production`. GitHub almacena como variables, no como secretos:

- `AZURE_CLIENT_ID`
- `AZURE_TENANT_ID`
- `AZURE_SUBSCRIPTION_ID`
- `AZURE_RESOURCE_GROUP=dnd-campaign-manager-production`
- `AZURE_CONTAINER_APP=dnd-campaign-api`
- `GRAFANA_CLOUD_OTLP_ENDPOINT`
- `API_BASE_URL`, con el output `api_url` de Terraform

La configuración federada utiliza el sujeto:

```text
repo:PeterBFoo/dnd-campaign-manager:environment:production
```

## 4. Secretos de producción

El environment protegido `production` contiene:

- `DATABASE_CONNECTION_STRING`: cadena Npgsql de Neon con `SSL Mode=Require`;
- `GRAFANA_CLOUD_OTLP_HEADERS`: `Authorization=Basic%20<credencial-base64>`.
- `GRAFANA_SERVICE_ACCOUNT_TOKEN`: token Grafana con rol `Editor` para crear la carpeta y actualizar dashboards.

El workflow instala ambos como secretos de Container Apps y los referencia desde variables de entorno. No se imprimen, no llegan al frontend y no forman parte de Terraform.

## 5. Publicación y verificación

1. Ejecutar manualmente `deploy-azure` desde GitHub Actions.
2. Verificar que `scripts/smoke-test-api.sh` confirma liveness, readiness y conexión PostgreSQL.
3. Ejecutar `deploy-pages`, o integrar el cambio en `main`, para publicar Angular.
4. Abrir `https://peterbfoo.github.io/dnd-campaign-manager/` y comprobar el estado operativo.
5. Confirmar en Grafana que llegan logs, métricas y trazas y que el mismo workflow ha actualizado los dashboards versionados.

La API puede tardar varios segundos en responder a la primera petición después de escalar a cero. Los smoke tests reintentan liveness para contemplar ese arranque en frío.

## Recuperación y retirada

Las revisiones de Container Apps permiten volver a una imagen anterior. La base de datos debe contar con un procedimiento probado de exportación y restauración antes de aceptar datos reales. Si se abandona Azure, `terraform destroy` se ejecutará únicamente tras verificar el grupo de recursos exacto y conservar las copias necesarias.
