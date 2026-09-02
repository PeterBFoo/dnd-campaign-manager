# ADR-0011: Lectura de contenido de campaña sin ciclos modulares

- Estado: Aceptado
- Fecha: 2026-08-30
- Contexto: spec 014

## Contexto

Campaigns ya consume el contrato público de AdventureCatalog para validar y proyectar el módulo asociado. La lectura de capítulos necesita combinar contenido propiedad de AdventureCatalog con la autorización DM y el módulo vigente propiedad de Campaigns. Una referencia directa de AdventureCatalog a Campaigns introduciría un ciclo entre proyectos y contradiría los límites aceptados en los ADR-0004 y ADR-0008.

## Decisión

AdventureCatalog define un puerto de aplicación mínimo que resuelve, para un usuario y una campaña, si la campaña existe, si el actor es su DM y cuál es su módulo vigente. Campaigns publica esos datos mediante un contrato de solo lectura. La composición raíz de `apps/api` adapta el contrato de Campaigns al puerto de AdventureCatalog, porque ya referencia ambos módulos.

El adaptador no expone agregados, repositorios ni `DbContext`, no autoriza mediante datos suministrados por el cliente y no conserva estado. AdventureCatalog sigue siendo propietario de capítulos, persistencia y proyección; Campaigns sigue siendo propietario de la campaña, su DM y la asociación de módulo.

## Consecuencias

- Se preserva la dirección Campaigns → AdventureCatalog para el flujo de asociación sin crear una referencia inversa entre módulos.
- La lectura DM exige resolver campaña, rol y módulo actual en cada petición; cambiar o retirar el módulo revoca inmediatamente el acceso.
- Las pruebas de composición deben verificar el adaptador, además de las pruebas aisladas de los handlers.
- Otros consumidores no reutilizarán este puerto como acceso genérico: ampliaciones futuras requieren contratos explícitos y minimizados.

## Alternativas descartadas

- **Referencia directa AdventureCatalog → Campaigns:** crea un ciclo de proyectos.
- **Consultar tablas de Campaigns desde AdventureCatalog:** viola ownership y acopla esquemas internos.
- **Copiar la asociación mediante eventos:** introduce consistencia eventual innecesaria para una comprobación de autorización.
- **Recibir rol o módulo desde Angular:** convierte datos manipulables del cliente en una frontera de seguridad.
