using Microsoft.Extensions.Logging;
using MySqlConnector;
using TicketingMundial.Application.Abstractions.Repositories;
using TicketingMundial.Application.DTOs;
using TicketingMundial.Domain.Identity;
using TicketingMundial.Infrastructure.Data;
using TicketingMundial.Infrastructure.Errors;

namespace TicketingMundial.Infrastructure.Repositories;

public sealed class UsuarioRepository(
    IDbConnectionFactory connectionFactory,
    IMySqlExceptionTranslator exceptionTranslator,
    ILogger<UsuarioRepository> logger) : IUsuarioRepository
{
    public async Task<UsuarioAutenticacion?> ObtenerParaAutenticacionAsync(
        string correoElectronico,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                u.TipoDocumento,
                u.PaisDocumento,
                u.Numero,
                u.PrimerNombre,
                u.PrimerApellido,
                u.CorreoElectronico,
                u.HashContrasena,
                EXISTS (
                    SELECT 1
                    FROM UsuarioGeneral ug
                    WHERE ug.TipoDocumento = u.TipoDocumento
                      AND ug.PaisDocumento = u.PaisDocumento
                      AND ug.Numero = u.Numero
                ) AS EsUsuarioGeneral,
                EXISTS (
                    SELECT 1
                    FROM Funcionario f
                    WHERE f.TipoDocumento = u.TipoDocumento
                      AND f.PaisDocumento = u.PaisDocumento
                      AND f.Numero = u.Numero
                ) AS EsFuncionario,
                EXISTS (
                    SELECT 1
                    FROM Administrador a
                    WHERE a.TipoDocumento = u.TipoDocumento
                      AND a.PaisDocumento = u.PaisDocumento
                      AND a.Numero = u.Numero
                ) AS EsAdministrador
            FROM Usuario u
            WHERE u.CorreoElectronico = @CorreoElectronico;
            """;

        try
        {
            await using var connection = await connectionFactory.OpenAsync(cancellationToken);
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.Add("@CorreoElectronico", MySqlDbType.VarChar, 254).Value = correoElectronico;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            return new UsuarioAutenticacion
            {
                Documento = new DocumentoUsuario(
                    reader.GetRequiredString("TipoDocumento"),
                    reader.GetRequiredString("PaisDocumento"),
                    reader.GetRequiredString("Numero")),
                PrimerNombre = reader.GetRequiredString("PrimerNombre"),
                PrimerApellido = reader.GetRequiredString("PrimerApellido"),
                CorreoElectronico = reader.GetRequiredString("CorreoElectronico"),
                HashContrasena = reader.GetNullableString("HashContrasena"),
                Roles = MapRoles(reader)
            };
        }
        catch (MySqlException ex)
        {
            throw exceptionTranslator.Translate(ex, "obtener usuario para autenticacion");
        }
    }

    public async Task<bool> ExisteCorreoAsync(
        string correoElectronico,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1
                FROM Usuario
                WHERE CorreoElectronico = @CorreoElectronico
            );
            """;

        try
        {
            await using var connection = await connectionFactory.OpenAsync(cancellationToken);
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.Add("@CorreoElectronico", MySqlDbType.VarChar, 254).Value = correoElectronico;
            return Convert.ToBoolean(await command.ExecuteScalarAsync(cancellationToken));
        }
        catch (MySqlException ex)
        {
            throw exceptionTranslator.Translate(ex, "verificar correo de usuario");
        }
    }

    public async Task<bool> ExisteDocumentoAsync(
        DocumentoUsuario documento,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1
                FROM Usuario
                WHERE TipoDocumento = @TipoDocumento
                  AND PaisDocumento = @PaisDocumento
                  AND Numero = @Numero
            );
            """;

        try
        {
            await using var connection = await connectionFactory.OpenAsync(cancellationToken);
            await using var command = new MySqlCommand(sql, connection);
            AddDocumentoParameters(command, documento);
            return Convert.ToBoolean(await command.ExecuteScalarAsync(cancellationToken));
        }
        catch (MySqlException ex)
        {
            throw exceptionTranslator.Translate(ex, "verificar documento de usuario");
        }
    }

    public async Task RegistrarUsuarioGeneralAsync(
        RegistroUsuarioData usuario,
        string hashContrasena,
        CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            if (await ExisteCorreoEnTransaccionAsync(connection, transaction, usuario.CorreoElectronico, cancellationToken))
            {
                throw new DatabaseException("Ya existe un usuario registrado con ese correo electrónico.",
                    new InvalidOperationException("Correo duplicado detectado antes del insert."));
            }

            if (await ExisteDocumentoEnTransaccionAsync(connection, transaction, usuario.Documento, cancellationToken))
            {
                throw new DatabaseException("Ya existe un usuario registrado con ese documento.",
                    new InvalidOperationException("Documento duplicado detectado antes del insert."));
            }

            await InsertarUsuarioAsync(connection, transaction, usuario, hashContrasena, cancellationToken);
            await InsertarUsuarioGeneralAsync(connection, transaction, usuario.Documento, cancellationToken);

            foreach (var telefono in usuario.Telefonos)
            {
                await InsertarTelefonoAsync(connection, transaction, usuario.Documento, telefono, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch (DatabaseException)
        {
            await RollbackAsync(transaction);
            throw;
        }
        catch (MySqlException ex)
        {
            await RollbackAsync(transaction);
            throw exceptionTranslator.Translate(ex, "registrar usuario general");
        }
        catch (Exception ex)
        {
            await RollbackAsync(transaction);
            logger.LogError(ex, "Error inesperado durante el registro de usuario general.");
            throw;
        }
    }

    public async Task ActualizarHashContrasenaAsync(
        DocumentoUsuario documento,
        string hashContrasena,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE Usuario
            SET HashContrasena = @HashContrasena
            WHERE TipoDocumento = @TipoDocumento
              AND PaisDocumento = @PaisDocumento
              AND Numero = @Numero;
            """;

        try
        {
            await using var connection = await connectionFactory.OpenAsync(cancellationToken);
            await using var command = new MySqlCommand(sql, connection);
            AddDocumentoParameters(command, documento);
            command.Parameters.Add("@HashContrasena", MySqlDbType.VarChar, 255).Value = hashContrasena;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (MySqlException ex)
        {
            throw exceptionTranslator.Translate(ex, "actualizar hash de contraseña");
        }
    }

    private static async Task<bool> ExisteCorreoEnTransaccionAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string correoElectronico,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1
                FROM Usuario
                WHERE CorreoElectronico = @CorreoElectronico
            );
            """;

        await using var command = new MySqlCommand(sql, connection, transaction);
        command.Parameters.Add("@CorreoElectronico", MySqlDbType.VarChar, 254).Value = correoElectronico;
        return Convert.ToBoolean(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static async Task<bool> ExisteDocumentoEnTransaccionAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        DocumentoUsuario documento,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1
                FROM Usuario
                WHERE TipoDocumento = @TipoDocumento
                  AND PaisDocumento = @PaisDocumento
                  AND Numero = @Numero
            );
            """;

        await using var command = new MySqlCommand(sql, connection, transaction);
        AddDocumentoParameters(command, documento);
        return Convert.ToBoolean(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static async Task InsertarUsuarioAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        RegistroUsuarioData usuario,
        string hashContrasena,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO Usuario (
                TipoDocumento,
                PaisDocumento,
                Numero,
                PrimerNombre,
                PrimerApellido,
                CorreoElectronico,
                HashContrasena,
                Contrasena,
                DireccionPais,
                DireccionLocalidad,
                DireccionCalle,
                DireccionNumero,
                DireccionCodigoPostal
            )
            VALUES (
                @TipoDocumento,
                @PaisDocumento,
                @Numero,
                @PrimerNombre,
                @PrimerApellido,
                @CorreoElectronico,
                @HashContrasena,
                @Contrasena,
                @DireccionPais,
                @DireccionLocalidad,
                @DireccionCalle,
                @DireccionNumero,
                @DireccionCodigoPostal
            );
            """;

        await using var command = new MySqlCommand(sql, connection, transaction);
        AddDocumentoParameters(command, usuario.Documento);
        command.Parameters.Add("@PrimerNombre", MySqlDbType.VarChar, 60).Value = usuario.PrimerNombre;
        command.Parameters.Add("@PrimerApellido", MySqlDbType.VarChar, 60).Value = usuario.PrimerApellido;
        command.Parameters.Add("@CorreoElectronico", MySqlDbType.VarChar, 254).Value = usuario.CorreoElectronico;
        command.Parameters.Add("@HashContrasena", MySqlDbType.VarChar, 255).Value = hashContrasena;
        command.Parameters.Add("@Contrasena", MySqlDbType.VarChar, 255).Value = hashContrasena;
        AddNullableString(command, "@DireccionPais", MySqlDbType.VarChar, 50, usuario.DireccionPais);
        AddNullableString(command, "@DireccionLocalidad", MySqlDbType.VarChar, 100, usuario.DireccionLocalidad);
        AddNullableString(command, "@DireccionCalle", MySqlDbType.VarChar, 100, usuario.DireccionCalle);
        AddNullableString(command, "@DireccionNumero", MySqlDbType.VarChar, 20, usuario.DireccionNumero);
        AddNullableString(command, "@DireccionCodigoPostal", MySqlDbType.VarChar, 15, usuario.DireccionCodigoPostal);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertarUsuarioGeneralAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        DocumentoUsuario documento,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO UsuarioGeneral (
                TipoDocumento,
                PaisDocumento,
                Numero
            )
            VALUES (
                @TipoDocumento,
                @PaisDocumento,
                @Numero
            );
            """;

        await using var command = new MySqlCommand(sql, connection, transaction);
        AddDocumentoParameters(command, documento);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertarTelefonoAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        DocumentoUsuario documento,
        string telefono,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO TelefonoUsuario (
                TipoDocumento,
                PaisDocumento,
                NumeroDoc,
                Telefono
            )
            VALUES (
                @TipoDocumento,
                @PaisDocumento,
                @Numero,
                @Telefono
            );
            """;

        await using var command = new MySqlCommand(sql, connection, transaction);
        AddDocumentoParameters(command, documento);
        command.Parameters.Add("@Telefono", MySqlDbType.VarChar, 30).Value = telefono;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static IReadOnlyCollection<string> MapRoles(MySqlDataReader reader)
    {
        var roles = new List<string>();
        if (reader.GetBooleanValue("EsUsuarioGeneral"))
        {
            roles.Add(RolesAplicacion.UsuarioGeneral);
        }

        if (reader.GetBooleanValue("EsFuncionario"))
        {
            roles.Add(RolesAplicacion.Funcionario);
        }

        if (reader.GetBooleanValue("EsAdministrador"))
        {
            roles.Add(RolesAplicacion.Administrador);
        }

        return roles;
    }

    private static void AddDocumentoParameters(MySqlCommand command, DocumentoUsuario documento)
    {
        command.Parameters.Add("@TipoDocumento", MySqlDbType.VarChar, 20).Value = documento.TipoDocumento;
        command.Parameters.Add("@PaisDocumento", MySqlDbType.VarChar, 50).Value = documento.PaisDocumento;
        command.Parameters.Add("@Numero", MySqlDbType.VarChar, 30).Value = documento.NumeroDocumento;
    }

    private static void AddNullableString(
        MySqlCommand command,
        string name,
        MySqlDbType type,
        int size,
        string? value)
    {
        command.Parameters.Add(name, type, size).Value =
            string.IsNullOrWhiteSpace(value) ? DBNull.Value : value;
    }

    private static async Task RollbackAsync(MySqlTransaction transaction)
    {
        try
        {
            await transaction.RollbackAsync();
        }
        catch
        {
            // The original exception is more useful to callers and is logged by the repository/translator.
        }
    }
}
