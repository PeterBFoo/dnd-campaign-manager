# Plan 015: Mapas reutilizables de un módulo

- Estado: Implementado
- Especificación: [spec.md](spec.md)

## Diseño

1. Incorporar `AdventureMap`, metadatos de imagen y asociación única mapa-capítulo en AdventureCatalog.
2. Persistir metadatos en PostgreSQL y binarios bajo `adventure-modules/{moduleId}/maps/{mapId}/` en el contenedor privado existente.
3. Validar firma, MIME, límite de 20 MiB, dimensiones y 50 megapíxeles antes de publicar una sustitución.
4. Publicar CRUD administrativo, operaciones de imagen y asociaciones con concurrencia optimista.
5. Resolver el módulo actual y el rol DM mediante un contrato público implementado por Campaigns, sin acceso directo a sus tablas.
6. Añadir recorridos Angular de autoría y consulta DM con carga diferida del binario.
7. Cubrir dominio, persistencia, almacenamiento, cliente Angular y límites arquitectónicos; actualizar documentación operativa y trazabilidad.

## Límites

Los capítulos, su CRUD, ordenación y lectura proceden íntegramente de la spec 014. Esta entrega solo añade su relación con mapas. Localizaciones, placements y cuadrículas siguen fuera de alcance.
