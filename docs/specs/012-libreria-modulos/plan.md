# Plan 012: Librería administrable de módulos de aventura

- Estado: En ejecución
- Fecha: 2026-08-29
- Especificación: [spec.md](spec.md)
- Dependencias: [spec 003](../003-modularizacion-frontend/spec.md) y [spec 004](../004-creacion-campanas/spec.md)

## Estrategia

El incremento incorporará un módulo funcional `AdventureCatalog` independiente de Campaigns. Será propietario del agregado de módulo de aventura, la procedencia editorial, la portada privada, su esquema PostgreSQL, endpoints administrativos y telemetría. Angular incorporará un módulo lazy `adventure-catalog` con listado y editor para administradores de plataforma.

La implementación se hará en este orden:

1. fijar dominio, validaciones, concurrencia y contratos de Application;
2. añadir persistencia, almacenamiento privado y migración;
3. exponer el contrato HTTP administrativo y componer el módulo en el host;
4. construir el recorrido Angular y la navegación administrativa;
5. ampliar pruebas, infraestructura, documentación y observabilidad;
6. verificar suites, builds, imágenes y Compose antes de actualizar la trazabilidad.

## Dominio y modelo de datos

### Agregado `AdventureModule`

El agregado tendrá:

- `Id` inmutable;
- `Name` normalizado y `NormalizedName` derivado para unicidad sin distinguir mayúsculas ni espacios exteriores;
- `Description` opcional en texto plano;
- procedencia del texto;
- metadatos opcionales de portada y su procedencia separada;
- `CreatedAt`, `UpdatedAt`, `LastModifiedByUserId` y `Version`.

`Version` comienza en uno y aumenta con cada edición. Alta, actualización y borrado usan concurrencia optimista; la versión se configura como concurrency token y las restricciones de PostgreSQL siguen siendo la defensa autoritativa ante carreras.

El módulo puede existir vacío. No contendrá `CampaignId`, estados editoriales, capítulos ni colecciones de recursos anticipadas.

### Procedencia

Se modelará como valor explícito y no como JSON libre. Los tipos iniciales serán:

- `Original`;
- `Licensed`;
- `Permission`;
- `PublicDomain`;
- `FanContentPolicy`.

Cada registro guarda:

- tipo;
- referencia de fuente opcional para `Original` y obligatoria para el resto;
- fundamento de uso obligatorio;
- atribución opcional;
- instante de verificación asignado por el servidor;
- usuario administrador que verificó.

Texto y portada tienen registros independientes. Una portada nueva exige su propia procedencia; retirar la portada retira también esos metadatos. Editar únicamente el texto no reescribe la procedencia vigente de una portada.

### Validaciones

- nombre: 3–120 caracteres después de recortar extremos;
- descripción: opcional, máximo 5.000 caracteres;
- referencia de fuente: máximo 2.000 caracteres;
- fundamento de uso: 3–2.000 caracteres;
- atribución: máximo 2.000 caracteres;
- portada: JPEG, PNG o WebP, entre 1 byte y 10 MiB, con firma binaria coherente;
- portada nueva y `removeCover=true` son mutuamente excluyentes;
- `ExpectedVersion` debe coincidir en edición y borrado.

La base de datos impondrá unicidad sobre `NormalizedName`, longitudes, versión positiva y coherencia de los metadatos opcionales de portada.

## Application

Se implementarán casos de uso separados:

- `ListAdventureModulesQuery`;
- `GetAdventureModuleQuery`;
- `CreateAdventureModuleCommand`;
- `UpdateAdventureModuleCommand`;
- `DeleteAdventureModuleCommand`;
- `GetAdventureModuleCoverQuery`.

Cada operación recibe un actor con `UserId` e `IsPlatformAdmin`. Application rechaza actores inválidos o sin administración aunque el endpoint tenga policy, para que la autorización sobreviva a otros transportes y a pruebas directas de handlers.

Los handlers devolverán resultados tipados para validación, prohibición, ausencia, conflicto y fallo de imagen. No devolverán tipos HTTP ni dependerán de EF Core, Azure, logging o ASP.NET Core.

Las consultas proyectarán DTO sin tracking. No expondrán `NormalizedName`, claves de blob, identificador del verificador ni otros datos internos de auditoría.

## Persistencia PostgreSQL

AdventureCatalog tendrá proyecto, esquema `adventure_catalog`, `AdventureCatalogDbContext`, factoría de diseño y migraciones propios.

La tabla inicial almacenará el agregado y los campos de procedencia de texto y portada. Un índice único protegerá `NormalizedName`. La portada conservará solo `ObjectKey`, tipo detectado y tamaño; nunca el binario ni una URL externa.

El repositorio ofrecerá:

- listado ordenado por actualización descendente y desempate inmutable;
- búsqueda por identificador;
- comprobación de nombre normalizado excluyendo la entidad editada;
- alta, borrado y confirmación;
- traducción de unicidad y concurrencia a conflictos funcionales.

La migración no toca esquemas de otros módulos. El host aplicará AdventureCatalog después de Access y antes de consumidores futuros del catálogo.

## Portadas privadas

Se reutiliza la decisión técnica de ADR-0007 con ownership independiente:

- contenedor privado `adventure-module-images` en la cuenta Blob existente;
- Azurite en desarrollo y tests;
- identidad administrada y RBAC ya limitado a la cuenta en producción;
- claves opacas `adventure-modules/{moduleId}/{random}.{extension}`;
- API como único canal de lectura, sin SAS ni URL pública.

El puerto `IAdventureModuleCoverStore` ofrecerá guardar, abrir y borrar idempotentemente. La implementación detectará el formato por firma, comprobará tamaño real y creará el contenedor con acceso privado.

Consistencia por operación:

- si se escribe un blob nuevo y falla PostgreSQL, se intenta eliminar el blob nuevo;
- tras confirmar sustitución o retirada, se elimina idempotentemente el blob anterior;
- tras confirmar el borrado del agregado, se elimina idempotentemente su portada;
- una eliminación de blob fallida no restaura una referencia ya retirada; queda observable para auditoría operativa de huérfanos.

## API HTTP

Se añadirá un controlador interno con `[Authorize(Policy = "platform-admin")]` bajo `/api/v1/admin/adventure-modules`.

Operaciones:

- `GET /api/v1/admin/adventure-modules`;
- `POST /api/v1/admin/adventure-modules`;
- `GET /api/v1/admin/adventure-modules/{moduleId}`;
- `PUT /api/v1/admin/adventure-modules/{moduleId}`;
- `DELETE /api/v1/admin/adventure-modules/{moduleId}?expectedVersion=...`;
- `GET /api/v1/admin/adventure-modules/{moduleId}/cover`.

Alta y edición consumirán `multipart/form-data`. La edición recibe `expectedVersion`, `removeCover` y, cuando haya archivo nuevo, los campos de procedencia de portada. El alta devuelve `201`, edición `200`, borrado `204` y lectura de portada un archivo con `nosniff` y caché privada.

Los errores se mapearán a `ValidationProblemDetails`, `401`, `403`, `404` y `409`. No incluirán nombres, textos, fuentes, claves de almacenamiento ni excepciones internas.

## Composición y límites del backend

Se creará un único proyecto `DndCampaign.Modules.AdventureCatalog` con Domain, Application, Infrastructure y Api internos. Solo su fachada pública expondrá:

- `AddAdventureCatalogModule`;
- `MapAdventureCatalogModule`;
- `ApplyAdventureCatalogMigrationsAsync`.

No referenciará Access: consumirá los claims creados por el esquema de autenticación mediante ASP.NET Core y la policy global ya registrada. No referenciará Campaigns ni expondrá contratos intermodulares hasta el spec 013.

Se actualizarán solución, proyecto host y fitness functions para reconocer el nuevo módulo, mantener el grafo acíclico y prohibir que el host conozca internals.

## Web Angular

Se creará `apps/web/src/app/modules/adventure-catalog` con:

- `adventure-catalog.routes.ts`;
- `api/adventure-catalog.client.ts`;
- `api/adventure-catalog.contracts.ts`;
- listado administrativo;
- editor reutilizado para alta y detalle/edición;
- estilos y pruebas propios;
- `public-api.ts` mínimo solo si otro módulo lo necesita.

Rutas lazy:

- `/admin/adventure-modules`;
- `/admin/adventure-modules/new`;
- `/admin/adventure-modules/:moduleId`.

Todas usan `platformAdminGuard`; la API sigue siendo el control autoritativo.

El listado mostrará carga, error, vacío, nombre, actualización, portada autenticada o fallback local y acciones. Las portadas se obtendrán como `Blob` mediante `HttpClient` para incluir bearer token y se convertirán en object URLs revocadas al destruir la página.

El editor tendrá validación de campos, selector de tipo de procedencia, subida/sustitución/retirada de portada, estado de envío y confirmación de borrado. Ante `409` conservará la edición local y ofrecerá recargar la versión vigente. El detalle mostrará accesos deshabilitados o informativos para capítulos, mapas, localizaciones y NPC, sin crear rutas vacías.

La entrada de sesión administrativa añadirá navegación al catálogo junto a invitaciones. La tabla de rutas y las fitness functions reconocerán el nuevo entrypoint sin permitir importaciones profundas.

## Infraestructura y despliegue

- Compose suministrará `Storage__AdventureCatalog__ConnectionString` a API y tests usando el Azurite existente.
- Terraform creará el contenedor privado adicional en la cuenta existente; no creará otra cuenta ni amplía RBAC.
- El workflow de despliegue pasará `Storage__AdventureCatalog__ServiceUri` usando el endpoint Blob ya disponible.
- Se actualizarán ejemplos y documentación operativa para distinguir contenedores de personajes y catálogo.
- No se introducen secretos nuevos.

## Observabilidad, privacidad y seguridad

AdventureCatalog publicará contador y duración para listado, detalle, alta, edición, lectura de portada y borrado. Las únicas etiquetas serán operación y resultado de cardinalidad cerrada.

Los logs y errores no contendrán nombres, descripciones, procedencia, claves de blob o identificadores. La lectura de portada vuelve a exigir administración. Los archivos se presentan como contenido, nunca como HTML o SVG, y usan el tipo detectado por servidor.

La documentación de dashboards enumerará las nuevas métricas y el procedimiento para investigar fallos de limpieza sin revelar datos editoriales.

## Pruebas

### API

- dominio: normalización, límites, procedencia, portada y versión;
- Application: autorización, CRUD, conflictos, compensaciones y proyección minimizada;
- persistencia PostgreSQL: migración, esquema, unicidad y concurrency token;
- almacenamiento Azurite: firma, tamaño, round trip, privacidad y borrado idempotente;
- contrato HTTP: administrador, usuario no administrador, anónimo, `ProblemDetails`, multipart, portada y ausencia de campos internos;
- arquitectura: capas, fachada, referencias y grafo global.

### Web

- cliente: contratos multipart, blob, query de versión y errores;
- rutas y guard administrativo;
- listado: carga, vacío, error, fallback y portadas;
- editor: alta, edición, validación, procedencia, sustitución/retirada, conflicto y borrado;
- límites modulares y build de producción.

### Verificación final

- `docker compose run --build --rm api-tests`;
- `pnpm test:web`;
- `dotnet build DndCampaign.slnx --no-restore -m:1`;
- `pnpm build`;
- `docker compose build api web`;
- `docker compose config --quiet`;
- `terraform -chdir=infra/azure validate` cuando el runtime esté disponible.

## Documentación y cierre

Al finalizar se actualizarán:

- estado y evidencia en `spec.md`, `tasks.md`, `docs/specs/README.md` y roadmap;
- ADR-0007 o documentación relacionada para reflejar la reutilización del patrón privado sin cambiar su decisión original;
- diagramas de componentes y despliegue;
- secretos/despliegue, migraciones y dashboards operativos;
- `AGENTS.md` solo si cambia el siguiente identificador, que no ocurre en este incremento.

No se incluirá contenido editorial concreto en código, fixtures, migraciones o documentación.

