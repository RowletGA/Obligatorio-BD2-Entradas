# Configuración demo

No incluir correos, contraseñas, documentos reales ni connection strings reales en Git.

## Crear Usuario General

1. Abrir `/Account/Register`.
2. Registrar un usuario con datos demo y contraseña conocida solo localmente.
3. Confirmar login en `/Account/Login`.

## Convertir un usuario demo en Administrador

Buscar la clave compuesta por correo:

```sql
SELECT TipoDocumento, PaisDocumento, Numero
FROM Usuario
WHERE CorreoElectronico = @CorreoDemo;
```

Insertar el perfil si no existe:

```sql
INSERT INTO Administrador (TipoDocumento, PaisDocumento, Numero, FechaAsignacion, PaisSede)
SELECT TipoDocumento, PaisDocumento, Numero, CURRENT_DATE, @PaisSede
FROM Usuario
WHERE CorreoElectronico = @CorreoDemo
  AND NOT EXISTS (
      SELECT 1
      FROM Administrador a
      WHERE a.TipoDocumento = Usuario.TipoDocumento
        AND a.PaisDocumento = Usuario.PaisDocumento
        AND a.Numero = Usuario.Numero
  );
```

Cerrar sesión e iniciar sesión nuevamente para regenerar claims.

## Convertir un usuario demo en Funcionario

```sql
INSERT INTO Funcionario (TipoDocumento, PaisDocumento, Numero, NumLegajo)
SELECT TipoDocumento, PaisDocumento, Numero, @LegajoDemo
FROM Usuario
WHERE CorreoElectronico = @CorreoDemo
  AND NOT EXISTS (
      SELECT 1
      FROM Funcionario f
      WHERE f.TipoDocumento = Usuario.TipoDocumento
        AND f.PaisDocumento = Usuario.PaisDocumento
        AND f.Numero = Usuario.Numero
  );
```

Cerrar sesión e iniciar sesión nuevamente para regenerar claims.

## Asignar funcionario a evento y sector

Desde la aplicación:

1. Iniciar sesión como administrador.
2. Abrir `/Admin/Funcionarios`.
3. Seleccionar funcionario, evento y sector habilitado.
4. Guardar.

SQL equivalente para diagnóstico:

```sql
INSERT INTO FuncionarioEventoSector (TipoDocumento, PaisDocumento, Numero, IDEvento, IDSector)
VALUES (@TipoDocumento, @PaisDocumento, @Numero, @IDEvento, @IDSector);
```

La clave `(IDEvento, IDSector)` debe existir en `EventoSector`.
