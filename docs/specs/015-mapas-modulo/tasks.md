# Tareas 015: Mapas reutilizables de un módulo

- Estado: Implementación completada; integraciones externas pendientes de entorno
- Especificación: [spec.md](spec.md)
- Plan: [plan.md](plan.md)

## Implementación

- [x] Crear la rama `feat/spec-015-mapas-modulo` desde `origin/main`.
- [x] Implementar modelo, validación, versiones y asociaciones idempotentes.
- [x] Añadir repositorio, configuración EF Core y migración `AdventureMaps`.
- [x] Añadir almacenamiento privado con compensación y validación de imagen.
- [x] Publicar endpoints administrativos y lectura reservada al DM.
- [x] Publicar el contrato Campaigns → AdventureCatalog para rol y módulo actual.
- [x] Implementar colección/editor administrativo y consulta DM en Angular.
- [x] Actualizar README, roadmap, índice, arquitectura y runbook.

## Verificación

- [x] Compilación de API sin errores ni advertencias.
- [x] Build de producción Angular.
- [x] Pruebas unitarias de dominio y contrato del cliente Angular.
- [x] Suite .NET integrada con capítulos y reconciliación de migraciones: 99 correctas, 0 fallos y 17 integraciones omitidas sin servicios.
- [x] Suite Angular: 77 pruebas correctas en 28 archivos.
- [x] `docker compose config --quiet` valida la configuración integrada.
- [ ] Ejecutar PostgreSQL y Azurite reales en CI o Compose para activar las integraciones omitidas.
- [x] Integrar la implementación completa de capítulos de la spec 014 y reutilizar su agregado, persistencia, contratos y rutas.
- [x] Hacer que la migración oficial de capítulos adopte sin pérdida una tabla provisional preexistente y cubrir la reconciliación con una prueba.
- [x] Hacer que la migración definitiva de mapas adopte sin pérdida tablas provisionales preexistentes y cubrir la reconciliación con una prueba.
- [ ] Verificar manualmente el recorrido completo con PostgreSQL y Azurite reales.
