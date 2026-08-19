# ADR-0001: Monorepositorio y monolito modular

- Estado: Aceptado
- Fecha: 2026-08-19
- Responsables: propietario del proyecto
- Ámbito: estructura del repositorio y arquitectura de despliegue

## Contexto

El proyecto es una aplicación web privada para un grupo de juego, aunque su código residirá en un repositorio público de GitHub. Incluye un frontend Angular, un backend en C#, PostgreSQL y componentes de observabilidad y despliegue administrados mediante Docker Compose.

Los requisitos se agrupan en varios dominios: identidad e invitaciones, campañas, personajes, contenido de módulos, capítulos, biblioteca, bitácora, misiones e iniciativa. Estos dominios necesitan límites claros, pero el volumen previsto de usuarios, el equipo de desarrollo y el despliegue en una sola instalación no justifican servicios distribuidos independientes.

El proyecto seguirá un flujo Spec-Driven Development. Las especificaciones, planes, tareas, decisiones arquitectónicas, código, pruebas y configuración de despliegue deben evolucionar de forma coordinada.

## Decisión

Se utilizará un único repositorio público de GitHub organizado como monorepositorio.

El backend se implementará como un monolito modular:

- Existirá un único proceso y artefacto desplegable para la API.
- Cada dominio funcional tendrá un límite explícito y será dueño de su lógica y modelo.
- Las dependencias entre módulos serán explícitas y se dirigirán hacia contratos internos, no hacia detalles de infraestructura.
- Un módulo no accederá directamente a las tablas o clases internas de otro módulo.
- La separación inicial será lógica y dentro del proceso; no se introducirán llamadas de red entre dominios.

El frontend, el backend, las pruebas, la infraestructura, la observabilidad y la documentación residirán en el mismo repositorio. Los componentes de infraestructura seguirán ejecutándose en contenedores independientes porque tienen ciclos de ejecución y almacenamiento propios; esto no convierte el backend en una arquitectura de microservicios.

La estructura objetivo será:

```text
/
├── apps/
│   ├── frontend/
│   └── backend/
├── tests/
├── deploy/
├── observability/
├── docs/
│   ├── architecture/
│   ├── adr/
│   ├── specs/
│   └── runbooks/
├── samples/
├── compose.yaml
└── README.md
```

Las carpetas se incorporarán de manera incremental cuando una especificación aprobada las necesite.

## Reglas del repositorio

- La rama principal será `main`.
- Cada cambio funcional comenzará con una especificación aprobada.
- Cada especificación tendrá `spec.md`, `plan.md` y `tasks.md`.
- Las decisiones transversales se documentarán mediante ADR antes de implementarlas.
- Código y documentación afectados se modificarán dentro del mismo cambio.
- El repositorio público solo incluirá datos ficticios y configuración sin secretos.
- Los recursos privados de campaña, secretos y datos persistentes se inyectarán durante el despliegue y nunca se confirmarán en Git.

## Alternativas consideradas

### Repositorios separados

Rechazada porque aumenta la coordinación entre versiones, revisiones y despliegues. Para un único equipo y una única aplicación no aporta aislamiento suficiente para compensar esa complejidad.

### Microservicios por dominio

Rechazada porque introduciría descubrimiento de servicios, contratos remotos, tolerancia a fallos distribuida, observabilidad entre procesos y consistencia eventual sin que exista una necesidad actual de escalado independiente.

### Aplicación sin límites modulares

Rechazada porque simplificaría el arranque, pero aumentaría el acoplamiento entre campañas, personajes, contenido y combate. La modularidad interna permite mantener esos límites sin el coste operativo de los microservicios.

## Consecuencias

### Positivas

- Un único cambio puede actualizar código, migraciones, infraestructura y documentación de forma atómica.
- El desarrollo local y el despliegue se realizan desde una sola revisión del repositorio.
- Las pruebas end-to-end pueden validar el sistema completo.
- La operación es apropiada para una única instalación privada.
- Los límites modulares facilitan extraer un servicio en el futuro si aparece una necesidad demostrable.

### Negativas

- El pipeline puede crecer y deberá ejecutar únicamente las comprobaciones necesarias cuando sea posible.
- Un error del backend puede afectar a todos los dominios funcionales.
- Los límites entre módulos dependen de convenciones y pruebas de arquitectura, no de aislamiento de red.
- Todos los componentes de aplicación comparten un ciclo de publicación coordinado.

## Criterios para revisar la decisión

La decisión se revisará si aparece alguna de estas condiciones:

- Un módulo necesita escalar o desplegarse de forma independiente.
- Diferentes equipos necesitan ciclos de entrega autónomos.
- Un requisito exige aislamiento de fallos o seguridad a nivel de proceso.
- El tiempo de compilación o pruebas del monorepositorio se vuelve un impedimento medible.
- Docker Compose deja de ser adecuado para el entorno de despliegue.

## Acciones derivadas

- Crear ADR específicos para stack tecnológico, autenticación, correo, observabilidad, secretos y despliegue.
- Definir la primera especificación de fundación técnica.
- Incorporar pruebas que impidan dependencias no autorizadas entre módulos del backend.
- Mantener actualizados los índices de documentación y ADR.
