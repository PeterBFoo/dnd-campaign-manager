# Despliegue en Azure con coste acotado

- Estado: infraestructura aprovisionada; publicación automatizada mediante GitHub Actions
- ADR relacionado: [ADR-0001](../adr/0001-monorepositorio-y-monolito-modular.md)
- Alcance: infraestructura y plataforma sin datos funcionales ni contenido de campaña

## Topología y límites de coste

Terraform crea un grupo de recursos, un entorno Azure Container Apps Consumption, una Container App Linux y una cuenta StorageV2 Standard LRS con contenedor privado para retratos. La API queda limitada a `0.25` vCPU, `0.5 GiB` y una réplica como máximo. No se crea VM, Azure Container Registry, IP pública dedicada, Log Analytics ni PostgreSQL de Azure.

Angular se publica en GitHub Pages, PostgreSQL reside en Neon Free y la telemetría se envía a Grafana Cloud Free. Azure Blob Storage se factura por capacidad y operaciones, aunque el volumen inicial sea pequeño; debe existir un presupuesto Azure con avisos. Los planes gratuitos no tienen SLA.

## 1. Requisitos de las cuentas

1. Activar una suscripción Azure y comprobarla con `az account show`.
   Registrar `Microsoft.Storage` además de `Microsoft.App`.
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
az provider register --namespace Microsoft.Storage
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
- `CHARACTER_STORAGE_SERVICE_URI`, con el output Terraform del mismo nombre;
- `EVENTGRID_TOPIC_ENDPOINT`, con el output `eventgrid_topic_endpoint` de Terraform;
- `EVENTGRID_AUDIENCE`, con la audiencia de la App Registration del webhook;
- `API_BASE_URL`, con el output `api_url` de Terraform

La configuración federada utiliza el sujeto:

```text
repo:PeterBFoo@91556382/dnd-campaign-manager@1339476932:environment:production
```

GitHub emite el sujeto con los identificadores inmutables del propietario y del repositorio. Esto evita que un cambio de nombre altere la identidad y debe coincidir exactamente con la credencial federada de Entra.

## 4. Secretos de producción

El environment protegido `production` contiene:

- `DATABASE_CONNECTION_STRING`: cadena Npgsql de Neon con `SSL Mode=Require`;
- `GRAFANA_CLOUD_OTLP_HEADERS`: `Authorization=Basic%20<credencial-base64>`.
- `GRAFANA_SERVICE_ACCOUNT_TOKEN`: token Grafana con rol `Editor` para crear la carpeta y actualizar dashboards.

El workflow instala ambos como secretos de Container Apps y los referencia desde variables de entorno. No se imprimen, no llegan al frontend y no forman parte de Terraform.

## 5. Publicación y verificación

1. Integrar el cambio en `main` y esperar a que el workflow `ci` termine correctamente.
2. Comprobar que la finalización satisfactoria de `ci` inicia automáticamente `deploy-azure` con el mismo commit validado. El disparo manual se conserva para recuperación operativa.
3. Verificar que `scripts/smoke-test-api.sh` confirma liveness, readiness y conexión PostgreSQL.
4. Esperar a que `deploy-pages`, también iniciado al integrar en `main`, publique Angular.
5. Abrir `https://peterbfoo.github.io/dnd-campaign-manager/` y comprobar el estado operativo.
6. Confirmar en Grafana que llegan logs, métricas y trazas y que el mismo workflow ha actualizado los dashboards versionados.
7. Crear un personaje con imagen y comprobar que el blob permanece privado y que otro jugador no puede modificarlo.

## Imágenes y recuperación

La cuenta de almacenamiento usa LRS, acceso público deshabilitado y retención lógica de borrados durante siete días. La identidad administrada de la Container App tiene `Storage Blob Data Contributor` limitado a esa cuenta. La restauración funcional debe coordinar el snapshot/export de PostgreSQL con los blobs referenciados; antes de una recuperación masiva se ensaya en un entorno aislado y se comprueban objetos huérfanos o metadatos sin blob.

La API puede tardar varios segundos en responder a la primera petición después de escalar a cero. Los smoke tests reintentan liveness para contemplar ese arranque en frío.

## Recuperación y retirada

Las revisiones de Container Apps permiten volver a una imagen anterior. La base de datos debe contar con un procedimiento probado de exportación y restauración antes de aceptar datos reales. Si se abandona Azure, `terraform destroy` se ejecutará únicamente tras verificar el grupo de recursos exacto y conservar las copias necesarias.
