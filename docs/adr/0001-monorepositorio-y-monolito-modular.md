# ADR-0001: Monorepositorio, plataforma web y observabilidad

- Estado: Aceptado
- Fecha: 2026-08-19
- Decisores: equipo del proyecto
- Alcance: estructura del repositorio, frontend, backend, datos, ejecución y operación

## Contexto

El producto debe servir a dos experiencias estrechamente relacionadas: una consola de dirección para el DM y una vista segura para jugadores. Ambas necesitan una API común, persistencia transaccional, control de acceso y una forma reproducible de ejecutar y diagnosticar el sistema desde el inicio.

El repositorio parte de documentación arquitectónica, especificaciones y análisis privados, sin una plataforma de aplicación previa. Su código residirá en un repositorio público, mientras que módulos editoriales, recursos de campaña, credenciales, datos persistentes y copias de seguridad permanecerán fuera. Antes de profundizar en los dominios funcionales necesitamos fijar una base que permita construir flujos verticales, comprobarlos en integración y observar su comportamiento.

Las restricciones acordadas son:

- el frontend se implementará con Angular;
- el backend se implementará en C# con ASP.NET Core;
- los secretos del DM deben filtrarse antes de llegar al navegador;
- las fuentes editoriales protegidas no se publicarán ni se incluirán en imágenes desplegables;
- el entorno local debe ser reproducible y aproximarse a producción sin exigir servicios SaaS.

El proyecto seguirá un flujo Spec-Driven Development. Especificaciones, planes, tareas, decisiones, implementación, pruebas y configuración operativa deberán evolucionar de forma coordinada.

## Decisión

### 1. Monorepo y monolito modular

Mantendremos frontend, backend, pruebas, documentación e infraestructura en un único repositorio:

```text
apps/
  web/                  Angular
  api/                  ASP.NET Core
  api-tests/            pruebas del backend
docs/
  adr/                  decisiones arquitectónicas
infra/                  configuración operativa adicional
sources/                fuentes internas excluidas del despliegue
```

El backend comienza como monolito modular. Los límites entre dominio, aplicación, infraestructura y endpoints se conservarán dentro de la solución, pero no se desplegarán microservicios hasta que exista una necesidad medible de escalado, aislamiento o propiedad independiente.

### 2. Frontend Angular 22

La aplicación web utilizará Angular 22 en modo estricto, componentes standalone, routing, signals para estado local y `HttpClient` para hablar con la API.

Principios iniciales:

- dos áreas de navegación: DM y jugadores;
- guards y permisos en cliente solo como ayuda de UX, nunca como frontera de seguridad;
- configuración pública del endpoint de API en tiempo de ejecución y ruta relativa `/api` por defecto;
- accesibilidad y diseño responsive como requisitos de los componentes;
- pruebas unitarias con el runner integrado del workspace Angular;
- build estático servido por Nginx en la imagen local integrada.

### 3. Backend ASP.NET Core 10 LTS

La API utilizará .NET 10 LTS y C# con nullable reference types habilitado. ASP.NET Core expondrá endpoints versionados bajo `/api/v1` y health checks separados:

- `/health/live`: el proceso puede atender peticiones;
- `/health/ready`: las dependencias necesarias están disponibles.

Las capas son:

- **Domain**: agregados, eventos, invariantes y proyecciones, sin dependencias de ASP.NET o EF Core;
- **Application**: casos de uso, autorización y puertos;
- **Infrastructure**: PostgreSQL, telemetría y adaptadores;
- **Endpoints**: contratos HTTP y composición de la aplicación.

El contrato HTTP será JSON y se versionará desde el principio. Los errores públicos seguirán `ProblemDetails` y nunca incluirán excepciones o datos secretos.

### 4. PostgreSQL como persistencia primaria

PostgreSQL será la fuente de persistencia del producto y Entity Framework Core el mecanismo de acceso. Este ADR establece la conexión, el health check y la capacidad transaccional, pero no anticipa el modelo de dominio ni su forma concreta de almacenamiento.

Los tests de integración que dependan de persistencia usarán PostgreSQL real en contenedor. Las decisiones posteriores de dominio deberán conservar las garantías transaccionales que requieran sus invariantes.

### 5. Seguridad y autenticación

La arquitectura será same-origin en el despliegue local integrado: Nginx servirá Angular y redirigirá `/api` al backend. La validación productiva inicial separa el origen estático de GitHub Pages y la API serverless, con una lista CORS exacta y únicamente endpoints públicos de plataforma. Antes de publicar identidad o datos de campaña se deberá recuperar same-origin mediante un dominio y proxy controlados, o aceptar expresamente mediante otro ADR un diseño de autenticación entre orígenes.

ASP.NET Core será la autoridad para sesiones, roles y políticas. Los roles iniciales son `dm` y `player`, siempre limitados a una campaña. La implementación completa de identidad se hará en un incremento específico antes de publicar endpoints con información de campaña; hasta entonces solo se expondrán estado de plataforma y health checks sin datos sensibles.

Los secretos se inyectarán por variables o un gestor externo. Los valores locales de Compose son solo de desarrollo y nunca se reutilizarán en producción.

### 6. Observabilidad con OpenTelemetry

El backend emitirá las tres señales correlacionadas:

- trazas de ASP.NET Core y clientes HTTP;
- métricas de runtime, proceso y peticiones;
- logs estructurados con `trace_id`, `span_id`, nivel y propiedades, sin contenido narrativo sensible.

Las señales se exportarán mediante OTLP, evitando instrumentación específica de proveedor. El entorno local utilizará la imagen oficial de desarrollo `grafana/otel-lgtm`, que integra OpenTelemetry Collector, Prometheus, Tempo, Loki y Grafana. Producción utilizará el gateway OTLP de Grafana Cloud. La imagen LGTM no se considera una topología de producción.

Reglas operativas iniciales:

- cada petición tendrá identificador de correlación;
- no se registrarán cuerpos, cookies, tokens, nombres secretos ni valores de banderas narrativas;
- liveness y readiness serán observables de forma independiente;
- la ausencia temporal del collector no impedirá que la API atienda tráfico;
- producción podrá sustituir el backend de observabilidad conservando OTLP.

### 7. Ejecución reproducible con Docker Compose

El entorno integrado local se levantará con Docker Compose y contendrá:

- `web`: build Angular servido por Nginx;
- `api`: ASP.NET Core;
- `postgres`: base de datos con volumen persistente;
- `observability`: collector y stack Grafana LGTM con volumen persistente.

El desarrollo rápido permite ejecutar Angular en el host con proxy a la API, mientras PostgreSQL y observabilidad permanecen en contenedores.

Las imágenes usarán builds multi-stage y no copiarán `sources/` al contexto final. `.dockerignore` excluirá expresamente las fuentes editoriales y artefactos locales.

### 8. Integración continua

Cada cambio deberá ejecutar como mínimo:

- instalación reproducible de dependencias frontend;
- pruebas y build de Angular;
- restore, build y pruebas de .NET;
- validación de Docker Compose;
- revisión de formato y ausencia de secretos cuando se incorpore la herramienta correspondiente.

### 9. Gobierno del repositorio

- La rama principal será `main`.
- Cada incremento funcional comenzará con una especificación y criterios de aceptación.
- Las decisiones transversales se documentarán mediante ADR antes de implementarse.
- Código, pruebas, documentación e infraestructura afectados se publicarán en el mismo cambio.
- Un módulo no accederá directamente a las tablas o clases internas de otro módulo.
- El repositorio público solo incluirá datos ficticios y configuración sin secretos.
- Recursos privados, secretos y datos persistentes se inyectarán durante el despliegue y nunca se confirmarán en Git.

## Versiones base

- Angular `22.x`, actualmente en soporte activo.
- Node.js `24.x`, compatible con Angular 22.
- .NET y ASP.NET Core `10.0`, LTS hasta noviembre de 2028.
- PostgreSQL `18.x` para desarrollo local.
- OpenTelemetry .NET `1.17.x`.
- Grafana OpenTelemetry LGTM `0.30.x`, solo para desarrollo, demo y pruebas.

Las versiones patch se actualizarán de forma regular. Las versiones major requieren comprobar compatibilidad y, si alteran esta decisión, un ADR nuevo.

## Alternativas consideradas

### React o Vue para el frontend

Son opciones válidas, pero contradicen la decisión explícita de usar Angular y no aportan una ventaja que justifique mantener dos stacks. Se descartan.

### Node.js o Python para el backend

Reducirían la diversidad de lenguajes si el frontend compartiera TypeScript, pero el backend está fijado en C# ASP.NET Core. Además, .NET ofrece una base sólida para contratos, políticas, telemetría y trabajo transaccional. Se descartan.

### Microservicios desde el inicio

Aumentarían despliegues, consistencia distribuida, trazabilidad y superficie operativa antes de conocer los límites reales. Se descartan por ahora.

### SQLite como persistencia principal

Simplificaría la ejecución de un solo proceso, pero se aleja de las necesidades de concurrencia, transacciones y colaboración futura. Se conserva únicamente como posible herramienta efímera, no como persistencia canónica.

### Stack de observabilidad separado desde el inicio

Levantar Collector, Prometheus/Mimir, Tempo, Loki y Grafana como contenedores independientes permite configurarlos con más precisión, pero incrementa mucho el coste local. La imagen LGTM ofrece las mismas fronteras y protocolos para desarrollo; producción deberá diseñarse aparte.

## Consecuencias

### Positivas

- Existe una ruta vertical clara desde Angular hasta PostgreSQL.
- Las reglas sensibles permanecen en el backend.
- El desarrollo y la integración son reproducibles en cualquier máquina con Docker.
- Logs, métricas y trazas se incorporan antes de que aparezcan incidentes difíciles de diagnosticar.
- El monolito modular conserva una vía de extracción futura sin pagar hoy el coste de microservicios.

### Costes y riesgos

- El equipo mantiene dos ecosistemas: TypeScript/Angular y C#/.NET.
- El stack LGTM consume memoria y disco; se podrá levantar de forma opcional en equipos limitados.
- PostgreSQL y Docker son requisitos para integración completa.
- La autenticación same-origin requiere diseñar correctamente cookies, CSRF y proxy antes de exponer datos reales.
- La instrumentación puede filtrar secretos si se añaden tags o logs sin revisión.
- Los límites del monolito modular dependen de convenciones y pruebas de arquitectura, no de aislamiento de red.
- Todos los componentes de aplicación comparten un ciclo de publicación coordinado.

## Documentación arquitectónica

Esta decisión se concreta en dos vistas complementarias, que deben leerse en este orden:

1. [Diagrama de componentes](../architecture/diagrama-de-componentes.md): responsabilidades y dependencias lógicas.
2. [Diagrama de despliegue](../architecture/diagrama-de-despliegue.md): distribución de esos componentes en el entorno local integrado.
3. [Dashboards de observabilidad](../operations/dashboards-de-observabilidad.md): vistas operativas aprovisionadas y criterios de interpretación.

## Primer incremento autorizado

La implementación inicial de este ADR debe entregar:

- workspace Angular compilable con una pantalla de estado de plataforma;
- API ASP.NET Core compilable con endpoints de estado, liveness y readiness;
- conexión y health check de PostgreSQL;
- exportación OTLP de logs, métricas y trazas;
- Dockerfiles y Docker Compose para el recorrido completo;
- prueba mínima de frontend y backend;
- documentación de arranque y verificación.

Después de validar esta base, las decisiones de dominio podrán incorporarse de forma incremental tras su revisión y aceptación.

## Addendum: topología productiva gratuita

La primera opción evaluada fue Oracle Cloud Infrastructure Always Free con una VM Ampere A1. Los intentos de creación en Madrid fallaron de forma repetida por falta de capacidad tanto desde Terraform como desde la consola. Para no depender de reservar un host concreto se sustituye por una topología serverless:

- GitHub Pages sirve el build estático de Angular;
- Azure Container Apps Consumption ejecuta la API ASP.NET Core con `0.25` vCPU, `0.5 GiB`, mínimo cero y máximo una réplica;
- Neon Free proporciona PostgreSQL externo con suspensión por inactividad;
- Grafana Cloud Free recibe logs, métricas y trazas directamente mediante OTLP.

GitHub Actions construye la imagen Linux AMD64 con un tag inmutable de commit, la publica en GHCR y crea una nueva revisión de Container Apps mediante federación OIDC con Azure. No existe secreto de cliente Azure. La cadena de PostgreSQL y la autorización OTLP se mantienen en GitHub Environments y se instalan como secretos de Container Apps; Terraform no recibe sus valores.

La separación de orígenes se limita al incremento de plataforma, cuyos endpoints no contienen información de campaña. ASP.NET restringe CORS a `https://peterbfoo.github.io`, y el frontend recibe únicamente la URL pública de la API en `config.js`. GitHub Pages no almacena credenciales.

Esta elección prioriza coste cero, escala a cero y ausencia de una reserva de VM. Los planes gratuitos no ofrecen SLA, pueden introducir arranques en frío y tienen cuotas. Se configurarán alertas de presupuesto antes de activar cualquier servicio facturable. Antes de almacenar datos reales se deberá definir copia cifrada y restauración de PostgreSQL, además de resolver la autenticación same-origin.

## Criterios de revisión

Revisaremos esta decisión si:

- un módulo necesita escalar o desplegarse de forma independiente;
- los requisitos offline hacen inviable la dependencia normal de la API;
- PostgreSQL no satisface una carga medida;
- la solución de identidad requiere una topología distinta;
- el backend de observabilidad deja de aceptar OTLP o la operación local resulta desproporcionada.
