# Secretos de despliegue

- Estado: preparado para integración con el gestor de secretos del entorno
- Alcance: credenciales de infraestructura del ADR-0001

Los valores de `.env` son únicamente para desarrollo local. Un despliegue no debe copiar ese archivo ni proporcionar secretos como argumentos de build, variables del frontend o valores versionados.

PostgreSQL utiliza la contraseña de entorno solo durante la primera inicialización del volumen. Modificar `POSTGRES_PASSWORD` con un volumen ya existente no rota la credencial de la base de datos y hará fallar el readiness de la API; la rotación debe ejecutarse explícitamente dentro de PostgreSQL antes de actualizar sus consumidores.

## Contrato de configuración

| Consumidor | Configuración no secreta | Secreto montado |
|---|---|---|
| PostgreSQL | `POSTGRES_DB`, `POSTGRES_USER` | `POSTGRES_PASSWORD_FILE=/run/secrets/postgres_password` |
| API ASP.NET Core | `Database__Host`, `Database__Port`, `Database__Name`, `Database__User` | `Database__Password_FILE=/run/secrets/postgres_password` |
| PostgreSQL exporter | `DATA_SOURCE_URI`, `DATA_SOURCE_USER` | `DATA_SOURCE_PASS_FILE=/run/secrets/postgres_password` |
| Grafana | `GF_SECURITY_ADMIN_USER` | `GF_SECURITY_ADMIN_PASSWORD__FILE=/run/secrets/grafana_admin_password` |

La API mantiene `ConnectionStrings__Campaigns` para desarrollo local y acepta el secreto por archivo en despliegue. El contenido de los archivos se lee al arrancar, se elimina el salto de línea final y nunca se devuelve en endpoints ni se registra.

## Secretos externos requeridos

`compose.deploy.yaml` espera dos secretos administrados fuera del repositorio:

- `dnd-postgres-password`: contraseña aleatoria y exclusiva de PostgreSQL, compartida con la API y el exporter dentro de la red privada.
- `dnd-grafana-admin-password`: contraseña aleatoria y exclusiva del administrador local de Grafana.

Los nombres pueden cambiarse mediante `POSTGRES_PASSWORD_SECRET_NAME` y `GRAFANA_ADMIN_PASSWORD_SECRET_NAME`. El procedimiento exacto de alta depende del orquestador o gestor elegido. Deben crearse antes del despliegue y montarse como archivos legibles solo por el proceso correspondiente.

## Despliegue

1. Publicar las imágenes `web` y `api` con tags inmutables o digest.
2. Crear los dos secretos externos en el entorno de destino.
3. Definir `WEB_IMAGE`, `API_IMAGE`, nombres de base de datos y nombres de secretos como configuración no sensible.
4. Validar la configuración con `docker compose -f compose.deploy.yaml config` sin imprimir el contenido de los secretos.
5. Desplegar y comprobar `/health/live`, `/health/ready` y la recepción de telemetría.

La plantilla de despliegue solo publica el puerto web. PostgreSQL, la API y los puertos OTLP permanecen en la red interna; Grafana se enlaza a loopback para requerir un túnel o proxy administrativo.

## Rotación

La contraseña de PostgreSQL exige coordinar el cambio en la base de datos con la actualización del secreto montado. Después se reinicia la API y se verifica readiness. La contraseña de Grafana puede rotarse de forma independiente. Las versiones antiguas deben revocarse después de validar las nuevas.

Cuando se implemente autenticación de usuarios habrá que añadir, como mínimo, secretos independientes para firma o cifrado de sesión. Esa decisión no forma parte de la infraestructura actual y no debe reutilizar ninguna de las credenciales anteriores.
