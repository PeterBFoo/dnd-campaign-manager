# Roadmap del producto

El [roadmap funcional](product-roadmap.md) es la fuente de verdad del alcance completo previsto para la aplicación. Conserva el vocabulario, los requisitos `RF-*`, las dependencias entre capacidades y su estado agregado.

No es una especificación ejecutable ni tiene `plan.md` o `tasks.md`. Cada sesión que inicie funcionalidad nueva debe seleccionar una capacidad acotada y crear un spec independiente en [`docs/specs/`](../specs/README.md), con cambios verificables de frontend y API.

## Estados

- **Implementado:** existe comportamiento productivo en frontend y API, con pruebas proporcionales al riesgo.
- **Parcial:** existe una parte reutilizable o un recorrido incompleto, pero la capacidad todavía no se puede considerar entregada de extremo a extremo.
- **Pendiente:** no existe implementación funcional productiva.

Los estados se actualizan al cerrar el spec que aporta la evidencia; no se deducen de planes ni de tareas pendientes.
