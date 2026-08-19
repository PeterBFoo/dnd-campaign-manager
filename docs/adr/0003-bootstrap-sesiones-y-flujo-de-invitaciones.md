# ADR-0003: Bootstrap único, sesiones opacas y flujo funcional de invitaciones

- Estado: Aceptado
- Fecha: 2026-08-19
- Decisores: equipo del proyecto
- Alcance: autenticación inicial, aceptación y administración de invitaciones
- Depende de: ADR-0001 y ADR-0002

## Contexto

El ADR-0002 cerró el autorregistro y definió las invitaciones y el correo transaccional, pero dejó pendientes la creación segura de la primera administración, la autenticación entre Angular y la API, el reenvío y la persistencia del outbox. Estos elementos son necesarios para exponer el primer flujo funcional sin crear una puerta de registro pública.

Producción sirve Angular desde GitHub Pages y la API desde Azure Container Apps. Por tanto, durante esta etapa no comparten origen. La solución debe funcionar con CORS exacto, no depender de cookies de terceros y conservar la autoridad de acceso en ASP.NET Core.

## Decisión

### 1. Bootstrap de un solo uso

La API admitirá el alta inicial únicamente mientras la tabla de usuarios esté vacía. La petición necesitará un secreto aleatorio de despliegue de al menos 32 caracteres, estará limitada por dirección de red y creará una única cuenta con capacidad global de administración.

En cuanto exista cualquier usuario, el endpoint de escritura quedará cerrado de forma irreversible por la regla funcional, aunque el secreto continúe configurado. El secreto será independiente de Brevo, PostgreSQL y Grafana.

### 2. Credenciales locales y sesiones opacas

Las contraseñas se almacenarán mediante el formato versionado de `PasswordHasher` de ASP.NET Core. Tendrán entre 12 y 128 caracteres e incluirán mayúscula, minúscula, número y símbolo.

Al iniciar sesión, la API emitirá un token opaco aleatorio de 256 bits con una duración de ocho horas. PostgreSQL conservará solo su SHA-256. Cerrar sesión revocará la sesión en servidor y Angular eliminará el token de `sessionStorage`; cerrar el navegador también lo descartará. No habrá token en `localStorage`, cookies, URL, logs o telemetría.

Durante la topología actual Angular enviará el token en `Authorization: Bearer` y la API permitirá únicamente el origen productivo exacto. Se acepta temporalmente el riesgo de mantener el token en memoria web persistida durante la pestaña. Un dominio y proxy same-origin seguirá siendo la evolución preferente antes de ampliar el volumen de datos privados.

### 3. Aceptación sin revelar el token

El correo utilizará un fragmento `#token=...`. Los fragmentos no se envían al servidor estático. Angular extraerá el token, lo retirará inmediatamente de la barra de direcciones y lo enviará en el cuerpo de las peticiones de previsualización y aceptación.

Si la dirección todavía no tiene cuenta, la invitación autorizará crear sus credenciales. Si ya existe, el destinatario deberá autenticarse con esa misma cuenta. Una invitación de campaña solo podrá añadir una membresía de jugador y nunca conceder el rol de DM.

### 4. Reenvío funcional

Un reintento técnico del outbox conservará la misma invitación. Un reenvío solicitado por una persona autorizada revocará la invitación pendiente, rotará el token y generará otra invitación.

Se permitirá como máximo un reenvío cada 15 minutos y cinco emisiones para la misma dirección y contexto durante 24 horas. No se reenviarán invitaciones aceptadas, caducadas o revocadas.

### 5. Outbox cifrado y migraciones

La invitación y el mensaje de outbox se confirmarán en la misma transacción. El token necesario para el correo permanecerá cifrado con AES-256-GCM y una clave exclusiva de despliegue. Tras la entrega o descarte, el ciphertext se eliminará. El worker aplicará reintentos acotados y nunca registrará direcciones, cuerpos o tokens.

EF Core versionará el esquema. El despliegue actual, limitado a una réplica, aplicará las migraciones al iniciar la revisión. Esta excepción operativa deberá sustituirse por un bundle o paso previo revisable antes de permitir más de una réplica o cambios destructivos.

## Consecuencias

- No existe endpoint de autorregistro.
- El primer administrador puede crear, consultar, revocar y reenviar invitaciones de plataforma desde Angular.
- Los DM disponen de los mismos contratos para invitaciones de su campaña; el backend valida la membresía y el rol.
- Las sesiones son revocables y sobreviven a reinicios y escalado a cero sin una clave de firma compartida.
- Se añaden dos secretos: `IDENTITY_BOOTSTRAP_TOKEN` y `OUTBOX_ENCRYPTION_KEY`.
- La recuperación y el cambio de contraseña, el segundo factor y la rotación administrativa del primer administrador quedan fuera de este incremento.

## Criterios de aceptación

1. Sin usuarios, un secreto válido crea exactamente un administrador; un segundo intento no puede crear ni elevar otra cuenta.
2. No se puede crear una cuenta sin una invitación válida o el bootstrap inicial.
3. Un administrador autenticado puede emitir, listar, revocar y reenviar invitaciones de plataforma.
4. Una persona nueva crea credenciales al aceptar; una existente debe iniciar sesión y no genera un duplicado.
5. Una invitación de campaña aceptada crea únicamente una membresía de jugador.
6. Un token caducado, revocado o aceptado no puede reutilizarse.
7. Los tokens no aparecen en la URL enviada a servidores, respuestas de administración, logs ni persistencia en claro.
