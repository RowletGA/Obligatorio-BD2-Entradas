using System.Text;
using MySqlConnector;
using TicketingMundial.Application.Abstractions.Repositories;
using TicketingMundial.Application.DTOs;
using TicketingMundial.Application.Security;
using TicketingMundial.Domain.Identity;
using TicketingMundial.Infrastructure.Data;
using TicketingMundial.Infrastructure.Errors;

namespace TicketingMundial.Infrastructure.Repositories;

public sealed class OperativaRepository(
    IDbConnectionFactory connectionFactory,
    IMySqlExceptionTranslator exceptionTranslator) : IOperativaRepository
{
    public async Task<CompraPreviewDto> ObtenerPreviewCompraAsync(ulong idEvento, IReadOnlyList<CompraSectorCantidad> cantidades, CancellationToken cancellationToken)
    {
        var evento = await ObtenerEventoCompraAsync(idEvento, cancellationToken);
        var disponibilidad = await ObtenerDisponibilidadAsync(idEvento, cancellationToken);
        var lineas = cantidades.Join(disponibilidad, c => c.IdSector, d => d.IdSector, (c, d) => new CompraLineaDto
        {
            IdSector = d.IdSector,
            Sector = d.NombreSector,
            Cantidad = c.Cantidad,
            PrecioUnitario = d.PrecioBase,
            Disponibles = d.LugaresDisponibles
        }).ToArray();

        return new CompraPreviewDto
        {
            IdEvento = evento.IdEvento,
            Evento = $"{evento.EquipoLocal} vs {evento.EquipoVisitante}",
            FechaHora = evento.FechaHora,
            Estadio = evento.Estadio,
            Lineas = lineas
        };
    }

    public async Task<CompraResultadoDto> ComprarAsync(DocumentoUsuario comprador, ulong idEvento, IReadOnlyList<CompraSectorCantidad> cantidades, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            if (!await ExisteUsuarioGeneralAsync(connection, transaction, comprador, cancellationToken))
            {
                throw new DatabaseException("El comprador autenticado no es usuario general.", new InvalidOperationException("UsuarioGeneral no encontrado."));
            }

            const string eventSql = "SELECT EstadoEvento FROM Evento WHERE IDEvento = @IdEvento FOR UPDATE;";
            await using (var cmd = new MySqlCommand(eventSql, connection, transaction))
            {
                cmd.Parameters.Add("@IdEvento", MySqlDbType.UInt64).Value = idEvento;
                var estado = Convert.ToString(await cmd.ExecuteScalarAsync(cancellationToken));
                if (!string.Equals(estado, "PROGRAMADO", StringComparison.Ordinal))
                {
                    throw new DatabaseException("Solamente se pueden comprar entradas de eventos programados.", new InvalidOperationException("Evento no programado."));
                }
            }

            var precios = await LockPreciosAsync(connection, transaction, idEvento, cantidades.Select(c => c.IdSector).ToArray(), cancellationToken);
            foreach (var cantidad in cantidades)
            {
                if (!precios.TryGetValue(cantidad.IdSector, out var precio))
                {
                    throw new DatabaseException("Uno de los sectores seleccionados no está habilitado para el evento.", new InvalidOperationException("Sector no habilitado."));
                }

                var disponibles = await ObtenerDisponiblesTransaccionAsync(connection, transaction, idEvento, cantidad.IdSector, cancellationToken);
                if (disponibles < cantidad.Cantidad)
                {
                    throw new DatabaseException("No hay disponibilidad suficiente para completar la compra.", new InvalidOperationException("Disponibilidad insuficiente."));
                }
            }

            var idVenta = await InsertVentaAsync(connection, transaction, comprador, cancellationToken);
            foreach (var cantidad in cantidades)
            {
                for (var i = 0; i < cantidad.Cantidad; i++)
                {
                    await InsertEntradaAsync(connection, transaction, idVenta, idEvento, cantidad.IdSector, precios[cantidad.IdSector], cancellationToken);
                }
            }

            await using (var cmd = new MySqlCommand("UPDATE Venta SET EstadoVenta = 'confirmada' WHERE IDVenta = @IdVenta;", connection, transaction))
            {
                cmd.Parameters.Add("@IdVenta", MySqlDbType.UInt64).Value = idVenta;
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            decimal monto;
            await using (var cmd = new MySqlCommand("SELECT MontoTotal FROM Venta WHERE IDVenta = @IdVenta;", connection, transaction))
            {
                cmd.Parameters.Add("@IdVenta", MySqlDbType.UInt64).Value = idVenta;
                monto = Convert.ToDecimal(await cmd.ExecuteScalarAsync(cancellationToken));
            }

            await transaction.CommitAsync(cancellationToken);
            return new CompraResultadoDto { IdVenta = idVenta, MontoTotal = monto };
        }
        catch (DatabaseException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
        catch (MySqlException ex)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw exceptionTranslator.Translate(ex, "comprar entradas");
        }
    }

    public async Task<IReadOnlyList<CompraResumenDto>> ListarComprasAsync(DocumentoUsuario comprador, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT v.IDVenta, v.FechaVenta, v.EstadoVenta, v.MontoTotal, v.PorcentajeComision,
                   COUNT(en.IDEntrada) AS CantidadEntradas,
                   GROUP_CONCAT(DISTINCT CONCAT(COALESCE(eloc.Pais, 'Local'), ' vs ', COALESCE(evis.Pais, 'Visitante')) SEPARATOR ', ') AS Eventos
            FROM Venta v
            INNER JOIN Entrada en ON en.IDVenta = v.IDVenta
            INNER JOIN Evento ev ON ev.IDEvento = en.IDEvento
            LEFT JOIN EventoLocal l ON l.IDEvento = ev.IDEvento
            LEFT JOIN Equipo eloc ON eloc.IDEquipo = l.IDEquipo
            LEFT JOIN EventoVisita vi ON vi.IDEvento = ev.IDEvento
            LEFT JOIN Equipo evis ON evis.IDEquipo = vi.IDEquipo
            WHERE v.TipoDocUsuario = @TipoDocumento AND v.PaisDocUsuario = @PaisDocumento AND v.NumeroUsuario = @Numero
            GROUP BY v.IDVenta, v.FechaVenta, v.EstadoVenta, v.MontoTotal, v.PorcentajeComision
            ORDER BY v.FechaVenta DESC;
            """;
        return await QueryListAsync(sql, cmd => AddDocumento(cmd, comprador), MapCompraResumen, "listar compras", cancellationToken);
    }

    public async Task<CompraDetalleDto?> ObtenerCompraAsync(DocumentoUsuario comprador, ulong idVenta, CancellationToken cancellationToken)
    {
        var compras = await ListarComprasAsync(comprador, cancellationToken);
        var venta = compras.FirstOrDefault(v => v.IdVenta == idVenta);
        if (venta is null)
        {
            return null;
        }

        const string sql = """
            SELECT en.IDEntrada, ev.IDEvento, ev.FechaHora AS FechaEvento,
                   CONCAT(COALESCE(eloc.Pais, 'Local'), ' vs ', COALESCE(evis.Pais, 'Visitante')) AS Evento,
                   est.Nombre AS Estadio, s.IDSector, s.NombreSector AS Sector,
                   en.EstadoEntrada, en.Costo,
                   (SELECT COUNT(*) FROM Transferencia t WHERE t.IDEntrada = en.IDEntrada AND t.Estado = 'ACEPTADA') AS TransferenciasAceptadas
            FROM Entrada en
            INNER JOIN Venta v ON v.IDVenta = en.IDVenta
            INNER JOIN Evento ev ON ev.IDEvento = en.IDEvento
            INNER JOIN Estadio est ON est.IDEstadio = ev.IDEstadio
            INNER JOIN Sector s ON s.IDSector = en.IDSector
            LEFT JOIN EventoLocal l ON l.IDEvento = ev.IDEvento
            LEFT JOIN Equipo eloc ON eloc.IDEquipo = l.IDEquipo
            LEFT JOIN EventoVisita vi ON vi.IDEvento = ev.IDEvento
            LEFT JOIN Equipo evis ON evis.IDEquipo = vi.IDEquipo
            WHERE v.IDVenta = @IdVenta AND v.TipoDocUsuario = @TipoDocumento AND v.PaisDocUsuario = @PaisDocumento AND v.NumeroUsuario = @Numero
            ORDER BY en.IDEntrada;
            """;
        var entradas = await QueryListAsync(sql, cmd =>
        {
            cmd.Parameters.Add("@IdVenta", MySqlDbType.UInt64).Value = idVenta;
            AddDocumento(cmd, comprador);
        }, MapEntradaResumen, "obtener compra", cancellationToken);
        return new CompraDetalleDto { Venta = venta, Entradas = entradas };
    }

    public Task<IReadOnlyList<EntradaResumenDto>> ListarEntradasPropiasAsync(DocumentoUsuario propietario, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT en.IDEntrada, ev.IDEvento, ev.FechaHora AS FechaEvento,
                   CONCAT(COALESCE(eloc.Pais, 'Local'), ' vs ', COALESCE(evis.Pais, 'Visitante')) AS Evento,
                   est.Nombre AS Estadio, s.IDSector, s.NombreSector AS Sector,
                   en.EstadoEntrada, en.Costo,
                   (SELECT COUNT(*) FROM Transferencia t WHERE t.IDEntrada = en.IDEntrada AND t.Estado = 'ACEPTADA') AS TransferenciasAceptadas
            FROM V_PropietarioActual p
            INNER JOIN Entrada en ON en.IDEntrada = p.IDEntrada
            INNER JOIN Evento ev ON ev.IDEvento = en.IDEvento
            INNER JOIN Estadio est ON est.IDEstadio = ev.IDEstadio
            INNER JOIN Sector s ON s.IDSector = en.IDSector
            LEFT JOIN EventoLocal l ON l.IDEvento = ev.IDEvento
            LEFT JOIN Equipo eloc ON eloc.IDEquipo = l.IDEquipo
            LEFT JOIN EventoVisita vi ON vi.IDEvento = ev.IDEvento
            LEFT JOIN Equipo evis ON evis.IDEquipo = vi.IDEquipo
            WHERE p.TipoDocumento = @TipoDocumento AND p.PaisDocumento = @PaisDocumento AND p.Numero = @Numero
            ORDER BY ev.FechaHora DESC, en.IDEntrada;
            """;
        return QueryListAsync(sql, cmd => AddDocumento(cmd, propietario), MapEntradaResumen, "listar entradas propias", cancellationToken);
    }

    public async Task<EntradaResumenDto?> ObtenerEntradaPropiaAsync(DocumentoUsuario propietario, ulong idEntrada, CancellationToken cancellationToken) =>
        (await ListarEntradasPropiasAsync(propietario, cancellationToken)).FirstOrDefault(e => e.IdEntrada == idEntrada);

    public Task<UsuarioDestinoDto?> BuscarUsuarioGeneralPorCorreoAsync(string correo, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT u.TipoDocumento, u.PaisDocumento, u.Numero, u.CorreoElectronico,
                   CONCAT_WS(' ', u.PrimerNombre, u.PrimerApellido) AS Nombre
            FROM UsuarioGeneral ug
            INNER JOIN Usuario u ON u.TipoDocumento = ug.TipoDocumento AND u.PaisDocumento = ug.PaisDocumento AND u.Numero = ug.Numero
            WHERE u.CorreoElectronico = @Correo;
            """;
        return QuerySingleAsync(sql, cmd => cmd.Parameters.Add("@Correo", MySqlDbType.VarChar, 254).Value = correo, reader => new UsuarioDestinoDto
        {
            Documento = new DocumentoUsuario(reader.GetRequiredString("TipoDocumento"), reader.GetRequiredString("PaisDocumento"), reader.GetRequiredString("Numero")),
            Correo = reader.GetRequiredString("CorreoElectronico"),
            Nombre = reader.GetRequiredString("Nombre")
        }, "buscar usuario destino", cancellationToken);
    }

    public async Task<ulong> CrearTransferenciaAsync(DocumentoUsuario otorga, ulong idEntrada, string correoDestino, CancellationToken cancellationToken)
    {
        var destino = await BuscarUsuarioGeneralPorCorreoAsync(correoDestino, cancellationToken);
        if (destino is null)
        {
            throw new DatabaseException("No se encontró un usuario general con ese correo.", new InvalidOperationException("Destino no existe."));
        }

        const string sql = """
            INSERT INTO Transferencia (TipoDocOtorga, PaisDocOtorga, NumeroOtorga, TipoDocRecibe, PaisDocRecibe, NumeroRecibe, IDEntrada)
            VALUES (@TipoDocumento, @PaisDocumento, @Numero, @TipoRecibe, @PaisRecibe, @NumeroRecibe, @IdEntrada);
            SELECT LAST_INSERT_ID();
            """;
        return await ExecuteScalarUInt64Async(sql, cmd =>
        {
            AddDocumento(cmd, otorga);
            cmd.Parameters.Add("@TipoRecibe", MySqlDbType.VarChar, 20).Value = destino.Documento.TipoDocumento;
            cmd.Parameters.Add("@PaisRecibe", MySqlDbType.VarChar, 50).Value = destino.Documento.PaisDocumento;
            cmd.Parameters.Add("@NumeroRecibe", MySqlDbType.VarChar, 30).Value = destino.Documento.NumeroDocumento;
            cmd.Parameters.Add("@IdEntrada", MySqlDbType.UInt64).Value = idEntrada;
        }, "crear transferencia", cancellationToken);
    }

    public Task<IReadOnlyList<TransferenciaDto>> ListarTransferenciasEnviadasAsync(DocumentoUsuario usuario, CancellationToken cancellationToken) =>
        ListarTransferenciasAsync(usuario, true, cancellationToken);

    public Task<IReadOnlyList<TransferenciaDto>> ListarTransferenciasRecibidasAsync(DocumentoUsuario usuario, CancellationToken cancellationToken) =>
        ListarTransferenciasAsync(usuario, false, cancellationToken);

    public async Task<bool> ResponderTransferenciaAsync(DocumentoUsuario usuario, ulong idTransferencia, string estado, bool receptor, CancellationToken cancellationToken)
    {
        var columnPrefix = receptor ? "Recibe" : "Otorga";
        const string baseSql = """
            UPDATE Transferencia
            SET Estado = @Estado, FechaRespuesta = CURRENT_TIMESTAMP
            WHERE IDTransferencia = @IdTransferencia
              AND Estado = 'PENDIENTE'
            """;
        var sql = baseSql + $" AND TipoDoc{columnPrefix} = @TipoDocumento AND PaisDoc{columnPrefix} = @PaisDocumento AND Numero{columnPrefix} = @Numero;";
        return await ExecuteNonQueryAsync(sql, cmd =>
        {
            cmd.Parameters.Add("@Estado", MySqlDbType.VarChar, 20).Value = estado;
            cmd.Parameters.Add("@IdTransferencia", MySqlDbType.UInt64).Value = idTransferencia;
            AddDocumento(cmd, usuario);
        }, "responder transferencia", cancellationToken) > 0;
    }

    public Task<IReadOnlyList<FuncionarioDto>> ListarFuncionariosAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT f.TipoDocumento, f.PaisDocumento, f.Numero, f.NumLegajo, u.CorreoElectronico,
                   CONCAT_WS(' ', u.PrimerNombre, u.PrimerApellido) AS Nombre
            FROM Funcionario f
            INNER JOIN Usuario u ON u.TipoDocumento = f.TipoDocumento AND u.PaisDocumento = f.PaisDocumento AND u.Numero = f.Numero
            ORDER BY Nombre;
            """;
        return QueryListAsync(sql, _ => { }, MapFuncionario, "listar funcionarios", cancellationToken);
    }

    public Task<IReadOnlyList<AsignacionFuncionarioDto>> ListarAsignacionesAsync(string paisSede, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT v.TipoDocumento, v.PaisDocumento, v.Numero, v.Funcionario, v.NumLegajo,
                   v.IDEvento, v.FechaEvento, v.IDSector, v.NombreSector, ev.Estadio
            FROM V_ValidacionesPorFuncionario v
            INNER JOIN V_Eventos ev ON ev.IDEvento = v.IDEvento
            WHERE ev.PaisEstadio = @PaisSede
            ORDER BY v.FechaEvento DESC;
            """;
        return QueryListAsync(sql, cmd => cmd.Parameters.Add("@PaisSede", MySqlDbType.VarChar, 50).Value = paisSede, MapAsignacion, "listar asignaciones", cancellationToken);
    }

    public async Task AsignarFuncionarioAsync(DocumentoUsuario funcionario, ulong idEvento, ulong idSector, string paisSede, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO FuncionarioEventoSector (TipoDocumento, PaisDocumento, Numero, IDEvento, IDSector)
            SELECT @TipoDocumento, @PaisDocumento, @Numero, es.IDEvento, es.IDSector
            FROM EventoSector es
            INNER JOIN Evento ev ON ev.IDEvento = es.IDEvento
            INNER JOIN Estadio est ON est.IDEstadio = ev.IDEstadio
            WHERE es.IDEvento = @IdEvento AND es.IDSector = @IdSector AND est.UbicacionPais = @PaisSede;
            """;
        var rows = await ExecuteNonQueryAsync(sql, cmd =>
        {
            AddDocumento(cmd, funcionario);
            cmd.Parameters.Add("@IdEvento", MySqlDbType.UInt64).Value = idEvento;
            cmd.Parameters.Add("@IdSector", MySqlDbType.UInt64).Value = idSector;
            cmd.Parameters.Add("@PaisSede", MySqlDbType.VarChar, 50).Value = paisSede;
        }, "asignar funcionario", cancellationToken);
        if (rows == 0)
        {
            throw new DatabaseException("No se pudo asignar: el evento o sector no pertenecen a la jurisdicción.", new InvalidOperationException("Asignación fuera de jurisdicción."));
        }
    }

    public Task<IReadOnlyList<AsignacionFuncionarioDto>> ListarAsignacionesFuncionarioAsync(DocumentoUsuario funcionario, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT v.TipoDocumento, v.PaisDocumento, v.Numero, v.Funcionario, v.NumLegajo,
                   v.IDEvento, v.FechaEvento, v.IDSector, v.NombreSector, ev.Estadio
            FROM V_ValidacionesPorFuncionario v
            INNER JOIN V_Eventos ev ON ev.IDEvento = v.IDEvento
            WHERE v.TipoDocumento = @TipoDocumento AND v.PaisDocumento = @PaisDocumento AND v.Numero = @Numero
            ORDER BY v.FechaEvento DESC;
            """;
        return QueryListAsync(sql, cmd => AddDocumento(cmd, funcionario), MapAsignacion, "listar asignaciones de funcionario", cancellationToken);
    }

    public async Task<ValidacionEntradaDto> ValidarEntradaAsync(DocumentoUsuario funcionario, ulong idEntrada, string token, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await using (var cmd = new MySqlCommand("""
                INSERT INTO Validacion (IDEntrada, TipoDocFuncionario, PaisDocFuncionario, NumeroFuncionario, TokenValidado)
                VALUES (@IdEntrada, @TipoDocumento, @PaisDocumento, @Numero, @Token);
                """, connection, transaction))
            {
                cmd.Parameters.Add("@IdEntrada", MySqlDbType.UInt64).Value = idEntrada;
                AddDocumento(cmd, funcionario);
                cmd.Parameters.Add("@Token", MySqlDbType.VarChar, 255).Value = token;
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            var result = await ObtenerValidacionEntradaAsync(connection, transaction, idEntrada, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch (MySqlException ex)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw exceptionTranslator.Translate(ex, "validar entrada");
        }
    }

    public Task<IReadOnlyList<ReporteEventoVendidoDto>> ReporteEventosVendidosAsync(DateTime? desde, DateTime? hasta, int limite, CancellationToken cancellationToken)
    {
        var sql = new StringBuilder("""
            SELECT ev.IDEvento, ev.FechaHora, est.Nombre AS Estadio,
                   CONCAT(COALESCE(eloc.Pais, 'Local'), ' vs ', COALESCE(evis.Pais, 'Visitante')) AS Evento,
                   COUNT(en.IDEntrada) AS EntradasVendidas
            FROM Evento ev
            INNER JOIN Estadio est ON est.IDEstadio = ev.IDEstadio
            LEFT JOIN Entrada en ON en.IDEvento = ev.IDEvento AND en.EstadoEntrada <> 'ANULADA'
            LEFT JOIN EventoLocal l ON l.IDEvento = ev.IDEvento
            LEFT JOIN Equipo eloc ON eloc.IDEquipo = l.IDEquipo
            LEFT JOIN EventoVisita vi ON vi.IDEvento = ev.IDEvento
            LEFT JOIN Equipo evis ON evis.IDEquipo = vi.IDEquipo
            WHERE 1 = 1
            """);
        var parameters = AddDateFilters(sql, desde, hasta);
        sql.AppendLine();
        sql.AppendLine("""
            GROUP BY ev.IDEvento, ev.FechaHora, est.Nombre, Evento
            ORDER BY EntradasVendidas DESC
            LIMIT @Limite;
            """);
        return QueryListAsync(sql.ToString(), cmd => { AddParams(cmd, parameters); cmd.Parameters.Add("@Limite", MySqlDbType.Int32).Value = limite; }, r => new ReporteEventoVendidoDto
        {
            IdEvento = r.GetUInt64Value("IDEvento"),
            FechaHora = r.GetDateTimeValue("FechaHora"),
            Estadio = r.GetRequiredString("Estadio"),
            Evento = r.GetRequiredString("Evento"),
            EntradasVendidas = Convert.ToInt32(r.GetValue(r.GetOrdinal("EntradasVendidas")))
        }, "reporte eventos vendidos", cancellationToken);
    }

    public Task<IReadOnlyList<ReporteCompradorDto>> ReporteCompradoresAsync(DateTime? desde, DateTime? hasta, int limite, CancellationToken cancellationToken)
    {
        var sql = new StringBuilder("""
            SELECT comprador.Comprador, comprador.CorreoElectronico,
                   SUM(comprador.EntradasVenta) AS EntradasCompradas,
                   SUM(comprador.MontoTotal) AS TotalGastado
            FROM (
                SELECT v.IDVenta,
                       CONCAT_WS(' ', u.PrimerNombre, u.PrimerApellido) AS Comprador,
                       u.CorreoElectronico,
                       COUNT(en.IDEntrada) AS EntradasVenta,
                       v.MontoTotal
                FROM Venta v
                INNER JOIN Usuario u ON u.TipoDocumento = v.TipoDocUsuario AND u.PaisDocumento = v.PaisDocUsuario AND u.Numero = v.NumeroUsuario
                INNER JOIN Entrada en ON en.IDVenta = v.IDVenta
                WHERE 1 = 1
            """);
        var parameters = AddDateFilters(sql, desde, hasta, "v.FechaVenta");
        sql.AppendLine();
        sql.AppendLine("""
                GROUP BY v.IDVenta, Comprador, u.CorreoElectronico, v.MontoTotal
            ) comprador
            GROUP BY comprador.Comprador, comprador.CorreoElectronico
            ORDER BY EntradasCompradas DESC, TotalGastado DESC
            LIMIT @Limite;
            """);
        return QueryListAsync(sql.ToString(), cmd => { AddParams(cmd, parameters); cmd.Parameters.Add("@Limite", MySqlDbType.Int32).Value = limite; }, r => new ReporteCompradorDto
        {
            Comprador = r.GetRequiredString("Comprador"),
            Correo = r.GetRequiredString("CorreoElectronico"),
            EntradasCompradas = Convert.ToInt32(r.GetValue(r.GetOrdinal("EntradasCompradas"))),
            TotalGastado = Convert.ToDecimal(r.GetValue(r.GetOrdinal("TotalGastado")))
        }, "reporte compradores", cancellationToken);
    }

    private async Task<EventoResumenDto> ObtenerEventoCompraAsync(ulong idEvento, CancellationToken ct)
    {
        const string sql = "SELECT IDEvento, FechaHora, EstadoEvento, IDEstadio, Estadio, PaisEstadio, LocalidadEstadio, EquipoLocal, EquipoVisitante FROM V_Eventos WHERE IDEvento = @IdEvento;";
        return await QuerySingleAsync(sql, cmd => cmd.Parameters.Add("@IdEvento", MySqlDbType.UInt64).Value = idEvento, r => new EventoResumenDto
        {
            IdEvento = r.GetUInt64Value("IDEvento"),
            FechaHora = r.GetDateTimeValue("FechaHora"),
            Estado = r.GetRequiredString("EstadoEvento"),
            IdEstadio = r.GetUInt64Value("IDEstadio"),
            Estadio = r.GetRequiredString("Estadio"),
            PaisEstadio = r.GetRequiredString("PaisEstadio"),
            LocalidadEstadio = r.GetRequiredString("LocalidadEstadio"),
            EquipoLocal = r.GetNullableString("EquipoLocal"),
            EquipoVisitante = r.GetNullableString("EquipoVisitante")
        }, "obtener evento para compra", ct) ?? throw new DatabaseException("El evento no existe.", new InvalidOperationException("Evento no encontrado."));
    }

    private async Task<IReadOnlyList<SectorDisponibilidadDto>> ObtenerDisponibilidadAsync(ulong idEvento, CancellationToken ct)
    {
        const string sql = "SELECT IDEvento, IDSector, NombreSector, Capacidad, PrecioBase, EntradasEmitidas, LugaresDisponibles FROM V_DisponibilidadSectores WHERE IDEvento = @IdEvento;";
        return await QueryListAsync(sql, cmd => cmd.Parameters.Add("@IdEvento", MySqlDbType.UInt64).Value = idEvento, r => new SectorDisponibilidadDto
        {
            IdEvento = r.GetUInt64Value("IDEvento"),
            IdSector = r.GetUInt64Value("IDSector"),
            NombreSector = r.GetRequiredString("NombreSector"),
            Capacidad = r.GetUInt32Value("Capacidad"),
            PrecioBase = r.GetDecimalValue("PrecioBase"),
            EntradasEmitidas = r.GetInt64Value("EntradasEmitidas"),
            LugaresDisponibles = r.GetInt64Value("LugaresDisponibles")
        }, "obtener disponibilidad para compra", ct);
    }

    private static async Task<bool> ExisteUsuarioGeneralAsync(MySqlConnection c, MySqlTransaction tx, DocumentoUsuario d, CancellationToken ct)
    {
        await using var cmd = new MySqlCommand("SELECT EXISTS (SELECT 1 FROM UsuarioGeneral WHERE TipoDocumento=@TipoDocumento AND PaisDocumento=@PaisDocumento AND Numero=@Numero);", c, tx);
        AddDocumento(cmd, d);
        return Convert.ToBoolean(await cmd.ExecuteScalarAsync(ct));
    }

    private static async Task<Dictionary<ulong, decimal>> LockPreciosAsync(MySqlConnection c, MySqlTransaction tx, ulong idEvento, IReadOnlyList<ulong> sectores, CancellationToken ct)
    {
        var result = new Dictionary<ulong, decimal>();
        foreach (var idSector in sectores)
        {
            await using var cmd = new MySqlCommand("SELECT PrecioBase FROM EventoSector WHERE IDEvento=@IdEvento AND IDSector=@IdSector FOR UPDATE;", c, tx);
            cmd.Parameters.Add("@IdEvento", MySqlDbType.UInt64).Value = idEvento;
            cmd.Parameters.Add("@IdSector", MySqlDbType.UInt64).Value = idSector;
            var value = await cmd.ExecuteScalarAsync(ct);
            if (value is not null)
            {
                result[idSector] = Convert.ToDecimal(value);
            }
        }
        return result;
    }

    private static async Task<long> ObtenerDisponiblesTransaccionAsync(MySqlConnection c, MySqlTransaction tx, ulong idEvento, ulong idSector, CancellationToken ct)
    {
        const string sql = """
            SELECT s.Capacidad - COUNT(en.IDEntrada)
            FROM Sector s
            LEFT JOIN Entrada en ON en.IDSector = s.IDSector AND en.IDEvento = @IdEvento AND en.EstadoEntrada <> 'ANULADA'
            WHERE s.IDSector = @IdSector
            GROUP BY s.Capacidad;
            """;
        await using var cmd = new MySqlCommand(sql, c, tx);
        cmd.Parameters.Add("@IdEvento", MySqlDbType.UInt64).Value = idEvento;
        cmd.Parameters.Add("@IdSector", MySqlDbType.UInt64).Value = idSector;
        return Convert.ToInt64(await cmd.ExecuteScalarAsync(ct));
    }

    private static async Task<ulong> InsertVentaAsync(MySqlConnection c, MySqlTransaction tx, DocumentoUsuario d, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO Venta (EstadoVenta, TipoDocUsuario, PaisDocUsuario, NumeroUsuario)
            VALUES ('pendiente', @TipoDocumento, @PaisDocumento, @Numero);
            SELECT LAST_INSERT_ID();
            """;
        await using var cmd = new MySqlCommand(sql, c, tx);
        AddDocumento(cmd, d);
        return Convert.ToUInt64(await cmd.ExecuteScalarAsync(ct));
    }

    private static async Task InsertEntradaAsync(MySqlConnection c, MySqlTransaction tx, ulong idVenta, ulong idEvento, ulong idSector, decimal costo, CancellationToken ct)
    {
        await using var cmd = new MySqlCommand("INSERT INTO Entrada (Costo, IDVenta, IDEvento, IDSector) VALUES (@Costo, @IdVenta, @IdEvento, @IdSector);", c, tx);
        cmd.Parameters.Add("@Costo", MySqlDbType.Decimal).Value = costo;
        cmd.Parameters.Add("@IdVenta", MySqlDbType.UInt64).Value = idVenta;
        cmd.Parameters.Add("@IdEvento", MySqlDbType.UInt64).Value = idEvento;
        cmd.Parameters.Add("@IdSector", MySqlDbType.UInt64).Value = idSector;
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private Task<IReadOnlyList<TransferenciaDto>> ListarTransferenciasAsync(DocumentoUsuario usuario, bool enviadas, CancellationToken ct)
    {
        var prefix = enviadas ? "Otorga" : "Recibe";
        var sql = $"""
            SELECT IDTransferencia, IDEntrada, FechaSolicitud, FechaRespuesta, EstadoTransferencia,
                   UsuarioOtorga, CorreoOtorga, UsuarioRecibe, CorreoRecibe,
                   CONCAT('Evento ', IDEvento, ' · ', DATE_FORMAT(FechaEvento, '%Y-%m-%d %H:%i')) AS Evento,
                   Estadio, NombreSector
            FROM V_Transferencias
            WHERE Documento{prefix} = CONCAT(@TipoDocumento, ' ', @Numero)
            ORDER BY FechaSolicitud DESC;
            """;
        return QueryListAsync(sql, cmd => AddDocumento(cmd, usuario), MapTransferencia, "listar transferencias", ct);
    }

    private async Task<ValidacionEntradaDto> ObtenerValidacionEntradaAsync(MySqlConnection c, MySqlTransaction tx, ulong idEntrada, CancellationToken ct)
    {
        await using var cmd = new MySqlCommand("SELECT IDEntrada, EstadoEntrada, ResultadoValidacion, IDEvento, FechaEvento, IDSector, NombreSector, Estadio FROM V_ValidacionEntrada WHERE IDEntrada=@IdEntrada;", c, tx);
        cmd.Parameters.Add("@IdEntrada", MySqlDbType.UInt64).Value = idEntrada;
        await using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct))
        {
            throw new DatabaseException("No se encontró la entrada validada.", new InvalidOperationException("Validación sin resultado."));
        }
        return new ValidacionEntradaDto
        {
            IdEntrada = r.GetUInt64Value("IDEntrada"),
            EstadoEntrada = r.GetRequiredString("EstadoEntrada"),
            ResultadoValidacion = r.GetRequiredString("ResultadoValidacion"),
            IdEvento = r.GetUInt64Value("IDEvento"),
            FechaEvento = r.GetDateTimeValue("FechaEvento"),
            IdSector = r.GetUInt64Value("IDSector"),
            Sector = r.GetRequiredString("NombreSector"),
            Estadio = r.GetRequiredString("Estadio")
        };
    }

    private async Task<IReadOnlyList<T>> QueryListAsync<T>(string sql, Action<MySqlCommand> addParameters, Func<MySqlDataReader, T> map, string op, CancellationToken ct)
    {
        try
        {
            await using var c = await connectionFactory.OpenAsync(ct);
            await using var cmd = new MySqlCommand(sql, c);
            addParameters(cmd);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            var list = new List<T>();
            while (await r.ReadAsync(ct)) list.Add(map(r));
            return list;
        }
        catch (MySqlException ex) { throw exceptionTranslator.Translate(ex, op); }
    }

    private async Task<T?> QuerySingleAsync<T>(string sql, Action<MySqlCommand> addParameters, Func<MySqlDataReader, T> map, string op, CancellationToken ct)
    {
        var list = await QueryListAsync(sql, addParameters, map, op, ct);
        return list.FirstOrDefault();
    }

    private async Task<ulong> ExecuteScalarUInt64Async(string sql, Action<MySqlCommand> addParameters, string op, CancellationToken ct)
    {
        try
        {
            await using var c = await connectionFactory.OpenAsync(ct);
            await using var cmd = new MySqlCommand(sql, c);
            addParameters(cmd);
            return Convert.ToUInt64(await cmd.ExecuteScalarAsync(ct));
        }
        catch (MySqlException ex) { throw exceptionTranslator.Translate(ex, op); }
    }

    private async Task<int> ExecuteNonQueryAsync(string sql, Action<MySqlCommand> addParameters, string op, CancellationToken ct)
    {
        try
        {
            await using var c = await connectionFactory.OpenAsync(ct);
            await using var cmd = new MySqlCommand(sql, c);
            addParameters(cmd);
            return await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (MySqlException ex) { throw exceptionTranslator.Translate(ex, op); }
    }

    private static void AddDocumento(MySqlCommand cmd, DocumentoUsuario d)
    {
        cmd.Parameters.Add("@TipoDocumento", MySqlDbType.VarChar, 20).Value = d.TipoDocumento;
        cmd.Parameters.Add("@PaisDocumento", MySqlDbType.VarChar, 50).Value = d.PaisDocumento;
        cmd.Parameters.Add("@Numero", MySqlDbType.VarChar, 30).Value = d.NumeroDocumento;
    }

    private static void AddParams(MySqlCommand cmd, IEnumerable<MySqlParameter> parameters)
    {
        foreach (var p in parameters) cmd.Parameters.Add(p);
    }

    private static List<MySqlParameter> AddDateFilters(StringBuilder sql, DateTime? desde, DateTime? hasta, string column = "ev.FechaHora")
    {
        var parameters = new List<MySqlParameter>();
        if (desde.HasValue)
        {
            sql.AppendLine($"AND {column} >= @Desde");
            parameters.Add(new MySqlParameter("@Desde", MySqlDbType.DateTime) { Value = desde.Value });
        }
        if (hasta.HasValue)
        {
            sql.AppendLine($"AND {column} <= @Hasta");
            parameters.Add(new MySqlParameter("@Hasta", MySqlDbType.DateTime) { Value = hasta.Value });
        }
        return parameters;
    }

    private static CompraResumenDto MapCompraResumen(MySqlDataReader r) => new()
    {
        IdVenta = r.GetUInt64Value("IDVenta"),
        FechaVenta = r.GetDateTimeValue("FechaVenta"),
        EstadoVenta = r.GetRequiredString("EstadoVenta"),
        MontoTotal = r.GetDecimalValue("MontoTotal"),
        PorcentajeComision = r.GetDecimalValue("PorcentajeComision"),
        CantidadEntradas = Convert.ToInt32(r.GetValue(r.GetOrdinal("CantidadEntradas"))),
        Eventos = r.GetNullableString("Eventos") ?? string.Empty
    };

    private static EntradaResumenDto MapEntradaResumen(MySqlDataReader r) => new()
    {
        IdEntrada = r.GetUInt64Value("IDEntrada"),
        IdEvento = r.GetUInt64Value("IDEvento"),
        FechaEvento = r.GetDateTimeValue("FechaEvento"),
        Evento = r.GetRequiredString("Evento"),
        Estadio = r.GetRequiredString("Estadio"),
        IdSector = r.GetUInt64Value("IDSector"),
        Sector = r.GetRequiredString("Sector"),
        EstadoEntrada = r.GetRequiredString("EstadoEntrada"),
        Costo = r.GetDecimalValue("Costo"),
        TransferenciasAceptadas = Convert.ToInt32(r.GetValue(r.GetOrdinal("TransferenciasAceptadas")))
    };

    private static TransferenciaDto MapTransferencia(MySqlDataReader r) => new()
    {
        IdTransferencia = r.GetUInt64Value("IDTransferencia"),
        IdEntrada = r.GetUInt64Value("IDEntrada"),
        FechaSolicitud = r.GetDateTimeValue("FechaSolicitud"),
        FechaRespuesta = r.GetNullableString("FechaRespuesta") is null ? null : r.GetDateTimeValue("FechaRespuesta"),
        Estado = r.GetRequiredString("EstadoTransferencia"),
        UsuarioOtorga = r.GetRequiredString("UsuarioOtorga"),
        CorreoOtorga = r.GetRequiredString("CorreoOtorga"),
        UsuarioRecibe = r.GetRequiredString("UsuarioRecibe"),
        CorreoRecibe = r.GetRequiredString("CorreoRecibe"),
        Evento = r.GetRequiredString("Evento"),
        Estadio = r.GetRequiredString("Estadio"),
        Sector = r.GetRequiredString("NombreSector")
    };

    private static FuncionarioDto MapFuncionario(MySqlDataReader r) => new()
    {
        Documento = new DocumentoUsuario(r.GetRequiredString("TipoDocumento"), r.GetRequiredString("PaisDocumento"), r.GetRequiredString("Numero")),
        Nombre = r.GetRequiredString("Nombre"),
        Correo = r.GetRequiredString("CorreoElectronico"),
        NumLegajo = r.GetRequiredString("NumLegajo")
    };

    private static AsignacionFuncionarioDto MapAsignacion(MySqlDataReader r) => new()
    {
        Documento = new DocumentoUsuario(r.GetRequiredString("TipoDocumento"), r.GetRequiredString("PaisDocumento"), r.GetRequiredString("Numero")),
        Funcionario = r.GetRequiredString("Funcionario"),
        NumLegajo = r.GetRequiredString("NumLegajo"),
        IdEvento = r.GetUInt64Value("IDEvento"),
        FechaEvento = r.GetDateTimeValue("FechaEvento"),
        IdSector = r.GetUInt64Value("IDSector"),
        Sector = r.GetRequiredString("NombreSector"),
        Estadio = r.GetRequiredString("Estadio")
    };
}
