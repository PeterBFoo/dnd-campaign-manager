# D&D Campaign Manager

Aplicación web privada para gestionar campañas de D&D: participantes, personajes minimalistas, capítulos, NPC, localizaciones, bitácora, misiones e iniciativa.

El código se mantiene en un repositorio público, pero los módulos, recursos de campaña, credenciales, datos de usuarios y copias de seguridad son privados y no forman parte del repositorio.

## Estado

Proyecto en fase de diseño mediante Spec-Driven Development (SDD). La primera decisión arquitectónica está documentada en [ADR-0001](docs/adr/0001-monorepositorio-y-monolito-modular.md).

## Arquitectura aprobada

- Monorepositorio.
- Frontend Angular.
- Backend modular en ASP.NET Core.
- PostgreSQL como base de datos.
- Docker Compose para desarrollo y despliegue.
- Grafana, Prometheus, Loki y Tempo para observabilidad.

La selección y las versiones concretas de tecnologías se formalizarán en ADR posteriores.

## Estructura prevista

```text
apps/               Aplicaciones frontend y backend
tests/              Pruebas unitarias, de integración y end-to-end
deploy/             Despliegue y configuración de Docker Compose
observability/      Configuración de telemetría y dashboards
docs/adr/           Decisiones arquitectónicas
docs/specs/         Especificaciones, planes y tareas
docs/runbooks/      Procedimientos de operación
samples/            Datos ficticios y seguros para publicar
```

Las carpetas se crearán cuando una especificación aprobada las necesite; no se mantienen directorios vacíos.

## Flujo SDD

Cada incremento sigue este orden:

1. Especificación y criterios de aceptación.
2. ADR necesarios.
3. Plan de implementación.
4. Tareas atómicas.
5. Implementación y pruebas.
6. Validación y actualización documental.

Consulta [la guía de especificaciones](docs/specs/README.md) y [el índice de ADR](docs/adr/README.md).

## Información privada

No se deben confirmar en Git:

- Archivos `.env` o secretos.
- Contraseñas, tokens o cadenas de conexión.
- PDFs, mapas, imágenes o recursos privados de módulos.
- Datos persistentes de PostgreSQL o Grafana.
- Copias de seguridad.

El archivo `.gitignore` protege las ubicaciones previstas, pero cada cambio debe revisarse antes de confirmarlo.

## Licencia

Todavía no se ha seleccionado una licencia para el código. Que el repositorio sea público no concede por sí mismo permiso para reutilizarlo o redistribuirlo.
