# Especificaciones de incremento y flujo SDD

Cada capacidad funcional o técnica se desarrolla a partir de una especificación versionada en este directorio. El alcance completo del producto, su secuencia y su trazabilidad se mantienen aparte en el [roadmap de producto](../roadmap/product-roadmap.md).

El roadmap no es un spec implementable. Para iniciar un módulo se selecciona un incremento acotado del roadmap y se crea un nuevo directorio `NNN-nombre`; no se redacta un plan ni se modifica código directamente a partir del roadmap.

## Estructura

```text
docs/specs/NNN-nombre/
├── spec.md
├── plan.md
└── tasks.md
```

### `spec.md`

Define el problema, objetivo, alcance, actores, reglas, criterios de aceptación, observabilidad y elementos fuera de alcance. Enlaza los requisitos `RF-*` del roadmap y declara sus dependencias con otros specs. No contiene detalles accidentales de implementación salvo que sean una restricción aceptada.

### `plan.md`

Se crea después de aceptar la especificación. Describe cambios de frontend, backend, base de datos, seguridad, API, telemetría, pruebas, despliegue, documentación y ADR relacionados.

### `tasks.md`

Descompone el plan aprobado en tareas pequeñas, ordenadas y verificables. Cada tarea debe producir un resultado comprobable y mantener actualizado su estado.

## Ciclo de vida

1. Seleccionar en el roadmap una capacidad vertical acotada y comprobar sus dependencias.
2. Auditar el código y los specs existentes para evitar duplicar comportamiento ya implementado.
3. Crear y revisar `spec.md`, incluyendo trazabilidad `RF-*` y alcance de frontend y API.
4. Aceptar o rechazar las decisiones abiertas.
5. Crear los ADR transversales necesarios.
6. Redactar `plan.md`.
7. Descomponer el plan en `tasks.md`.
8. Implementar junto con pruebas y telemetría.
9. Verificar los criterios de aceptación.
10. Actualizar el roadmap, este índice, la documentación, los ADR y los runbooks afectados.

## Tamaño y límites de un incremento

- Debe producir una capacidad demostrable de principio a fin y poder verificarse independientemente.
- Debe asignar ownership explícito en `apps/web` y `apps/api`; una excepción debe quedar justificada en el spec.
- No debe agrupar varios dominios solo porque aparezcan juntos en el roadmap.
- Las dependencias transversales se resuelven mediante contratos o ADR, no ampliando silenciosamente el alcance.
- Las preguntas del roadmap se resuelven en el primer spec que necesite la respuesta.

## Especificaciones

| Especificación | Estado | Alcance |
|---|---|---|
| [002: modularización de Access](002-modularizacion-access/spec.md) | Aceptada; implementación en curso | Extracción modular de la API actual, CQRS ligero y límites arquitectónicos verificables |
| [003: modularización del frontend](003-modularizacion-frontend/spec.md) | Completada | Organización Angular por capacidades, APIs públicas y crecimiento alineado semánticamente con la API |
| [004: creación de campañas](004-creacion-campanas/spec.md) | Completada | Creación sin módulo, DM único, búsqueda de usuarios activos e invitaciones integradas |
| [005: personajes de campaña](005-personajes-campana/spec.md) | Completada | CRUD autorizado, vínculo opcional, imágenes privadas y selección activa por jugador y campaña |
| [006: resumen de personajes activos](006-resumen-personajes-activos/spec.md) | Completada | Portada de campaña con personajes activos y gestión diferenciada para jugador y DM |
| [007: bitácora compartida de campaña](007-bitacora-campana/spec.md) | Completada | Entradas compartidas por campaña, autoría visible, edición colaborativa entre jugadores y eliminación por su creador |
| [008: registro y gestión compartida de misiones](008-calendario-misiones/spec.md) | Implementada; cierre de imágenes pendiente | Registro sin fechas funcionales, creación por DM y jugadores, estados, borrado autorizado y misión principal única |
| [009: encuentros e iniciativa de combate](009-encuentros-iniciativa/spec.md) | Completada | Preparación de encuentros, iniciativa, turnos, rondas, enemigos y proyección segura para jugadores |
| [010: grupos de enemigos y eliminación de encuentros](010-grupos-enemigos-eliminacion-encuentros/spec.md) | En implementación | Turno compartido con vida individual por criatura y eliminación segura de encuentros no activos |
| [011: eliminación de campañas](011-eliminacion-campanas/spec.md) | Completada | Baja irreversible por el DM, revocación inmediata de acceso e invalidación de invitaciones pendientes |
| [012: librería de módulos](012-libreria-modulos/spec.md) | Aceptada; implementación iniciada | Catálogo administrable, metadatos, portada privada, procedencia y eliminación |
| [013: asignación de módulo a campaña](013-asignacion-modulo-campana/spec.md) | Propuesta | Selección opcional al crear, cambio, retirada y desasociación segura al eliminar el módulo |
| [014: capítulos del módulo](014-capitulos-modulo/spec.md) | Propuesta | Capítulos editables, orden estable y consulta completa reservada al DM |
| [015: mapas del módulo](015-mapas-modulo/spec.md) | Propuesta | Mapas e imágenes privadas reutilizables y asociaciones con capítulos |
| [016: localizaciones y puntos de interés](016-localizaciones-puntos-interes/spec.md) | Propuesta | Localizaciones, mapas detallados, placements, POI y relaciones sin duplicación |
| [017: viajes mediante cuadrícula](017-viajes-cuadricula/spec.md) | Propuesta | Cuadrículas cuadradas y hexagonales con cálculo escalado de distancia para el DM |
| [018: NPC del módulo](018-npc-modulo/spec.md) | Propuesta | Autoría de NPC, relaciones reutilizables y catálogo completo para el DM |
| [019: visibilidad de NPC por campaña](019-visibilidad-npc-campana/spec.md) | Propuesta | Desbloqueo independiente y proyección pública segura para jugadores |
| [020: contenido de Brujaluz](020-contenido-brujaluz/spec.md) | Propuesta | Carga editorial autorizada, trazable y sin excepciones específicas en el producto |
| [021: broker de eventos y entrega asíncrona de correo](021-broker-eventos-correo/spec.md) | Implementada; verificación de build/despliegue pendiente | Sustitución del sondeo PostgreSQL por eventos push, entrega de correo con Brevo, escala a cero y observabilidad Grafana en Azure |

El antiguo documento `001` se conserva como origen histórico de requisitos, pero ahora es el [roadmap de producto](../roadmap/product-roadmap.md) y no forma parte de la cola de specs ejecutables. El siguiente identificador libre es `022`.

## Definición de terminado

Una especificación está terminada cuando:

- Se cumplen y verifican todos sus criterios de aceptación.
- Las tareas están completadas o justificadamente descartadas.
- Las pruebas relevantes pasan.
- Los cambios de datos tienen una migración verificable.
- Los errores y operaciones relevantes son observables.
- No se han incorporado secretos ni datos privados.
- Docker Compose continúa arrancando de forma saludable.
- La documentación general y los ADR están actualizados.
