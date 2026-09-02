# Tareas 014: Capítulos ordenados de un módulo

- Estado: Implementada; verificaciones de entorno pendientes
- Especificación: [spec.md](spec.md)
- Plan: [plan.md](plan.md)

## Preparación y decisiones

- [x] Revisar roadmap, flujo SDD, specs dependientes, ADR y código existente.
- [x] Aceptar el alcance vertical del spec 014.
- [x] Documentar en ADR-0011 la composición intermodular sin ciclos.

## Dominio y datos

- [x] Añadir capítulo, procedencia, auditoría y versión independiente del índice.
- [x] Implementar alta final, edición, eliminación compacta y ordenación atómica.
- [x] Crear migración con FK, restricciones e índice único `(ModuleId, Position)`.
- [x] Cubrir invariantes con pruebas de dominio; persistencia PostgreSQL queda pendiente del entorno.

## API y arquitectura

- [x] Implementar CRUD y orden administrativos.
- [x] Publicar lectura completa y minimizada para DM desde campaña.
- [x] Adaptar el contrato de Campaigns en la composición raíz sin referencias cíclicas.
- [x] Añadir métricas acotadas; pruebas .NET de autorización, contrato y arquitectura quedan pendientes del SDK.

## Web

- [x] Implementar cliente, índice y formularios de autoría administrativa.
- [x] Implementar ruta, índice y detalle de solo lectura para DM.
- [x] Enlazar desde módulo y campaña respetando rol y estados vacíos/error/conflicto.
- [x] Cubrir rutas y suite Angular; los controles de orden ofrecen etiquetas y operación por teclado.

## Cierre

- [x] Ejecutar suite y build Angular, además de comprobaciones Git.
- [ ] Verificar build/pruebas .NET, migración y Compose (SDK .NET y servicios no disponibles en el entorno).
- [ ] Capturar evidencia visual (el entorno no ofrece navegador automatizable).
- [x] Actualizar spec, índice, roadmap, ADR y estas tareas con evidencia real.

## Evidencias

- `pnpm --filter web test`: 27 archivos y 75 pruebas correctas.
- `pnpm --filter web build`: bundle de producción correcto, incluida la ruta lazy de capítulos.
- `git diff --check`: parche sin errores de espacios.
- `dotnet build DndCampaign.slnx --no-restore -m:1`: no ejecutable porque el contenedor no contiene `dotnet`.
