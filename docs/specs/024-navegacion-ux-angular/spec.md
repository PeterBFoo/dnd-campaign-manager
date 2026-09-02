# Spec 024 — Navegación y lenguaje visual de la aplicación Angular

## Estado

Aceptada e implementada el 2026-09-02.

## Problema y objetivo

Las capacidades ya entregadas aparecen como páginas aisladas bajo una cabecera global. Esto obliga a volver al resumen de campaña para cambiar de herramienta y no comunica con claridad cuándo se navega por una campaña o por la administración de plataforma.

El incremento incorpora un shell autenticado persistente, adaptable y accesible que agrupa las rutas existentes por contexto y adopta un lenguaje visual común inspirado en una mesa de juego sobria. No crea capacidades de dominio ni reproduce contenido editorial del prototipo de referencia.

## Trazabilidad y dependencias

- **RF-001:** mantiene visibles la identidad autenticada y la salida de sesión.
- **RF-002:** separa visualmente las herramientas de campaña de la administración global; la autorización continúa perteneciendo al backend y a los guards.
- **RF-016:** muestra la entrada de módulos solo a administradores de plataforma.
- Depende de los specs 003, 004, 007, 008, 009, 012 y 014, cuyas rutas existentes integra sin alterar sus contratos.

## Alcance

### Web (`apps/web`)

- Sustituir la cabecera autenticada por un shell con marca, navegación, identidad y cierre de sesión.
- Ofrecer accesos contextuales a resumen, aventura, personajes, misiones, bitácora y encuentros cuando la URL identifica una campaña.
- Ofrecer accesos administrativos independientes cuando la sesión declara ese permiso.
- Mantener una cabecera pública para acceso, bootstrap y aceptación de invitaciones.
- Unificar colores, tipografías, superficies, jerarquía de títulos y densidad con un diseño adaptable a escritorio y móvil.
- Conservar rutas, acciones, permisos y datos reales; el prototipo es una referencia de UX, no una fuente de datos ni una especificación funcional.

### API (`apps/api`)

No cambia. El shell deriva exclusivamente la navegación del estado de sesión y de rutas ya disponibles. No necesita persistencia, contratos nuevos ni relajar autorización.

## Criterios de aceptación

1. Una sesión autenticada muestra navegación persistente y permite volver a la lista de campañas.
2. En una URL de campaña, el shell enlaza las herramientas implementadas usando el mismo identificador de campaña.
3. Las opciones de administración solo se renderizan para una sesión administradora.
4. La opción correspondiente a la URL actual queda identificada visualmente.
5. El usuario puede cerrar sesión desde el shell.
6. Sin sesión se mantiene la experiencia pública y no se muestra navegación privada.
7. A 760 px o menos, el shell deja de reservar una columna lateral y la navegación sigue siendo utilizable.
8. Las pruebas web y el build de producción terminan correctamente.

## Seguridad y observabilidad

La ocultación de enlaces no constituye autorización: los guards y la API siguen validando identidad, rol y campaña. No se añaden eventos de dominio ni telemetría porque el incremento no ejecuta operaciones nuevas.

## Fuera de alcance

- Implementar mapas, localizaciones, NPC u otras capacidades todavía propuestas.
- Simular roles, campañas o contenido de aventura como hacía el prototipo estático.
- Cambiar contratos, autorización, persistencia o comportamiento de las páginas.
- Descargar fuentes, imágenes o contenido editorial externo.

