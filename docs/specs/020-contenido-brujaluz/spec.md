# Spec 020: Carga validada del módulo Brujaluz

- Estado: Propuesta
- Fecha: 2026-08-29
- Tipo: incremento editorial y de validación integral
- Roadmap: [roadmap funcional](../../roadmap/product-roadmap.md)
- Requisitos principales: RF-013, RF-015 a RF-019, RF-021 a RF-024, RF-026 y RF-060 a RF-066
- Requisito relacionado pendiente: RF-025, porque la carga no introduce estadísticas de NPC
- Dependencias: [spec 012](../012-libreria-modulos/spec.md), [spec 013](../013-asignacion-modulo-campana/spec.md), [spec 014](../014-capitulos-modulo/spec.md), [spec 015](../015-mapas-modulo/spec.md), [spec 016](../016-localizaciones-puntos-interes/spec.md), [spec 017](../017-viajes-cuadricula/spec.md), [spec 018](../018-npc-modulo/spec.md) y [spec 019](../019-visibilidad-npc-campana/spec.md)
- Autorización editorial: solicitada expresamente por el usuario el 2026-08-29, condicionada a redacción original y procedencia verificable

## Problema

Las capacidades genéricas necesitan validarse con un módulo real suficientemente variado. Un modelo que solo funciona con fixtures mínimos puede ocultar carencias en capítulos, relaciones reutilizables, mapas, localizaciones, puntos de interés, cuadrículas, NPC y proyecciones por rol.

## Objetivo

Cargar Brujaluz como primer módulo real utilizando exclusivamente las operaciones normales de AdventureCatalog, Campaigns y Library, con textos redactados originalmente y evidencia de procedencia y derecho de uso para cada recurso.

Este incremento valida el modelo; no autoriza excepciones por nombre, rutas especiales, seeds acoplados ni estructuras diseñadas para una aventura concreta.

## Naturaleza y ownership

- Es un incremento editorial y de aceptación, no una nueva capacidad de software.
- `apps/api` no incorpora endpoints, ramas condicionales, entidades ni migraciones específicas.
- `apps/web` no incorpora componentes, rutas ni estilos específicos.
- La ausencia de cambios en ambas superficies está justificada porque la carga se realiza mediante las experiencias y contratos ya aceptados. Si una operación necesaria no existe, se detiene esa parte y se propone un spec genérico antes de cambiar código.
- AdventureCatalog posee los datos base y Library únicamente los estados por campaña creados durante la validación.

## Reglas editoriales y jurídicas del producto

- Los textos cargados se redactan de forma original a partir del material autorizado para el proyecto; no se copian ni reconstruyen pasajes oficiales de forma sustancialmente equivalente.
- Parafrasear no se registra como única justificación jurídica. Cada elemento conserva la fuente consultada y el fundamento de uso declarado por el responsable del proyecto.
- No se cargan imágenes, mapas o ilustraciones oficiales salvo que exista licencia, permiso o política aplicable que cubra esa reutilización concreta.
- Se admiten recursos originales creados para el proyecto y recursos de terceros con licencia compatible, atribución y evidencia conservadas.
- Cuando se invoque la política de contenido de fans de Wizards, el producto permanece gratuito, se identifica como no oficial e incorpora el aviso y las atribuciones aplicables.
- La spec, los ADR, los fixtures y el código no contendrán el corpus editorial. Los textos e imágenes viven como datos privados del entorno autorizado.
- La autorización de esta spec no constituye asesoramiento jurídico ni sustituye la comprobación de derechos sobre cada fuente o recurso.

## Estrategia de carga

La carga progresiva utiliza las mismas pantallas y API que cualquier otro módulo:

1. metadatos, portada y procedencia;
2. capítulos y orden;
3. mapas e imágenes autorizadas;
4. localizaciones y mapas detallados;
5. puntos de interés y posiciones;
6. NPC, imágenes y campos públicos/privados;
7. relaciones entre capítulos, mapas, localizaciones y NPC;
8. cuadrículas y capacidad de viaje únicamente donde tenga sentido;
9. asociación con una campaña privada de validación;
10. desbloqueo de una muestra de NPC para comprobar la proyección de jugador.

No se crean copias de recursos para representar múltiples apariciones. Las relaciones deben reutilizar las identidades ya cargadas.

## Control de procedencia

Antes de cargar cada recurso se registra:

- identificador interno del elemento;
- tipo de origen y responsable de la redacción o creación;
- referencia de fuente, sin incluir el texto fuente en el repositorio;
- fundamento de uso y restricciones conocidas;
- atribución o aviso requerido;
- fecha y actor que verificó la evidencia;
- para archivos, huella criptográfica que identifica exactamente el binario aprobado.

Un elemento sin evidencia suficiente queda excluido de la carga aunque sea necesario para completar narrativamente el módulo.

## Validación funcional

- El administrador puede recorrer y editar todo el módulo desde el catálogo sin comportamiento especial.
- El módulo puede asociarse, cambiarse y retirarse de una campaña de prueba.
- El DM consulta capítulos, mapas, localizaciones, POI, viajes y todos los NPC.
- El jugador no consulta recursos de dirección y solo ve NPC desbloqueados mediante la proyección pública.
- Las notas privadas, procedencia interna y recursos bloqueados no aparecen en respuestas ni interfaz de jugador.
- Cambiar un recurso base se refleja en la campaña sin crear una copia.
- Las relaciones múltiples no generan duplicados y las eliminaciones respetan las reglas de cada spec.

## Gestión de carencias

Cuando el contenido no pueda representarse:

1. se documenta el caso de forma abstracta, sin copiar contenido editorial;
2. se determina si expresa una necesidad reutilizable o una particularidad de esta aventura;
3. solo una necesidad reutilizable genera un nuevo requisito de roadmap y un spec independiente;
4. no se añaden campos genéricos sin validar, JSON libre, excepciones por nombre ni lógica condicional específica;
5. hasta resolverla, el elemento afectado queda parcial o no se carga.

## Evidencia y observabilidad

- La aceptación conserva un inventario privado con recuentos, relaciones, recursos excluidos y verificaciones de procedencia, sin publicar el corpus.
- Las pruebas automáticas usan datos genéricos; no copian nombres, textos ni imágenes del módulo real.
- La telemetría productiva mantiene las reglas de privacidad de las specs funcionales y no etiqueta nombres o contenido.
- Se registra evidencia de navegación por rol y de que no existen rutas o ramas específicas para este módulo.

## Criterios de aceptación

1. Brujaluz existe como una instancia normal del catálogo y no requiere código, migraciones o configuración específica por nombre.
2. Sus recursos cargados tienen texto original y un registro completo de procedencia y fundamento de uso.
3. Ninguna imagen o mapa se incorpora sin evidencia aplicable al binario exacto.
4. Capítulos, mapas, localizaciones, POI y NPC se relacionan reutilizando identidades, sin copias por aparición.
5. El DM navega el contenido completo desde una campaña privada asociada.
6. Un jugador de esa campaña no puede consultar capítulos, mapas, localizaciones, POI, notas de DM ni NPC bloqueados.
7. Después de desbloquear un NPC de prueba, el jugador ve únicamente sus campos públicos y su imagen autorizada.
8. Una segunda campaña que use el módulo conserva su propio estado de visibilidad.
9. Un mapa configurado para viaje devuelve distancias conformes al spec 017 y otro mapa puede conservar cuadrícula con viaje deshabilitado.
10. Editar un recurso actualiza ambas campañas sin duplicarlo ni modificar sus estados propios.
11. Toda carencia detectada queda clasificada y ninguna introduce una solución específica dentro de esta carga.
12. El inventario privado y la evidencia de aceptación permiten reproducir la revisión sin incorporar el corpus editorial al repositorio.

## Fuera de alcance

- Importación masiva, exportación, sincronización o seed editorial en migraciones.
- Publicación, marketplace, descarga o redistribución del corpus.
- Digitalización literal de libros, imágenes o mapas oficiales sin permiso aplicable.
- Excepciones funcionales o visuales exclusivas de Brujaluz.
- Completar elementos cuya procedencia o fundamento de uso no pueda verificarse.
- Declarar que la mera reformulación de un texto garantiza por sí sola cumplimiento jurídico.
