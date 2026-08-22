# ADR-0002: Identidad por invitación y correo transaccional con Brevo

- Estado: Aceptado
- Fecha: 2026-08-19
- Decisores: equipo del proyecto
- Alcance: alta de usuarios, invitaciones y entrega de correo transaccional
- Depende de: ADR-0001 y [roadmap funcional](../roadmap/product-roadmap.md), antes denominado Especificación 001

## Contexto

La plataforma no tendrá autorregistro público. Toda cuenta debe proceder de una invitación emitida por el administrador de la plataforma o por el DM de una campaña. Además, cualquier usuario registrado podrá crear una campaña seleccionando un módulo y se convertirá en su único DM.

El correo de invitación es parte de un flujo de seguridad: contiene la capacidad temporal para crear una cuenta o incorporarse a una campaña. No puede depender del frontend para aplicar permisos, revelar si una cuenta existe ni exponer credenciales del proveedor de correo.

El despliegue actual utiliza ASP.NET Core en Azure Container Apps, PostgreSQL en Neon y Angular en GitHub Pages. Se necesita una solución de bajo coste, compatible con C#, observable y adecuada para destinatarios europeos.

## Decisión

### 1. El alta será exclusivamente por invitación

Existirán dos tipos de invitación:

- `Platform`: permite crear una cuenta, pero no concede acceso a ninguna campaña.
- `Campaign`: permite crear una cuenta cuando sea necesario e incorporarse como jugador a una campaña concreta.

Una invitación de campaña nunca podrá conceder el rol de DM. El creador de una campaña será automáticamente su único DM.

### 2. Las invitaciones caducarán a los siete días

Cada invitación tendrá una vigencia exacta de siete días desde su emisión y uno de estos estados: `Pending`, `Accepted`, `Expired` o `Revoked`.

Solo una invitación `Pending` y no caducada podrá aceptarse. La aceptación y la revocación serán transiciones atómicas y una invitación aceptada, caducada o revocada no podrá reutilizarse.

### 3. Los tokens serán opacos, aleatorios y de un solo uso

El token entregado al destinatario tendrá al menos 256 bits de entropía criptográfica. La base de datos conservará únicamente un resumen SHA-256 del token; el valor original solo existirá durante la composición del enlace y el envío.

La comparación del token se realizará en tiempo constante. Los tokens no se incluirán en logs, trazas, métricas, mensajes de error ni analítica del frontend.

### 4. Brevo será el proveedor transaccional

El backend enviará las invitaciones mediante la API transaccional v3 de Brevo. No se utilizará el servicio de campañas de marketing ni se crearán contactos de marketing como efecto secundario.

La integración residirá detrás de un puerto propio de correo transaccional. El dominio y la aplicación no dependerán de tipos del SDK de Brevo, de modo que el proveedor pueda sustituirse sin modificar las reglas de invitación.

El primer adaptador utilizará un `HttpClient` tipado contra `POST /v3/smtp/email`. Cada envío incluirá versiones HTML y texto plano, una categoría funcional y un identificador de correlación sin datos secretos.

### 5. Persistencia y entrega no compartirán una transacción distribuida

La invitación se persistirá antes de solicitar el envío. La entrega se coordinará mediante un outbox transaccional almacenado en PostgreSQL y procesado en segundo plano con reintentos acotados. Esto evita perder la invitación si Brevo no está disponible después de confirmar la transacción local.

Un reintento técnico no creará otra invitación ni rotará el token. La política funcional para que una persona solicite un nuevo envío se concretará antes de exponer el flujo completo.

### 6. El backend será el único custodio de las credenciales

La API key y la dirección remitente verificada de Brevo se proporcionarán como secretos de ejecución independientes. Nunca se incluirán en el repositorio, imágenes de contenedor, argumentos de build, respuestas HTTP o configuración pública de Angular.

Desarrollo local podrá leerla desde una variable ignorada por Git. Los despliegues basados en Docker la montarán como archivo secreto y Azure Container Apps la obtendrá de su almacén de secretos.

### 7. La entrega será observable sin registrar información sensible

La aplicación medirá intentos, aceptaciones del proveedor, fallos y duración de envío. El identificador devuelto por Brevo podrá correlacionarse con el outbox, pero no se expondrá al navegador.

Los webhooks de Brevo podrán actualizar estados de entrega como entregado, rebote o queja. Esos estados describen el transporte del correo; nunca sustituyen el estado funcional de aceptación de la invitación.

Los logs no contendrán direcciones completas, cuerpos de correo, enlaces de aceptación, tokens ni la API key.

## Alternativas consideradas

### Azure Communication Services Email

Se integra bien con Azure y su coste por mensaje es reducido, pero no ofrece un plan gratuito equivalente. El dominio administrado tiene además límites bajos y una identidad de remitente poco reconocible. Se descarta a favor de Brevo para el alcance inicial.

### Mailjet

Dispone de plan gratuito, API y alojamiento europeo. Su límite gratuito es inferior al de Brevo y no aporta una ventaja suficiente para introducirlo como primera opción. Se conserva como alternativa futura.

### Resend

Ofrece una API sencilla, pero el plan gratuito tiene menor capacidad y los datos de cuenta, metadatos y registros se almacenan en Estados Unidos aunque el correo se envíe desde Irlanda. Se descarta para reducir transferencias internacionales de datos.

### SMTP directo

Es portable, pero pierde contratos HTTP más explícitos y dificulta tratar de forma uniforme errores, identificadores y eventos del proveedor. La API REST de Brevo será el adaptador inicial.

### Guardar el token original

Simplificaría el reenvío, pero una filtración de la base de datos permitiría utilizar invitaciones pendientes. Se descarta; solo se persistirá su resumen.

## Consecuencias

### Positivas

- El alta queda cerrada por defecto y cada acceso tiene una procedencia auditable.
- Una filtración de la base de datos no revela tokens de invitación utilizables.
- El plan gratuito de Brevo cubre holgadamente el volumen inicial.
- El proveedor queda aislado tras una interfaz propia.
- El outbox permite reintentar fallos sin duplicar invitaciones.

### Costes y riesgos

- La aceptación requiere consultar el resumen del token y ejecutar una transición atómica.
- El worker de outbox y los webhooks añaden estados operativos que deben monitorizarse.
- El plan gratuito y los límites de Brevo pueden cambiar; habrá que alertar antes de agotar la cuota.
- La entregabilidad exigirá verificar remitente, SPF, DKIM y DMARC antes de abrir el producto a usuarios reales.
- Brevo procesa direcciones y contenido de correo, por lo que se deberá mantener un acuerdo de tratamiento de datos y una política de retención adecuada.

## Primer incremento de implementación

Este ADR autoriza comenzar con:

- tipos de invitación, estados y política de caducidad de siete días;
- generación y validación segura de tokens, persistiendo solo su resumen;
- puerto de correo transaccional y adaptador HTTP para Brevo;
- configuración de secreto para desarrollo y despliegue;
- métricas y errores tipados que no filtren datos sensibles;
- pruebas unitarias de caducidad, uso único, revocación y contrato HTTP de Brevo.

Quedan fuera de este primer incremento:

- endpoints públicos de registro, login o administración;
- creación segura del primer administrador;
- persistencia y worker completos del outbox;
- plantillas definitivas y dominio remitente productivo;
- política funcional de reenvío;
- webhooks de entrega;
- UI de invitaciones.

Estas capacidades se implementarán cuando sus requisitos pendientes estén aceptados.

## Criterios de revisión

Revisaremos esta decisión si:

- el volumen supera de forma sostenida los límites del plan elegido;
- Brevo deja de cumplir los requisitos económicos o de tratamiento de datos;
- las medidas de entregabilidad muestran una tasa de fallo inaceptable;
- aparece una exigencia regulatoria de residencia o proveedor diferente;
- el outbox no alcanza los objetivos operativos medidos.
