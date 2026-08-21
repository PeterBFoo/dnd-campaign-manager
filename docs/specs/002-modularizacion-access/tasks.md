# Tareas 002: Modularización de Access

- Estado general: En curso
- Plan: [plan.md](plan.md)
- ADR: [ADR-0004](../../adr/0004-arquitectura-modular-cqrs-y-limites-de-dependencia.md)

## Convención de estado

- `[ ]` Pendiente.
- `[-]` En curso.
- `[x]` Completada.
- `[~]` Descartada, con justificación escrita bajo la tarea.

Una tarea solo puede marcarse como completada cuando cumple todos sus criterios, incluidas pruebas y documentación afectada. Las tareas deben implementarse en orden salvo que sus dependencias indiquen que pueden avanzar en paralelo.

## Resumen

| ID | Tarea | Depende de | Estado |
|---|---|---|---|
| ACC-001 | Fijar contratos y comportamiento actual | — | Completada |
| ACC-002 | Crear el proyecto del módulo y su fachada | ACC-001 | Completada |
| ACC-003 | Incorporar architecture tests iniciales | ACC-002 | Completada |
| ACC-004 | Extraer el dominio de Access | ACC-002, ACC-003 | Completada |
| ACC-005 | Unificar y persistir el agregado Invitation | ACC-004 | Completada |
| ACC-006 | Crear puertos y primitivas CQRS de Application | ACC-004 | Completada |
| ACC-007 | Extraer persistencia y composición de Access | ACC-005, ACC-006 | Completada |
| ACC-008 | Migrar bootstrap a una vertical slice | ACC-007 | En curso |
| ACC-009 | Migrar sesiones e identidad actual | ACC-007 | En curso |
| ACC-010 | Migrar queries de invitaciones sin efectos laterales | ACC-007, ACC-009 | En curso |
| ACC-011 | Migrar emisión, reenvío y revocación | ACC-007, ACC-010 | En curso |
| ACC-012 | Migrar aceptación transaccional de invitaciones | ACC-009, ACC-011 | En curso |
| ACC-013 | Reestructurar y endurecer el outbox | ACC-011, ACC-012 | Pendiente |
| ACC-014 | Separar observabilidad de Application y Domain | ACC-008 a ACC-013 | Pendiente |
| ACC-015 | Completar suites de seguridad, concurrencia y migraciones | ACC-008 a ACC-014 | Pendiente |
| ACC-016 | Endurecer CI con fitness functions | ACC-003, ACC-015 | Pendiente |
| ACC-017 | Retirar la arquitectura anterior y simplificar el host | ACC-008 a ACC-016 | En curso |
| ACC-018 | Verificar despliegue y actualizar documentación | ACC-017 | Pendiente |

## Tareas

### [x] ACC-001 — Fijar contratos y comportamiento actual

**Resultado:** una red de seguridad describe la API existente antes de mover código.

**Trabajo:**

- Inventariar rutas de identidad, invitaciones, plataforma y campañas, incluyendo autorización, rate limiting, status codes, payloads y `ProblemDetails`.
- Añadir tests de caracterización de los endpoints que no estén cubiertos: bootstrap status, logout, `me`, listado, revocación y variantes de invitación de campaña.
- Registrar explícitamente los campos y estados públicos actuales para detectar cambios accidentales.
- Separar los helpers de test reutilizables para host, autenticación y base de datos.
- No modificar comportamiento productivo en esta tarea.

**Criterios de aceptación:**

- Cada ruta de Access tiene al menos un test de componente de camino principal y uno de autorización o error relevante.
- Los tests usan PostgreSQL real cuando interviene persistencia.
- La suite actual sigue verde y los nuevos tests fallarían ante un cambio de contrato significativo.
- Queda documentada cualquier conducta actual dudosa que deba conservarse temporalmente o corregirse en una tarea posterior.

### [x] ACC-002 — Crear el proyecto del módulo y su fachada

**Resultado:** existe un único ensamblado de Access y el host lo compone exclusivamente mediante su fachada pública.

**Trabajo:**

- Crear `DndCampaign.Modules.Access` bajo `apps/api/Modules/Access`, con carpetas Domain, Application, Infrastructure y Api.
- Añadirlo a `DndCampaign.slnx` y eliminar los proyectos por capa.
- Mantener `apps/api` como host y composition root.
- Centralizar propiedades comunes en `Directory.Build.props` sin añadir dependencias transversales innecesarias.
- Preparar una fachada explícita de registro, mapeo y ciclo de vida del módulo.
- Ajustar Docker únicamente si el nuevo árbol debe copiarse para compilar la solución.

**Criterios de aceptación:**

- El grafo entre módulos coincide con ADR-0004 y no contiene ciclos.
- El proyecto representa al módulo, no a una capa.
- El host no conoce namespaces ni tipos internos de Access.
- `dotnet build DndCampaign.slnx` y las pruebas existentes pasan.
- La imagen Docker de API sigue compilando.

### [x] ACC-003 — Incorporar architecture tests iniciales

**Resultado:** las fronteras entre módulos y las capas internas de Access se verifican automáticamente.

**Trabajo:**

- Crear `DndCampaign.ArchitectureTests` para límites globales entre módulos y host.
- Crear `Architecture/LayerBoundaryTests` dentro de `DndCampaign.Modules.Access.Tests`.
- Comprobar referencias permitidas entre módulos y dependencias internas por namespace/carpeta.
- Prohibir ASP.NET Core, EF Core, Infrastructure y OpenTelemetry en Domain y Application según corresponda.
- Prohibir que Access.Api dependa de tipos de persistencia.
- Detectar ciclos y exposición pública no autorizada.
- Mantener separadas las fitness functions globales de las reglas propias de cada módulo.

**Criterios de aceptación:**

- Una dependencia prohibida introducida deliberadamente hace fallar un test.
- Las excepciones temporales son explícitas, mínimas y enlazan ACC-017.
- Los tests se ejecutan mediante `dotnet test DndCampaign.slnx`.
- La frontera entre módulos se comprueba por ensamblado y la interna mediante análisis estático mantenido por el módulo.

### [x] ACC-004 — Extraer el dominio de Access

**Resultado:** cuentas, sesiones, invitaciones y concesiones de acceso residen en la capa Domain de Access sin dependencias externas.

**Trabajo:**

- Mover `UserAccount`, `UserSession`, `CampaignMembership` y los tipos de invitación al proyecto Domain.
- Organizar namespaces por capacidad: Accounts, Sessions, Invitations y CampaignAccess.
- Mantener invariantes, constructores controlados y generación segura de tokens.
- Mover sus tests puros a `Domain/` dentro del proyecto de tests de Access.
- Revisar visibilidad pública; dejar internos los tipos que no formen parte de contratos necesarios.

**Criterios de aceptación:**

- Domain compila sin ASP.NET, EF Core, logging, configuración ni telemetría.
- Los tests unitarios actuales del dominio se ejecutan contra los tipos trasladados.
- No se han añadido setters públicos para facilitar persistencia.
- El host continúa ejecutando los flujos actuales mediante referencias transitorias controladas.

### [x] ACC-005 — Unificar y persistir el agregado Invitation

**Resultado:** `Invitation` es la única fuente de verdad del ciclo de vida y EF Core la persiste directamente.

**Trabajo:**

- Incorporar al agregado los datos persistentes necesarios: emisor, receptor, aceptación, envío y contexto de campaña.
- Mantener en el agregado las transiciones de aceptación, revocación y expiración.
- Crear configuración EF Core en Access.Infrastructure sin contaminar Domain.
- Migrar consultas y escritura temporal que todavía usen `InvitationRecord`.
- Eliminar `InvitationRecord` cuando no tenga consumidores.
- Mantener compatibilidad de tablas y columnas o generar una migración explícita.
- Añadir control de concurrencia o constraints para las invariantes que no baste comprobar en memoria.

**Criterios de aceptación:**

- No existe una segunda clase con comportamiento de estado de invitación.
- Los tests unitarios validan el mismo agregado que carga EF Core.
- Una ida y vuelta real por PostgreSQL conserva todos los estados y timestamps.
- Las migraciones funcionan sobre base vacía y desde la versión anterior.
- Los endpoints existentes siguen produciendo los mismos contratos.

### [x] ACC-006 — Crear puertos y primitivas CQRS de Application

**Resultado:** Application dispone de contratos mínimos para casos de uso sin depender de infraestructura ni HTTP.

**Trabajo:**

- Definir interfaces de command handler y query handler, o equivalentes explícitos.
- Definir `Result` y errores funcionales tipados suficientes para Access.
- Definir puertos específicos de cuentas, sesiones, invitaciones, acceso a campañas, reloj, hashing/protección, read stores y unidad de trabajo.
- Diseñar un mecanismo de transacción por comando sin filtrar `DbContext` a handlers.
- Evitar repositorios genéricos y un service locator.
- Añadir tests de las primitivas y decoradores cuando contengan comportamiento.

**Criterios de aceptación:**

- Ningún contrato de Application devuelve `IResult`, `ProblemDetails` o status codes.
- Ningún puerto expone `IQueryable`, `DbSet`, entidades de Infrastructure o tipos de proveedor.
- Commands y queries incluyen identidad de actor cuando la autorización funcional la requiere.
- Architecture tests confirman la independencia de Application.

### [x] ACC-007 — Extraer persistencia y composición de Access

**Resultado:** la capa Infrastructure implementa los puertos y la fachada de Access encapsula su composición.

**Trabajo:**

- Crear el DbContext propietario de Access y mover las configuraciones EF.
- Mantener la continuidad de migraciones y del assembly usado para generarlas.
- Implementar repositorios de escritura, read stores y unidad de trabajo.
- Mover autenticación de sesión, password hashing, protección de tokens, correo y opciones a adaptadores adecuados.
- Mantener `AddAccessInfrastructure` y el registro de Api internos; el host solo invoca `AddAccessModule`.
- Mantener tablas y esquema actuales durante este incremento salvo migración justificada.

**Criterios de aceptación:**

- Application no referencia el DbContext.
- Api no referencia repositorios ni entidades persistentes.
- Una prueba de integración valida cada implementación de puerto crítica.
- `Database.Migrate` conserva el historial existente.
- Liveness, readiness y arranque del host mantienen su comportamiento.

### [-] ACC-008 — Migrar bootstrap a una vertical slice

**Resultado:** status y creación inicial se ejecutan mediante query y command handlers de Access.

**Trabajo:**

- Implementar `GetBootstrapStatusQuery` y `CompleteBootstrapCommand`.
- Trasladar validación funcional, hashing y transacción fuera del endpoint.
- Mantener token de bootstrap, rate limiting, respuesta `Created` y errores actuales.
- Proteger mediante base de datos y transacción la creación única bajo concurrencia.
- Mapear resultados tipados a HTTP en Access.Api.
- Retirar las funciones de bootstrap del endpoint legado.

**Criterios de aceptación:**

- El endpoint no usa EF Core ni muta entidades directamente.
- Dos bootstrap simultáneos crean exactamente una cuenta administradora.
- Se conservan rutas, payloads, status codes y `ProblemDetails`.
- Hay tests unitarios de handler y tests de componente con PostgreSQL.

### [-] ACC-009 — Migrar sesiones e identidad actual

**Resultado:** login, logout, usuario actual y autenticación de sesión atraviesan límites de Access explícitos.

**Trabajo:**

- Implementar commands de login y logout y query de usuario actual.
- Mantener rehash de contraseña, emisión opaca, expiración y revocación de sesión.
- Encapsular búsqueda y validación de sesión en Infrastructure.
- Mantener la construcción de claims en el adaptador de autenticación.
- Eliminar acceso al DbContext desde endpoints de identidad.
- Conservar métricas de intentos, fallos y finalización mediante adaptadores o decoradores.

**Criterios de aceptación:**

- Los endpoints solo construyen requests de Application y mapean resultados.
- Los tokens no se almacenan ni registran en claro.
- Login, rehash, expiración, logout idempotente y `me` tienen cobertura.
- Autenticación inválida no revela si una cuenta existe.
- Las métricas no obligan a Application a depender de OpenTelemetry.

### [-] ACC-010 — Migrar queries de invitaciones sin efectos laterales

**Resultado:** preview y listados son lecturas puras, sin tracking ni escrituras implícitas.

**Trabajo:**

- Implementar `PreviewInvitationQuery` y `ListInvitationsQuery`.
- Crear modelos de lectura específicos y proyecciones `AsNoTracking`.
- Calcular el estado efectivo de expiración durante la lectura.
- Resolver el estado de entrega mediante una proyección eficiente del outbox.
- Integrar autorización de campaña en el handler de listado.
- Eliminar `MarkExpired` y `SaveChanges` de los caminos de lectura.

**Criterios de aceptación:**

- Ejecutar una query no genera `INSERT`, `UPDATE` ni `DELETE`.
- Preview no revela información privada adicional para tokens inválidos o finalizados.
- Listado conserva orden, límite y campos públicos.
- Un usuario no puede listar invitaciones de una campaña ajena alterando el identificador.
- Architecture tests impiden dependencias de escritura en query handlers.

### [-] ACC-011 — Migrar emisión, reenvío y revocación

**Resultado:** cada cambio de estado de invitación tiene un command handler específico.

**Trabajo:**

- Implementar commands de emisión de plataforma y campaña, reenvío y revocación.
- Trasladar normalización, autorización funcional y política de reenvío.
- Crear invitación y outbox en una única unidad de trabajo.
- Proteger la unicidad de invitación pendiente ante emisiones concurrentes.
- Mantener métricas, errores de conflicto y `retryAt` mediante mapeo de resultados.
- Retirar `InvitationService` cuando deje de tener consumidores.

**Criterios de aceptación:**

- No existe un servicio general que concentre las operaciones de invitación.
- Dos emisiones concurrentes al mismo contexto producen una sola invitación pendiente.
- Reenvío conserva límites de 15 minutos y cinco emisiones en 24 horas.
- Revocación es consistente ante estados finalizados.
- API y tests de componente conservan la semántica pública.

### [-] ACC-012 — Migrar aceptación transaccional de invitaciones

**Resultado:** aceptación coordina invitación, cuenta, sesión y acceso desde un único command handler.

**Trabajo:**

- Implementar `AcceptInvitationCommand` con actor opcional y datos de alta.
- Distinguir usuario existente de nuevo sin filtrar esa decisión al endpoint.
- Validar que un usuario existente autenticado coincide con el destinatario.
- Crear cuenta y sesión solo cuando corresponda.
- Conceder acceso de jugador únicamente para invitaciones de campaña.
- Confirmar invitación, cuenta, sesión y concesión en una transacción.
- Traducir resultados a `Unauthorized`, `Forbid`, `Gone`, validación o éxito como actualmente.

**Criterios de aceptación:**

- Dos aceptaciones simultáneas del mismo token producen un único efecto.
- Un fallo intermedio no deja cuenta, sesión, membresía o invitación parcialmente confirmadas.
- Un usuario existente no puede aceptar una invitación dirigida a otra identidad.
- Una invitación de campaña nunca concede rol DM.
- Los escenarios de usuario existente, usuario nuevo, token reutilizado y token expirado tienen cobertura de componente.

### [ ] ACC-013 — Reestructurar y endurecer el outbox

**Resultado:** el polling pertenece a Infrastructure y el procesamiento es seguro ante varias instancias.

**Trabajo:**

- Mover el hosted service a Access.Infrastructure.
- Extraer un caso de uso para procesar un mensaje adquirido.
- Implementar claim atómico mediante bloqueo de fila o actualización condicional equivalente.
- Conservar lease, backoff, máximo de intentos, descarte y borrado del ciphertext procesado.
- Añadir clave de idempotencia o mecanismo de deduplicación compatible con el proveedor.
- Evitar resolución manual de dependencias dentro del procesamiento funcional.
- Mantener cancelación y espera ante indisponibilidad de PostgreSQL.

**Criterios de aceptación:**

- Dos workers concurrentes no envían dos veces el mismo mensaje.
- Un lease abandonado puede recuperarse después de su vencimiento.
- Los fallos transitorios reintentan y los payloads inválidos quedan diagnosticados sin exponer secretos.
- La invitación revocada, aceptada o expirada no se envía.
- Los tests usan PostgreSQL real y un sender controlado.

### [ ] ACC-014 — Separar observabilidad de Application y Domain

**Resultado:** se conservan señales operativas sin dependencias internas de OpenTelemetry.

**Trabajo:**

- Mover contadores, histogramas y logging concreto a Api o Infrastructure.
- Introducir decoradores o listeners para observar commands, queries y entrega de correo.
- Conservar nombres y atributos utilizados por dashboards cuando sean adecuados.
- Actualizar dashboards si cambia alguna métrica.
- Revisar logs y tags para impedir tokens, correo en claro o contenido sensible.

**Criterios de aceptación:**

- Domain y Application no referencian OpenTelemetry ni `ILogger` de adaptadores.
- Los flujos de bootstrap, login, invitación y outbox siguen siendo observables.
- Trazas y logs conservan correlación.
- Una revisión automatizada o test confirma que las excepciones públicas y logs controlados no incluyen tokens ni destinatarios.

### [ ] ACC-015 — Completar suites de seguridad, concurrencia y migraciones

**Resultado:** las garantías más arriesgadas se verifican con el almacén y host reales.

**Trabajo:**

- Organizar Domain, Application, Infrastructure, Architecture y Component dentro del proyecto de tests de Access.
- Añadir escenarios concurrentes de bootstrap, emisión, aceptación y outbox.
- Añadir matriz de autorización para administrador, DM, jugador, anónimo y campaña ajena.
- Probar migración desde base vacía y desde el snapshot anterior soportado.
- Mantener contrato del adaptador Brevo con HTTP controlado.
- Clasificar suites para que CI conozca cuándo debería ejecutar integración.

**Criterios de aceptación:**

- No se simulan `DbContext` ni `DbSet` en tests unitarios.
- Integración y componente usan PostgreSQL real.
- Todas las condiciones de carrera indicadas en ADR-0004 tienen un test reproducible.
- La ausencia de configuración de integración en CI produce fallo, no una suite silenciosamente verde.
- Los proyectos de test tienen responsabilidades y nombres inequívocos.

### [ ] ACC-016 — Endurecer CI con fitness functions

**Resultado:** el pipeline impide integrar violaciones arquitectónicas o suites incompletas.

**Trabajo:**

- Ejecutar unitarios y architecture tests antes de integración.
- Ejecutar integración, componente y migraciones con PostgreSQL aprovisionado.
- Tratar warnings del código propio como errores y documentar excepciones externas.
- Añadir comprobación de dependencias vulnerables y secretos.
- Generar y comparar el contrato OpenAPI cuando esté disponible.
- Hacer visible el número de tests ejecutados, omitidos y fallidos por suite.
- Mantener validaciones de Docker Compose e infraestructura existentes.

**Criterios de aceptación:**

- Una referencia arquitectónica prohibida falla en pull request.
- Una suite obligatoria completamente omitida falla en CI.
- Una migración incompatible falla antes del merge.
- La build de producción y la imagen Docker se verifican con la nueva solución.
- El tiempo añadido se registra para poder optimizar sin eliminar garantías.

### [-] ACC-017 — Retirar la arquitectura anterior y simplificar el host

**Resultado:** solo existe el camino modular; el host no contiene lógica de Access.

**Trabajo:**

- Eliminar `IdentityInvitationEndpoints`, `InvitationService`, `InvitationRecord`, el DbContext global y helpers sustituidos cuando no tengan consumidores.
- Sustituir los registros Minimal API de Access por controladores MVC internos descubiertos explícitamente por el módulo.
- Instalar Swashbuckle y publicar Swagger JSON/UI únicamente en `Development`.
- Retirar excepciones temporales de architecture tests.
- Reducir `Program.cs` a configuración, middleware, health checks y registro/mapeo de módulos.
- Confirmar que no quedan namespaces globales `Application`, `Domain` o `Infrastructure` con funcionalidad de Access.
- Revisar visibilidad pública y eliminar contratos no utilizados.
- Ejecutar análisis de referencias y código muerto.

**Criterios de aceptación:**

- No existe implementación paralela de ningún caso de uso de Access.
- Todas las reglas de arquitectura pasan sin excepciones temporales.
- El host no usa EF Core para endpoints funcionales ni construye entidades de Access.
- No quedan `MapGroup`, `MapGet`, `MapPost` o `MapDelete` en la capa Api de Access.
- `/swagger/v1/swagger.json` y `/swagger/index.html` están disponibles en Development y devuelven 404 en Production.
- La solución completa compila y todas las suites pasan.
- Los únicos cambios de comportamiento son los aprobados y documentados.

### [ ] ACC-018 — Verificar despliegue y actualizar documentación

**Resultado:** la implementación final está operativa y la documentación describe el sistema real.

**Trabajo:**

- Construir imágenes y levantar Docker Compose desde cero.
- Ejecutar smoke tests de API y recorrido integrado.
- Verificar migrations-at-startup con una base nueva y una actualizada.
- Actualizar README, diagrama de componentes y cualquier runbook afectado.
- Documentar cómo crear un módulo nuevo tomando Access como referencia.
- Revisar ADR-0004 y registrar cualquier desviación aprobada mediante addendum o ADR nuevo.
- Cerrar esta lista con evidencias de pruebas y decisiones descartadas.

**Criterios de aceptación:**

- Docker Compose arranca con liveness y readiness saludables.
- La imagen productiva contiene todos los ensamblados y migraciones necesarios.
- Los smoke tests existentes pasan sin cambios incompatibles.
- El diagrama de componentes refleja módulos, capas y dependencias reales.
- Todas las tareas están completadas o descartadas con justificación.
- La especificación 002 cumple su definición de terminado.

## Verificación final del incremento

Al completar ACC-018 se registrarán aquí:

- commit o pull request de implementación;
- resultado de las suites unitarias, arquitectura, integración y componente;
- resultado de migraciones desde cero y actualización;
- resultado de Docker Compose y smoke tests;
- desviaciones respecto de ADR-0004;
- deuda técnica expresamente aceptada.
