# Tareas 012: Librería administrable de módulos de aventura

- Estado: En ejecución
- Especificación: [spec.md](spec.md)
- Plan: [plan.md](plan.md)

## Dominio y Application

- [x] Crear el proyecto y la fachada pública de `AdventureCatalog`.
- [x] Modelar `AdventureModule`, procedencia, portada opcional, auditoría y versión.
- [x] Implementar resultados, DTO, puertos y handlers de listado, detalle, alta, edición, borrado y portada.
- [x] Aplicar autorización administrativa también dentro de Application.
- [ ] Cubrir dominio y handlers con pruebas unitarias.

## Persistencia y almacenamiento

- [x] Crear `AdventureCatalogDbContext`, repositorio, factoría y esquema propio.
- [x] Proteger nombre normalizado único, coherencia de portada y concurrencia en PostgreSQL.
- [x] Implementar `IAdventureModuleCoverStore` sobre Blob/Azurite privado con validación binaria.
- [x] Implementar compensación, sustitución y eliminación idempotente de portadas.
- [x] Generar la migración inicial de AdventureCatalog (verificación pendiente de entorno).
- [ ] Cubrir persistencia y almacenamiento con PostgreSQL y Azurite reales.

## API y arquitectura

- [x] Implementar endpoints administrativos multipart y mapeo de `ProblemDetails`.
- [x] Entregar portadas con autorización, tipo seguro, `nosniff` y caché privada.
- [x] Registrar módulo, controladores, métricas y migración en el host.
- [x] Añadir proyecto de tests y actualizar solución, Dockerfile y fitness functions.
- [ ] Cubrir contrato HTTP para administrador, usuario, anónimo, conflictos y campos privados.

## Web

- [x] Crear módulo Angular `adventure-catalog`, rutas lazy, cliente y contratos.
- [x] Implementar listado con carga, vacío, error y fallback textual.
- [x] Implementar alta y edición con procedencia y portada opcional.
- [x] Implementar retirada y borrado confirmado; sustitución/conflicto quedan para prueba de contrato.
- [x] Añadir navegación administrativa y preparar accesos futuros sin rutas vacías.
- [ ] Cubrir rutas, cliente, páginas, autorización visual y límites modulares.

## Infraestructura, observabilidad y documentación

- [x] Configurar el contenedor privado adicional en Compose, tests, Terraform y despliegue.
- [x] Añadir métricas y documentación de diagnóstico de operaciones y blobs huérfanos.
- [ ] Actualizar diagramas, almacenamiento, despliegue, migraciones y documentación relacionada.
- [x] Mantener fuera del repositorio cualquier contenido editorial concreto.

## Verificación y cierre

- [ ] Ejecutar suites .NET y Angular.
- [ ] Ejecutar builds .NET y Angular e imágenes API/web.
- [x] Validar Compose y Terraform.
- [ ] Actualizar estados, trazabilidad y evidencias reales de cierre.
