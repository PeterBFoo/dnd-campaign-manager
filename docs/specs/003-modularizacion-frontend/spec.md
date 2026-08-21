# Especificación 003: Modularización del frontend por capacidades

- Estado: Aceptada
- Fecha: 2026-08-22
- Tipo: incremento técnico
- Decisión vinculante propuesta: [ADR-0005](../../adr/0005-frontend-modular-por-capacidades.md)

## Problema

El frontend Angular concentra composition root, shell, páginas, estado, autenticación, clientes HTTP, contratos y utilidades en un único nivel bajo `apps/web/src/app`. Esta estructura permitió completar el primer flujo funcional, pero no expresa ownership ni límites de dependencia y no ofrece una localización predecible para los módulos que se incorporen al API.

La página inicial ya compone acceso y estado de plataforma; los servicios actuales mezclan transporte y estado; y sesión, guards e interceptor son globales por alcance aunque funcionalmente pertenezcan a Access. Si el árbol continúa creciendo de forma plana, cada nuevo módulo aumentará el acoplamiento y dificultará encontrar, probar y cargar su código de forma independiente.

## Objetivo

Reorganizar la aplicación Angular como un monolito frontend modular, alineado por capacidades con los bounded contexts del API y organizado internamente por recorridos de usuario, sin modificar el comportamiento observable.

El resultado debe permitir añadir una nueva capacidad con una localización clara, una API pública mínima, rutas cargables de forma diferida y dependencias verificadas automáticamente.

## Alcance

- Composition root, shell y tabla de rutas de Angular.
- Módulo frontend `platform` para el estado público de la plataforma.
- Módulo frontend `access` para sesión, login, bootstrap, política de contraseña e invitaciones.
- Clientes HTTP y contratos de transporte de esos módulos.
- Estado local, de recorrido y global existente.
- Guards e interceptor de autenticación.
- Separación de la página inicial como composición de APIs públicas.
- Alias TypeScript y fitness functions del grafo de importaciones.
- Pruebas de caracterización, clientes, estado, routing, componentes y arquitectura.
- Documentación arquitectónica y comandos de CI afectados.

## Comportamiento que se debe conservar

- La ruta `/` muestra el mismo shell, acceso y estado de plataforma.
- `/bootstrap` mantiene el alta inicial, validación y mensajes actuales.
- `/accept-invitation` continúa leyendo el token desde el fragmento, retirándolo de la URL y soportando creación de cuenta o autenticación previa.
- `/admin/invitations` mantiene su guard de administración de plataforma.
- `/campaigns/:campaignId/invitations` mantiene autenticación y contexto de campaña.
- Las rutas desconocidas continúan redirigiendo a `/`.
- Se conservan URLs, métodos, payloads y respuestas de `/api/v1`.
- La sesión sigue usando la clave y el mecanismo de almacenamiento decididos por ADR-0003.
- El interceptor continúa adjuntando el bearer token cuando existe una sesión válida.
- Guards y navegación siguen siendo ayudas de UX; el API conserva toda autoridad de seguridad.
- No cambia el resultado visual como consecuencia de mover código.

## Restricciones

- Se conserva una única aplicación Angular, un único build y un único despliegue.
- Se mantienen componentes standalone, signals, RxJS y `HttpClient`.
- No se introduce un store global externo, Nx, librerías de workspace ni microfrontends.
- No se duplican entidades, invariantes ni autorización del backend en el navegador.
- `shared` solo contiene responsabilidades agnósticas del dominio.
- Cada incremento intermedio debe compilar y mantener verde la suite aplicable.
- Los movimientos se realizan por recorridos verticales, no mediante una reescritura simultánea.
- No se crearán directorios vacíos para capacidades futuras.

## Fuera de alcance

- Implementar nuevos módulos funcionales como Campaigns, Journal, Missions o Encounters.
- Cambiar diseño visual, contenido, accesibilidad o navegación funcional.
- Modificar autenticación, persistencia de sesión o política de contraseñas.
- Generar clientes desde OpenAPI.
- Cambiar endpoints o código del backend.
- Introducir SSR, hidratación, PWA u operación offline.
- Extraer paquetes reutilizables o despliegues independientes.
- Refactorizar estilos globales salvo lo imprescindible para conservar su resolución tras mover componentes.

## Criterios de aceptación

1. La raíz de `apps/web/src/app` contiene únicamente composition root, shell, módulos y elementos compartidos; no conserva páginas o servicios funcionales planos.
2. `platform` y `access` tienen ownership, rutas y APIs públicas explícitas según ADR-0005.
3. Cada recorrido actual está colocado en el módulo propietario y sus archivos relacionados permanecen próximos.
4. El shell compone plataforma y acceso únicamente a través de sus APIs públicas.
5. `shared` no depende de `modules` ni de `shell` y no contiene tipos de usuario, campaña, sesión o invitación.
6. Ningún módulo realiza deep imports sobre otro módulo y no existen ciclos en el grafo TypeScript.
7. Una fitness function ejecutada por la suite frontend falla al introducir deliberadamente una dependencia prohibida.
8. Los clientes HTTP no conservan estado de pantalla y sus contratos están dentro del módulo propietario.
9. El estado se encuentra en el ámbito mínimo: componente, recorrido, módulo o sesión global justificada.
10. La sesión conserva restauración, expiración, almacenamiento, logout e integración con interceptor y guards.
11. Todas las rutas actuales conservan path, parámetros, fragmentos, guards, redirecciones y lazy loading efectivo.
12. Los flujos existentes conservan comportamiento, validaciones, mensajes y contratos HTTP.
13. Las pruebas de caracterización, arquitectura, componentes y clientes pasan junto con el build de producción.
14. La imagen web continúa compilando y sirviéndose con la configuración Nginx actual.
15. README, diagrama de componentes, índice SDD y estado del ADR representan la arquitectura finalmente implementada.

## Validación

La especificación y su `plan.md` fueron validados explícitamente por el usuario el 2026-08-22. La validación autoriza crear `tasks.md`; no inicia por sí sola la implementación.

## Definición de terminado

El incremento estará terminado cuando todos los criterios de aceptación sean verificables, las tareas que se creen tras aprobar el plan estén completadas o justificadamente descartadas, no quede código funcional en la estructura plana anterior y la documentación describa la implementación real.
