using System.Text;
using MySqlConnector;
using TicketingMundial.Application.Abstractions.Repositories;
using TicketingMundial.Application.DTOs;
using TicketingMundial.Application.Security;
using TicketingMundial.Domain.Estados;
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

    public async Task<PagedResult<EstadioAdminDto>> ListarEstadiosAsync(string paisSede, string? busqueda, string? sort, string? direction, int page, int pageSize, CancellationToken cancellationToken)
    {
        var orderBy = sort switch
        {
            "localidad" => "e.UbicacionLocalidad",
            "pais" => "e.UbicacionPais",
            "sectores" => "Sectores",
            _ => "e.Nombre"
        };
        var dir = direction == "asc" ? "ASC" : "DESC";
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
            ORDER BY {orderBy} {dir}, e.IDEstadio {dir}
            LIMIT @Limit OFFSET @Offset;
            """;
        var countSql = $"SELECT COUNT(*) FROM Estadio e {where};";
        return await QueryPagedAsync(sql, countSql, parameters, page, pageSize, MapEstadio, "listar estadios", cancellationToken);
    }

    public async Task<IReadOnlyList<EstadioAdminDto>> ListarEstadiosParaSeleccionAsync(string paisSede, CancellationToken cancellationToken)
    {
        var result = await ListarEstadiosAsync(paisSede, null, "nombre", "asc", 1, 200, cancellationToken);
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

    public async Task<PagedResult<SectorAdminDto>> ListarSectoresAsync(string paisSede, ulong? idEstadio, string? busqueda, string? sort, string? direction, int page, int pageSize, CancellationToken cancellationToken)
    {
        var orderBy = sort switch
        {
            "estadio" => "e.Nombre",
            "capacidad" => "s.Capacidad",
            _ => "s.NombreSector"
        };
        var dir = direction == "asc" ? "ASC" : "DESC";
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
            ORDER BY {orderBy} {dir}, s.IDSector {dir}
            LIMIT @Limit OFFSET @Offset;
            """;
        var countSql = $"SELECT COUNT(*) FROM Sector s INNER JOIN Estadio e ON e.IDEstadio = s.IDEstadio {where};";
        return await QueryPagedAsync(sql, countSql, parameters, page, pageSize, MapSector, "listar sectores", cancellationToken);
    }

    public async Task<IReadOnlyList<SectorAdminDto>> ListarSectoresPorEstadioAsync(ulong idEstadio, string paisSede, CancellationToken cancellationToken)
    {
        var result = await ListarSectoresAsync(paisSede, idEstadio, null, "nombre", "asc", 1, 500, cancellationToken);
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

    public async Task<PagedResult<EquipoAdminDto>> ListarEquiposAsync(string? busqueda, string? sort, string? direction, int page, int pageSize, CancellationToken cancellationToken)
    {
        var orderBy = sort == "grupo" ? "Grupo" : "Pais";
        var dir = direction == "asc" ? "ASC" : "DESC";
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
            ORDER BY {orderBy} {dir}, IDEquipo {dir}
            LIMIT @Limit OFFSET @Offset;
            """;
        var countSql = $"SELECT COUNT(*) FROM Equipo {where};";
        return await QueryPagedAsync(sql, countSql, parameters, page, pageSize, MapEquipo, "listar equipos", cancellationToken);
    }

    public async Task<IReadOnlyList<EquipoAdminDto>> ListarEquiposParaSeleccionAsync(CancellationToken cancellationToken)
    {
        var result = await ListarEquiposAsync(null, "pais", "asc", 1, 500, cancellationToken);
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

    public async Task<PagedResult<EventoAdminDto>> ListarEventosAsync(string paisSede, string? busqueda, string? estado, string? sort, string? direction, int page, int pageSize, CancellationToken cancellationToken)
    {
        var orderBy = sort switch
        {
            "local" => "v.EquipoLocal",
            "visitante" => "v.EquipoVisitante",
            "estadio" => "v.Estadio",
            "estado" => "v.EstadoEvento",
            "disponibilidad" => "LugaresDisponibles",
            _ => "v.FechaHora"
        };
        var dir = direction == "asc" ? "ASC" : "DESC";
        var where = new StringBuilder("WHERE v.PaisEstadio = @PaisSede");
        var parameters = new List<MySqlParameter> { new("@PaisSede", MySqlDbType.VarChar, 50) { Value = paisSede } };
        if (!string.IsNullOrWhiteSpace(busqueda))
        {
            where.AppendLine(" AND (v.EquipoLocal LIKE @Busqueda ESCAPE '\\\\' OR v.EquipoVisitante LIKE @Busqueda ESCAPE '\\\\' OR v.Estadio LIKE @Busqueda ESCAPE '\\\\')");
            parameters.Add(new MySqlParameter("@Busqueda", MySqlDbType.VarChar, 120) { Value = $"%{SqlSafety.EscapeLikePattern(busqueda.Trim())}%" });
        }
        if (!string.IsNullOrWhiteSpace(estado))
        {
            where.AppendLine(" AND v.EstadoEvento = @Estado");
            parameters.Add(new MySqlParameter("@Estado", MySqlDbType.VarChar, 20) { Value = estado });
        }
        var sql = $"""
            SELECT v.IDEvento, v.FechaHora, v.EstadoEvento, v.IDEstadio, v.Estadio, v.PaisEstadio,
                   v.IDEquipoLocal, v.EquipoLocal, v.IDEquipoVisitante, v.EquipoVisitante,
                   (SELECT COUNT(*) FROM Entrada en WHERE en.IDEvento = v.IDEvento) AS EntradasEmitidas,
                   COALESCE((SELECT SUM(ds.LugaresDisponibles) FROM V_DisponibilidadSectores ds WHERE ds.IDEvento = v.IDEvento), 0) AS LugaresDisponibles
            FROM V_Eventos v
            {where}
            ORDER BY {orderBy} {dir}, v.IDEvento {dir}
            LIMIT @Limit OFFSET @Offset;
            """;
        var countSql = $"SELECT COUNT(*) FROM V_Eventos v {where};";
        return await QueryPagedAsync(sql, countSql, parameters, page, pageSize, MapEvento, "listar eventos administrativos", cancellationToken);
    }

    public async Task<IReadOnlyList<EventoAdminDto>> ListarEventosAsignablesAsync(string paisSede, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT v.IDEvento, v.FechaHora, v.EstadoEvento, v.IDEstadio, v.Estadio, v.PaisEstadio,
                   v.IDEquipoLocal, v.EquipoLocal, v.IDEquipoVisitante, v.EquipoVisitante,
                   (SELECT COUNT(*) FROM Entrada en WHERE en.IDEvento = v.IDEvento) AS EntradasEmitidas
            FROM V_Eventos v
            WHERE v.PaisEstadio = @PaisSede
              AND v.EstadoEvento IN ('PROGRAMADO', 'EN_CURSO')
            ORDER BY v.FechaHora ASC;
            """;
        return await QueryListAsync(sql, command =>
            command.Parameters.Add("@PaisSede", MySqlDbType.VarChar, 50).Value = paisSede,
            MapEvento,
            "listar eventos asignables",
            cancellationToken);
    }

    public async Task<EventoAdminDto?> ObtenerEventoAsync(ulong idEvento, string paisSede, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT v.IDEvento, v.FechaHora, v.EstadoEvento, v.IDEstadio, v.Estadio, v.PaisEstadio,
                   v.IDEquipoLocal, v.EquipoLocal, v.IDEquipoVisitante, v.EquipoVisitante,
                   (SELECT COUNT(*) FROM Entrada en WHERE en.IDEvento = v.IDEvento) AS EntradasEmitidas
            FROM V_Eventos v
            WHERE v.IDEvento = @IdEvento AND v.PaisEstadio = @PaisSede;
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
            EntradasEmitidas = evento.EntradasEmitidas,
            Sectores = await ListarSectoresHabilitadosEventoAsync(idEvento, paisSede, cancellationToken)
        };
    }

    public Task<IReadOnlyList<EventoSectorAdminDto>> ListarSectoresHabilitadosEventoAsync(ulong idEvento, string paisSede, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT s.IDSector, s.NombreSector, s.Capacidad, es.PrecioBase
            FROM EventoSector es
            INNER JOIN Evento ev ON ev.IDEvento = es.IDEvento
            INNER JOIN Estadio est ON est.IDEstadio = ev.IDEstadio
            INNER JOIN Sector s ON s.IDSector = es.IDSector
            WHERE es.IDEvento = @IdEvento
              AND est.UbicacionPais = @PaisSede
            ORDER BY s.NombreSector;
            """;
        return QueryListAsync(sql, command =>
        {
            command.Parameters.Add("@IdEvento", MySqlDbType.UInt64).Value = idEvento;
            command.Parameters.Add("@PaisSede", MySqlDbType.VarChar, 50).Value = paisSede;
        }, MapEventoSector, "listar sectores habilitados del evento", cancellationToken);
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

    public async Task<bool> ActualizarEventoCompletoAsync(EventoUpdateCommand command, string paisSede, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            const string validateSql = """
                SELECT COUNT(en.IDEntrada)
                FROM Evento ev
                INNER JOIN Estadio e ON e.IDEstadio = ev.IDEstadio
                LEFT JOIN Entrada en ON en.IDEvento = ev.IDEvento
                WHERE ev.IDEvento = @IdEvento
                  AND e.UbicacionPais = @PaisSede
                GROUP BY ev.IDEvento;
                """;
            int? entradas = null;
            await using (var cmd = new MySqlCommand(validateSql, connection, transaction))
            {
                cmd.Parameters.Add("@IdEvento", MySqlDbType.UInt64).Value = command.IdEvento;
                cmd.Parameters.Add("@PaisSede", MySqlDbType.VarChar, 50).Value = paisSede;
                var value = await cmd.ExecuteScalarAsync(cancellationToken);
                if (value is not null && value != DBNull.Value)
                {
                    entradas = Convert.ToInt32(value);
                }
            }

            if (!entradas.HasValue)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return false;
            }

            if (entradas.Value > 0)
            {
                throw new DatabaseException(
                    "Este evento ya tiene entradas emitidas. Para preservar la consistencia de las entradas, solamente puede modificarse su estado.",
                    new InvalidOperationException("Intento de edición estructural con entradas emitidas."));
            }

            const string updateEvento = """
                UPDATE Evento ev
                INNER JOIN Estadio eActual ON eActual.IDEstadio = ev.IDEstadio
                INNER JOIN Estadio eNuevo ON eNuevo.IDEstadio = @IdEstadio
                SET ev.EstadoEvento = @Estado,
                    ev.FechaHora = @FechaHora,
                    ev.IDEstadio = @IdEstadio
                WHERE ev.IDEvento = @IdEvento
                  AND eActual.UbicacionPais = @PaisSede
                  AND eNuevo.UbicacionPais = @PaisSede;
                """;
            await using (var cmd = new MySqlCommand(updateEvento, connection, transaction))
            {
                cmd.Parameters.Add("@Estado", MySqlDbType.VarChar, 20).Value = command.EstadoEvento;
                cmd.Parameters.Add("@FechaHora", MySqlDbType.DateTime).Value = command.Fecha.ToDateTime(command.Hora);
                cmd.Parameters.Add("@IdEstadio", MySqlDbType.UInt64).Value = command.IdEstadio;
                cmd.Parameters.Add("@IdEvento", MySqlDbType.UInt64).Value = command.IdEvento;
                cmd.Parameters.Add("@PaisSede", MySqlDbType.VarChar, 50).Value = paisSede;
                if (await cmd.ExecuteNonQueryAsync(cancellationToken) != 1)
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                    return false;
                }
            }

            await ExecuteInTransactionAsync(connection, transaction, "DELETE FROM EventoLocal WHERE IDEvento = @IdEvento;", command.IdEvento, cancellationToken);
            await ExecuteInTransactionAsync(connection, transaction, "DELETE FROM EventoVisita WHERE IDEvento = @IdEvento;", command.IdEvento, cancellationToken);
            await InsertEventoEquipoAsync(connection, transaction, "EventoLocal", command.IdEvento, command.IdEquipoLocal, cancellationToken);
            await InsertEventoEquipoAsync(connection, transaction, "EventoVisita", command.IdEvento, command.IdEquipoVisitante, cancellationToken);

            var nuevosSectores = command.Sectores.Select(sector => sector.IdSector).ToArray();
            await using (var deleteCmd = new MySqlCommand("""
                DELETE FROM EventoSector
                WHERE IDEvento = @IdEvento
                  AND IDSector NOT IN (
                      SELECT IDSector FROM (
                          SELECT @Keep0 AS IDSector
                      ) keep0
                  );
                """, connection, transaction))
            {
                deleteCmd.Parameters.Add("@IdEvento", MySqlDbType.UInt64).Value = command.IdEvento;
                deleteCmd.Parameters.Add("@Keep0", MySqlDbType.UInt64).Value = 0;
                if (nuevosSectores.Length > 0)
                {
                    deleteCmd.CommandText = $"DELETE FROM EventoSector WHERE IDEvento = @IdEvento AND IDSector NOT IN ({string.Join(", ", nuevosSectores.Select((_, i) => $"@Sector{i}"))});";
                    for (var i = 0; i < nuevosSectores.Length; i++)
                    {
                        deleteCmd.Parameters.Add($"@Sector{i}", MySqlDbType.UInt64).Value = nuevosSectores[i];
                    }
                }
                await deleteCmd.ExecuteNonQueryAsync(cancellationToken);
            }

            foreach (var sector in command.Sectores)
            {
                const string upsertSector = """
                    INSERT INTO EventoSector (IDEvento, IDSector, PrecioBase)
                    VALUES (@IdEvento, @IdSector, @PrecioBase)
                    ON DUPLICATE KEY UPDATE PrecioBase = VALUES(PrecioBase);
                    """;
                await using var cmd = new MySqlCommand(upsertSector, connection, transaction);
                cmd.Parameters.Add("@IdEvento", MySqlDbType.UInt64).Value = command.IdEvento;
                cmd.Parameters.Add("@IdSector", MySqlDbType.UInt64).Value = sector.IdSector;
                cmd.Parameters.Add("@PrecioBase", MySqlDbType.Decimal).Value = sector.PrecioBase;
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch (DatabaseException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
        catch (MySqlException ex)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw exceptionTranslator.Translate(ex, "editar evento");
        }
    }

    public async Task<EventoCambioEstadoResultadoDto?> CambiarEstadoEventoAsync(ulong idEvento, string estado, string paisSede, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            var estadoAnterior = await ObtenerEstadoEventoParaActualizarAsync(connection, transaction, idEvento, paisSede, cancellationToken);
            if (estadoAnterior is null)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return null;
            }

            var rechazo = EstadoEventoTransitions.ObtenerMotivoRechazo(estadoAnterior, estado);
            if (rechazo is not null)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return null;
            }

            var entradasValidadas = await ContarEntradasAsync(connection, transaction, idEvento, EstadoEntrada.Validada, cancellationToken);

            await using (var updateEvento = new MySqlCommand("""
                UPDATE Evento
                SET EstadoEvento = @Estado
                WHERE IDEvento = @IdEvento;
                """, connection, transaction))
            {
                updateEvento.Parameters.Add("@Estado", MySqlDbType.VarChar, 20).Value = estado;
                updateEvento.Parameters.Add("@IdEvento", MySqlDbType.UInt64).Value = idEvento;
                await updateEvento.ExecuteNonQueryAsync(cancellationToken);
            }

            var entradasAnuladas = 0;
            var transferenciasCanceladas = 0;
            if (EstadoEventoTransitions.EsCerrado(estado))
            {
                await using (var updateEntradas = new MySqlCommand("""
                    UPDATE Entrada
                    SET EstadoEntrada = @Anulada
                    WHERE IDEvento = @IdEvento
                      AND EstadoEntrada IN (@Activa, @Transferida);
                    """, connection, transaction))
                {
                    updateEntradas.Parameters.Add("@Anulada", MySqlDbType.VarChar, 20).Value = EstadoEntrada.Anulada;
                    updateEntradas.Parameters.Add("@Activa", MySqlDbType.VarChar, 20).Value = EstadoEntrada.Activa;
                    updateEntradas.Parameters.Add("@Transferida", MySqlDbType.VarChar, 20).Value = EstadoEntrada.Transferida;
                    updateEntradas.Parameters.Add("@IdEvento", MySqlDbType.UInt64).Value = idEvento;
                    entradasAnuladas = await updateEntradas.ExecuteNonQueryAsync(cancellationToken);
                }

                await using (var updateTransferencias = new MySqlCommand("""
                    UPDATE Transferencia t
                    INNER JOIN Entrada en ON en.IDEntrada = t.IDEntrada
                    SET t.Estado = @Cancelada,
                        t.FechaRespuesta = CURRENT_TIMESTAMP
                    WHERE en.IDEvento = @IdEvento
                      AND t.Estado = @Pendiente;
                    """, connection, transaction))
                {
                    updateTransferencias.Parameters.Add("@Cancelada", MySqlDbType.VarChar, 20).Value = EstadoTransferencia.Cancelada;
                    updateTransferencias.Parameters.Add("@Pendiente", MySqlDbType.VarChar, 20).Value = EstadoTransferencia.Pendiente;
                    updateTransferencias.Parameters.Add("@IdEvento", MySqlDbType.UInt64).Value = idEvento;
                    transferenciasCanceladas = await updateTransferencias.ExecuteNonQueryAsync(cancellationToken);
                }
            }

            await transaction.CommitAsync(cancellationToken);
            return new EventoCambioEstadoResultadoDto
            {
                EstadoAnterior = estadoAnterior,
                EstadoNuevo = estado,
                EntradasValidadas = entradasValidadas,
                EntradasAnuladas = entradasAnuladas,
                TransferenciasPendientesCanceladas = transferenciasCanceladas
            };
        }
        catch (MySqlException ex)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw exceptionTranslator.Translate(ex, "cambiar estado de evento");
        }
    }

    private static async Task<string?> ObtenerEstadoEventoParaActualizarAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        ulong idEvento,
        string paisSede,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand("""
            SELECT ev.EstadoEvento
            FROM Evento ev
            INNER JOIN Estadio est ON est.IDEstadio = ev.IDEstadio
            WHERE ev.IDEvento = @IdEvento
              AND est.UbicacionPais = @PaisSede
            FOR UPDATE;
            """, connection, transaction);
        command.Parameters.Add("@IdEvento", MySqlDbType.UInt64).Value = idEvento;
        command.Parameters.Add("@PaisSede", MySqlDbType.VarChar, 50).Value = paisSede;
        return Convert.ToString(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static async Task<int> ContarEntradasAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        ulong idEvento,
        string estado,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand("""
            SELECT COUNT(*)
            FROM Entrada
            WHERE IDEvento = @IdEvento
              AND EstadoEntrada = @Estado;
            """, connection, transaction);
        command.Parameters.Add("@IdEvento", MySqlDbType.UInt64).Value = idEvento;
        command.Parameters.Add("@Estado", MySqlDbType.VarChar, 20).Value = estado;
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
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

    private async Task<IReadOnlyList<T>> QueryListAsync<T>(string sql, Action<MySqlCommand> addParameters, Func<MySqlDataReader, T> map, string operation, CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await connectionFactory.OpenAsync(cancellationToken);
            await using var command = new MySqlCommand(sql, connection);
            addParameters(command);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var items = new List<T>();
            while (await reader.ReadAsync(cancellationToken))
            {
                items.Add(map(reader));
            }

            return items;
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

    private static async Task ExecuteInTransactionAsync(MySqlConnection connection, MySqlTransaction transaction, string sql, ulong idEvento, CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(sql, connection, transaction);
        command.Parameters.Add("@IdEvento", MySqlDbType.UInt64).Value = idEvento;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<EventoAdminDto>> ListarProximosEventosAsync(MySqlConnection connection, string paisSede, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT v.IDEvento, v.FechaHora, v.EstadoEvento, v.IDEstadio, v.Estadio, v.PaisEstadio,
                   v.IDEquipoLocal, v.EquipoLocal, v.IDEquipoVisitante, v.EquipoVisitante,
                   (SELECT COUNT(*) FROM Entrada en WHERE en.IDEvento = v.IDEvento) AS EntradasEmitidas
            FROM V_Eventos v
            WHERE v.PaisEstadio = @PaisSede AND v.FechaHora >= CURRENT_TIMESTAMP
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
        EquipoVisitante = reader.GetNullableString("EquipoVisitante"),
        EntradasEmitidas = Convert.ToInt32(reader.GetValue(reader.GetOrdinal("EntradasEmitidas")))
    };

    private static EventoSectorAdminDto MapEventoSector(MySqlDataReader reader) => new()
    {
        IdSector = reader.GetUInt64Value("IDSector"),
        NombreSector = reader.GetRequiredString("NombreSector"),
        Capacidad = reader.GetUInt32Value("Capacidad"),
        PrecioBase = reader.GetDecimalValue("PrecioBase")
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
