# Spec 012: Librería administrable de módulos de aventura

- Estado: Completada y mergeada en `main`
- Fecha: 2026-08-29
- Tipo: incremento funcional vertical
- Requisitos: RF-013, RF-016 y RF-017
- Dependencias: [spec 003](../003-modularizacion-frontend/spec.md) y [spec 004](../004-creacion-campanas/spec.md)

## Resultado

AdventureCatalog es propietario del catálogo de módulos, sus metadatos, procedencia, portadas privadas, persistencia y endpoints administrativos. Angular expone el recorrido lazy de administración para administradores de plataforma. Campaigns no contiene entidades ni tablas del catálogo.

La API ofrece alta, listado, detalle, edición, borrado y portada con validación, concurrencia optimista, autorización, métricas y almacenamiento privado. La selección autenticada para campañas y la desasociación transversal pertenecen a la spec 013.

## Fuera de alcance

Capítulos, mapas, localizaciones, NPC, progreso por campaña y contenido editorial concreto.
