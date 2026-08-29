# Instrucciones de trabajo del repositorio

## Roadmap y especificaciones

- La fuente de verdad del alcance funcional completo es `docs/roadmap/product-roadmap.md`.
- El roadmap no es una especificación ejecutable y nunca debe implementarse de una sola vez.
- Antes de proponer o implementar una capacidad, revisar el roadmap, `docs/specs/README.md`, los specs relacionados y el código existente.
- Cada incremento nuevo debe vivir en `docs/specs/NNN-nombre/` y contener `spec.md`; `plan.md` y `tasks.md` se crean después de aceptar el spec.
- Un spec funcional debe ser vertical y acotado: entregar una capacidad verificable, con su ownership y cambios necesarios tanto en `apps/web` como en `apps/api`. Si una de las superficies no cambia, debe justificarlo expresamente.
- Cada spec debe enlazar los requisitos `RF-*` del roadmap que cubre, indicar dependencias con otros specs y declarar qué queda fuera de alcance.
- El roadmap solo registra alcance, secuencia, dependencias y estado agregado. Los criterios de aceptación ejecutables y las decisiones concretas pertenecen al spec del incremento.
- Al terminar un incremento, actualizar su estado en `docs/specs/README.md` y la trazabilidad correspondiente en el roadmap usando evidencia de código y pruebas.
- El siguiente identificador disponible para un spec es `023`; no se reutiliza `001`, que quedó reservado históricamente para el documento que originó el roadmap.

## Contenido editorial

- Mantener fuera de specs, ADR, diagramas y código cualquier contenido editorial concreto de aventuras salvo autorización expresa y fundamento de uso verificable.
- Usar ejemplos genéricos y respetar las restricciones de procedencia, licencia y atribución del roadmap.
