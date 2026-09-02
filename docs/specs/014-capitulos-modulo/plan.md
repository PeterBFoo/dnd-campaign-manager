# Plan 014: Capítulos ordenados de un módulo

- Estado: Implementado; verificaciones de entorno pendientes
- Fecha: 2026-08-30
- Especificación: [spec.md](spec.md)
- Dependencias: [spec 012](../012-libreria-modulos/spec.md) y [spec 013](../013-asignacion-modulo-campana/spec.md)
- Decisión arquitectónica: [ADR-0011](../../adr/0011-lectura-de-contenido-de-campana-sin-ciclos-modulares.md)

## Estrategia

1. Incorporar capítulos y una versión independiente del índice al agregado de módulo, con posiciones densas y operaciones transaccionales.
2. Persistir capítulos en el esquema de AdventureCatalog con unicidad `(ModuleId, Position)` y concurrencia optimista.
3. Publicar CRUD y reordenación administrativos con procedencia editorial y errores coherentes.
4. Resolver lectura DM mediante un puerto mínimo adaptado en la composición raíz al contrato público de Campaigns.
5. Entregar rutas Angular separadas para autoría administrativa y consulta DM, sin exponer contenido a jugadores.
6. Verificar dominio, persistencia PostgreSQL, autorización, HTTP, límites modulares y experiencia web antes de cerrar trazabilidad.

## Backend, dominio y persistencia

- `AdventureChapter` conserva identificador, módulo, nombre normalizado, descripción opcional, posición, procedencia, auditoría y versión.
- `AdventureModule` conserva `ChaptersVersion`; crear, eliminar y reordenar incrementan esa versión. Editar un capítulo conserva posición y no invalida una reordenación que no cambie la membresía del índice.
- Crear añade al final; eliminar compacta; reordenar valida exactamente el conjunto vigente y una versión esperada antes de mutar.
- La migración crea `adventure_chapters`, FK con borrado en cascada del módulo, restricciones de longitud/posición/versión e índice único por módulo y posición.
- El repositorio carga el agregado con capítulos para escrituras y traduce concurrencia o unicidad a conflicto sin cambios parciales.

## API, autorización y contratos

- Los endpoints administrativos exigen `platform-admin`, procedencia verificable y versión esperada para edición, borrado y orden.
- La lectura de campaña obtiene el usuario autenticado y resuelve en servidor campaña, rol DM y módulo actual. Una campaña inexistente o sin módulo produce `404`; un actor que no es DM produce `403`.
- Índices y detalles DM omiten procedencia, usuario modificador y datos administrativos.
- El adaptador definido por ADR-0011 vive en la composición raíz y evita referencias circulares.
- Las métricas usan únicamente operación y resultado para listado, detalle, alta, edición, orden y borrado.

## Web

- La ruta administrativa muestra carga, vacío, error y conflicto; permite alta, edición, eliminación y movimiento mediante botones accesibles por teclado.
- La ruta de campaña es lazy, solo se enlaza para el DM y muestra índice y detalle de solo lectura. El estado sin módulo explica la ausencia y permite volver.
- Los clientes tipados no comparten contratos administrativos con Campaigns y muestran pérdida de autorización o conflicto como estados recuperables.
- Las pruebas cubren formularios, orden, vacío, error, conflicto, visibilidad de navegación y lectura DM.

## Seguridad, observabilidad y despliegue

- Backend aplica autorización completa; ocultar enlaces en Angular no constituye control de acceso.
- No se registran nombres, descripciones, identificadores ni procedencia en métricas o logs.
- La migración de spec 014 se aplica después de las de specs 012 y 013; su rollback elimina solo capítulos y su versión, nunca campañas.
- No se incorpora contenido editorial concreto; las fixtures usan ejemplos genéricos con procedencia original.

## Verificación y cierre

- Pruebas unitarias del agregado y handlers para invariantes, autorización y concurrencia.
- Pruebas PostgreSQL para unicidad, compactación, transacciones y carreras, condicionadas a la base de integración.
- Pruebas HTTP de estados `200`, `201`, `400`, `401`, `403`, `404` y `409` y ausencia de campos administrativos en lectura DM.
- Suite Angular, build .NET, build web y reglas de arquitectura.
- Al completar, actualizar spec, tareas, índice, roadmap, ADR y evidencias sin atribuir éxito a verificaciones no ejecutadas.
