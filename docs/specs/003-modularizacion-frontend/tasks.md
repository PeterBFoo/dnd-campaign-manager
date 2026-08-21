# Tareas 003: Modularización del frontend por capacidades

- Estado general: Completado
- Plan: [plan.md](plan.md)
- Especificación: [spec.md](spec.md)
- ADR: [ADR-0005](../../adr/0005-frontend-modular-por-capacidades.md)

## Convención de estado

- `[ ]` Pendiente.
- `[-]` En curso.
- `[x]` Completada.
- `[~]` Descartada, con justificación escrita bajo la tarea.

Una tarea solo puede marcarse como completada cuando satisface todo su trabajo y criterios de aceptación, incluidas pruebas y documentación afectada. Las tareas deben ejecutarse en orden salvo que la tabla indique dependencias que permitan trabajo paralelo.

No se aprovecharán estas tareas para cambiar contratos HTTP, comportamiento, copy, estilos, autenticación o seguridad. Un defecto descubierto se documentará y se resolverá por separado, salvo que bloquee directamente la modularización.

## Resumen

| ID | Tarea | Depende de | Estado |
|---|---|---|---|
| WEB-001 | Caracterizar routing, sesión y autenticación técnica | — | Completada |
| WEB-002 | Caracterizar clientes HTTP y recorridos visibles | — | Completada |
| WEB-003 | Crear límites, alias y fitness function inicial | WEB-001, WEB-002 | Completada |
| WEB-004 | Extraer primitivas técnicas compartidas | WEB-003 | Completada |
| WEB-005 | Extraer el módulo frontend Platform | WEB-002, WEB-004 | Completada |
| WEB-006 | Separar contratos y clientes HTTP de Access | WEB-002, WEB-004 | Completada |
| WEB-007 | Extraer el store de sesión y los providers de Access | WEB-001, WEB-006 | Completada |
| WEB-008 | Encapsular guards e interceptor en Access | WEB-003, WEB-007 | Completada |
| WEB-009 | Mover la política frontend de contraseña a Access | WEB-002, WEB-003 | Completada |
| WEB-010 | Migrar el recorrido de bootstrap | WEB-006, WEB-007, WEB-009 | Completada |
| WEB-011 | Migrar el recorrido de aceptación de invitaciones | WEB-006, WEB-007, WEB-008, WEB-009 | Completada |
| WEB-012 | Migrar la gestión de invitaciones de plataforma | WEB-006, WEB-007, WEB-008 | Completada |
| WEB-013 | Migrar la gestión de invitaciones de campaña | WEB-006, WEB-007, WEB-008 | Completada |
| WEB-014 | Separar la home y la entrada de Access | WEB-005, WEB-007, WEB-008 | Completada |
| WEB-015 | Modularizar routing y APIs públicas | WEB-010 a WEB-014 | Completada |
| WEB-016 | Retirar la estructura plana y endurecer los límites | WEB-015 | Completada |
| WEB-017 | Verificar build, imagen y recorrido integrado | WEB-016 | Completada |
| WEB-018 | Actualizar documentación y cerrar el incremento | WEB-017 | Completada |

## Tareas

### [x] WEB-001 — Caracterizar routing, sesión y autenticación técnica

**Resultado:** existe una red de seguridad para rutas, sesión, guards e interceptor antes de mover código.

**Trabajo:**

- Añadir tests de la tabla de rutas para `/`, `/bootstrap`, `/accept-invitation`, `/admin/invitations`, `/campaigns/:campaignId/invitations` y wildcard.
- Fijar guards para visitante, usuario autenticado y administrador de plataforma, incluyendo los `UrlTree` de redirección.
- Caracterizar restauración desde `sessionStorage` para sesión válida, caducada, incompleta y JSON inválido.
- Fijar almacenamiento tras login o aceptación, exposición de usuario/token y limpieza local.
- Comprobar logout correcto y fallo remoto, conservando la limpieza mediante `finalize`.
- Probar que el interceptor añade `Authorization: Bearer` solo cuando existe un token válido y conserva el resto de la petición.
- No modificar comportamiento productivo en esta tarea.

**Criterios de aceptación:**

- Cada ruta y guard actual tiene al menos un escenario representativo.
- Los tests detectan cambios en la clave de almacenamiento, expiración, redirecciones o header bearer.
- `sessionStorage` se aísla y limpia entre tests.
- `pnpm test:web` pasa sin cambios funcionales.

### [x] WEB-002 — Caracterizar clientes HTTP y recorridos visibles

**Resultado:** contratos de transporte y comportamiento de páginas están fijados antes de dividir servicios y componentes.

**Trabajo:**

- Añadir tests con el backend HTTP de pruebas de Angular para Platform, bootstrap, login, logout, preview, aceptación y gestión de invitaciones.
- Fijar método, URL, path params y payload de cada operación actual.
- Caracterizar resolución de `apiBaseUrl` relativa y configurada mediante `config.js`.
- Completar tests de bootstrap y aceptación para loading, validación, error y camino principal.
- Cubrir listado, emisión, reenvío y revocación en las páginas de invitaciones de plataforma y campaña.
- Conservar las pruebas existentes de política y flujos de contraseña.
- Registrar cualquier comportamiento dudoso que deba conservarse temporalmente.

**Criterios de aceptación:**

- Un cambio de URL, método o forma JSON relevante hace fallar un test.
- Cada página funcional tiene cobertura del camino principal y de un error o validación relevante.
- Los tests no requieren red ni API real.
- La suite sigue verde y no cambia el código productivo salvo ajustes mínimos de testabilidad sin efecto observable.

### [x] WEB-003 — Crear límites, alias y fitness function inicial

**Resultado:** existe el esqueleto modular y una comprobación automática del grafo TypeScript.

**Trabajo:**

- Crear `shell`, `modules/platform`, `modules/access`, `shared` y `architecture` sin anticipar módulos futuros.
- Configurar los alias `@modules/access`, `@modules/access/entry`, `@modules/platform` y `@shared/*` hacia entrypoints explícitos.
- Crear APIs públicas mínimas para Platform y Access sin reexportar internals indiscriminadamente.
- Implementar `architecture/module-boundaries.spec.ts` con la API del compilador TypeScript.
- Resolver imports estáticos, dinámicos, relativos y por alias sobre archivos productivos; excluir specs y la propia fitness function del grafo productivo.
- Detectar ciclos, deep imports intermodulares, dependencias ascendentes desde `shared` y uso interno del propio barrel.
- Añadir excepciones temporales explícitas para los archivos planos, vinculadas a WEB-016.
- Incorporar fixtures o casos controlados que demuestren que cada familia de infracción es detectada.

**Criterios de aceptación:**

- Una dependencia prohibida y un ciclo introducidos deliberadamente hacen fallar la fitness function.
- Los aliases resuelven tanto en test como en build.
- Las excepciones contienen una justificación, un conjunto exacto de archivos y una tarea de retirada.
- La comprobación se ejecuta dentro de `pnpm test:web`, sin un comando opcional separado.
- No se añade Nx ni ESLint únicamente para esta función.

### [x] WEB-004 — Extraer primitivas técnicas compartidas

**Resultado:** configuración de runtime y traducción genérica de errores residen en `shared` sin dependencias funcionales.

**Trabajo:**

- Mover `runtime-config.ts` a `shared/config` y conservar la semántica de URL relativa, valor configurado y normalización.
- Mover `api-error.ts` a `shared/http/problem-details.ts` con un nombre que exprese el protocolo.
- Conservar selección de errores de validación, detail, title y fallback.
- Actualizar consumidores mediante `@shared/*`.
- Añadir o trasladar tests unitarios de ambas responsabilidades.
- Eliminar los archivos planos antiguos cuando no tengan consumidores.

**Criterios de aceptación:**

- `shared` no importa desde `modules`, `shell` ni composition root.
- Runtime config y mensajes de error conservan todos los escenarios caracterizados.
- No aparecen tipos de usuario, sesión, invitación o campaña en `shared`.
- Suite, fitness function y build pasan.

### [x] WEB-005 — Extraer el módulo frontend Platform

**Resultado:** Platform separa transporte, estado y presentación tras una API pública mínima.

**Trabajo:**

- Crear `platform.contracts.ts` con el contrato público de status.
- Crear `PlatformClient` sin estado para `GET /api/v1/platform/status`.
- Crear `PlatformStatusStore` para resultado, loading y error con alcance de componente o recorrido.
- Extraer `PlatformStatusComponent` conservando exactamente el markup, mensajes y formato actual.
- Exponer desde `public-api.ts` solo el componente o fachada que necesita la home.
- Adaptar temporalmente la landing actual para consumir la API pública de Platform.
- Mover los tests al cliente, store y componente correspondientes.
- Eliminar `platform-status.service.ts` cuando deje de tener consumidores.

**Criterios de aceptación:**

- El cliente no contiene signals ni estado de pantalla.
- El store no es global y se reinicia con el ciclo de vida de la portada.
- La landing no realiza deep imports en Platform.
- Se conservan petición, estado operativo, error y loading visibles.
- Tests, arquitectura y build pasan.

### [x] WEB-006 — Separar contratos y clientes HTTP de Access

**Resultado:** Access posee clientes HTTP sin estado y DTO agrupados por vocabulario.

**Trabajo:**

- Crear `identity.contracts.ts` para usuario, sesión y bootstrap.
- Crear `invitation.contracts.ts` para preview, aceptación y resumen de invitación.
- Crear `IdentityClient` para bootstrap, login, logout y operaciones actuales de identidad.
- Crear `InvitationsClient` para preview, aceptación, listado, emisión, reenvío y revocación de plataforma y campaña.
- Mantener nombres orientados a operaciones del producto y evitar una abstracción CRUD genérica.
- Hacer que los servicios actuales deleguen temporalmente o migrar sus consumidores sin mantener dos implementaciones HTTP.
- Trasladar los tests de contrato creados en WEB-002.
- Retirar `identity-api.service.ts` e `invitation-api.service.ts` cuando no tengan consumidores.

**Criterios de aceptación:**

- Cada endpoint actual se invoca con el mismo método, URL y payload.
- Los clientes no conservan signals, formularios, loading ni cache de pantalla.
- Los DTO de Access no se exportan desde `shared`.
- No hay dos caminos productivos para la misma petición.
- Tests de clientes, fitness function y build pasan.

### [x] WEB-007 — Extraer el store de sesión y los providers de Access

**Resultado:** la sesión global tiene ownership Access y el transporte está separado de su estado.

**Trabajo:**

- Crear `SessionStore` a partir de la responsabilidad de estado de `AuthService`.
- Conservar clave de `sessionStorage`, restauración, expiración, usuario derivado, token, almacenamiento y limpieza.
- Coordinar login/logout mediante `IdentityClient` sin exponer DTO innecesarios al shell.
- Conservar `useAcceptedSession` o una operación semántica equivalente para aceptación de invitación.
- Crear `access.providers.ts` como punto deliberado de registro global.
- Exportar una superficie pública mínima de lectura de sesión y acciones necesarias.
- Actualizar los consumidores productivos a un único store y retirar `AuthService`.
- Mover y ampliar los tests de WEB-001.

**Criterios de aceptación:**

- Existe una única instancia y fuente de verdad de sesión.
- No cambia ningún escenario de almacenamiento, expiración, login o logout caracterizado.
- Shell y módulos externos no conocen `sessionStorage` ni contratos HTTP.
- `SessionStore` pertenece a Access aunque tenga alcance global.
- Tests, fitness function y build pasan.

### [x] WEB-008 — Encapsular guards e interceptor en Access

**Resultado:** autenticación técnica está localizada en Access y se registra mediante su fachada.

**Trabajo:**

- Separar `authenticated.guard.ts` y `platform-admin.guard.ts` dentro de `access/session`.
- Mover el interceptor bearer a `access/session` y hacerlo depender de la API del store apropiada.
- Registrar interceptor y providers desde el composition root a través del entrypoint deliberado de Access.
- Mantener las mismas redirecciones, condiciones y clonación de requests.
- Actualizar los tests de WEB-001 a las nuevas localizaciones.
- Eliminar `auth.guard.ts` y `auth.interceptor.ts` planos.

**Criterios de aceptación:**

- Guards e interceptor no son accesibles mediante deep imports desde shell u otros módulos.
- Se conservan todos los escenarios caracterizados de autorización UX y bearer.
- No se realizan peticiones ni lecturas extra de almacenamiento durante bootstrap.
- Tests, fitness function y build pasan.

### [x] WEB-009 — Mover la política frontend de contraseña a Access

**Resultado:** validadores y mensajes de contraseña están compartidos únicamente por recorridos de Access.

**Trabajo:**

- Mover `password-validation.ts` y su spec a `modules/access/password`.
- Actualizar bootstrap y aceptación para usar la nueva localización interna.
- Conservar límites, categorías Unicode, orden de mensajes y composición con `Validators.required`.
- Confirmar que la política es feedback de UI y que el API continúa siendo autoritativo.
- Evitar exportarla desde la API pública de Access si ningún consumidor externo la necesita.

**Criterios de aceptación:**

- Las pruebas existentes y los flujos de formulario conservan resultados y mensajes.
- La política no reside en `shared` ni duplica validaciones por página.
- No se relajan ni endurecen reglas en esta reorganización.
- Tests, fitness function y build pasan.

### [x] WEB-010 — Migrar el recorrido de bootstrap

**Resultado:** bootstrap es una página vertical de Access con ownership y tests propios.

**Trabajo:**

- Mover y renombrar el componente como `access/bootstrap/bootstrap.page.*`.
- Mantener formulario, autocomplete, validación, submitting, mensajes y navegación posterior.
- Sustituir el servicio anterior por `IdentityClient` y `SessionStore` solo donde corresponda.
- Colocar sus tests junto al recorrido y conservar los escenarios de WEB-002 y WEB-009.
- Actualizar temporalmente la ruta raíz para cargar la nueva página sin cambiar el path.
- Eliminar los archivos de bootstrap planos.

**Criterios de aceptación:**

- `/bootstrap` conserva contrato, renderizado y comportamiento.
- La página no importa internals de Platform ni accede directamente a `sessionStorage`.
- No permanece una segunda implementación de bootstrap.
- Tests de página, clientes, routing, arquitectura y build pasan.

### [x] WEB-011 — Migrar el recorrido de aceptación de invitaciones

**Resultado:** la aceptación de invitación es una vertical slice de Access sin perder seguridad ni navegación.

**Trabajo:**

- Mover y renombrar el componente como `access/invitation-acceptance/invitation-acceptance.page.*`.
- Usar `InvitationsClient`, `SessionStore` y la política de contraseña del módulo.
- Conservar lectura del fragmento, retirada inmediata del token visible y ausencia de token en query params.
- Mantener preview, estados de invitación, creación de cuenta, login previo, aceptación y sesión devuelta.
- Mantener loading, submitting, accepted, errores y navegación final.
- Trasladar y completar sus tests de componente.
- Actualizar temporalmente la ruta y retirar los archivos planos.

**Criterios de aceptación:**

- `/accept-invitation#token=...` conserva todos los recorridos caracterizados.
- El token se elimina de la URL y no aparece en logs o errores añadidos por el cambio.
- No se duplica estado de sesión entre la página y `SessionStore`.
- Tests de página, clientes, sesión, routing, arquitectura y build pasan.

### [x] WEB-012 — Migrar la gestión de invitaciones de plataforma

**Resultado:** la administración de invitaciones de plataforma reside en Access como página de gestión.

**Trabajo:**

- Mover y renombrar el componente como `access/invitation-management/platform-invitations.page.*`.
- Sustituir `InvitationApiService` por `InvitationsClient`.
- Conservar listado, loading, formulario, emisión, reenvío, revocación, notices y errores.
- Mantener estados y formateo de fechas de la plantilla.
- Trasladar los tests y actualizar temporalmente la ruta con el mismo guard.
- Eliminar los archivos planos antiguos.

**Criterios de aceptación:**

- `/admin/invitations` mantiene path, guard, UI y operaciones HTTP.
- La página no conserva cache global ni conoce detalles de sesión.
- Cada acción actualiza el listado y mensajes como antes.
- Tests de página, clientes, routing, arquitectura y build pasan.

### [x] WEB-013 — Migrar la gestión de invitaciones de campaña

**Resultado:** las invitaciones de campaña permanecen correctamente en Access aunque su URL esté bajo Campaigns.

**Trabajo:**

- Mover y renombrar el componente como `access/invitation-management/campaign-invitations.page.*`.
- Sustituir el servicio anterior por `InvitationsClient`.
- Conservar lectura de `campaignId`, formulario, listado, emisión, reenvío, revocación y mensajes.
- Mantener el guard autenticado y la autoridad real del API.
- Trasladar los tests y actualizar temporalmente la ruta.
- Documentar en el test o módulo que la localización responde al ownership decidido en ADR-0004.
- Eliminar los archivos planos antiguos.

**Criterios de aceptación:**

- `/campaigns/:campaignId/invitations` mantiene path, param, guard y operaciones HTTP.
- No se crea un módulo Campaigns vacío ni se mueve ownership por la forma de la URL.
- Un cambio de campaignId produce las peticiones esperadas sin estado residual.
- Tests de página, clientes, routing, arquitectura y build pasan.

### [x] WEB-014 — Separar la home y la entrada de Access

**Resultado:** la portada es composición de Platform y Access mediante superficies públicas pequeñas.

**Trabajo:**

- Mover la estructura de landing a `shell/home/home.page.*` conservando el resultado visual.
- Extraer `AccessEntryComponent` para sesión activa, bootstrap requerido y login.
- Hacer que AccessEntry coordine `IdentityClient` y `SessionStore` dentro de Access.
- Componer `AccessEntryComponent` y `PlatformStatusComponent` desde sus APIs públicas.
- Mantener navegación tras login y enlace de bootstrap.
- Conservar o recolocar los estilos sin convertir estilos funcionales en globales innecesarios.
- Mover y dividir los tests de landing en home y AccessEntry.
- Eliminar `landing.component.*`.

**Criterios de aceptación:**

- `/` mantiene contenido, layout, login, bootstrap status y platform status.
- Home no importa clientes, DTO ni stores internos de los módulos.
- Platform y Access no se importan entre sí para componer la página.
- El shell raíz sigue mostrando navegación y sesión correctamente.
- Tests de composición, arquitectura y build pasan.

### [x] WEB-015 — Modularizar routing y APIs públicas

**Resultado:** la tabla raíz solo compone home y rutas lazy de Access.

**Trabajo:**

- Crear `ACCESS_ROUTES` con bootstrap, aceptación y ambas páginas de invitaciones.
- Reducir `app.routes.ts` a home con `pathMatch: 'full'`, `loadChildren` de Access y wildcard.
- Mantener `loadComponent` para las páginas y los guards en los mismos paths.
- Revisar `public-api.ts` de Access y Platform para exportar solo consumidores reales.
- Mantener `access.routes.ts` y `access.providers.ts` como entrypoints explícitos sin convertir todos los internals en públicos.
- Actualizar tests de routing para lazy loading, params, guards y wildcard.
- Comprobar que home continúa lazy y que la API pública no hace eager las páginas de gestión.

**Criterios de aceptación:**

- Todas las URLs y redirecciones caracterizadas siguen resolviendo igual.
- `app.routes.ts` no importa páginas funcionales concretas de Access.
- Los barrels no contienen `export *` indiscriminados ni crean ciclos.
- El build genera fronteras lazy coherentes y respeta budgets.
- Tests de routing, fitness function y build pasan.

### [x] WEB-016 — Retirar la estructura plana y endurecer los límites

**Resultado:** la arquitectura objetivo está completa, sin compatibilidad residual ni excepciones temporales.

**Trabajo:**

- Eliminar reexports, shims y archivos funcionales antiguos en la raíz de `app`.
- Confirmar que solo permanecen composition root, shell, modules, shared y architecture.
- Retirar todas las excepciones temporales de la fitness function.
- Endurecer reglas para deep imports, ciclos, imports ascendentes y barrels internos.
- Comprobar que specs y arquitectura no se incorporan al bundle productivo.
- Revisar nombres `.page`, `.component`, `.client`, `.contracts`, `.store`, `.guard` e `.interceptor`.
- Buscar imports relativos que atraviesen módulos o eviten aliases públicos.

**Criterios de aceptación:**

- No queda ningún componente, cliente, store, guard o utilidad funcional plano.
- La fitness function pasa sin allowlist de la arquitectura anterior.
- Una infracción controlada de cada regla continúa siendo detectada.
- No hay ciclos ni deep imports intermodulares.
- Suite completa y build pasan.

### [x] WEB-017 — Verificar build, imagen y recorrido integrado

**Resultado:** la aplicación modular funciona en el artefacto y la topología reales.

**Trabajo:**

- Ejecutar la suite frontend completa sin watch.
- Ejecutar el build de producción y comprobar budgets, warnings, resolución de templates/estilos y chunks lazy.
- Construir la imagen Docker web.
- Validar `docker compose config --quiet`.
- Levantar el recorrido integrado cuando el entorno local esté disponible y ejecutar `scripts/smoke-test.sh` contra la entrada Nginx.
- Comprobar fallback SPA al abrir directamente cada ruta pública.
- Comprobar proxy `/api`, runtime config y ausencia de nuevos secretos o datos privados en estáticos.
- Registrar resultados y cualquier limitación reproducible del entorno.

**Criterios de aceptación:**

- `pnpm test:web` y `pnpm build` terminan correctamente.
- `docker compose build web` y la validación de Compose pasan.
- Nginx sirve `/` y hace fallback correcto en rutas profundas.
- El smoke test integrado pasa o, si una dependencia externa impide ejecutarlo, existe evidencia del bloqueo y una verificación local equivalente acordada.
- No se elevan budgets ni se debilitan tests para cerrar la tarea.

**Evidencia 2026-08-22:** `pnpm test:web` pasa con 17 archivos y 40 tests; `pnpm build` pasa con 306,11 kB iniciales frente a 307,28 kB de línea base; `docker compose config --quiet` y `docker compose build web` pasan. La imagen se verificó primero en aislamiento: `/`, `/bootstrap`, `/accept-invitation`, `/campaigns/:campaignId/invitations` y `/config.js` respondieron con `200`, incluyendo fallback SPA en rutas profundas. Después se recreó únicamente el servicio `web` del stack local y `BASE_URL=http://127.0.0.1:4200 sh scripts/smoke-test.sh` pasó contra Nginx, la API y PostgreSQL. La primera descarga anónima de imágenes base requirió un segundo intento por un timeout de Docker Hub; no afectó al artefacto final.

### [x] WEB-018 — Actualizar documentación y cerrar el incremento

**Resultado:** documentación y estados representan la arquitectura frontend realmente verificada.

**Trabajo:**

- Actualizar el diagrama de componentes con shell, módulos Platform y Access, APIs públicas y dirección de dependencias.
- Actualizar README con la estructura de `apps/web` y comandos de verificación.
- Revisar ADR-0005 para que cualquier detalle de fitness functions coincida con la implementación, sin alterar la decisión aceptada.
- Actualizar índices ADR y SDD si fuera necesario.
- Marcar tareas completadas o justificar descartes.
- Cambiar el estado general de este documento únicamente cuando no quede trabajo requerido.
- Comprobar enlaces relativos, estados y ausencia de documentación obsoleta sobre la estructura plana.

**Criterios de aceptación:**

- README, diagrama, ADR, especificación, plan y tareas son coherentes entre sí.
- La arquitectura documentada coincide con el árbol e imports reales.
- Todos los criterios de aceptación de `spec.md` están cubiertos por evidencia.
- No quedan tareas pendientes ni descartes sin justificación.
- El incremento puede declararse terminado sin trabajo residual oculto.

## Evidencia de cierre

| Área | Evidencia |
|---|---|
| Comportamiento | 40 tests en 17 archivos cubren routing, sesión, guards, interceptor, contratos HTTP, formularios y recorridos principales. |
| Límites | La fitness function analiza imports estáticos y dinámicos con TypeScript, detecta deep imports, dependencias ascendentes, barrels internos, ciclos y archivos funcionales planos. |
| Artefacto | Build de producción dentro y fuera de Docker; bundle inicial de 306,11 kB, por debajo de la línea base de 307,28 kB y dentro de budgets. |
| Despliegue | Compose válido, fallback de Nginx en rutas profundas y smoke integrado de `/`, health y estado de plataforma. |
| Documentación | README, diagrama de componentes, ADR-0005, especificación, plan e índices reflejan la estructura modular implantada. |
