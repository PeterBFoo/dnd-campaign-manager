# ADR-0005: Frontend modular por capacidades y recorridos de usuario

- Estado: Aceptado
- Fecha: 2026-08-22
- Decisores: equipo del proyecto
- Alcance: estructura del frontend Angular, límites de dependencia, routing, estado, acceso al API y estrategia de crecimiento
- Depende de: ADR-0001 y ADR-0004

## Contexto

ADR-0001 decidió utilizar Angular con componentes standalone, signals y `HttpClient`. ADR-0004 reorganizó el backend como un monolito modular: primero por módulos de negocio y, dentro de cada módulo, por `Api`, `Application`, `Domain` e `Infrastructure`.

El frontend todavía conserva la estructura del primer incremento. Sus archivos de `apps/web/src/app` están en un único nivel e incluyen:

- composition root, shell y routing;
- páginas de login, bootstrap, aceptación y gestión de invitaciones;
- guards e interceptor de autenticación;
- sesión y estado de plataforma;
- clientes HTTP, contratos y traducción de `ProblemDetails`;
- plantillas, estilos y tests.

Esta estructura es manejable con una sola capacidad funcional, pero no expresa ownership. Por ejemplo, `landing.component.ts` conoce sesión, bootstrap y estado de plataforma; `identity-api.service.ts` agrupa bootstrap y aceptación de invitaciones; y los contratos HTTP se importan desde servicios con nombres generales. Al incorporar campañas, catálogo de aventuras, bitácora, misiones y encuentros, añadir más archivos al mismo directorio haría difícil responder preguntas básicas:

- qué código cambia al añadir una capacidad del API;
- quién es propietario de una pantalla, un contrato o un estado;
- qué dependencias entre áreas están permitidas;
- qué puede reutilizarse y qué solo es un detalle de una funcionalidad;
- qué código puede cargarse de forma diferida.

La arquitectura del frontend debe poder entenderse junto a la del API, pero no debe copiarla literalmente. El servidor organiza invariantes, transacciones, persistencia y transportes; el navegador organiza navegación, interacción, presentación, estado efímero y consumo de contratos. Una pantalla también puede componer información de más de un módulo del API. La semejanza útil está en el lenguaje y los límites de negocio, no en repetir las mismas capas técnicas.

## Fuerzas de decisión

La solución debe priorizar:

1. **Descubribilidad**: el árbol de directorios debe mostrar las capacidades del producto antes que Angular o los tipos técnicos.
2. **Crecimiento por módulo**: un nuevo módulo del API debe tener una localización frontend predecible cuando origine navegación o interacción.
3. **Cohesión**: página, estado, validación y acceso a datos de un recorrido deben evolucionar juntos.
4. **Límites verificables**: no deben aparecer importaciones profundas o ciclos accidentales entre capacidades.
5. **Semántica frontend**: la organización debe representar pantallas y acciones del usuario, sin duplicar el dominio ni las invariantes del backend.
6. **Carga incremental**: las áreas no esenciales deben poder cargarse mediante el router.
7. **Coste proporcional**: seguiremos teniendo una aplicación, un equipo y un despliegue frontend; no necesitamos distribución operativa prematura.
8. **Migración segura**: la reorganización no debe modificar rutas públicas, contratos HTTP, comportamiento ni estilos como efecto colateral.

## Investigación y alternativas consideradas

### A. Estructura plana o carpetas por tipo técnico

Consiste en conservar un único directorio o crear carpetas globales como `components`, `services`, `models` y `guards`.

Es sencilla al principio y permite localizar todos los elementos de un tipo. Sin embargo, dispersa cada cambio funcional entre varias carpetas, favorece servicios generales y hace crecer el acoplamiento global. El nombre de un tipo técnico aporta poca información sobre el producto.

La guía oficial de Angular recomienda organizar el proyecto por áreas funcionales y evitar directorios basados únicamente en categorías como `components`, `directives` o `services`. Esta alternativa no resuelve el crecimiento esperado y se rechaza.

### B. Carpetas por feature dentro de una única aplicación Angular

Agrupa el código por capacidad o recorrido y coloca juntos los archivos relacionados. Coincide con la recomendación actual de Angular y no requiere cambiar el toolchain, el despliegue ni el workspace.

Por sí sola, la etiqueta “feature” no define granularidad, ownership o dependencias. Si cada botón se convierte en una feature o todas las features pueden importarse entre sí, la estructura vuelve a degradarse. Es la base adecuada si se acompaña de módulos de negocio, API pública y reglas de dependencia.

### C. Feature-Sliced Design completo

Feature-Sliced Design (FSD) propone las capas `app`, `pages`, `widgets`, `features`, `entities` y `shared`, una dirección descendente de importaciones, slices de negocio y APIs públicas. Aporta un vocabulario preciso para cohesión, aislamiento y composición.

Adoptarlo literalmente introduciría varias decisiones que hoy no aportan suficiente valor:

- separar globalmente `pages`, `features` y `entities` volvería a dispersar una misma capacidad del producto;
- la diferencia entre feature, widget y entity sería ambigua para el tamaño actual;
- un frontend principalmente transaccional podría duplicar modelos de dominio que ya pertenecen al API;
- siete capas estandarizadas añaden más localizaciones posibles de las necesarias.

Se adoptan sus ideas de slices cohesionados, dependencias dirigidas y API pública, pero no su jerarquía completa.

### D. Clean Architecture o puertos y adaptadores por módulo frontend

Separaría cada capacidad en dominio, casos de uso, puertos, adaptadores y presentación. Es útil cuando el navegador contiene reglas complejas que deben ejecutarse sin framework, tiene persistencia offline o admite varias fuentes de datos e interfaces.

En esta solución la autoridad funcional y de seguridad está deliberadamente en el backend. Replicar `Domain`, `Application` e `Infrastructure` en Angular induciría modelos e invariantes paralelos, más mapeos y casos de uso que en muchos recorridos solo delegarían a `HttpClient`. Se conservan la inversión de dependencias y la testabilidad cuando exista lógica frontend real, pero no se imponen estas capas a todos los módulos.

### E. Librerías de workspace y Nx

Cada módulo o tipo de responsabilidad podría convertirse en una librería del workspace. Nx permite etiquetar proyectos por alcance y tipo, visualizar el grafo y aplicar restricciones de importación mediante ESLint.

Los límites serían más visibles y escalables que simples carpetas, pero se pagarían desde ahora con más configuración, proyectos, targets y mantenimiento. La aplicación actual no comparte código con otro frontend y su tamaño no justifica todavía una librería por capacidad. Extraer librerías tampoco crea despliegues independientes.

Se difiere Nx y la extracción de librerías. La estructura decidida permitirá promocionar un directorio `modules/<capability>` a una librería sin rediseñar su semántica.

### F. Microfrontends

Los microfrontends permiten que aplicaciones frontend autónomas se construyan y desplieguen de forma independiente. Son apropiados cuando varios equipos necesitan ownership y ciclos de publicación realmente independientes.

El proyecto tiene una aplicación, un equipo, un runtime Angular y un despliegue coordinado con el API. Introducir un shell distribuido, contratos en runtime, duplicación o negociación de dependencias, integración visual y más pipelines resolvería un problema organizativo inexistente. Se rechazan hasta que exista una necesidad demostrada de despliegue independiente.

### Comparación

| Alternativa | Descubribilidad funcional | Límites verificables | Coste actual | Despliegue independiente | Adecuación actual |
|---|---:|---:|---:|---:|---:|
| Plana o por tipo | Baja | Baja | Bajo | No | Baja |
| Features sin reglas adicionales | Alta | Baja | Bajo | No | Media |
| FSD completo | Alta | Alta | Medio | No | Media |
| Clean Architecture por módulo | Media | Alta | Alto | No | Baja |
| Librerías Nx | Alta | Alta | Medio/alto | No | Media, más adelante |
| Microfrontends | Alta | Alta en runtime | Muy alto | Sí | Baja |
| **Módulos por capacidad con slices internos** | **Alta** | **Alta** | **Bajo/medio** | **No** | **Alta** |

## Decisión

### 1. Un monolito frontend modular

Mantendremos una única aplicación Angular, un único build y un único despliegue. Su código se organizará primero por **capacidades de negocio** y después por **recorridos de usuario**.

Los módulos frontend utilizarán, cuando resulte natural, el mismo lenguaje de los módulos del backend:

- `access` para sesión, bootstrap, autenticación e invitaciones;
- `campaigns` para selección, configuración y navegación de campañas;
- futuros `adventure-catalog`, `journal`, `missions` y `encounters` cuando esas capacidades sean confirmadas en el API.

Esto es una correspondencia de bounded contexts, no una réplica de ensamblados, controladores, endpoints o entidades. Solo se creará un módulo frontend cuando exista comportamiento en el navegador: una ruta, un recorrido, estado o UI propia. Un módulo del API sin experiencia frontend no obliga a crear una carpeta vacía.

### 2. Estructura objetivo

La estructura de referencia será:

```text
apps/web/src/app/
  app.component.ts
  app.component.html
  app.component.scss
  app.config.ts
  app.routes.ts

  shell/
    home/
      home.page.ts
      home.page.html
      home.page.scss
    navigation/

  modules/
    platform/
      platform.routes.ts
      public-api.ts
      api/
        platform.client.ts
        platform.contracts.ts
      status/
        platform-status.store.ts
        platform-status.component.ts

    access/
      access.routes.ts
      access.providers.ts
      public-api.ts
      api/
        identity.client.ts
        invitations.client.ts
        access.contracts.ts
      session/
        session.store.ts
        authenticated.guard.ts
        platform-admin.guard.ts
        auth.interceptor.ts
      login/
        login-form.component.ts
        login-form.component.html
      bootstrap/
        bootstrap.page.ts
        bootstrap.page.html
      invitation-acceptance/
        invitation-acceptance.page.ts
        invitation-acceptance.page.html
      invitation-management/
        platform-invitations.page.ts
        platform-invitations.page.html
        campaign-invitations.page.ts
        campaign-invitations.page.html
      ui/

    campaigns/
      campaigns.routes.ts
      public-api.ts
      api/
      campaign-selection/
      campaign-shell/

  shared/
    config/
      runtime-config.ts
    http/
      problem-details.ts
    ui/
```

El árbol es ilustrativo, no una obligación de crear directorios vacíos. Una carpeta aparece cuando contiene una responsabilidad real.

Las localizaciones significan:

| Localización | Contenido | No contiene |
|---|---|---|
| `app/` raíz | bootstrap Angular, configuración y tabla de rutas | lógica de una capacidad |
| `shell/` | layout, navegación y páginas que componen varios módulos | clientes HTTP o reglas internas de un módulo |
| `modules/<capability>` | todo lo propio de una capacidad del producto | detalles internos de otra capacidad |
| recorrido, por ejemplo `bootstrap/` | página, componentes, validación y estado exclusivo del recorrido | elementos reutilizados globalmente por defecto |
| `api/` de un módulo | clientes HTTP, DTO de transporte y mappers de ese módulo | estado de pantalla o reglas de dominio duplicadas |
| `session/` o equivalente | modelo frontend compartido dentro del módulo | utilidades genéricas o datos de otros módulos |
| `ui/` de un módulo | componentes presentacionales reutilizados en ese módulo | llamadas HTTP o estado global |
| `shared/` | configuración y primitivas realmente agnósticas del dominio | `User`, `Campaign`, autenticación o “helpers” sin ownership |

No se creará una carpeta global `core` o `services`: suelen ocultar ownership y convertirse en una localización residual. La autenticación es transversal en uso, pero pertenece funcionalmente a `access`; la configuración de runtime y la traducción genérica de `ProblemDetails` sí son compartidas.

### 3. Relación semántica con el API

La correspondencia principal será:

```text
API host / plataforma ───────────────> modules/platform
Modules/Access ──────────────────────> modules/access
futuro Modules/Campaigns ────────────> modules/campaigns
futuro Modules/Journal ──────────────> modules/journal
```

La decisión de localización se toma con estas reglas, en orden:

1. una capacidad compartirá nombre con el bounded context del API que posee el comportamiento;
2. dentro de la capacidad, el código se nombrará por la acción o recorrido que reconoce el usuario;
3. la URL no determina por sí sola el ownership;
4. un componente que compone varias capacidades vivirá en `shell` o en el módulo que posee el recorrido, consumiendo únicamente las APIs públicas de los demás;
5. no se duplicarán DTO o stores para simular que un dato pertenece a dos módulos.

Por ejemplo, las invitaciones de campaña continúan siendo propiedad de `Access` según ADR-0004. Por tanto, la pantalla actual `/campaigns/:campaignId/invitations` vivirá inicialmente en `modules/access/invitation-management`, aunque su URL esté anidada bajo una campaña. Si el futuro módulo `Campaigns` pasa a poseer ese recorrido, el cambio de ownership deberá ser explícito y no una consecuencia accidental de la ruta.

### 4. Slices verticales dentro de cada módulo

Dentro de un módulo se agrupará primero por recorrido (`login`, `bootstrap`, `invitation-acceptance`) y no por tipos globales (`components`, `services`, `models`).

Un recorrido puede contener su página, plantilla, estilos, formulario, estado local y tests. Solo se extraerá código hacia `api`, `session`, `model` o `ui` del módulo cuando lo consuman varios recorridos o represente una responsabilidad estable independiente.

No se exige una carpeta por cada componente ni una capa por cada operación. Se prefiere mantener juntos dos archivos que cambian juntos antes que alcanzar una simetría artificial.

Los sufijos expresarán responsabilidades concretas:

- `.page` para componentes activados directamente por una ruta;
- `.component` para UI compuesta dentro de una página;
- `.client` para acceso a endpoints HTTP;
- `.contracts` para DTO de transporte;
- `.store` para estado frontend con ciclo de vida definido;
- `.guard`, `.interceptor`, `.routes` y `.providers` para mecanismos Angular específicos.

Se evitará `.service` cuando oculte si una clase es un cliente, un store, una fachada o una política.

### 5. Dirección de dependencias y API pública

La dirección permitida será:

```text
app config / routing / shell
        │
        ├──────────────> modules/*/public-api
        │                         │
        └──────────────> shared <─┘

modules/<capability> ──> sus propios recorridos, api, model y ui
modules/<capability> ──> public-api de otro módulo solo mediante contrato deliberado
shared ────────────────> Angular, TypeScript y librerías externas
```

Reglas obligatorias:

- `shared` no importa desde `modules` ni desde `shell`;
- un módulo no realiza deep imports en otro módulo, solo importa su `public-api.ts`;
- una feature no importa detalles internos de una feature hermana; el código común se eleva a una responsabilidad nombrada dentro del módulo;
- `shell` puede componer APIs públicas, pero no acceder a clientes, DTO o stores internos;
- no se permiten ciclos;
- los símbolos son privados al directorio por defecto y solo se exportan cuando existe un consumidor real;
- un módulo no reexporta el contenido completo de otro módulo.

Los alias de TypeScript harán visible el límite, inicialmente `@modules/access`, `@modules/platform` y `@shared/*`. La implementación añade `@modules/access/entry` como segundo entrypoint público de Access: permite componer el login desde la home sin incorporar Angular Forms al grafo eager de sesión y providers. La CI incorporará una fitness function ejecutada por Vitest y basada en el compilador TypeScript que impida deep imports, ciclos y dependencias desde `shared` hacia módulos. Si en el futuro se adopta linting arquitectónico o los módulos se convierten en librerías Nx, estas mismas reglas se trasladarán conservando la dirección de dependencias.

### 6. Routing y carga diferida

`app.routes.ts` será una tabla de composición; no importará páginas concretas de todos los módulos. Cada capacidad publicará sus rutas y se cargará con `loadChildren`. Las páginas se cargarán con `loadComponent` cuando aporte una frontera adicional útil.

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
  {
    path: 'campaigns',
    loadChildren: () => import('./modules/campaigns/campaigns.routes').then((m) => m.CAMPAIGN_ROUTES),
  },
  { path: '**', redirectTo: '' },
];
```

Las rutas públicas actuales se conservarán durante la migración. Los guards seguirán siendo ayudas de navegación y UX; el API continuará siendo la única frontera de autorización.

Los providers específicos de una capacidad se registrarán en su ruta para limitar alcance y favorecer lazy loading. Solo serán globales los mecanismos realmente usados por toda la aplicación, como el estado de sesión necesario por shell, guards e interceptor. Aunque tenga ciclo de vida global, su implementación y contrato seguirán perteneciendo a `access` y se registrarán mediante `access.providers.ts`.

### 7. Estado frontend

Se mantendrán Angular signals y RxJS; esta decisión no introduce NgRx ni otro store global.

El estado se ubicará en el ámbito más estrecho que lo necesite:

1. estado de interacción de un único componente: signals en el componente;
2. estado compartido por un recorrido: store provisto en la ruta o página;
3. estado compartido por varios recorridos del mismo módulo: store del módulo;
4. estado realmente transversal: store propiedad del módulo correspondiente y expuesto mediante una API pública mínima.

Los datos obtenidos del API no se copiarán automáticamente a un store global. Se introducirá cache solo con una política explícita de vigencia, invalidación y ownership. El estado derivado se expresará con `computed`; no se mantendrán copias sincronizadas manualmente.

La sesión es la excepción global actual porque shell, interceptor y routing la consumen. `PlatformStatusService`, en cambio, se dividirá en cliente y estado del recorrido: realizar HTTP y conservar loading/error son responsabilidades distintas.

### 8. Acceso al API y contratos

Cada módulo será propietario de los clientes que consumen los endpoints de su bounded context. Los clientes:

- solo realizan transporte, serialización y mapeo de errores de protocolo;
- exponen operaciones con nombres del producto, no un CRUD genérico;
- no conservan estado de pantalla;
- devuelven DTO de transporte o view models mapeados explícitamente;
- se prueban contra URLs, métodos y formas JSON mediante `HttpTestingController` o equivalente.

Los contratos del navegador no son entidades de dominio. Reflejan el JSON público que necesita la UI y pueden añadir mappers hacia modelos de presentación. Las invariantes, autorización y transacciones permanecen en el backend.

No se creará un único `ApiService`, un repositorio genérico ni un directorio global con todos los contratos. Un cliente puede agrupar varias operaciones cuando comparten capacidad y cambian juntas; se dividirá cuando tenga consumidores, ciclos de vida o vocabularios distintos.

La generación futura de clientes desde OpenAPI es compatible con esta decisión: el código generado viviría en un adaptador técnico y cada módulo expondría una fachada semántica para no acoplar componentes a la forma del generador.

### 9. Componentes y composición

Las páginas son componentes contenedores: leen parámetros de ruta, coordinan estado y acciones, y componen UI. Los componentes de `ui` reciben datos y emiten eventos; no conocen URLs ni `HttpClient`.

No se extraerá un componente a `shared/ui` por aparecer dos veces. Debe ser agnóstico del dominio, tener una API estable y aportar una interacción o presentación reutilizable. Un componente con vocabulario de campañas seguirá en `modules/campaigns/ui`, aunque visualmente pueda parecer genérico.

La página inicial es una composición excepcional de plataforma y acceso. Vivirá en `shell/home` y consumirá fachadas públicas pequeñas de ambos módulos, en lugar de importar sus clientes internos.

### 10. Pruebas y gobierno arquitectónico

Los tests se colocarán junto al archivo o recorrido que validan:

- tests de clientes para contratos HTTP;
- tests de stores para transiciones de estado y concurrencia de peticiones;
- tests de componentes para renderizado, validación y eventos;
- tests de páginas para integración de un recorrido con dobles de sus puertos;
- tests de routing para guards, parámetros y lazy loading;
- fitness functions para el grafo de importaciones.

Una nueva capacidad frontend se considerará integrada cuando:

1. tenga ownership y nombre acordes al módulo del API o documente por qué difiere;
2. publique rutas lazy si dispone de navegación propia;
3. mantenga clientes y contratos dentro de su módulo;
4. exponga solo los símbolos intermodulares necesarios;
5. no introduzca dependencias circulares ni código de dominio en `shared`;
6. incluya tests proporcionales al recorrido.

## Estrategia de adopción

La migración será incremental:

1. añadir alias y fitness functions sin mover todavía comportamiento;
2. dejar la raíz de `app` como composition root y crear `shell`, `modules` y `shared`;
3. mover `runtime-config` y la traducción de `ProblemDetails` a responsabilidades compartidas nombradas;
4. extraer `platform` separando cliente HTTP y estado de presentación;
5. migrar `access`: primero contratos/clientes y sesión, después bootstrap, login, aceptación y gestión de invitaciones;
6. convertir las rutas de capacidad a `loadChildren` y registrar providers en el ámbito mínimo;
7. adaptar imports y tests después de cada slice, manteniendo el build verde;
8. eliminar los archivos planos solo cuando no tengan consumidores.

Durante la migración no cambiarán:

- las URLs visibles;
- los contratos `/api/v1`;
- la persistencia de sesión decidida por ADR-0003;
- la autorización del servidor;
- el contenido ni el diseño visual, salvo correcciones independientes.

No se realizará un movimiento masivo sin tests de caracterización. Cada recorrido se podrá migrar y verificar por separado.

## Consecuencias

### Positivas

- El árbol del frontend mostrará las mismas capacidades principales que el backend.
- Añadir un módulo funcional tendrá una localización predecible y acotada.
- Los cambios de UI, estado y transporte de un recorrido permanecerán próximos.
- El router y la inyección de dependencias crearán fronteras de carga y ciclo de vida.
- Las APIs públicas y fitness functions harán visibles dependencias que hoy son implícitas.
- La estructura podrá evolucionar a librerías sin renombrar el dominio ni redistribuir las features.
- `shared` dejará de ser el destino por defecto de cualquier código reutilizado dos veces.

### Costes y riesgos

- Habrá más directorios y algunos archivos de composición (`routes`, `providers`, `public-api`).
- La frontera exacta de un recorrido requiere criterio; una convención no sustituye el análisis de ownership.
- El frontend y el backend no siempre tendrán una relación uno a uno, y las excepciones deberán documentarse.
- Los barrels pueden ocultar ciclos si se usan internamente; `public-api.ts` se reservará para consumidores externos al módulo.
- La carga diferida excesivamente granular puede aumentar peticiones y empeorar la navegación; se medirá antes de dividir cada página en chunks.
- La fitness function del grafo TypeScript añade mantenimiento a la suite y CI.

## Opciones diferidas y criterios de revisión

Revisaremos la promoción de módulos a librerías de workspace o Nx cuando ocurra alguno de estos hechos:

- aparece un segundo frontend que necesita reutilizar capacidades;
- varios equipos trabajan con ownership distinto y los conflictos de integración son medibles;
- las restricciones por paths dejan de expresar adecuadamente el grafo;
- los tiempos de build justifican ejecución afectada y cache por proyecto;
- un módulo necesita empaquetado o versionado propio.

Revisaremos microfrontends únicamente si dos o más áreas necesitan **desplegarse de forma independiente**, con equipos y pipelines capaces de asumir contratos en runtime, observabilidad e integración visual separadas. El tamaño de un directorio, por sí solo, no será motivo suficiente.

Revisaremos Clean Architecture dentro de un módulo concreto si el navegador adquiere reglas complejas, funcionamiento offline, persistencia local transaccional o varias fuentes de datos intercambiables. La excepción se aplicará al módulo que lo necesite, no obligatoriamente a toda la aplicación.

## Fuentes

- [Angular: guía de estilo](https://angular.dev/style-guide), especialmente la organización por áreas funcionales.
- [Angular: lazy-loaded routes](https://angular.dev/best-practices/performance/lazy-loaded-routes), sobre `loadChildren`, `loadComponent` y el equilibrio entre carga eager y lazy.
- [Angular: definición de rutas](https://angular.dev/guide/routing/define-routes), incluidos providers con alcance de ruta.
- [Feature-Sliced Design: layers](https://feature-sliced.design/docs/reference/layers) y [slices and segments](https://feature-sliced.design/docs/reference/slices-segments), como referencia para cohesión, dirección de imports y APIs públicas.
- [Nx: enforce module boundaries](https://nx.dev/docs/features/enforce-module-boundaries), para la posible evolución a librerías etiquetadas por alcance y tipo.
- [Robert C. Martin: Screaming Architecture](https://blog.cleancoder.com/uncle-bob/2011/09/30/Screaming-Architecture.html) y [The Clean Architecture](https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html), sobre organización por casos de uso y separación de interfaces.
- [Cam Jackson: Micro Frontends](https://martinfowler.com/articles/micro-frontends.html), sobre autonomía, despliegue independiente y costes operativos.

## Resultado esperado

Al abrir `apps/web/src/app/modules`, una persona debería reconocer el producto y relacionarlo con `src/Modules` del backend. Al entrar en un módulo, debería encontrar los recorridos del usuario antes que una colección de tipos Angular. El parecido entre frontend y API se conservará donde comunica ownership y vocabulario; las diferencias se mantendrán donde navegación, presentación y estado del navegador requieren otra semántica.
