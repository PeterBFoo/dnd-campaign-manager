# Spec 006: Resumen de personajes activos en campaña

- Estado: Completada
- Fecha: 2026-08-23
- Requisitos del roadmap: RF-005, RF-006, RF-008 y RF-020
- Dependencias: [spec 004](../004-creacion-campanas/spec.md) y [spec 005](../005-personajes-campana/spec.md)

## Problema y objetivo

La página principal de una campaña obliga actualmente a entrar en la gestión del elenco para descubrir sus personajes. El incremento mostrará directamente los personajes activos de la campaña mediante su retrato y nombre, y reservará una acción secundaria para administrar personajes.

El jugador accederá desde esa acción únicamente a sus personajes. El DM accederá al elenco completo, ya que puede administrar cualquier personaje de su campaña.

## Alcance funcional

- La página principal de campaña muestra una sección de personajes activos.
- La sección incluye únicamente personajes con estado activo y presenta, como mínimo, su imagen y nombre.
- La imagen por defecto se muestra de la misma forma que un retrato subido por el usuario.
- Un estado de carga y otro estado vacío evitan presentar el antiguo bloque genérico como si hubiera personajes disponibles.
- La página incluye una acción de gestión diferenciada por rol:
  - el jugador ve `Gestionar mis personajes` y accede únicamente a los personajes que le pertenecen;
  - el DM ve `Gestionar personajes` y accede al elenco completo de la campaña.
- Las acciones de creación, activación, edición y eliminación permanecen en la pantalla de gestión existente.

## Reglas y seguridad

- El resumen consume el listado autorizado de personajes de la campaña y filtra por `isActive`; no crea un estado activo alternativo en el navegador.
- El resumen es visible únicamente para DM o jugadores aceptados que ya pueden consultar la campaña.
- La imagen privada se obtiene mediante el endpoint autenticado existente. No se expone la clave de Azure Blob Storage ni se generan URLs públicas.
- Ocultar personajes ajenos en la pantalla de gestión del jugador es una decisión de experiencia, no un control de seguridad. La API mantiene la autorización de ownership para todas las escrituras.
- El DM conserva la capacidad de editar o eliminar personajes vinculados, no vinculados y activos de su campaña.

## Criterios de aceptación

1. Al abrir una campaña con personajes activos, aparecen en su página principal solo esos personajes, cada uno con nombre e imagen.
2. Un personaje inactivo no aparece en el resumen, aunque continúe disponible en la gestión correspondiente.
3. Si no hay personajes activos, se muestra un estado vacío comprensible y la acción de gestión sigue disponible.
4. Un jugador encuentra `Gestionar mis personajes` y la pantalla enlazada no muestra personajes de otros jugadores.
5. El DM encuentra `Gestionar personajes` y la pantalla enlazada muestra el elenco completo con sus controles autorizados.
6. Los retratos subidos continúan descargándose con autorización y los personajes sin retrato muestran la imagen por defecto.
7. Las pruebas de componentes, límites modulares y build Angular quedan verdes.

## Ownership técnico

- `apps/web/modules/characters`: propietario del componente de resumen, la carga segura de retratos y la proyección del listado de gestión por rol.
- `apps/web/modules/campaigns`: integra el componente público de Characters en la página principal de campaña.
- `apps/api`: no cambia. El spec 005 ya entrega listado autorizado, estado activo, ownership y lectura privada de imágenes; añadir un endpoint específico duplicaría reglas sin aportar un límite nuevo.

## Observabilidad y privacidad

Los fallos de listado se muestran mediante el tratamiento común de `ProblemDetails`. Un error aislado al descargar un retrato conserva el avatar por defecto y no impide mostrar el resto del resumen. No se registran nombres de personaje ni claves de objeto en el navegador.

## Fuera de alcance

- Añadir CA, iniciativa u otros datos al resumen principal.
- Permitir edición directa desde las tarjetas del resumen.
- Elegir el personaje activo desde la página principal.
- Cambiar las reglas de activación, ownership o almacenamiento del spec 005.
- Integrar los personajes activos en combates.

## Validación

La disposición del resumen, el filtrado de activos y las acciones de gestión por rol fueron solicitados por el usuario el 2026-08-23 sobre la pantalla principal existente.

La implementación quedó verificada con 52 pruebas Angular, incluida la prueba de límites modulares, build de producción y reconstrucción de la imagen Docker web.
