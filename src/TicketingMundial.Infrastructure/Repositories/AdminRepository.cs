using System.Text;
using MySqlConnector;
using TicketingMundial.Application.Abstractions.Repositories;
using TicketingMundial.Application.DTOs;
using TicketingMundial.Application.Security;
using TicketingMundial.Domain.Identity;
using TicketingMundial.Infrastructure.Data;
using TicketingMundial.Infrastructure.Errors;

namespace TicketingMundial.Infrastructure.Repositories;

public sealed class AdminRepository(
    IDbConnectionFactory connectionFactory,
    IMySqlExceptionTranslator exceptionTranslator) : IAdminRepository
{
    public async Task<AdministradorActualDto?> ObtenerAdministradorAsync(DocumentoUsuario documento, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TipoDocumento, PaisDocumento, Numero, FechaAsignacion, PaisSede
            FROM Administrador
            WHERE TipoDocumento = @TipoDocumento
              AND PaisDocumento = @PaisDocumento
              AND Numero = @Numero;
            """;

        try
        {
            await using var connection = await connectionFactory.OpenAsync(cancellationToken);
            await using var command = new MySqlCommand(sql, connection);
            AddDocumento(command, documento);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            return new AdministradorActualDto
            {
                Documento = new DocumentoUsuario(
                    reader.GetRequiredString("TipoDocumento"),
                    reader.GetRequiredString("PaisDocumento"),
                    reader.GetRequiredString("Numero")),
                FechaAsignacion = DateOnly.FromDateTime(reader.GetDateTimeValue("FechaAsignacion")),
                PaisSede = reader.GetRequiredString("PaisSede").Trim().ToUpperInvariant()
            };
        }
        catch (MySqlException ex)
        {
            throw exceptionTranslator.Translate(ex, "obtener administrador");
        }
    }

    public async Task<AdminDashboardDto> ObtenerDashboardAsync(string paisSede, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                (SELECT COUNT(*) FROM Estadio WHERE UbicacionPais = @PaisSede) AS Estadios,
                (SELECT COUNT(*)
                 FROM Sector s INNER JOIN Estadio e ON e.IDEstadio = s.IDEstadio
                 WHERE e.UbicacionPais = @PaisSede) AS Sectores,
                (SELECT COUNT(*) FROM Equipo) AS Equipos,
                (SELECT COUNT(*)
                 FROM Evento ev INNER JOIN Estadio e ON e.IDEstadio = ev.IDEstadio
                 WHERE e.UbicacionPais = @PaisSede) AS Eventos,
                (SELECT COUNT(*)
                 FROM Evento ev
                 INNER JOIN Estadio e ON e.IDEstadio = ev.IDEstadio
                 LEFT JOIN EventoSector es ON es.IDEvento = ev.IDEvento
                 WHERE e.UbicacionPais = @PaisSede
                 GROUP BY e.UbicacionPais) AS EventosAgrupados;
            """;

        try
        {
            await using var connection = await connectionFactory.OpenAsync(cancellationToken);
            var dashboard = new AdminDashboardDto { PaisSede = paisSede };

            await using (var command = new MySqlCommand(sql, connection))
            {
                command.Parameters.Add("@PaisSede", MySqlDbType.VarChar, 50).Value = paisSede;
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                if (await reader.ReadAsync(cancellationToken))
                {
                    dashboard = new AdminDashboardDto
                    {
                        PaisSede = paisSede,
                        Estadios = Convert.ToInt32(reader.GetValue(reader.GetOrdinal("Estadios"))),
                        Sectores = Convert.ToInt32(reader.GetValue(reader.GetOrdinal("Sectores"))),
                        Equipos = Convert.ToInt32(reader.GetValue(reader.GetOrdinal("Equipos"))),
                        Eventos = Convert.ToInt32(reader.GetValue(reader.GetOrdinal("Eventos"))),
                        EventosSinSectores = 0
                    };
                }
            }

            var proximos = await ListarProximosEventosAsync(connection, paisSede, cancellationToken);
            return new AdminDashboardDto
            {
                PaisSede = dashboard.PaisSede,
                Estadios = dashboard.Estadios,
                Sectores = dashboard.Sectores,
                Equipos = dashboard.Equipos,
                Eventos = dashboard.Eventos,
                EventosSinSectores = await CountEventosSinSectoresAsync(connection, paisSede, cancellationToken),
                ProximosEventos = proximos
            };
        }
        catch (MySqlException ex)
        {
            throw exceptionTranslator.Translate(ex, "obtener dashboard administrativo");
        }
    }

    public async Task<PagedResult<EstadioAdminDto>> ListarEstadiosAsync(string paisSede, string? busqueda, int page, int pageSize, CancellationToken cancellationToken)
    {
        var where = new StringBuilder("WHERE e.UbicacionPais = @PaisSede");
        var parameters = new List<MySqlParameter> { new("@PaisSede", MySqlDbType.VarChar, 50) { Value = paisSede } };
        if (!string.IsNullOrWhiteSpace(busqueda))
        {
            where.AppendLine(" AND (e.Nombre LIKE @Busqueda ESCAPE '\\\\' OR e.UbicacionLocalidad LIKE @Busqueda ESCAPE '\\\\')");
            parameters.Add(new MySqlParameter("@Busqueda", MySqlDbType.VarChar, 120) { Value = $"%{SqlSafety.EscapeLikePattern(busqueda.Trim())}%" });
        }

        var sql = $"""
            SELECT e.IDEstadio, e.Nombre, e.UbicacionPais, e.UbicacionLocalidad, e.UbicacionCalle, e.UbicacionNumero,
                   COUNT(DISTINCT s.IDSector) AS Sectores,
                   COUNT(DISTINCT ev.IDEvento) AS Eventos
            FROM Estadio e
            LEFT JOIN Sector s ON s.IDEstadio = e.IDEstadio
            LEFT JOIN Evento ev ON ev.IDEstadio = e.IDEstadio
            {where}
            GROUP BY e.IDEstadio, e.Nombre, e.UbicacionPais, e.UbicacionLocalidad, e.UbicacionCalle, e.UbicacionNumero
            ORDER BY e.Nombre, e.UbicacionLocalidad
            LIMIT @Limit OFFSET @Offset;
            """;
        var countSql = $"SELECT COUNT(*) FROM Estadio e {where};";
        return await QueryPagedAsync(sql, countSql, parameters, page, pageSize, MapEstadio, "listar estadios", cancellationToken);
    }

    public async Task<IReadOnlyList<EstadioAdminDto>> ListarEstadiosParaSeleccionAsync(string paisSede, CancellationToken cancellationToken)
    {
        var result = await ListarEstadiosAsync(paisSede, null, 1, 200, cancellationToken);
        return result.Items;
    }

    public async Task<EstadioAdminDto?> ObtenerEstadioAsync(ulong idEstadio, string paisSede, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT e.IDEstadio, e.Nombre, e.UbicacionPais, e.UbicacionLocalidad, e.UbicacionCalle, e.UbicacionNumero,
                   COUNT(DISTINCT s.IDSector) AS Sectores,
                   COUNT(DISTINCT ev.IDEvento) AS Eventos
            FROM Estadio e
            LEFT JOIN Sector s ON s.IDEstadio = e.IDEstadio
            LEFT JOIN Evento ev ON ev.IDEstadio = e.IDEstadio
            WHERE e.IDEstadio = @IdEstadio AND e.UbicacionPais = @PaisSede
            GROUP BY e.IDEstadio, e.Nombre, e.UbicacionPais, e.UbicacionLocalidad, e.UbicacionCalle, e.UbicacionNumero;
            """;
        return await QuerySingleAsync(sql, command =>
        {
            command.Parameters.Add("@IdEstadio", MySqlDbType.UInt64).Value = idEstadio;
            command.Parameters.Add("@PaisSede", MySqlDbType.VarChar, 50).Value = paisSede;
        }, MapEstadio, "obtener estadio", cancellationToken);
    }

    public async Task<ulong> CrearEstadioAsync(EstadioUpsertCommand command, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO Estadio (Nombre, UbicacionPais, UbicacionLocalidad, UbicacionCalle, UbicacionNumero)
            VALUES (@Nombre, @Pais, @Localidad, @Calle, @Numero);
            SELECT LAST_INSERT_ID();
            """;
        return await ExecuteScalarUInt64Async(sql, cmd => AddEstadioParameters(cmd, command), "crear estadio", cancellationToken);
    }

    public async Task<bool> ActualizarEstadioAsync(EstadioUpsertCommand command, string paisSede, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE Estadio
            SET Nombre = @Nombre,
                UbicacionLocalidad = @Localidad,
                UbicacionCalle = @Calle,
                UbicacionNumero = @Numero
            WHERE IDEstadio = @IdEstadio
              AND UbicacionPais = @PaisSede;
            """;
        return await ExecuteNonQueryAsync(sql, cmd =>
        {
            AddEstadioParameters(cmd, command);
            cmd.Parameters.Add("@IdEstadio", MySqlDbType.UInt64).Value = command.IdEstadio!.Value;
            cmd.Parameters.Add("@PaisSede", MySqlDbType.VarChar, 50).Value = paisSede;
        }, "actualizar estadio", cancellationToken) > 0;
    }

    public async Task<PagedResult<SectorAdminDto>> ListarSectoresAsync(string paisSede, ulong? idEstadio, string? busqueda, int page, int pageSize, CancellationToken cancellationToken)
    {
        var where = new StringBuilder("WHERE e.UbicacionPais = @PaisSede");
        var parameters = new List<MySqlParameter> { new("@PaisSede", MySqlDbType.VarChar, 50) { Value = paisSede } };
        if (idEstadio.HasValue)
        {
            where.AppendLine(" AND s.IDEstadio = @IdEstadio");
            parameters.Add(new MySqlParameter("@IdEstadio", MySqlDbType.UInt64) { Value = idEstadio.Value });
        }
        if (!string.IsNullOrWhiteSpace(busqueda))
        {
            where.AppendLine(" AND (s.NombreSector LIKE @Busqueda ESCAPE '\\\\' OR e.Nombre LIKE @Busqueda ESCAPE '\\\\')");
            parameters.Add(new MySqlParameter("@Busqueda", MySqlDbType.VarChar, 120) { Value = $"%{SqlSafety.EscapeLikePattern(busqueda.Trim())}%" });
        }

        var sql = $"""
            SELECT s.IDSector, s.NombreSector, s.Capacidad, s.IDEstadio, e.Nombre AS Estadio, e.UbicacionPais AS PaisEstadio
            FROM Sector s
            INNER JOIN Estadio e ON e.IDEstadio = s.IDEstadio
            {where}
            ORDER BY e.Nombre, s.NombreSector
            LIMIT @Limit OFFSET @Offset;
            """;
        var countSql = $"SELECT COUNT(*) FROM Sector s INNER JOIN Estadio e ON e.IDEstadio = s.IDEstadio {where};";
        return await QueryPagedAsync(sql, countSql, parameters, page, pageSize, MapSector, "listar sectores", cancellationToken);
    }

    public async Task<IReadOnlyList<SectorAdminDto>> ListarSectoresPorEstadioAsync(ulong idEstadio, string paisSede, CancellationToken cancellationToken)
    {
        var result = await ListarSectoresAsync(paisSede, idEstadio, null, 1, 500, cancellationToken);
        return result.Items;
    }

    public async Task<SectorAdminDto?> ObtenerSectorAsync(ulong idSector, string paisSede, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT s.IDSector, s.NombreSector, s.Capacidad, s.IDEstadio, e.Nombre AS Estadio, e.UbicacionPais AS PaisEstadio
            FROM Sector s
            INNER JOIN Estadio e ON e.IDEstadio = s.IDEstadio
            WHERE s.IDSector = @IdSector AND e.UbicacionPais = @PaisSede;
            """;
        return await QuerySingleAsync(sql, cmd =>
        {
            cmd.Parameters.Add("@IdSector", MySqlDbType.UInt64).Value = idSector;
            cmd.Parameters.Add("@PaisSede", MySqlDbType.VarChar, 50).Value = paisSede;
        }, MapSector, "obtener sector", cancellationToken);
    }

    public async Task<ulong> CrearSectorAsync(SectorUpsertCommand command, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO Sector (NombreSector, Capacidad, IDEstadio)
            VALUES (@Nombre, @Capacidad, @IdEstadio);
            SELECT LAST_INSERT_ID();
            """;
        return await ExecuteScalarUInt64Async(sql, cmd => AddSectorParameters(cmd, command), "crear sector", cancellationToken);
    }

    public async Task<bool> ActualizarSectorAsync(SectorUpsertCommand command, string paisSede, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE Sector s
            INNER JOIN Estadio e ON e.IDEstadio = s.IDEstadio
            SET s.NombreSector = @Nombre,
                s.Capacidad = @Capacidad,
                s.IDEstadio = @IdEstadio
            WHERE s.IDSector = @IdSector
              AND e.UbicacionPais = @PaisSede;
            """;
        return await ExecuteNonQueryAsync(sql, cmd =>
        {
            AddSectorParameters(cmd, command);
            cmd.Parameters.Add("@IdSector", MySqlDbType.UInt64).Value = command.IdSector!.Value;
            cmd.Parameters.Add("@PaisSede", MySqlDbType.VarChar, 50).Value = paisSede;
        }, "actualizar sector", cancellationToken) > 0;
    }

    public async Task<bool> ExisteSectorNombreAsync(ulong idEstadio, string nombreSector, ulong? excludingId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1 FROM Sector
                WHERE IDEstadio = @IdEstadio
                  AND NombreSector = @Nombre
                  AND (@ExcludingId IS NULL OR IDSector <> @ExcludingId)
            );
            """;
        return await ExecuteScalarBoolAsync(sql, cmd =>
        {
            cmd.Parameters.Add("@IdEstadio", MySqlDbType.UInt64).Value = idEstadio;
            cmd.Parameters.Add("@Nombre", MySqlDbType.VarChar, 100).Value = nombreSector;
            cmd.Parameters.Add("@ExcludingId", MySqlDbType.UInt64).Value = excludingId.HasValue ? excludingId.Value : DBNull.Value;
        }, "verificar sector duplicado", cancellationToken);
    }

    public async Task<PagedResult<EquipoAdminDto>> ListarEquiposAsync(string? busqueda, int page, int pageSize, CancellationToken cancellationToken)
    {
        var where = new StringBuilder();
        var parameters = new List<MySqlParameter>();
        if (!string.IsNullOrWhiteSpace(busqueda))
        {
            where.Append("WHERE Pais LIKE @Busqueda ESCAPE '\\\\' OR Grupo LIKE @Busqueda ESCAPE '\\\\'");
            parameters.Add(new MySqlParameter("@Busqueda", MySqlDbType.VarChar, 80) { Value = $"%{SqlSafety.EscapeLikePattern(busqueda.Trim())}%" });
        }
        var sql = $"""
            SELECT IDEquipo, Pais, Grupo
            FROM Equipo
            {where}
            ORDER BY Pais, Grupo
            LIMIT @Limit OFFSET @Offset;
            """;
        var countSql = $"SELECT COUNT(*) FROM Equipo {where};";
        return await QueryPagedAsync(sql, countSql, parameters, page, pageSize, MapEquipo, "listar equipos", cancellationToken);
    }

    public async Task<IReadOnlyList<EquipoAdminDto>> ListarEquiposParaSeleccionAsync(CancellationToken cancellationToken)
    {
        var result = await ListarEquiposAsync(null, 1, 500, cancellationToken);
        return result.Items;
    }

    public Task<EquipoAdminDto?> ObtenerEquipoAsync(ulong idEquipo, CancellationToken cancellationToken)
    {
        const string sql = "SELECT IDEquipo, Pais, Grupo FROM Equipo WHERE IDEquipo = @IdEquipo;";
        return QuerySingleAsync(sql, cmd => cmd.Parameters.Add("@IdEquipo", MySqlDbType.UInt64).Value = idEquipo, MapEquipo, "obtener equipo", cancellationToken);
    }

    public async Task<ulong> CrearEquipoAsync(EquipoUpsertCommand command, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO Equipo (Pais, Grupo)
            VALUES (@Pais, @Grupo);
            SELECT LAST_INSERT_ID();
            """;
        return await ExecuteScalarUInt64Async(sql, cmd => AddEquipoParameters(cmd, command), "crear equipo", cancellationToken);
    }

    public async Task<bool> ActualizarEquipoAsync(EquipoUpsertCommand command, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE Equipo
            SET Pais = @Pais,
                Grupo = @Grupo
            WHERE IDEquipo = @IdEquipo;
            """;
        return await ExecuteNonQueryAsync(sql, cmd =>
        {
            AddEquipoParameters(cmd, command);
            cmd.Parameters.Add("@IdEquipo", MySqlDbType.UInt64).Value = command.IdEquipo!.Value;
        }, "actualizar equipo", cancellationToken) > 0;
    }

    public async Task<PagedResult<EventoAdminDto>> ListarEventosAsync(string paisSede, int page, int pageSize, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT IDEvento, FechaHora, EstadoEvento, IDEstadio, Estadio, PaisEstadio,
                   IDEquipoLocal, EquipoLocal, IDEquipoVisitante, EquipoVisitante
            FROM V_Eventos
            WHERE PaisEstadio = @PaisSede
            ORDER BY FechaHora DESC
            LIMIT @Limit OFFSET @Offset;
            """;
        const string countSql = "SELECT COUNT(*) FROM V_Eventos WHERE PaisEstadio = @PaisSede;";
        var parameters = new List<MySqlParameter> { new("@PaisSede", MySqlDbType.VarChar, 50) { Value = paisSede } };
        return await QueryPagedAsync(sql, countSql, parameters, page, pageSize, MapEvento, "listar eventos administrativos", cancellationToken);
    }

    public async Task<EventoAdminDto?> ObtenerEventoAsync(ulong idEvento, string paisSede, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT IDEvento, FechaHora, EstadoEvento, IDEstadio, Estadio, PaisEstadio,
                   IDEquipoLocal, EquipoLocal, IDEquipoVisitante, EquipoVisitante
            FROM V_Eventos
            WHERE IDEvento = @IdEvento AND PaisEstadio = @PaisSede;
            """;
        var evento = await QuerySingleAsync(sql, cmd =>
        {
            cmd.Parameters.Add("@IdEvento", MySqlDbType.UInt64).Value = idEvento;
            cmd.Parameters.Add("@PaisSede", MySqlDbType.VarChar, 50).Value = paisSede;
        }, MapEvento, "obtener evento administrativo", cancellationToken);

        if (evento is null)
        {
            return null;
        }

        return new EventoAdminDto
        {
            IdEvento = evento.IdEvento,
            FechaHora = evento.FechaHora,
            EstadoEvento = evento.EstadoEvento,
            IdEstadio = evento.IdEstadio,
            Estadio = evento.Estadio,
            PaisEstadio = evento.PaisEstadio,
            IdEquipoLocal = evento.IdEquipoLocal,
            EquipoLocal = evento.EquipoLocal,
            IdEquipoVisitante = evento.IdEquipoVisitante,
            EquipoVisitante = evento.EquipoVisitante,
            Sectores = await ListarEventoSectoresAsync(idEvento, cancellationToken)
        };
    }

    public async Task<ulong> CrearEventoAsync(EventoCreateCommand command, string paisSede, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            var fechaHora = command.Fecha.ToDateTime(command.Hora);
            const string insertEvento = """
                INSERT INTO Evento (EstadoEvento, FechaHora, IDEstadio)
                SELECT @Estado, @FechaHora, e.IDEstadio
                FROM Estadio e
                WHERE e.IDEstadio = @IdEstadio
                  AND e.UbicacionPais = @PaisSede;
                """;
            await using (var cmd = new MySqlCommand(insertEvento, connection, transaction))
            {
                cmd.Parameters.Add("@Estado", MySqlDbType.VarChar, 20).Value = command.EstadoEvento;
                cmd.Parameters.Add("@FechaHora", MySqlDbType.DateTime).Value = fechaHora;
                cmd.Parameters.Add("@IdEstadio", MySqlDbType.UInt64).Value = command.IdEstadio;
                cmd.Parameters.Add("@PaisSede", MySqlDbType.VarChar, 50).Value = paisSede;
                if (await cmd.ExecuteNonQueryAsync(cancellationToken) != 1)
                {
                    throw new DatabaseException(
                        "El estadio seleccionado no pertenece a su jurisdicción.",
                        new InvalidOperationException("No se insertó el evento porque el estadio no coincide con la jurisdicción."));
                }
            }

            var idEvento = Convert.ToUInt64(new MySqlCommand("SELECT LAST_INSERT_ID();", connection, transaction)
                .ExecuteScalar());

            await InsertEventoEquipoAsync(connection, transaction, "EventoLocal", idEvento, command.IdEquipoLocal, cancellationToken);
            await InsertEventoEquipoAsync(connection, transaction, "EventoVisita", idEvento, command.IdEquipoVisitante, cancellationToken);

            foreach (var sector in command.Sectores)
            {
                const string insertSector = """
                    INSERT INTO EventoSector (IDEvento, IDSector, PrecioBase)
                    VALUES (@IdEvento, @IdSector, @PrecioBase);
                    """;
                await using var cmd = new MySqlCommand(insertSector, connection, transaction);
                cmd.Parameters.Add("@IdEvento", MySqlDbType.UInt64).Value = idEvento;
                cmd.Parameters.Add("@IdSector", MySqlDbType.UInt64).Value = sector.IdSector;
                cmd.Parameters.Add("@PrecioBase", MySqlDbType.Decimal).Value = sector.PrecioBase;
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return idEvento;
        }
        catch (DatabaseException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
        catch (MySqlException ex)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw exceptionTranslator.Translate(ex, "crear evento");
        }
    }

    public async Task<bool> CambiarEstadoEventoAsync(ulong idEvento, string estado, string paisSede, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE Evento ev
            INNER JOIN Estadio e ON e.IDEstadio = ev.IDEstadio
            SET ev.EstadoEvento = @Estado
            WHERE ev.IDEvento = @IdEvento
              AND e.UbicacionPais = @PaisSede;
            """;
        return await ExecuteNonQueryAsync(sql, cmd =>
        {
            cmd.Parameters.Add("@Estado", MySqlDbType.VarChar, 20).Value = estado;
            cmd.Parameters.Add("@IdEvento", MySqlDbType.UInt64).Value = idEvento;
            cmd.Parameters.Add("@PaisSede", MySqlDbType.VarChar, 50).Value = paisSede;
        }, "cambiar estado de evento", cancellationToken) > 0;
    }

    private async Task<PagedResult<T>> QueryPagedAsync<T>(string sql, string countSql, List<MySqlParameter> parameters, int page, int pageSize, Func<MySqlDataReader, T> map, string operation, CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await connectionFactory.OpenAsync(cancellationToken);
            var total = await ExecuteCountAsync(connection, countSql, parameters, cancellationToken);
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddRange(parameters.Select(CloneParameter).ToArray());
            command.Parameters.Add("@Limit", MySqlDbType.Int32).Value = pageSize;
            command.Parameters.Add("@Offset", MySqlDbType.Int32).Value = (page - 1) * pageSize;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var items = new List<T>();
            while (await reader.ReadAsync(cancellationToken))
            {
                items.Add(map(reader));
            }

            return new PagedResult<T> { Items = items, Page = page, PageSize = pageSize, TotalItems = total };
        }
        catch (MySqlException ex)
        {
            throw exceptionTranslator.Translate(ex, operation);
        }
    }

    private async Task<T?> QuerySingleAsync<T>(string sql, Action<MySqlCommand> addParameters, Func<MySqlDataReader, T> map, string operation, CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await connectionFactory.OpenAsync(cancellationToken);
            await using var command = new MySqlCommand(sql, connection);
            addParameters(command);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            return await reader.ReadAsync(cancellationToken) ? map(reader) : default;
        }
        catch (MySqlException ex)
        {
            throw exceptionTranslator.Translate(ex, operation);
        }
    }

    private async Task<ulong> ExecuteScalarUInt64Async(string sql, Action<MySqlCommand> addParameters, string operation, CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await connectionFactory.OpenAsync(cancellationToken);
            await using var command = new MySqlCommand(sql, connection);
            addParameters(command);
            return Convert.ToUInt64(await command.ExecuteScalarAsync(cancellationToken));
        }
        catch (MySqlException ex)
        {
            throw exceptionTranslator.Translate(ex, operation);
        }
    }

    private async Task<bool> ExecuteScalarBoolAsync(string sql, Action<MySqlCommand> addParameters, string operation, CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await connectionFactory.OpenAsync(cancellationToken);
            await using var command = new MySqlCommand(sql, connection);
            addParameters(command);
            return Convert.ToBoolean(await command.ExecuteScalarAsync(cancellationToken));
        }
        catch (MySqlException ex)
        {
            throw exceptionTranslator.Translate(ex, operation);
        }
    }

    private async Task<int> ExecuteNonQueryAsync(string sql, Action<MySqlCommand> addParameters, string operation, CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await connectionFactory.OpenAsync(cancellationToken);
            await using var command = new MySqlCommand(sql, connection);
            addParameters(command);
            return await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (MySqlException ex)
        {
            throw exceptionTranslator.Translate(ex, operation);
        }
    }

    private static async Task<int> ExecuteCountAsync(MySqlConnection connection, string sql, List<MySqlParameter> parameters, CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddRange(parameters.Select(CloneParameter).ToArray());
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static async Task InsertEventoEquipoAsync(MySqlConnection connection, MySqlTransaction transaction, string table, ulong idEvento, ulong idEquipo, CancellationToken cancellationToken)
    {
        var sql = table == "EventoLocal"
            ? "INSERT INTO EventoLocal (IDEvento, IDEquipo) VALUES (@IdEvento, @IdEquipo);"
            : "INSERT INTO EventoVisita (IDEvento, IDEquipo) VALUES (@IdEvento, @IdEquipo);";
        await using var command = new MySqlCommand(sql, connection, transaction);
        command.Parameters.Add("@IdEvento", MySqlDbType.UInt64).Value = idEvento;
        command.Parameters.Add("@IdEquipo", MySqlDbType.UInt64).Value = idEquipo;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<EventoAdminDto>> ListarProximosEventosAsync(MySqlConnection connection, string paisSede, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT IDEvento, FechaHora, EstadoEvento, IDEstadio, Estadio, PaisEstadio,
                   IDEquipoLocal, EquipoLocal, IDEquipoVisitante, EquipoVisitante
            FROM V_Eventos
            WHERE PaisEstadio = @PaisSede AND FechaHora >= CURRENT_TIMESTAMP
            ORDER BY FechaHora
            LIMIT 5;
            """;
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.Add("@PaisSede", MySqlDbType.VarChar, 50).Value = paisSede;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var eventos = new List<EventoAdminDto>();
        while (await reader.ReadAsync(cancellationToken))
        {
            eventos.Add(MapEvento(reader));
        }
        return eventos;
    }

    private static async Task<int> CountEventosSinSectoresAsync(MySqlConnection connection, string paisSede, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COUNT(*)
            FROM Evento ev
            INNER JOIN Estadio e ON e.IDEstadio = ev.IDEstadio
            LEFT JOIN EventoSector es ON es.IDEvento = ev.IDEvento
            WHERE e.UbicacionPais = @PaisSede
              AND es.IDEvento IS NULL;
            """;
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.Add("@PaisSede", MySqlDbType.VarChar, 50).Value = paisSede;
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    private async Task<IReadOnlyList<EventoSectorAdminDto>> ListarEventoSectoresAsync(ulong idEvento, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT es.IDSector, s.NombreSector, es.PrecioBase
            FROM EventoSector es
            INNER JOIN Sector s ON s.IDSector = es.IDSector
            WHERE es.IDEvento = @IdEvento
            ORDER BY s.NombreSector;
            """;
        try
        {
            await using var connection = await connectionFactory.OpenAsync(cancellationToken);
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.Add("@IdEvento", MySqlDbType.UInt64).Value = idEvento;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var sectores = new List<EventoSectorAdminDto>();
            while (await reader.ReadAsync(cancellationToken))
            {
                sectores.Add(new EventoSectorAdminDto
                {
                    IdSector = reader.GetUInt64Value("IDSector"),
                    NombreSector = reader.GetRequiredString("NombreSector"),
                    PrecioBase = reader.GetDecimalValue("PrecioBase")
                });
            }
            return sectores;
        }
        catch (MySqlException ex)
        {
            throw exceptionTranslator.Translate(ex, "listar sectores de evento");
        }
    }

    private static EstadioAdminDto MapEstadio(MySqlDataReader reader) => new()
    {
        IdEstadio = reader.GetUInt64Value("IDEstadio"),
        Nombre = reader.GetRequiredString("Nombre"),
        UbicacionPais = reader.GetRequiredString("UbicacionPais"),
        UbicacionLocalidad = reader.GetRequiredString("UbicacionLocalidad"),
        UbicacionCalle = reader.GetRequiredString("UbicacionCalle"),
        UbicacionNumero = reader.GetNullableString("UbicacionNumero"),
        Sectores = Convert.ToInt32(reader.GetValue(reader.GetOrdinal("Sectores"))),
        Eventos = Convert.ToInt32(reader.GetValue(reader.GetOrdinal("Eventos")))
    };

    private static SectorAdminDto MapSector(MySqlDataReader reader) => new()
    {
        IdSector = reader.GetUInt64Value("IDSector"),
        NombreSector = reader.GetRequiredString("NombreSector"),
        Capacidad = reader.GetUInt32Value("Capacidad"),
        IdEstadio = reader.GetUInt64Value("IDEstadio"),
        Estadio = reader.GetRequiredString("Estadio"),
        PaisEstadio = reader.GetRequiredString("PaisEstadio")
    };

    private static EquipoAdminDto MapEquipo(MySqlDataReader reader) => new()
    {
        IdEquipo = reader.GetUInt64Value("IDEquipo"),
        Pais = reader.GetRequiredString("Pais"),
        Grupo = reader.GetNullableString("Grupo")
    };

    private static EventoAdminDto MapEvento(MySqlDataReader reader) => new()
    {
        IdEvento = reader.GetUInt64Value("IDEvento"),
        FechaHora = reader.GetDateTimeValue("FechaHora"),
        EstadoEvento = reader.GetRequiredString("EstadoEvento"),
        IdEstadio = reader.GetUInt64Value("IDEstadio"),
        Estadio = reader.GetRequiredString("Estadio"),
        PaisEstadio = reader.GetRequiredString("PaisEstadio"),
        IdEquipoLocal = GetNullableUInt64(reader, "IDEquipoLocal"),
        EquipoLocal = reader.GetNullableString("EquipoLocal"),
        IdEquipoVisitante = GetNullableUInt64(reader, "IDEquipoVisitante"),
        EquipoVisitante = reader.GetNullableString("EquipoVisitante")
    };

    private static ulong? GetNullableUInt64(MySqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : Convert.ToUInt64(reader.GetValue(ordinal));
    }

    private static void AddDocumento(MySqlCommand command, DocumentoUsuario documento)
    {
        command.Parameters.Add("@TipoDocumento", MySqlDbType.VarChar, 20).Value = documento.TipoDocumento;
        command.Parameters.Add("@PaisDocumento", MySqlDbType.VarChar, 50).Value = documento.PaisDocumento;
        command.Parameters.Add("@Numero", MySqlDbType.VarChar, 30).Value = documento.NumeroDocumento;
    }

    private static void AddEstadioParameters(MySqlCommand command, EstadioUpsertCommand estadio)
    {
        command.Parameters.Add("@Nombre", MySqlDbType.VarChar, 100).Value = estadio.Nombre;
        command.Parameters.Add("@Pais", MySqlDbType.VarChar, 50).Value = estadio.UbicacionPais;
        command.Parameters.Add("@Localidad", MySqlDbType.VarChar, 100).Value = estadio.UbicacionLocalidad;
        command.Parameters.Add("@Calle", MySqlDbType.VarChar, 100).Value = estadio.UbicacionCalle;
        command.Parameters.Add("@Numero", MySqlDbType.VarChar, 20).Value = estadio.UbicacionNumero is null ? DBNull.Value : estadio.UbicacionNumero;
    }

    private static void AddSectorParameters(MySqlCommand command, SectorUpsertCommand sector)
    {
        command.Parameters.Add("@Nombre", MySqlDbType.VarChar, 100).Value = sector.NombreSector;
        command.Parameters.Add("@Capacidad", MySqlDbType.UInt32).Value = sector.Capacidad;
        command.Parameters.Add("@IdEstadio", MySqlDbType.UInt64).Value = sector.IdEstadio;
    }

    private static void AddEquipoParameters(MySqlCommand command, EquipoUpsertCommand equipo)
    {
        command.Parameters.Add("@Pais", MySqlDbType.VarChar, 50).Value = equipo.Pais;
        command.Parameters.Add("@Grupo", MySqlDbType.VarChar, 20).Value = equipo.Grupo is null ? DBNull.Value : equipo.Grupo;
    }

    private static MySqlParameter CloneParameter(MySqlParameter parameter)
    {
        return new MySqlParameter(parameter.ParameterName, parameter.MySqlDbType, parameter.Size) { Value = parameter.Value };
    }
}
