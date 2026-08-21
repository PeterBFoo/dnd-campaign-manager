# Plan 003: Modularización del frontend por capacidades

- Estado: Aprobado
- Fecha: 2026-08-22
- Especificación: [spec.md](spec.md)
- ADR: [ADR-0005](../../adr/0005-frontend-modular-por-capacidades.md)
- Validación: aprobada explícitamente por el usuario el 2026-08-22

## Resultado esperado

Angular seguirá siendo una única aplicación desplegable, pero su árbol expresará primero las capacidades del producto y después los recorridos de usuario. `platform` será propietario del estado técnico público y `access` de sesión, autenticación, bootstrap, política de contraseña e invitaciones. La raíz se limitará a componer aplicación, rutas y shell; `shared` contendrá solo primitivas sin vocabulario de negocio.

El recorrido productivo será compatible con el actual. La reorganización cambiará nombres, imports, límites de inyección y chunks, pero no rutas visibles, contratos HTTP, almacenamiento de sesión, seguridad ni presentación.

## Diagnóstico de partida

La estructura actual presenta estas dependencias relevantes:

- `landing.component` compone login, bootstrap status, sesión y platform status;
- `AuthService` mezcla cliente de identidad, almacenamiento y estado global de sesión;
- `IdentityApiService` agrupa bootstrap con preview y aceptación de invitaciones;
- `InvitationApiService` agrupa gestión de invitaciones de plataforma y campaña;
- `PlatformStatusService` mezcla petición HTTP con loading, error y cache de pantalla;
- guards, interceptor, contratos, páginas y utilidades comparten el mismo directorio;
- `app.routes.ts` conoce todas las páginas concretas;
- `styles.scss` contiene estilos globales usados por todos los recorridos;
- existen pruebas del shell, política de contraseña y dos flujos de formulario, pero no una caracterización suficiente de routing, sesión y clientes HTTP para moverlos con seguridad.

No existen actualmente ESLint, Nx ni otra herramienta para comprobar límites TypeScript. La suite frontend utiliza Vitest mediante el builder de Angular, por lo que las fitness functions se integrarán en el runner existente y evitarán incorporar un segundo pipeline de análisis solo para esta migración.

## Principios de ejecución

1. **Primero caracterizar, después mover.** Las pruebas deben distinguir una regresión de un cambio meramente estructural.
2. **Migrar por recorrido vertical.** Cada fase deja un único camino productivo y elimina sus adaptadores temporales.
3. **Imports públicos entre módulos.** Los movimientos no pueden sustituir la estructura plana por deep imports entre carpetas.
4. **Estado en el ámbito mínimo.** Mover un servicio no justifica convertirlo en singleton global.
5. **Sin cambios funcionales oportunistas.** Cualquier defecto descubierto se documenta y se corrige aparte salvo que impida la migración.
6. **Compatibilidad en cada fase.** Tests y build deben permanecer verdes antes de migrar el siguiente recorrido.
7. **Sin carpetas ceremoniales.** Solo se crean los segmentos que tengan contenido y consumidores reales.

## Estructura objetivo de este incremento

```text
apps/web/src/app/
  app.component.ts
  app.component.html
  app.component.scss
  app.component.spec.ts
  app.config.ts
  app.routes.ts

  shell/
    home/
      home.page.ts
      home.page.html
      home.page.scss

  modules/
    platform/
      public-api.ts
      api/
        platform.client.ts
        platform.contracts.ts
        platform.client.spec.ts
      status/
        platform-status.store.ts
        platform-status.store.spec.ts
        platform-status.component.ts
        platform-status.component.html

    access/
      access.routes.ts
      access.providers.ts
      public-api.ts
      api/
        identity.client.ts
        identity.contracts.ts
        identity.client.spec.ts
        invitations.client.ts
        invitation.contracts.ts
        invitations.client.spec.ts
      session/
        session.store.ts
        session.store.spec.ts
        authenticated.guard.ts
        platform-admin.guard.ts
        auth.interceptor.ts
      password/
        password-validation.ts
        password-validation.spec.ts
      access-entry/
        public-api.ts
        access-entry.component.ts
        access-entry.component.html
      bootstrap/
        bootstrap.page.ts
        bootstrap.page.html
        bootstrap.page.spec.ts
      invitation-acceptance/
        invitation-acceptance.page.ts
        invitation-acceptance.page.html
        invitation-acceptance.page.spec.ts
      invitation-management/
        platform-invitations.page.ts
        platform-invitations.page.html
        campaign-invitations.page.ts
        campaign-invitations.page.html

  shared/
    config/
      runtime-config.ts
    http/
      problem-details.ts

  architecture/
    module-boundaries.spec.ts
```

`architecture` se mantiene fuera de `shared` porque valida la aplicación durante tests y no forma parte del bundle productivo. Los ficheros `.spec.ts` exactos podrán agruparse cuando varias variantes validen un único recorrido, siempre que el ownership permanezca claro.

No se creará todavía `modules/campaigns`: la gestión de invitaciones de campaña pertenece a Access según ADR-0004 y no existe otro comportamiento frontend de Campaigns.

## Mapa de migración

| Origen actual | Destino | Tratamiento |
|---|---|---|
| `landing.component.*` | `shell/home/home.page.*` y `modules/access/access-entry/*` | Separar composición de portada de la interacción de acceso; conservar markup y estilos |
| `platform-status.service.ts` | `platform/api/platform.client.ts` + `platform/status/platform-status.store.ts` | Separar transporte de estado de presentación |
| `runtime-config.ts` | `shared/config/runtime-config.ts` | Movimiento directo; continúa sin vocabulario funcional |
| `api-error.ts` | `shared/http/problem-details.ts` | Renombrar por el protocolo que representa y conservar fallback/mapeo |
| `auth.service.ts` | `access/api/identity.client.ts` + `access/session/session.store.ts` | Separar HTTP de sesión, manteniendo clave, expiración y API observable |
| `auth.guard.ts` | `access/session/authenticated.guard.ts` + `platform-admin.guard.ts` | Separar por responsabilidad y conservar `UrlTree` de redirección |
| `auth.interceptor.ts` | `access/session/auth.interceptor.ts` | Mantener ownership Access y registro global deliberado |
| `identity-api.service.ts` | `access/api/identity.client.ts` + `invitations.client.ts` | Bootstrap en identidad; preview/accept en invitaciones |
| `invitation-api.service.ts` | `access/api/invitations.client.ts` | Mantener operaciones semánticas de plataforma y campaña sin CRUD genérico |
| `password-validation.ts` | `access/password/password-validation.ts` | Compartido solo por recorridos de Access, nunca `shared` global |
| `bootstrap.component.*` | `access/bootstrap/bootstrap.page.*` | Renombrar como página de ruta y mantener validación |
| `accept-invitation.component.*` | `access/invitation-acceptance/invitation-acceptance.page.*` | Mantener fragment handling, preview, alta, login y sesión aceptada |
| `admin-invitations.component.*` | `access/invitation-management/platform-invitations.page.*` | Mantener guard y acciones de gestión |
| `campaign-invitations.component.*` | `access/invitation-management/campaign-invitations.page.*` | Mantener parámetro de campaña y autorización del API |
| `app.routes.ts` | misma localización | Reducir a composición de home y `access.routes` lazy |
| `app.config.ts` | misma localización | Registrar providers globales mediante la fachada pública de Access |
| `app.component.*` | misma localización | Conservar shell raíz; consumir solo API pública de sesión |
| tests planos | junto al store, cliente, página o política propietaria | Dividir por responsabilidad sin perder escenarios |

Durante una fase se permitirán reexports temporales desde el nombre antiguo si son necesarios para mantener el build verde. Se retirarán dentro de la misma fase; no formarán parte de la arquitectura final.

## Dirección de dependencias

La dirección objetivo será:

```text
app.config / app.routes / app.component / shell
                    │
                    ├──> @modules/access
                    ├──> @modules/access/entry
                    ├──> @modules/platform
                    └──> @shared/*

modules/access ────────> @shared/*
modules/platform ──────> @shared/*
shared ────────────────> Angular, RxJS y TypeScript
```

Reglas concretas:

- `shared` no puede importar `modules`, `shell` ni el composition root;
- `platform` y `access` no pueden importar sus detalles internos mutuamente;
- una dependencia intermodular futura usará únicamente el alias de su `public-api.ts`;
- `shell` no puede importar `api`, stores internos ni recorridos profundos;
- las features de Access pueden depender de `api`, `session`, `password` y `ui` del mismo módulo mediante imports relativos;
- `api` no puede depender de páginas, componentes o stores;
- `public-api.ts` se usa solo desde fuera del módulo; los internals no importan su propio barrel;
- el composition root puede importar `access.routes.ts` como entrypoint de routing explícito;
- no se permiten ciclos directos ni transitivos.

## Alias y fitness functions

Se añadirán paths explícitos en el `tsconfig` de la aplicación:

```text
@modules/access   -> modules/access/public-api.ts
@modules/access/entry -> modules/access/access-entry/public-api.ts
@modules/platform -> modules/platform/public-api.ts
@shared/*         -> shared/*
```

Los imports internos de un módulo serán relativos. El entrypoint adicional `@modules/access/entry` mantiene la UI de login fuera del grafo eager de sesión y providers, evitando cargar Angular Forms desde el shell raíz. Esto diferencia APIs públicas con ciclos de carga distintos de una dependencia interna sin crear paquetes ni librerías.

`architecture/module-boundaries.spec.ts` analizará imports estáticos y dinámicos con la API del compilador TypeScript ya disponible en el workspace. Construirá el grafo de archivos y hará fallar Vitest ante:

- dependencias prohibidas por localización;
- deep imports entre módulos;
- imports desde `shared` hacia capas superiores;
- uso interno del propio `public-api.ts`;
- ciclos.

La propia fitness function tendrá fixtures mínimos o casos controlados que demuestren que una infracción es detectada. Se ejecutará con `pnpm test:web`, por lo que no podrá omitirse en CI sin omitir toda la suite frontend.

Esta elección concreta el mecanismo genérico mencionado en ADR-0005 sin introducir ESLint únicamente para arquitectura. Si más adelante se adopta linting general o Nx, las reglas podrán trasladarse conservando el mismo grafo permitido.

## Routing y composición

`app.routes.ts` conservará la home y delegará las rutas funcionales a Access:

```typescript
export const routes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    loadComponent: () => import('./shell/home/home.page').then((m) => m.HomePage),
  },
  {
    path: '',
    loadChildren: () => import('./modules/access/access.routes').then((m) => m.ACCESS_ROUTES),
  },
  { path: '**', redirectTo: '' },
];
```

`ACCESS_ROUTES` será propietario de `bootstrap`, `accept-invitation`, `admin/invitations` y `campaigns/:campaignId/invitations`. Las páginas continuarán usando `loadComponent`; los guards se aplicarán en los mismos paths.

La home permanecerá lazy para conservar el comportamiento de carga actual. Compondrá `AccessEntryComponent` y `PlatformStatusComponent` exportados por las APIs públicas de sus módulos. No coordinará directamente `HttpClient`, DTO ni stores internos.

El shell raíz conservará cabecera, footer y outlet. Su consumo de sesión se limitará a una fachada pública con usuario derivado y logout; no conocerá detalles de `sessionStorage` ni contratos HTTP.

## Providers y ciclos de vida

Access expondrá una función de providers para registrar deliberadamente:

- el store de sesión global;
- la integración del interceptor bearer;
- los clientes necesarios por sesión y shell.

Los stores de plataforma y de recorridos se proporcionarán en su componente o ruta, no en root. Los clientes HTTP podrán ser inyectables sin estado; su disponibilidad global no implicará ownership global.

Se conservará una única instancia de sesión porque la consumen shell, interceptor y guards. El orden de inicialización no leerá `sessionStorage` más de lo necesario ni realizará peticiones implícitas durante bootstrap.

## Estado y acceso a datos

### Sesión

`SessionStore` será propietario de:

- restauración segura desde `sessionStorage`;
- comprobación de expiración;
- estado reactivo de usuario y token;
- almacenamiento tras login o aceptación de invitación;
- limpieza local incluso si logout remoto falla.

`IdentityClient` será propietario únicamente de login, logout, bootstrap status, bootstrap y usuario actual cuando se consuma. El store coordinará el cliente, pero los contratos de transporte no se expondrán directamente al shell.

### Plataforma

`PlatformClient` realizará `GET /api/v1/platform/status`. `PlatformStatusStore` mantendrá loading, error y último resultado para el componente de estado. Su ciclo de vida quedará limitado a la home.

### Invitaciones

`InvitationsClient` agrupará preview, aceptación, listado, emisión, reenvío y revocación porque todas pertenecen a Access y comparten contratos de invitación. Sus métodos conservarán nombres orientados a operación; no se introducirá un repositorio CRUD.

El estado de formularios, submitting, mensajes y listados continuará en las páginas mientras solo tenga un consumidor. No se creará un store de invitaciones hasta que dos recorridos necesiten compartir cache o coordinación real.

### Política de contraseña

La validación permanecerá en `access/password` porque traduce una política de Access para feedback inmediato. El API seguirá validando de forma autoritativa. Las pruebas existentes de longitud, composición y mensajes se moverán con la política y los recorridos.

## Contratos HTTP y errores

Los DTO se dividirán por vocabulario:

- `identity.contracts.ts`: usuario autenticado, sesión y bootstrap;
- `invitation.contracts.ts`: preview, aceptación y resumen de invitación;
- `platform.contracts.ts`: estado técnico público.

`shared/http/problem-details.ts` contendrá únicamente la forma genérica RFC 9457 que consume el frontend y la selección de un mensaje público. No conocerá errores específicos de Access.

Los tests de cliente fijarán método, URL y payload de cada operación relevante. No se probará `HttpClient` en sí, sino el contrato que el adaptador promete al resto del módulo.

## Estrategia de implementación

### Fase 0: red de seguridad

Antes de mover código se caracterizarán rutas, guards, sesión, interceptor, runtime config y clientes HTTP. Se conservarán y ampliarán las pruebas de contraseña y de sus flujos. El build de producción servirá como baseline de resolución de templates, estilos y chunks.

### Fase 1: límites y esqueleto

Se crearán `shell`, `modules`, `shared` y `architecture`, los alias TypeScript, las APIs públicas vacías mínimas y la fitness function. Las reglas deberán demostrar que detectan una dependencia prohibida antes de comenzar la migración funcional.

### Fase 2: shared y Platform

Se moverán runtime config y ProblemDetails. Platform se separará en contrato, cliente, store y componente. La portada seguirá mostrando el mismo estado y mensajes antes de continuar.

### Fase 3: infraestructura de Access

Se dividirán clientes y contratos; después se migrarán sesión, guards, interceptor, providers y política de contraseña. Se verificará exhaustivamente almacenamiento, expiración, login, logout, bearer y redirecciones antes de mover páginas.

### Fase 4: recorridos de Access

Se migrarán, uno a uno, bootstrap, aceptación, gestión de plataforma y gestión de campaña. Cada recorrido cambiará su ruta al nuevo componente en el mismo incremento y retirará el import anterior tras pasar sus pruebas.

### Fase 5: home, routing y limpieza

La landing se dividirá en home de composición y access entry. `app.routes.ts` delegará en `ACCESS_ROUTES`; el shell usará exclusivamente APIs públicas. Se eliminarán reexports temporales y archivos funcionales planos.

### Fase 6: verificación integrada y documentación

Se ejecutarán suite, build productivo e imagen web; se comprobarán chunks y navegación servida por Nginx. Se actualizarán README, diagrama de componentes, índice SDD y ADR-0005 para reflejar únicamente el resultado verificado.

Estas fases establecen orden y condiciones de avance. El desglose aprobado en unidades ejecutables, dependencias e identificadores está registrado en [tasks.md](tasks.md).

## Estrategia de pruebas

### Caracterización

- tabla de rutas y redirección wildcard;
- guards sin sesión, con usuario normal y con administración de plataforma;
- lectura y retirada del token del fragmento de invitación;
- restauración de sesión válida, caducada, incompleta o con JSON inválido;
- limpieza local de sesión ante logout correcto o fallido;
- inclusión y ausencia del header Authorization;
- resolución de API base URL relativa y configurada en runtime.

### Unitarias y de componente

- clientes HTTP y contratos;
- stores de sesión y plataforma;
- validación de contraseña;
- formularios, estados loading/submitting, mensajes y acciones de páginas;
- composición de home y shell mediante dobles de APIs públicas.

### Arquitectura

- dirección `shared -> nada funcional`;
- aislamiento entre módulos;
- acceso externo solo por `public-api.ts`;
- ausencia de ciclos;
- rutas como entrypoints explícitos;
- exclusión del código de test y arquitectura del bundle productivo.

### Integrada

```sh
pnpm test:web
pnpm build
docker compose build web
```

No se requiere modificar ni ejecutar migraciones, PostgreSQL o tests del backend porque el contrato API no cambia. El smoke test integrado será recomendable al cierre para comprobar Nginx, fallback SPA y proxy `/api` con el build reorganizado.

## Despliegue y compatibilidad

No cambia la topología: Angular continuará produciendo estáticos servidos por Nginx o GitHub Pages. Las rutas del router mantendrán el fallback existente y no aparecerán nuevos orígenes ni secretos.

La división lazy puede cambiar nombres y cantidad de chunks. Se comparará el build antes y después para detectar que una importación desde `public-api.ts` no haga eager una capacidad completa. Los budgets actuales continuarán siendo la condición de aceptación; no se elevarán para acomodar la refactorización.

No cambia `config.js`, el proxy local, Nginx, Dockerfile ni los workflows salvo que sea imprescindible ajustar comandos para incluir la fitness function. Como esta se ejecutará dentro de Vitest, el objetivo es no cambiar CI.

## Reversibilidad

Cada recorrido se migrará como una unidad autocontenida. Mientras un consumidor no se haya actualizado podrá existir un reexport temporal, pero no dos implementaciones con estado independiente.

Una fase se podrá revertir moviendo su slice y restaurando imports sin afectar rutas o contratos. La limpieza final solo ocurrirá después de verificar que no quedan referencias a los nombres antiguos. No se mantendrá indefinidamente una capa de compatibilidad que oculte la nueva arquitectura.

## Riesgos y mitigaciones

| Riesgo | Mitigación |
|---|---|
| Cambiar comportamiento al separar `AuthService` | Caracterizar primero almacenamiento, expiración, login, logout, guards e interceptor |
| Crear ciclos mediante barrels | API pública solo para externos, imports relativos internos y fitness function de ciclos |
| Hacer eager todo Access desde shell | Exportaciones mínimas, revisar grafo/chunks y conservar rutas dinámicas directas |
| Romper rutas al delegarlas | Tests de tabla de rutas, paths, guards, parámetros, fragmentos y wildcard |
| Convertir `shared` en un cajón de sastre | Regla automática y revisión semántica; password y sesión permanecen en Access |
| Duplicar DTO o estado durante la transición | Un único propietario y reexports temporales sin implementación paralela |
| Romper templates o estilos al mover archivos | Build por slice, rutas `templateUrl/styleUrl` relativas y comparación visual básica |
| Fitness function casera incompleta | Validar infracciones controladas y usar el resolvedor TypeScript para imports dinámicos y aliases |
| Mezclar refactorización con cambios funcionales | Congelar rutas, contratos, copy, estilos y seguridad durante este incremento |
| Crear un módulo Campaigns vacío por simetría | Mantener invitaciones en Access hasta que Campaigns tenga comportamiento propio |

## Documentación afectada

Al finalizar la implementación se actualizarán:

- ADR-0005, pasando a `Aceptado` solo tras validar el plan y reflejando el mecanismo final de fitness functions;
- `docs/adr/README.md`;
- `docs/architecture/diagrama-de-componentes.md`, mostrando shell, Platform y Access frontend;
- `README.md`, describiendo la nueva estructura de `apps/web` y los comandos de verificación;
- `docs/specs/README.md` y el futuro `tasks.md` con el estado real.

## Decisiones validadas

La aprobación de este plan confirmó conjuntamente:

1. la estructura `shell / modules / shared / architecture`;
2. que las invitaciones de campaña permanecen en `modules/access`;
3. la separación de `AccessEntryComponent` y `PlatformStatusComponent` para componer la home;
4. los alias `@modules/access`, `@modules/access/entry`, `@modules/platform` y `@shared/*`;
5. una fitness function Vitest basada en el compilador TypeScript en lugar de introducir ESLint solo para límites;
6. la secuencia incremental descrita y la prohibición de cambios funcionales;
7. los criterios de aceptación de `spec.md`.

El desglose aprobado se registra en [tasks.md](tasks.md), con unidades pequeñas, dependencias y criterios verificables. La aprobación del plan y la creación de tareas no autorizan por sí solas el inicio de la implementación.
