# Spec 017: Distancias de viaje mediante cuadrícula

- Estado: Propuesta
- Fecha: 2026-08-29
- Tipo: incremento funcional vertical
- Roadmap: [roadmap funcional](../../roadmap/product-roadmap.md)
- Requisitos principales: RF-018 y RF-019
- Dependencias: [spec 015](../015-mapas-modulo/spec.md) y [spec 016](../016-localizaciones-puntos-interes/spec.md)

## Problema

Las posiciones visuales normalizadas permiten mostrar localizaciones, pero no representan celdas ni una escala con la que calcular distancias reproducibles. Tampoco debe confundirse que un mapa tenga cuadrícula con que permita calcular viajes.

## Objetivo

Permitir que el administrador configure opcionalmente una cuadrícula cuadrada o hexagonal, asigne celdas a placements de localización y habilite el cálculo de distancia. El DM podrá seleccionar origen y destino desde su campaña y obtener la distancia sin cálculo de ruta.

## Modelo y decisiones

- `hasGrid` deriva de que exista configuración; no se guarda como señal independiente.
- `travelEnabled` es una decisión explícita y solo puede activarse con una cuadrícula válida.
- Una cuadrícula declara tipo, orientación, escala positiva y unidad.
- Cuadrícula cuadrada: cada placement usa fila y columna enteras; la distancia en celdas es `max(|Δfila|, |Δcolumna|)`.
- Cuadrícula hexagonal: cada placement usa coordenadas axiales enteras `q/r`; la distancia es `(|Δq| + |Δr| + |Δq + Δr|) / 2`.
- Dos celdas adyacentes están a una celda. La misma celda produce distancia cero.
- La distancia final es `celdas × escala` y conserva la unidad configurada.
- Orientación admite cuadrada ortogonal y hexagonal con vértice o lado plano arriba; sirve para el editor visual y no altera la fórmula axial.
- Unidades iniciales: metros, kilómetros, pies y millas. No se convierten automáticamente entre sí.

## Actores y alcance

- El administrador configura, deshabilita o elimina la cuadrícula y asigna celdas a placements existentes.
- El DM calcula distancias en mapas del módulo asociado a su campaña.
- Los jugadores no acceden al mapa ni al cálculo en este incremento.
- La capacidad devuelve distancia geométrica entre celdas; no busca ni valida una ruta transitable.

## Reglas funcionales

- Un mapa sin imagen no admite cuadrícula.
- Escala decimal mayor que cero y con precisión acotada; la API devuelve número y unidad, no una cadena localizada.
- Origen y destino deben ser dos localizaciones distintas colocadas en el mismo mapa y con celda asignada.
- Habilitar viaje exige al menos dos placements con celda válida.
- Cambiar el tipo u orientación, o eliminar la cuadrícula, conserva posiciones visuales normalizadas pero retira todas las celdas asignadas y deshabilita viaje.
- Cambiar solo escala o unidad conserva las celdas.
- Deshabilitar viaje conserva configuración y celdas para poder reactivarlo.
- No hay estado de viaje, ubicación del grupo ni resultado persistido por campaña.

## Recorrido web

- El detalle administrativo del mapa añade configuración de cuadrícula y asignación visual o numérica de celdas.
- La interfaz diferencia claramente `Mostrar/configurar cuadrícula` de `Permitir cálculo de viaje`.
- El detalle DM del mapa muestra un calculador solo cuando `travelEnabled` es verdadero y existen destinos válidos.
- El resultado presenta número de celdas, escala aplicada, distancia y unidad.

## Contrato HTTP funcional

- `PUT|DELETE /api/v1/admin/adventure-modules/{moduleId}/maps/{mapId}/grid`
- `PUT /api/v1/admin/adventure-modules/{moduleId}/maps/{mapId}/locations/{locationId}/cell`
- `PUT|DELETE /api/v1/admin/adventure-modules/{moduleId}/maps/{mapId}/travel`
- `POST /api/v1/campaigns/{campaignId}/adventure/maps/{mapId}/travel-distance`

El cálculo recibe `originLocationId` y `destinationLocationId`; la API resuelve configuración y celdas autoritativas. No acepta coordenadas, escala o unidad elegidas por el cliente.

## Ownership técnico

- `apps/api/Modules/AdventureCatalog` posee configuración, celdas, fórmulas, validación y cálculo sin estado.
- `apps/web/src/app/modules/adventure-catalog` posee editor y calculador DM.

No se crea un módulo de software de viajes mientras la capacidad siga siendo una función pura sobre mapas del catálogo.

## Observabilidad

Métricas cubren configuración, asignación de celda y cálculo por tipo de cuadrícula y resultado. No incluyen coordenadas, escala, distancias, nombres o identificadores.

## Criterios de aceptación

1. El administrador configura una cuadrícula cuadrada con escala y unidad y asigna celdas a dos localizaciones.
2. Con desplazamiento de tres filas y cinco columnas, el cálculo cuadrado devuelve cinco celdas por la escala configurada.
3. Una configuración hexagonal devuelve la distancia axial correcta en casos horizontales, diagonales y de coordenadas negativas.
4. Dos placements en la misma celda devuelven cero; dos celdas adyacentes devuelven una.
5. Un mapa puede tener cuadrícula con viaje deshabilitado y no muestra calculador al DM.
6. No puede habilitarse viaje sin imagen, cuadrícula válida y dos placements con celda.
7. Cambiar tipo u orientación limpia celdas y deshabilita viaje, pero conserva markers normalizados.
8. Cambiar escala conserva celdas y modifica el resultado posterior.
9. El DM solo calcula en el módulo de su campaña; jugadores y usuarios ajenos no pueden invocar la operación.
10. Las pruebas matemáticas, de dominio, PostgreSQL, API y Angular cubren ambos tipos y mantienen verdes las suites.

## Fuera de alcance

- Terreno difícil, carreteras, ríos, obstáculos o costes variables.
- Velocidad del grupo, tiempo, clima, encuentros aleatorios o recursos consumidos.
- A*, ruta óptima, bloqueo de celdas o selección de camino.
- Movimiento en tiempo real, tokens o posición persistida de la campaña.
- Conversión entre unidades o mapas sin cuadrícula.
