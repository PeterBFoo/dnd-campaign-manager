# Plan 013: Asignación de un módulo a una campaña

- Estado: En ejecución
- Fecha: 2026-08-29
- Especificación: [spec.md](spec.md)
- Dependencias: [spec 004](../004-creacion-campanas/spec.md) y [spec 012](../012-libreria-modulos/spec.md)
- Decisión arquitectónica: [ADR-0008](../../adr/0008-asociacion-campanas-modulos.md)

## Estrategia

1. Incorporar al agregado Campaign las transiciones idempotentes para crear con módulo, asignarlo, sustituirlo y retirarlo.
2. Consumir desde Campaigns un contrato público y mínimo de AdventureCatalog que resuelva existencia y resumen sin exponer su persistencia.
3. Proteger las escrituras de la asociación con una versión esperada y traducir la concurrencia obsoleta a `409`.
4. Añadir una FK entre `campaigns.campaigns.AdventureModuleId` y la tabla propietaria de AdventureCatalog con `ON DELETE SET NULL`.
5. Ampliar creación, detalle y endpoints de asociación; enriquecer las respuestas con el resumen opcional del módulo.
6. Publicar las opciones minimizadas desde AdventureCatalog y consumirlas en los recorridos Angular de alta y detalle.
7. Verificar dominio, Application, PostgreSQL, HTTP, límites modulares y componentes web antes de cerrar la trazabilidad.

## Backend y contratos intermodulares

- AdventureCatalog publicará un lector de campaña que devuelva únicamente `Id`, `Name` y `CoverUrl`, además de la consulta autenticada de opciones.
- Campaigns dependerá del contrato público de AdventureCatalog; AdventureCatalog no dependerá de Campaigns. La FK resuelve la desasociación al borrar y evita una llamada inversa.
- Crear o asignar valida el módulo mediante el lector dentro de la operación. La FK actúa como última defensa si el módulo desaparece entre validación y escritura.
- El detalle y los listados resuelven los resúmenes por lote para evitar consultas N+1. Una referencia nula produce `adventureModule: null`.
- La creación acepta `adventureModuleId` nullable. La asignación `PUT` recibe `adventureModuleId` y `expectedVersion`; la retirada `DELETE` recibe la versión esperada mediante `If-Match` o un contrato equivalente concretado al implementar el cliente.
- Se mantiene temporalmente `adventureModuleId` en las respuestas para compatibilidad, acompañado de `adventureModule` y `version`.

## Persistencia y concurrencia

- Campaigns incorpora un token de versión explícito, con valor inicial compatible para filas existentes.
- Asignar el mismo módulo y retirar cuando no existe asociación son éxitos idempotentes y no generan una versión nueva.
- Un cambio real incrementa la versión. Una versión esperada obsoleta devuelve `409` y conserva el valor vigente.
- La migración de Campaigns se aplica después de la migración base de AdventureCatalog, crea la FK entre esquemas con `ON DELETE SET NULL` y preserva las campañas actuales sin módulo.
- La eliminación física del módulo y la puesta a null ocurren en la misma transacción PostgreSQL. El orden de despliegue y rollback se documenta junto a la migración.

## API, autorización y errores

- `POST /api/v1/campaigns` valida el módulo opcional antes de persistir y conserva el comportamiento actual cuando no se envía.
- `PUT` y `DELETE /api/v1/campaigns/{campaignId}/adventure-module` cargan para escritura, distinguen `404` de campaña, `403` de actor no DM, `404` de módulo y `409` de concurrencia.
- `GET /api/v1/campaigns` y el detalle proyectan el resumen seguro sin descripción ni contenido de dirección.
- `GET /api/v1/adventure-modules/options` exige autenticación y no exige rol de administración.
- Las métricas existentes de Campaigns incorporan `create_with_module`, `assign_module`, `change_module` y `remove_module`; AdventureCatalog mide `list_options`. Las etiquetas se limitan a operación y resultado.

## Web

- El cliente público de AdventureCatalog expone las opciones minimizadas; Campaigns no accede a detalles administrativos.
- El alta carga opciones de forma independiente. Un fallo deja crear sin módulo y ofrece reintento; una opción seleccionada que desaparezca mantiene el formulario y muestra un error recuperable.
- El detalle muestra nombre y portada o sustituto visual a DM y jugador.
- Solo el DM ve controles para asignar, cambiar o retirar. Cambiar y retirar requieren confirmación, bloquean dobles envíos y actualizan el resumen y versión devueltos.
- Las pruebas comprueban estado vacío, carga, error, creación con y sin módulo, visibilidad por rol, confirmación, conflicto y recarga tras desaparición.

## Seguridad y privacidad

- La autorización se aplica en handlers; la ausencia de controles en Angular no es una frontera de seguridad.
- Las opciones y el resumen excluyen descripción, procedencia, auditoría y contenido reservado al DM.
- Las portadas conservan el mecanismo privado y autorizado del spec 012; la web no recibe claves de almacenamiento.
- Logs, métricas y errores no incluyen nombres ni identificadores de campañas, módulos o usuarios.

## Pruebas y despliegue

- Pruebas de dominio para alta, asignación, cambio, retirada, idempotencia y rechazo de identificadores vacíos.
- Pruebas de handlers para autorización, existencia, proyección por lote y concurrencia.
- Pruebas PostgreSQL para FK, `ON DELETE SET NULL`, carreras de borrado/asignación y preservación de campañas.
- Pruebas de contrato HTTP con autenticación real para `201`, `204/200`, `401`, `403`, `404` y `409`.
- Pruebas Angular de clientes y páginas, suite completa, compilación .NET, build Angular, imágenes y Compose.
- El despliegue exige tener la spec 012 aplicada antes de ejecutar la migración de la 013. Hasta entonces solo se integran los bloques independientes del catálogo.

## Documentación y cierre

Al completar el recorrido se actualizarán el estado de la spec, `docs/specs/README.md`, la trazabilidad de RF-010, RF-011, RF-014, RF-015 y RF-017 en el roadmap, el ADR y las evidencias de pruebas. No se incorporará contenido editorial concreto.
