# Especificaciones y flujo SDD

Cada capacidad funcional o técnica se desarrolla a partir de una especificación versionada en este directorio.

## Estructura

```text
docs/specs/NNN-nombre/
├── spec.md
├── plan.md
└── tasks.md
```

### `spec.md`

Define el problema, objetivo, alcance, actores, reglas, criterios de aceptación, observabilidad y elementos fuera de alcance. No contiene detalles accidentales de implementación salvo que sean una restricción aceptada.

### `plan.md`

Se crea después de aceptar la especificación. Describe cambios de frontend, backend, base de datos, seguridad, API, telemetría, pruebas, despliegue, documentación y ADR relacionados.

### `tasks.md`

Descompone el plan aprobado en tareas pequeñas, ordenadas y verificables. Cada tarea debe producir un resultado comprobable y mantener actualizado su estado.

## Ciclo de vida

1. Redactar y revisar `spec.md`.
2. Aceptar o rechazar las decisiones abiertas.
3. Crear los ADR transversales necesarios.
4. Redactar `plan.md`.
5. Descomponer el plan en `tasks.md`.
6. Implementar junto con pruebas y telemetría.
7. Verificar los criterios de aceptación.
8. Actualizar documentación, ADR y runbooks afectados.

## Especificaciones

| Especificación | Estado | Alcance |
|---|---|---|
| [001: requisitos funcionales base](001-requisitos-funcionales-base/spec.md) | Borrador para validación | Identidad, campañas, módulos de aventura y herramientas comunes de juego |
| [002: modularización de Access](002-modularizacion-access/spec.md) | Aceptada | Extracción modular de la API actual, CQRS ligero y límites arquitectónicos verificables |

## Definición de terminado

Una especificación está terminada cuando:

- Se cumplen y verifican todos sus criterios de aceptación.
- Las tareas están completadas o justificadamente descartadas.
- Las pruebas relevantes pasan.
- Los cambios de datos tienen una migración verificable.
- Los errores y operaciones relevantes son observables.
- No se han incorporado secretos ni datos privados.
- Docker Compose continúa arrancando de forma saludable.
- La documentación general y los ADR están actualizados.
