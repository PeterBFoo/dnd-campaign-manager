# Plan 006: Resumen de personajes activos en campaña

- Estado: Ejecutado
- Especificación: [spec.md](spec.md)
- Validación: la petición de implementación del usuario acepta el alcance funcional descrito el 2026-08-23

## Resultado esperado

La portada de una campaña presentará el nombre y retrato de sus personajes activos. Desde allí, el jugador podrá gestionar solo sus personajes y el DM podrá gestionar todo el elenco.

## Diseño

1. Crear en Characters un componente reutilizable que consulte el listado autorizado, seleccione los activos y resuelva sus imágenes privadas.
2. Publicar el componente mediante la API pública del módulo e integrarlo en el detalle de Campaigns.
3. Sustituir el bloque vacío actual por una cuadrícula responsiva con estados de carga, error y ausencia de activos.
4. Proyectar el listado de gestión según rol y ownership: propios para jugador, todos para DM.
5. Cubrir el filtrado, el texto de acción y la integración de campaña con pruebas Angular.
6. Verificar límites modulares y build de producción antes de cerrar trazabilidad.

## Verificación

- Tests de componentes Angular para resumen y gestión.
- Test de integración del detalle de campaña.
- Test de límites modulares del frontend.
- Build Angular de producción.

## Evidencia de cierre

- `pnpm --filter @dnd/web test --watch=false`: 52 tests correctos en 21 archivos.
- `pnpm --filter @dnd/web build`: build Angular de producción correcto.
- `docker compose up -d --build web`: imagen web reconstruida y servicios locales saludables.
