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

La primera generación o la renovación con permiso vencido consulta la base y emite un permiso temporal firmado (`generationGrant`) de 5 minutos. Mientras ese permiso esté vigente, las renovaciones de 30 segundos validan criptográficamente el permiso y generan el QR actual sin consultar la base y sin escribir nada.

La generación no realiza `INSERT`, `UPDATE` ni `DELETE`; la única escritura relacionada al QR ocurre cuando el funcionario valida correctamente y se inserta `Validacion.TokenValidado`.

El botón `Descargar QR actual` descarga el PNG ya renderizado en pantalla. No genera un token nuevo, no extiende la vigencia y no incluye datos personales en el nombre del archivo.

## Validación

`GET /Funcionario/Escanear` requiere `FUNCIONARIO` y ofrece tres alternativas:

- cargar imagen con QR;
- usar cámara con `BarcodeDetector`;
- ingresar token manualmente como fallback técnico.

La carga de imagen acepta PNG, JPG, WebP y capturas/fotos de hasta 10 MB. La decodificación ocurre en el navegador con `createImageBitmap` y `BarcodeDetector`; la imagen no se envía al servidor, no se guarda y no se copia a `wwwroot`.

Si el navegador no soporta `BarcodeDetector`, se muestra un mensaje claro y se mantiene el ingreso manual.

`POST /Funcionario/ValidarQr` recibe únicamente `token`, requiere antiforgery, rol `FUNCIONARIO` y rate limiting.

La validación abre transacción, bloquea `Entrada` con `SELECT ... FOR UPDATE`, recalcula propietario, valida firma/ventana/evento/marca, confirma asignación en `FuncionarioEventoSector`, inserta en `Validacion` con el token exacto y deja que los triggers marquen la entrada como `VALIDADA`.

## Dispositivos autorizados

Cobertura parcial: la base actual no modela dispositivos autorizados. La autorización actual se apoya en sesión autenticada, rol `FUNCIONARIO` y asignación `FuncionarioEventoSector`.

Mejora futura propuesta: tabla `DispositivoAutorizado` con identificador, funcionario responsable, estado, fecha de alta, última actividad y registro del dispositivo usado.

## Demo en una sola PC

1. Abrir una sesión normal como Usuario General.
2. Abrir una ventana privada como Funcionario.
3. Descargar el QR actual desde el detalle de entrada.
4. Cargar inmediatamente ese PNG en `/Funcionario/Escanear`.
5. Validar, repetir con el mismo PNG para comprobar doble escaneo y esperar más de 30 segundos para comprobar vencimiento.
