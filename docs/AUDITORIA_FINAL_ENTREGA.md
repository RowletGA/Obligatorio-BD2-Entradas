# Auditoria final de entrega

Fecha: 2026-06-23

## Alcance

Se realizo una auditoria final del repositorio actual de TicketingMundial antes de la entrega, sin recrear la solucion, sin modificar scripts de base de datos originales y sin hacer commit ni push.

## Secretos y configuracion

- No se detectaron credenciales reales, claves privadas ni connection strings reales en los archivos versionables actuales.
- Las coincidencias encontradas corresponden a placeholders, nombres de opciones o pruebas unitarias que generan claves en memoria.
- `src/TicketingMundial.Web/appsettings.Development.json` permanece como archivo local ignorado por Git.
- La historia de Git contiene en el commit inicial ejemplos de connection string con usuario/contrasena genericos de documentacion. No se observo una credencial real en ese barrido.
- `gitleaks` no esta instalado en esta maquina, por lo que el escaneo se hizo con `rg`, `git grep` y `git log -S`.

## Limpieza aplicada

- Se amplio `.gitignore` para cubrir `bin/`, `obj/`, `TestResults/`, logs, dumps, backups, archivos locales de IDE, `.env*` y `appsettings.*` locales.
- Se eliminaron artefactos generados: `bin/`, `obj/`, `TestResults/` y `.DS_Store`.
- Se removio el frontend de referencia `frontejemplo/`, que no formaba parte de la solucion ASP.NET Core ni del build.
- Se retiro la vista legacy `Views/Funcionario/Validar.cshtml` y el flujo anterior de validacion manual por ID, quedando la validacion operativa en `/Funcionario/Escanear`.
- Se rechazo explicitamente el placeholder `CONFIGURAR_MEDIANTE_VARIABLE_DE_ENTORNO` como clave QR valida al iniciar la aplicacion.
- Se actualizaron README y documentos de estado para reflejar QR dinamico, validacion con funcionario, configuracion local y estado real de la entrega.

## Dependencias

Se revisaron paquetes NuGet de la solucion. No se removieron dependencias porque las existentes estan vinculadas a DI, logging, health checks, Identity, MySQL, QR o tests.

No se encontro uso de ORM, Dapper ni migraciones.

## Seguridad de SQL y mappers

- Se revisaron patrones de `SELECT *`, `AddWithValue`, `DbContext`, `Dapper`, `Html.Raw` y salidas de consola. No se detectaron hallazgos relevantes en codigo productivo.
- `MapTransferencia` lee `FechaSolicitud` y `FechaEvento` como `DateTime`, y `FechaRespuesta` como `DateTime?`.
- Los formateos de fechas de transferencias quedan en Razor, no en SQL.

## Verificacion ejecutada

Comandos ejecutados:

```bash
DOTNET_ROLL_FORWARD=Major /usr/local/share/dotnet/dotnet clean TicketingMundial.sln
DOTNET_ROLL_FORWARD=Major /usr/local/share/dotnet/dotnet restore TicketingMundial.sln
DOTNET_ROLL_FORWARD=Major /usr/local/share/dotnet/dotnet build TicketingMundial.sln
DOTNET_ROLL_FORWARD=Major /usr/local/share/dotnet/dotnet test TicketingMundial.sln
```

Resultado:

- `clean`: OK, 0 warnings, 0 errores.
- `restore`: OK.
- `build`: OK, 0 warnings, 0 errores.
- `test`: OK, 111 superados, 0 fallidos, 0 omitidos.

Tambien se valido una copia temporal limpia del working tree, excluyendo `.git`, `bin/`, `obj/`, `TestResults/` y `src/TicketingMundial.Web/appsettings.Development.json`. En esa copia:

- `restore`: OK.
- `build`: OK, 0 warnings, 0 errores.
- `test`: OK, 111 superados, 0 fallidos, 0 omitidos.

## Riesgos residuales

- La verificacion funcional end-to-end contra MySQL real depende de credenciales y datos locales no versionados.
- El repositorio contiene cambios sin commit porque la consigna fue no commitear ni pushear.
- Un clon remoto limpio no reflejara esta auditoria hasta que estos cambios se commiteen y pusheen.
