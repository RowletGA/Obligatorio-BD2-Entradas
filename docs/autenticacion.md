# Autenticacion

La aplicacion utiliza autenticacion por cookies de ASP.NET Core sin ASP.NET Identity completo y sin tablas adicionales de Identity.

## Cambio autorizado de base

La unica extension estructural autorizada para esta iteracion es:

`database/04_AgregarHashContrasena.sql`

Ese script agrega `Usuario.HashContrasena VARCHAR(255) NULL`. Debe ejecutarse manualmente sobre la base existente y no se ejecuta automaticamente al iniciar la aplicacion.

La columna es nullable porque pueden existir usuarios creados antes de esta iteracion. Esos usuarios no pueden iniciar sesion mientras `HashContrasena IS NULL`; deberan definir una contrasena mediante un procedimiento posterior o registrarse como usuarios nuevos para la demostracion.

## Registro

El registro de usuario general utiliza las tablas existentes:

- `Usuario`
- `UsuarioGeneral`
- `TelefonoUsuario`

El alta se realiza dentro de una unica transaccion MySQL:

1. Verifica correo.
2. Verifica documento compuesto.
3. Inserta `Usuario` con `HashContrasena`.
4. Inserta `UsuarioGeneral`.
5. Inserta telefonos.
6. Confirma o revierte toda la transaccion.

La contrasena se valida en servidor y se hashea con `PasswordHasher<UsuarioAutenticacion>`.

## Login

El login solicita correo y contrasena. El correo se normaliza solamente recortando espacios externos.

El flujo es:

1. Busca el usuario por correo mediante SQL parametrizado.
2. Obtiene `HashContrasena` y los datos minimos de identidad.
3. Rechaza de forma generica usuarios inexistentes, usuarios sin hash y contrasenas invalidas.
4. Verifica con `PasswordHasher`.
5. Si el hasher devuelve `SuccessRehashNeeded`, actualiza el hash.
6. Consulta perfiles desde `UsuarioGeneral`, `Funcionario` y `Administrador`.
7. Emite cookie de autenticacion.

El mensaje publico de fallo siempre es:

`Correo o contraseña incorrectos.`

## Claims

Los claims emitidos son:

- `TipoDocumento`
- `PaisDocumento`
- `NumeroDocumento`
- `ClaimTypes.Email`
- `ClaimTypes.Name`
- `ClaimTypes.Role`

Roles posibles:

- `USUARIO_GENERAL`
- `FUNCIONARIO`
- `ADMINISTRADOR`

Los roles nunca se aceptan desde formularios, rutas, query strings ni campos ocultos. Siempre se determinan consultando la base.

## Seguridad

- Las cookies son `HttpOnly`.
- `SecurePolicy` es `Always` fuera de Development.
- `SameSite` es `Lax`.
- Login, registro y logout usan antiforgery.
- El endpoint de login tiene rate limiting por IP.
- No se guarda contrasena en texto plano.
- No se muestra ni se envia el hash al navegador.
- No se registran contrasenas ni hashes en logs.
