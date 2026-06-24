# QR dinámico y validación

El QR dinámico valida una entrada individual sin persistir tokens previos. La aplicación genera un token firmado en servidor, lo renderiza como imagen QR y lo renueva cada 30 segundos mientras la pantalla está visible.

No se modifican tablas, vistas ni triggers. La base sigue siendo la autoridad final mediante `Validacion`, `V_PropietarioActual`, `V_ValidacionEntrada`, `FuncionarioEventoSector` y los triggers de validación.

## Formato

```text
v1.<IDEntrada>.<IDEvento>.<VentanaTemporal>.<MarcaPropietario>.<Firma>
```

La marca de propietario es un HMAC truncado de `TipoDocumento`, `PaisDocumento` y `Numero`; no expone datos personales. La firma usa HMAC-SHA256 y Base64Url sobre todos los componentes anteriores.

## Configuración

La clave se lee desde `QrSecurity:SigningKey`, preferentemente como variable de entorno:

```bash
QrSecurity__SigningKey="$(openssl rand -base64 32)"
```

También existen `QrSecurity:LifetimeSeconds` y `QrSecurity:ClockSkewSeconds`. La clave real no se guarda en Git ni se imprime en logs.

## Generación

`GET /Entradas/QrDinamico/{idEntrada}` requiere `USUARIO_GENERAL`. Confirma propiedad actual con `V_PropietarioActual`, entrada `ACTIVA` y evento `PROGRAMADO` o `EN_CURSO`. Devuelve JSON con `imagenBase64`, `venceUtc` y `segundosRestantes`, más encabezados `no-store`.

El detalle de entrada solicita el QR al servidor, muestra contador, renueva al vencer, pausa si `document.hidden` es `true` y renueva inmediatamente al volver a estar visible.

Cada generación y renovación consulta la base para confirmar propietario actual, entrada `ACTIVA` y evento `PROGRAMADO` o `EN_CURSO`. El permiso temporal firmado (`generationGrant`) permite conservar continuidad criptográfica de la pantalla, pero no evita la comprobación final contra la base. Si la entrada se valida, se anula, se transfiere o el evento se cierra, la siguiente renovación se detiene.

La generación no realiza `INSERT`, `UPDATE` ni `DELETE`; solo lee `Entrada`, `Evento` y `V_PropietarioActual`. La única escritura relacionada al QR ocurre cuando el funcionario valida correctamente y se inserta `Validacion.TokenValidado`.

El botón `Descargar QR actual` descarga el PNG ya renderizado en pantalla. No genera un token nuevo, no extiende la vigencia y no incluye datos personales en el nombre del archivo.

## Validación

`GET /Funcionario/Escanear` requiere `FUNCIONARIO` y ofrece tres alternativas:

- cargar imagen con QR;
- usar cámara con `BarcodeDetector`;
- ingresar token manualmente como fallback técnico.

La carga de imagen acepta PNG, JPG, WebP y capturas/fotos de hasta 10 MB. La decodificación ocurre en el navegador con `createImageBitmap` y `BarcodeDetector`; la imagen no se envía al servidor, no se guarda y no se copia a `wwwroot`.

Si el navegador no soporta `BarcodeDetector`, se muestra un mensaje claro y se mantiene el ingreso manual.

`POST /Funcionario/ValidarQr` recibe únicamente `token`, requiere antiforgery, rol `FUNCIONARIO` y rate limiting.

La validación abre transacción, bloquea `Entrada` con `SELECT ... FOR UPDATE`, recalcula propietario, valida firma/ventana/evento/marca, confirma asignación en `FuncionarioEventoSector`, inserta en `Validacion` con el token exacto, cancela transferencias `PENDIENTE` de esa entrada y deja que los triggers marquen la entrada como `VALIDADA`. Luego vuelve a consultar el resultado final antes del commit.

El flujo web usa POST/Redirect/GET: después de validar redirige a `/Funcionario/Escanear` con una tarjeta de éxito o rechazo. La tarjeta de éxito muestra el estado final devuelto por la base, por lo que el funcionario ve `VALIDADA` cuando el trigger ya actualizó la entrada. Las páginas dinámicas de entrada y validación envían encabezados `no-store` para evitar que el navegador conserve una vista anterior con estado `ACTIVA`.

Política posterior a la validación:

```text
Compra
→ Entrada ACTIVA
→ Propietario actual
→ QR dinámico
→ Validación
→ Fila en Validacion
→ Entrada VALIDADA
→ QR y transferencia bloqueados
```

Una entrada `VALIDADA` no genera nuevos QR, no muestra acción de transferencia, no puede aceptar transferencias pendientes y rechaza un segundo escaneo.

## Dispositivos autorizados

Cobertura parcial: la base actual no modela dispositivos autorizados. La autorización actual se apoya en sesión autenticada, rol `FUNCIONARIO` y asignación `FuncionarioEventoSector`.

Mejora futura propuesta: tabla `DispositivoAutorizado` con identificador, funcionario responsable, estado, fecha de alta, última actividad y registro del dispositivo usado.

## Demo en una sola PC

1. Abrir una sesión normal como Usuario General.
2. Abrir una ventana privada como Funcionario.
3. Descargar el QR actual desde el detalle de entrada.
4. Cargar inmediatamente ese PNG en `/Funcionario/Escanear`.
5. Validar, repetir con el mismo PNG para comprobar doble escaneo y esperar más de 30 segundos para comprobar vencimiento.
