using System.Text;
using MySqlConnector;
using TicketingMundial.Application.Abstractions.Repositories;
using TicketingMundial.Application.DTOs;
using TicketingMundial.Application.Security;
using TicketingMundial.Infrastructure.Data;
using TicketingMundial.Infrastructure.Errors;

namespace TicketingMundial.Infrastructure.Repositories;

public sealed class EventoRepository(
    IDbConnectionFactory connectionFactory,
    IMySqlExceptionTranslator exceptionTranslator) : IEventoRepository
{
    public async Task<IReadOnlyList<EventoResumenDto>> ObtenerEventosAsync(
        EventoFiltroDto filtro,
        CancellationToken cancellationToken)
    {
        var orderBy = filtro.Sort switch
        {
            "local" => "EquipoLocal",
            "visitante" => "EquipoVisitante",
            "estadio" => "Estadio",
            "estado" => "EstadoEvento",
            "disponibilidad" => "LugaresDisponibles",
            _ => "FechaHora"
        };
        var direction = filtro.Direction == "asc" ? "ASC" : "DESC";
        var sql = new StringBuilder("""
            SELECT
                IDEvento,
                FechaHora,
                EstadoEvento,
                IDEstadio,
                Estadio,
                PaisEstadio,
                LocalidadEstadio,
                EquipoLocal,
                EquipoVisitante,
                GrupoLocal,
                GrupoVisitante,
                COALESCE((
                    SELECT SUM(ds.LugaresDisponibles)
                    FROM V_DisponibilidadSectores ds
                    WHERE ds.IDEvento = V_Eventos.IDEvento
                ), 0) AS LugaresDisponibles
            FROM V_Eventos
            WHERE 1 = 1
            """);

        var parameters = new List<MySqlParameter>();
        EnsureSqlLineBreak(sql);

        if (filtro.Desde.HasValue)
        {
            sql.AppendLine("AND FechaHora >= @Desde");
            parameters.Add(new MySqlParameter("@Desde", MySqlDbType.DateTime) { Value = filtro.Desde.Value });
        }

        if (filtro.Hasta.HasValue)
        {
            sql.AppendLine("AND FechaHora <= @Hasta");
            parameters.Add(new MySqlParameter("@Hasta", MySqlDbType.DateTime) { Value = NormalizeInclusiveHasta(filtro.Hasta.Value) });
        }

        if (filtro.IdEstadio.HasValue)
        {
            sql.AppendLine("AND IDEstadio = @IdEstadio");
            parameters.Add(new MySqlParameter("@IdEstadio", MySqlDbType.UInt64) { Value = filtro.IdEstadio.Value });
        }

        if (!string.IsNullOrWhiteSpace(filtro.Equipo))
        {
            sql.AppendLine("AND (EquipoLocal LIKE @Equipo ESCAPE '\\\\' OR EquipoVisitante LIKE @Equipo ESCAPE '\\\\')");
            parameters.Add(new MySqlParameter("@Equipo", MySqlDbType.VarChar, 54)
            {
                Value = $"%{SqlSafety.EscapeLikePattern(filtro.Equipo.Trim())}%"
            });
        }

        if (!string.IsNullOrWhiteSpace(filtro.Estado))
        {
            sql.AppendLine("AND EstadoEvento = @Estado");
            parameters.Add(new MySqlParameter("@Estado", MySqlDbType.VarChar, 20) { Value = filtro.Estado.Trim().ToUpperInvariant() });
        }

        if (filtro.IdSector.HasValue)
        {
            sql.AppendLine("AND EXISTS (SELECT 1 FROM EventoSector es WHERE es.IDEvento = V_Eventos.IDEvento AND es.IDSector = @IdSector)");
            parameters.Add(new MySqlParameter("@IdSector", MySqlDbType.UInt64) { Value = filtro.IdSector.Value });
        }

        sql.AppendLine();
        sql.AppendLine($"ORDER BY {orderBy} {direction}, IDEvento {direction};");

        try
        {
            await using var connection = await connectionFactory.OpenAsync(cancellationToken);
            await using var command = new MySqlCommand(sql.ToString(), connection);
            command.Parameters.AddRange(parameters.ToArray());

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var resultados = new List<EventoResumenDto>();

            while (await reader.ReadAsync(cancellationToken))
            {
                resultados.Add(MapEventoResumen(reader));
            }

            return resultados;
        }
        catch (MySqlException ex)
        {
            throw exceptionTranslator.Translate(ex, "obtener catalogo de eventos");
        }
    }

    public async Task<EventoResumenDto?> ObtenerEventoAsync(
        ulong idEvento,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                IDEvento,
                FechaHora,
                EstadoEvento,
                IDEstadio,
                Estadio,
                PaisEstadio,
                LocalidadEstadio,
                EquipoLocal,
                EquipoVisitante,
                GrupoLocal,
                GrupoVisitante,
                COALESCE((
                    SELECT SUM(ds.LugaresDisponibles)
                    FROM V_DisponibilidadSectores ds
                    WHERE ds.IDEvento = V_Eventos.IDEvento
                ), 0) AS LugaresDisponibles
            FROM V_Eventos
            WHERE IDEvento = @IdEvento;
            """;

        try
        {
            await using var connection = await connectionFactory.OpenAsync(cancellationToken);
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.Add("@IdEvento", MySqlDbType.UInt64).Value = idEvento;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            return await reader.ReadAsync(cancellationToken) ? MapEventoResumen(reader) : null;
        }
        catch (MySqlException ex)
        {
            throw exceptionTranslator.Translate(ex, "obtener detalle de evento");
        }
    }

    public async Task<IReadOnlyList<SectorDisponibilidadDto>> ObtenerDisponibilidadAsync(
        ulong idEvento,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                IDEvento,
                IDSector,
                NombreSector,
                Capacidad,
                PrecioBase,
                EntradasEmitidas,
                LugaresDisponibles
            FROM V_DisponibilidadSectores
            WHERE IDEvento = @IdEvento
            ORDER BY NombreSector;
            """;

        try
        {
            await using var connection = await connectionFactory.OpenAsync(cancellationToken);
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.Add("@IdEvento", MySqlDbType.UInt64).Value = idEvento;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var resultados = new List<SectorDisponibilidadDto>();

            while (await reader.ReadAsync(cancellationToken))
            {
                resultados.Add(new SectorDisponibilidadDto
                {
                    IdEvento = reader.GetUInt64Value("IDEvento"),
                    IdSector = reader.GetUInt64Value("IDSector"),
                    NombreSector = reader.GetRequiredString("NombreSector"),
                    Capacidad = reader.GetUInt32Value("Capacidad"),
                    PrecioBase = reader.GetDecimalValue("PrecioBase"),
                    EntradasEmitidas = reader.GetInt64Value("EntradasEmitidas"),
                    LugaresDisponibles = reader.GetInt64Value("LugaresDisponibles")
                });
            }

            return resultados;
        }
        catch (MySqlException ex)
        {
            throw exceptionTranslator.Translate(ex, "obtener disponibilidad de sectores");
        }
    }

    public async Task<IReadOnlyList<EstadioFiltroDto>> ObtenerEstadiosAsync(
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                IDEstadio,
                Nombre,
                UbicacionPais,
                UbicacionLocalidad
            FROM Estadio
            ORDER BY Nombre;
            """;

        try
        {
            await using var connection = await connectionFactory.OpenAsync(cancellationToken);
            await using var command = new MySqlCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var resultados = new List<EstadioFiltroDto>();

            while (await reader.ReadAsync(cancellationToken))
            {
                resultados.Add(new EstadioFiltroDto
                {
                    IdEstadio = reader.GetUInt64Value("IDEstadio"),
                    Nombre = reader.GetRequiredString("Nombre"),
                    Pais = reader.GetRequiredString("UbicacionPais"),
                    Localidad = reader.GetRequiredString("UbicacionLocalidad")
                });
            }

            return resultados;
        }
        catch (MySqlException ex)
        {
            throw exceptionTranslator.Translate(ex, "obtener estadios");
        }
    }

    private static EventoResumenDto MapEventoResumen(MySqlDataReader reader)
    {
        return new EventoResumenDto
        {
            IdEvento = reader.GetUInt64Value("IDEvento"),
            FechaHora = reader.GetDateTimeValue("FechaHora"),
            Estado = reader.GetRequiredString("EstadoEvento"),
            IdEstadio = reader.GetUInt64Value("IDEstadio"),
            Estadio = reader.GetRequiredString("Estadio"),
            PaisEstadio = reader.GetRequiredString("PaisEstadio"),
            LocalidadEstadio = reader.GetRequiredString("LocalidadEstadio"),
            EquipoLocal = reader.GetNullableString("EquipoLocal"),
            EquipoVisitante = reader.GetNullableString("EquipoVisitante"),
            GrupoLocal = reader.GetNullableString("GrupoLocal"),
            GrupoVisitante = reader.GetNullableString("GrupoVisitante"),
            LugaresDisponibles = Convert.ToInt64(reader.GetValue(reader.GetOrdinal("LugaresDisponibles")))
        };
    }

    private static DateTime NormalizeInclusiveHasta(DateTime hasta)
    {
        return hasta.TimeOfDay == TimeSpan.Zero
            ? hasta.Date.AddDays(1).AddTicks(-1)
            : hasta;
    }

    private static void EnsureSqlLineBreak(StringBuilder sql)
    {
        if (sql.Length > 0 && !char.IsWhiteSpace(sql[sql.Length - 1]))
        {
            sql.AppendLine();
        }
    }
}
