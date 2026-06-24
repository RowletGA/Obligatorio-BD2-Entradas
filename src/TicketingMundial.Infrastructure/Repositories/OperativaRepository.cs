using System.Text;
using MySqlConnector;
using TicketingMundial.Application.Abstractions.Security;
using TicketingMundial.Application.Abstractions.Repositories;
using TicketingMundial.Application.DTOs;
using TicketingMundial.Application.Security;
using TicketingMundial.Domain.Estados;
using TicketingMundial.Domain.Identity;
using TicketingMundial.Infrastructure.Data;
using TicketingMundial.Infrastructure.Errors;

namespace TicketingMundial.Infrastructure.Repositories;

public sealed class OperativaRepository(
    IDbConnectionFactory connectionFactory,
    IMySqlExceptionTranslator exceptionTranslator,
    IQrTokenService qrTokenService) : IOperativaRepository
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
            EstadoEvento = evento.Estado,
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
                if (!string.Equals(estado, EstadoEvento.Programado, StringComparison.Ordinal))
                {
                    throw new DatabaseException("Este evento ya no admite nuevas compras.", new InvalidOperationException("Evento no programado."));
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
            SELECT en.IDEntrada, ev.IDEvento, ev.FechaHora AS FechaEvento, ev.EstadoEvento,
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
            SELECT en.IDEntrada, ev.IDEvento, ev.FechaHora AS FechaEvento, ev.EstadoEvento,
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

    public Task<EntradaQrDto?> ObtenerEntradaQrAsync(DocumentoUsuario propietario, ulong idEntrada, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT en.IDEntrada, en.IDEvento, ev.FechaHora AS FechaEvento, ev.EstadoEvento,
                   en.IDSector, s.NombreSector AS Sector, en.EstadoEntrada,
                   CONCAT(COALESCE(eloc.Pais, 'Local'), ' vs ', COALESCE(evis.Pais, 'Visitante')) AS Evento,
                   est.Nombre AS Estadio,
                   p.TipoDocumento, p.PaisDocumento, p.Numero
            FROM Entrada en
            INNER JOIN V_PropietarioActual p ON p.IDEntrada = en.IDEntrada
            INNER JOIN Evento ev ON ev.IDEvento = en.IDEvento
            INNER JOIN Estadio est ON est.IDEstadio = ev.IDEstadio
            INNER JOIN Sector s ON s.IDSector = en.IDSector
            LEFT JOIN EventoLocal l ON l.IDEvento = ev.IDEvento
            LEFT JOIN Equipo eloc ON eloc.IDEquipo = l.IDEquipo
            LEFT JOIN EventoVisita vi ON vi.IDEvento = ev.IDEvento
            LEFT JOIN Equipo evis ON evis.IDEquipo = vi.IDEquipo
            WHERE en.IDEntrada = @IdEntrada
              AND p.TipoDocumento = @TipoDocumento
              AND p.PaisDocumento = @PaisDocumento
              AND p.Numero = @Numero;
            """;
        return QuerySingleAsync(sql, cmd =>
        {
            cmd.Parameters.Add("@IdEntrada", MySqlDbType.UInt64).Value = idEntrada;
            AddDocumento(cmd, propietario);
        }, MapEntradaQr, "obtener QR de entrada", cancellationToken);
    }

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
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            var destino = await BuscarUsuarioGeneralPorCorreoTransaccionAsync(connection, transaction, correoDestino, cancellationToken);
            if (destino is null)
            {
                throw new DatabaseException("No se encontró un usuario general con ese correo.", new InvalidOperationException("Destino no existe."));
            }

            var estadoEvento = await ObtenerEstadoEventoEntradaParaActualizarAsync(connection, transaction, idEntrada, cancellationToken);
            if (estadoEvento is null)
            {
                throw new DatabaseException("La entrada no existe.", new InvalidOperationException("Entrada no encontrada."));
            }

            if (EstadoEventoTransitions.EsCerrado(estadoEvento))
            {
                throw new DatabaseException("La entrada pertenece a un evento cerrado y ya no puede transferirse.", new InvalidOperationException("Evento cerrado."));
            }

            await using var cmd = new MySqlCommand("""
                INSERT INTO Transferencia (TipoDocOtorga, PaisDocOtorga, NumeroOtorga, TipoDocRecibe, PaisDocRecibe, NumeroRecibe, IDEntrada)
                VALUES (@TipoDocumento, @PaisDocumento, @Numero, @TipoRecibe, @PaisRecibe, @NumeroRecibe, @IdEntrada);
                SELECT LAST_INSERT_ID();
                """, connection, transaction);
            AddDocumento(cmd, otorga);
            cmd.Parameters.Add("@TipoRecibe", MySqlDbType.VarChar, 20).Value = destino.Documento.TipoDocumento;
            cmd.Parameters.Add("@PaisRecibe", MySqlDbType.VarChar, 50).Value = destino.Documento.PaisDocumento;
            cmd.Parameters.Add("@NumeroRecibe", MySqlDbType.VarChar, 30).Value = destino.Documento.NumeroDocumento;
            cmd.Parameters.Add("@IdEntrada", MySqlDbType.UInt64).Value = idEntrada;
            var id = Convert.ToUInt64(await cmd.ExecuteScalarAsync(cancellationToken));
            await transaction.CommitAsync(cancellationToken);
            return id;
        }
        catch (DatabaseException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
        catch (MySqlException ex)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw exceptionTranslator.Translate(ex, "crear transferencia");
        }
    }

    public Task<IReadOnlyList<TransferenciaDto>> ListarTransferenciasEnviadasAsync(DocumentoUsuario usuario, CancellationToken cancellationToken) =>
        ListarTransferenciasAsync(usuario, true, cancellationToken);

    public Task<IReadOnlyList<TransferenciaDto>> ListarTransferenciasRecibidasAsync(DocumentoUsuario usuario, CancellationToken cancellationToken) =>
        ListarTransferenciasAsync(usuario, false, cancellationToken);

    public async Task<bool> ResponderTransferenciaAsync(DocumentoUsuario usuario, ulong idTransferencia, string estado, bool receptor, CancellationToken cancellationToken)
    {
        var columnPrefix = receptor ? "Recibe" : "Otorga";
        var acepta = estado == EstadoTransferencia.Aceptada;
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            string? estadoEvento = null;
            string? estadoEntrada = null;
            var found = false;
            await using (var lockCmd = new MySqlCommand($"""
                SELECT ev.EstadoEvento, en.EstadoEntrada
                FROM Transferencia t
                INNER JOIN Entrada en ON en.IDEntrada = t.IDEntrada
                INNER JOIN Evento ev ON ev.IDEvento = en.IDEvento
                WHERE t.IDTransferencia = @IdTransferencia
                  AND t.Estado = @Pendiente
                  AND t.TipoDoc{columnPrefix} = @TipoDocumento
                  AND t.PaisDoc{columnPrefix} = @PaisDocumento
                  AND t.Numero{columnPrefix} = @Numero
                FOR UPDATE;
                """, connection, transaction))
            {
                lockCmd.Parameters.Add("@IdTransferencia", MySqlDbType.UInt64).Value = idTransferencia;
                lockCmd.Parameters.Add("@Pendiente", MySqlDbType.VarChar, 20).Value = EstadoTransferencia.Pendiente;
                AddDocumento(lockCmd, usuario);
                await using var reader = await lockCmd.ExecuteReaderAsync(cancellationToken);
                if (await reader.ReadAsync(cancellationToken))
                {
                    found = true;
                    estadoEvento = reader.GetRequiredString("EstadoEvento");
                    estadoEntrada = reader.GetRequiredString("EstadoEntrada");
                }
            }

            if (!found)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return false;
            }

            if (acepta && EstadoEventoTransitions.EsCerrado(estadoEvento))
            {
                throw new DatabaseException("La entrada pertenece a un evento cerrado y ya no puede transferirse.", new InvalidOperationException("Evento cerrado."));
            }

            if (acepta && estadoEntrada != EstadoEntrada.Activa)
            {
                throw new DatabaseException("La entrada ya no está activa y no puede transferirse.", new InvalidOperationException("Entrada no transferible."));
            }

            await using var updateCmd = new MySqlCommand("""
                UPDATE Transferencia
                SET Estado = @Estado, FechaRespuesta = CURRENT_TIMESTAMP
                WHERE IDTransferencia = @IdTransferencia
                  AND Estado = @Pendiente;
                """, connection, transaction);
            updateCmd.Parameters.Add("@Estado", MySqlDbType.VarChar, 20).Value = estado;
            updateCmd.Parameters.Add("@Pendiente", MySqlDbType.VarChar, 20).Value = EstadoTransferencia.Pendiente;
            updateCmd.Parameters.Add("@IdTransferencia", MySqlDbType.UInt64).Value = idTransferencia;
            var rows = await updateCmd.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return rows > 0;
        }
        catch (DatabaseException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
        catch (MySqlException ex)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw exceptionTranslator.Translate(ex, "responder transferencia");
        }
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
            SELECT v.TipoDocumento, v.PaisDocumento, v.Numero, v.Funcionario, v.NumLegajo, v.EntradasValidadas,
                   v.IDEvento, v.FechaEvento, ev.EstadoEvento, v.IDSector, v.NombreSector, ev.Estadio
            FROM V_ValidacionesPorFuncionario v
            INNER JOIN V_Eventos ev ON ev.IDEvento = v.IDEvento
            WHERE ev.PaisEstadio = @PaisSede
            ORDER BY v.FechaEvento DESC;
            """;
        return QueryListAsync(sql, cmd => cmd.Parameters.Add("@PaisSede", MySqlDbType.VarChar, 50).Value = paisSede, MapAsignacion, "listar asignaciones", cancellationToken);
    }

    public Task<bool> ExisteFuncionarioAsync(DocumentoUsuario funcionario, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1
                FROM Funcionario
                WHERE TipoDocumento = @TipoDocumento
                  AND PaisDocumento = @PaisDocumento
                  AND Numero = @Numero
            );
            """;
        return ExecuteScalarBoolAsync(sql, cmd => AddDocumento(cmd, funcionario), "verificar funcionario", cancellationToken);
    }

    public Task<EventoAdminDto?> ObtenerEventoAsignableAsync(ulong idEvento, string paisSede, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT v.IDEvento, v.FechaHora, v.EstadoEvento, v.IDEstadio, v.Estadio, v.PaisEstadio,
                   v.IDEquipoLocal, v.EquipoLocal, v.IDEquipoVisitante, v.EquipoVisitante,
                   (SELECT COUNT(*) FROM Entrada en WHERE en.IDEvento = v.IDEvento) AS EntradasEmitidas
            FROM V_Eventos v
            WHERE v.IDEvento = @IdEvento
              AND v.PaisEstadio = @PaisSede
              AND v.EstadoEvento IN ('PROGRAMADO', 'EN_CURSO');
            """;
        return QuerySingleAsync(sql, cmd =>
        {
            cmd.Parameters.Add("@IdEvento", MySqlDbType.UInt64).Value = idEvento;
            cmd.Parameters.Add("@PaisSede", MySqlDbType.VarChar, 50).Value = paisSede;
        }, MapEventoAsignable, "obtener evento asignable", cancellationToken);
    }

    public Task<bool> SectorHabilitadoParaEventoAsync(ulong idEvento, ulong idSector, string paisSede, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1
                FROM EventoSector es
                INNER JOIN Evento ev ON ev.IDEvento = es.IDEvento
                INNER JOIN Estadio est ON est.IDEstadio = ev.IDEstadio
                INNER JOIN Sector s ON s.IDSector = es.IDSector AND s.IDEstadio = ev.IDEstadio
                WHERE es.IDEvento = @IdEvento
                  AND es.IDSector = @IdSector
                  AND est.UbicacionPais = @PaisSede
                  AND ev.EstadoEvento IN ('PROGRAMADO', 'EN_CURSO')
            );
            """;
        return ExecuteScalarBoolAsync(sql, cmd =>
        {
            cmd.Parameters.Add("@IdEvento", MySqlDbType.UInt64).Value = idEvento;
            cmd.Parameters.Add("@IdSector", MySqlDbType.UInt64).Value = idSector;
            cmd.Parameters.Add("@PaisSede", MySqlDbType.VarChar, 50).Value = paisSede;
        }, "verificar sector habilitado", cancellationToken);
    }

    public Task<bool> ExisteAsignacionFuncionarioAsync(DocumentoUsuario funcionario, ulong idEvento, ulong idSector, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1
                FROM FuncionarioEventoSector
                WHERE TipoDocumento = @TipoDocumento
                  AND PaisDocumento = @PaisDocumento
                  AND Numero = @Numero
                  AND IDEvento = @IdEvento
                  AND IDSector = @IdSector
            );
            """;
        return ExecuteScalarBoolAsync(sql, cmd =>
        {
            AddDocumento(cmd, funcionario);
            cmd.Parameters.Add("@IdEvento", MySqlDbType.UInt64).Value = idEvento;
            cmd.Parameters.Add("@IdSector", MySqlDbType.UInt64).Value = idSector;
        }, "verificar asignación duplicada", cancellationToken);
    }

    public async Task AsignarFuncionarioAsync(DocumentoUsuario funcionario, ulong idEvento, ulong idSector, string paisSede, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO FuncionarioEventoSector (TipoDocumento, PaisDocumento, Numero, IDEvento, IDSector)
            SELECT @TipoDocumento, @PaisDocumento, @Numero, es.IDEvento, es.IDSector
            FROM EventoSector es
            INNER JOIN Evento ev ON ev.IDEvento = es.IDEvento
            INNER JOIN Estadio est ON est.IDEstadio = ev.IDEstadio
            INNER JOIN Sector s ON s.IDSector = es.IDSector AND s.IDEstadio = ev.IDEstadio
            WHERE es.IDEvento = @IdEvento
              AND es.IDSector = @IdSector
              AND est.UbicacionPais = @PaisSede
              AND ev.EstadoEvento IN ('PROGRAMADO', 'EN_CURSO')
              AND NOT EXISTS (
                  SELECT 1
                  FROM FuncionarioEventoSector fs
                  WHERE fs.TipoDocumento = @TipoDocumento
                    AND fs.PaisDocumento = @PaisDocumento
                    AND fs.Numero = @Numero
                    AND fs.IDEvento = es.IDEvento
                    AND fs.IDSector = es.IDSector
              );
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
            SELECT v.TipoDocumento, v.PaisDocumento, v.Numero, v.Funcionario, v.NumLegajo, v.EntradasValidadas,
                   v.IDEvento, v.FechaEvento, ev.EstadoEvento, v.IDSector, v.NombreSector, ev.Estadio
            FROM V_ValidacionesPorFuncionario v
            INNER JOIN V_Eventos ev ON ev.IDEvento = v.IDEvento
            WHERE v.TipoDocumento = @TipoDocumento AND v.PaisDocumento = @PaisDocumento AND v.Numero = @Numero
            ORDER BY v.FechaEvento DESC;
            """;
        return QueryListAsync(sql, cmd => AddDocumento(cmd, funcionario), MapAsignacion, "listar asignaciones de funcionario", cancellationToken);
    }

    public async Task<ValidacionEntradaDto> ValidarEntradaQrAsync(DocumentoUsuario funcionario, string token, CancellationToken cancellationToken)
    {
        var parsed = qrTokenService.LeerPayload(token);
        if (!parsed.EsValido || parsed.Payload is null)
        {
            throw new DatabaseException(parsed.Motivo ?? "QR inválido.", new InvalidOperationException("Token QR inválido."));
        }

        await using var connection = await connectionFactory.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            var entrada = await ObtenerEntradaQrTransaccionAsync(connection, transaction, parsed.Payload.IdEntrada, cancellationToken);
            if (entrada is null)
            {
                throw new DatabaseException("Entrada inexistente o no disponible.", new InvalidOperationException("Entrada no encontrada."));
            }

            if (parsed.Payload.IdEvento != entrada.IdEvento)
            {
                throw new DatabaseException("El QR no corresponde al evento de la entrada.", new InvalidOperationException("Evento de token no coincide."));
            }

            var tokenValidation = qrTokenService.Validar(token, new QrTokenValidationContext(entrada.IdEntrada, entrada.IdEvento, entrada.PropietarioActual));
            if (!tokenValidation.EsValido)
            {
                throw new DatabaseException(tokenValidation.Motivo ?? "QR inválido.", new InvalidOperationException("Token QR rechazado."));
            }

            if (entrada.EstadoEntrada == EstadoEntrada.Validada)
            {
                throw new DatabaseException("Entrada ya validada.", new InvalidOperationException("Entrada ya validada."));
            }

            if (entrada.EstadoEntrada == EstadoEntrada.Anulada)
            {
                throw new DatabaseException("Entrada anulada.", new InvalidOperationException("Entrada anulada."));
            }

            if (entrada.EstadoEntrada != EstadoEntrada.Activa)
            {
                throw new DatabaseException("Entrada no disponible para validación.", new InvalidOperationException("Estado inválido."));
            }

            if (entrada.EstadoEvento == EstadoEvento.Cancelado)
            {
                throw new DatabaseException("El evento fue cancelado.", new InvalidOperationException("Evento cancelado."));
            }

            if (entrada.EstadoEvento == EstadoEvento.Finalizado)
            {
                throw new DatabaseException("El evento ya finalizó.", new InvalidOperationException("Evento finalizado."));
            }

            if (entrada.EstadoEvento is not (EstadoEvento.Programado or EstadoEvento.EnCurso))
            {
                throw new DatabaseException("El evento no admite validación en este momento.", new InvalidOperationException("Evento no válido."));
            }

            if (!await ExisteAsignacionFuncionarioTransaccionAsync(connection, transaction, funcionario, entrada.IdEvento, entrada.IdSector, cancellationToken))
            {
                throw new DatabaseException("Funcionario no asignado a este sector.", new InvalidOperationException("Funcionario no asignado."));
            }

            await using (var insert = new MySqlCommand("""
                INSERT INTO Validacion (IDEntrada, TipoDocFuncionario, PaisDocFuncionario, NumeroFuncionario, TokenValidado)
                VALUES (@IdEntrada, @TipoDocumento, @PaisDocumento, @Numero, @Token);
                """, connection, transaction))
            {
                insert.Parameters.Add("@IdEntrada", MySqlDbType.UInt64).Value = entrada.IdEntrada;
                AddDocumento(insert, funcionario);
                insert.Parameters.Add("@Token", MySqlDbType.VarChar, 255).Value = token;
                await insert.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (var cancelTransfers = new MySqlCommand("""
                UPDATE Transferencia
                SET Estado = @Cancelada,
                    FechaRespuesta = CURRENT_TIMESTAMP
                WHERE IDEntrada = @IdEntrada
                  AND Estado = @Pendiente;
                """, connection, transaction))
            {
                cancelTransfers.Parameters.Add("@Cancelada", MySqlDbType.VarChar, 20).Value = EstadoTransferencia.Cancelada;
                cancelTransfers.Parameters.Add("@Pendiente", MySqlDbType.VarChar, 20).Value = EstadoTransferencia.Pendiente;
                cancelTransfers.Parameters.Add("@IdEntrada", MySqlDbType.UInt64).Value = entrada.IdEntrada;
                await cancelTransfers.ExecuteNonQueryAsync(cancellationToken);
            }

            var result = await ObtenerValidacionEntradaAsync(connection, transaction, entrada.IdEntrada, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch (DatabaseException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
        catch (MySqlException ex)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw exceptionTranslator.Translate(ex, "validar QR");
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

    public Task<IReadOnlyList<ReporteValidacionesFuncionarioDto>> ReporteValidacionesPorFuncionarioAsync(
        ulong? idEvento,
        string? funcionario,
        DateTime? desde,
        DateTime? hasta,
        CancellationToken cancellationToken)
    {
        var sql = new StringBuilder("""
            SELECT v.Funcionario, v.NumLegajo, v.IDEvento,
                   CONCAT(COALESCE(ev.EquipoLocal, 'Local'), ' vs ', COALESCE(ev.EquipoVisitante, 'Visitante')) AS Evento,
                   ev.Estadio,
                   v.NombreSector,
                   v.EntradasValidadas
            FROM V_ValidacionesPorFuncionario v
            INNER JOIN V_Eventos ev ON ev.IDEvento = v.IDEvento
            WHERE 1 = 1
            """);
        var parameters = AddDateFilters(sql, desde, hasta, "v.FechaEvento");
        if (idEvento.HasValue)
        {
            sql.AppendLine("AND v.IDEvento = @IdEvento");
            parameters.Add(new MySqlParameter("@IdEvento", MySqlDbType.UInt64) { Value = idEvento.Value });
        }

        if (!string.IsNullOrWhiteSpace(funcionario))
        {
            sql.AppendLine("AND (v.Funcionario LIKE @Funcionario ESCAPE '\\\\' OR v.NumLegajo LIKE @Funcionario ESCAPE '\\\\')");
            parameters.Add(new MySqlParameter("@Funcionario", MySqlDbType.VarChar, 120) { Value = $"%{SqlSafety.EscapeLikePattern(funcionario.Trim())}%" });
        }

        sql.AppendLine("ORDER BY v.EntradasValidadas DESC, v.Funcionario, v.IDEvento;");
        return QueryListAsync(sql.ToString(), cmd => AddParams(cmd, parameters), r => new ReporteValidacionesFuncionarioDto
        {
            Funcionario = r.GetRequiredString("Funcionario"),
            NumLegajo = r.GetRequiredString("NumLegajo"),
            IdEvento = r.GetUInt64Value("IDEvento"),
            Evento = r.GetRequiredString("Evento"),
            Estadio = r.GetRequiredString("Estadio"),
            Sector = r.GetRequiredString("NombreSector"),
            EntradasValidadas = Convert.ToInt32(r.GetValue(r.GetOrdinal("EntradasValidadas")))
        }, "reporte validaciones por funcionario", cancellationToken);
    }

    public Task<IReadOnlyList<ReporteTransferidorDto>> ReporteTransferidoresAsync(DateTime? desde, DateTime? hasta, int limite, CancellationToken cancellationToken)
    {
        var sql = new StringBuilder("""
            SELECT CONCAT_WS(' ', u.PrimerNombre, u.PrimerApellido) AS Usuario,
                   u.CorreoElectronico,
                   SUM(CASE WHEN t.Estado = 'ACEPTADA' THEN 1 ELSE 0 END) AS TransferenciasAceptadas,
                   COUNT(*) AS TransferenciasSolicitadas
            FROM Transferencia t
            INNER JOIN Usuario u
                ON u.TipoDocumento = t.TipoDocOtorga
               AND u.PaisDocumento = t.PaisDocOtorga
               AND u.Numero = t.NumeroOtorga
            WHERE 1 = 1
            """);
        var parameters = AddDateFilters(sql, desde, hasta, "t.FechaSolicitud");
        sql.AppendLine("""
            GROUP BY Usuario, u.CorreoElectronico
            HAVING TransferenciasAceptadas > 0
            ORDER BY TransferenciasAceptadas DESC, TransferenciasSolicitadas DESC
            LIMIT @Limite;
            """);
        return QueryListAsync(sql.ToString(), cmd =>
        {
            AddParams(cmd, parameters);
            cmd.Parameters.Add("@Limite", MySqlDbType.Int32).Value = limite;
        }, r => new ReporteTransferidorDto
        {
            Usuario = r.GetRequiredString("Usuario"),
            Correo = r.GetRequiredString("CorreoElectronico"),
            TransferenciasAceptadas = Convert.ToInt32(r.GetValue(r.GetOrdinal("TransferenciasAceptadas"))),
            TransferenciasSolicitadas = Convert.ToInt32(r.GetValue(r.GetOrdinal("TransferenciasSolicitadas")))
        }, "reporte transferidores", cancellationToken);
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

    private static async Task<UsuarioDestinoDto?> BuscarUsuarioGeneralPorCorreoTransaccionAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string correo,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand("""
            SELECT u.TipoDocumento, u.PaisDocumento, u.Numero, u.CorreoElectronico,
                   CONCAT_WS(' ', u.PrimerNombre, u.PrimerApellido) AS Nombre
            FROM UsuarioGeneral ug
            INNER JOIN Usuario u ON u.TipoDocumento = ug.TipoDocumento AND u.PaisDocumento = ug.PaisDocumento AND u.Numero = ug.Numero
            WHERE u.CorreoElectronico = @Correo;
            """, connection, transaction);
        command.Parameters.Add("@Correo", MySqlDbType.VarChar, 254).Value = correo;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new UsuarioDestinoDto
        {
            Documento = new DocumentoUsuario(reader.GetRequiredString("TipoDocumento"), reader.GetRequiredString("PaisDocumento"), reader.GetRequiredString("Numero")),
            Correo = reader.GetRequiredString("CorreoElectronico"),
            Nombre = reader.GetRequiredString("Nombre")
        };
    }

    private static async Task<string?> ObtenerEstadoEventoEntradaParaActualizarAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        ulong idEntrada,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand("""
            SELECT ev.EstadoEvento
            FROM Entrada en
            INNER JOIN Evento ev ON ev.IDEvento = en.IDEvento
            WHERE en.IDEntrada = @IdEntrada
            FOR UPDATE;
            """, connection, transaction);
        command.Parameters.Add("@IdEntrada", MySqlDbType.UInt64).Value = idEntrada;
        return Convert.ToString(await command.ExecuteScalarAsync(cancellationToken));
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
                   CONCAT(COALESCE(eloc.Pais, 'Local'), ' vs ', COALESCE(evis.Pais, 'Visitante')) AS Evento,
                   vt.FechaEvento, vt.Estadio, vt.NombreSector
            FROM V_Transferencias vt
            LEFT JOIN EventoLocal l ON l.IDEvento = vt.IDEvento
            LEFT JOIN Equipo eloc ON eloc.IDEquipo = l.IDEquipo
            LEFT JOIN EventoVisita vi ON vi.IDEvento = vt.IDEvento
            LEFT JOIN Equipo evis ON evis.IDEquipo = vi.IDEquipo
            WHERE Documento{prefix} = CONCAT(@TipoDocumento, ' ', @Numero)
            ORDER BY vt.FechaSolicitud DESC;
            """;
        return QueryListAsync(sql, cmd => AddDocumento(cmd, usuario), MapTransferencia, "listar transferencias", ct);
    }

    private async Task<ValidacionEntradaDto> ObtenerValidacionEntradaAsync(MySqlConnection c, MySqlTransaction tx, ulong idEntrada, CancellationToken ct)
    {
        await using var cmd = new MySqlCommand("""
            SELECT IDEntrada, EstadoEntrada, ResultadoValidacion, IDEvento, FechaEvento, IDSector, NombreSector, Estadio,
                   CONCAT(COALESCE(EquipoLocal, 'Local'), ' vs ', COALESCE(EquipoVisitante, 'Visitante')) AS Evento,
                   FechaHoraValidacion, FuncionarioValidador
            FROM V_ValidacionEntrada
            WHERE IDEntrada=@IdEntrada;
            """, c, tx);
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
            Estadio = r.GetRequiredString("Estadio"),
            Evento = r.GetRequiredString("Evento"),
            FechaHoraValidacion = r.GetNullableDateTime("FechaHoraValidacion"),
            FuncionarioValidador = r.GetNullableString("FuncionarioValidador") ?? string.Empty
        };
    }

    private async Task<EntradaQrDto?> ObtenerEntradaQrTransaccionAsync(MySqlConnection connection, MySqlTransaction transaction, ulong idEntrada, CancellationToken cancellationToken)
    {
        await using (var lockCmd = new MySqlCommand("SELECT IDEntrada FROM Entrada WHERE IDEntrada = @IdEntrada FOR UPDATE;", connection, transaction))
        {
            lockCmd.Parameters.Add("@IdEntrada", MySqlDbType.UInt64).Value = idEntrada;
            var locked = await lockCmd.ExecuteScalarAsync(cancellationToken);
            if (locked is null)
            {
                return null;
            }
        }

        await using var cmd = new MySqlCommand("""
            SELECT en.IDEntrada, en.IDEvento, ev.FechaHora AS FechaEvento, ev.EstadoEvento,
                   en.IDSector, s.NombreSector AS Sector, en.EstadoEntrada,
                   CONCAT(COALESCE(eloc.Pais, 'Local'), ' vs ', COALESCE(evis.Pais, 'Visitante')) AS Evento,
                   est.Nombre AS Estadio,
                   p.TipoDocumento, p.PaisDocumento, p.Numero
            FROM Entrada en
            INNER JOIN V_PropietarioActual p ON p.IDEntrada = en.IDEntrada
            INNER JOIN Evento ev ON ev.IDEvento = en.IDEvento
            INNER JOIN Estadio est ON est.IDEstadio = ev.IDEstadio
            INNER JOIN Sector s ON s.IDSector = en.IDSector
            LEFT JOIN EventoLocal l ON l.IDEvento = ev.IDEvento
            LEFT JOIN Equipo eloc ON eloc.IDEquipo = l.IDEquipo
            LEFT JOIN EventoVisita vi ON vi.IDEvento = ev.IDEvento
            LEFT JOIN Equipo evis ON evis.IDEquipo = vi.IDEquipo
            WHERE en.IDEntrada = @IdEntrada
            ;
            """, connection, transaction);
        cmd.Parameters.Add("@IdEntrada", MySqlDbType.UInt64).Value = idEntrada;
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? MapEntradaQr(reader) : null;
    }

    private static async Task<bool> ExisteAsignacionFuncionarioTransaccionAsync(MySqlConnection connection, MySqlTransaction transaction, DocumentoUsuario funcionario, ulong idEvento, ulong idSector, CancellationToken cancellationToken)
    {
        await using var cmd = new MySqlCommand("""
            SELECT EXISTS (
                SELECT 1
                FROM FuncionarioEventoSector
                WHERE TipoDocumento = @TipoDocumento
                  AND PaisDocumento = @PaisDocumento
                  AND Numero = @Numero
                  AND IDEvento = @IdEvento
                  AND IDSector = @IdSector
            );
            """, connection, transaction);
        AddDocumento(cmd, funcionario);
        cmd.Parameters.Add("@IdEvento", MySqlDbType.UInt64).Value = idEvento;
        cmd.Parameters.Add("@IdSector", MySqlDbType.UInt64).Value = idSector;
        return Convert.ToBoolean(await cmd.ExecuteScalarAsync(cancellationToken));
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

    private async Task<bool> ExecuteScalarBoolAsync(string sql, Action<MySqlCommand> addParameters, string op, CancellationToken ct)
    {
        try
        {
            await using var c = await connectionFactory.OpenAsync(ct);
            await using var cmd = new MySqlCommand(sql, c);
            addParameters(cmd);
            return Convert.ToBoolean(await cmd.ExecuteScalarAsync(ct));
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
        EnsureSqlLineBreak(sql);
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

    private static void EnsureSqlLineBreak(StringBuilder sql)
    {
        if (sql.Length > 0 && !char.IsWhiteSpace(sql[sql.Length - 1]))
        {
            sql.AppendLine();
        }
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

    private static EventoAdminDto MapEventoAsignable(MySqlDataReader r) => new()
    {
        IdEvento = r.GetUInt64Value("IDEvento"),
        FechaHora = r.GetDateTimeValue("FechaHora"),
        EstadoEvento = r.GetRequiredString("EstadoEvento"),
        IdEstadio = r.GetUInt64Value("IDEstadio"),
        Estadio = r.GetRequiredString("Estadio"),
        PaisEstadio = r.GetRequiredString("PaisEstadio"),
        IdEquipoLocal = GetNullableUInt64(r, "IDEquipoLocal"),
        EquipoLocal = r.GetNullableString("EquipoLocal"),
        IdEquipoVisitante = GetNullableUInt64(r, "IDEquipoVisitante"),
        EquipoVisitante = r.GetNullableString("EquipoVisitante"),
        EntradasEmitidas = Convert.ToInt32(r.GetValue(r.GetOrdinal("EntradasEmitidas")))
    };

    private static EntradaResumenDto MapEntradaResumen(MySqlDataReader r) => new()
    {
        IdEntrada = r.GetUInt64Value("IDEntrada"),
        IdEvento = r.GetUInt64Value("IDEvento"),
        FechaEvento = r.GetDateTimeValue("FechaEvento"),
        EstadoEvento = r.GetRequiredString("EstadoEvento"),
        Evento = r.GetRequiredString("Evento"),
        Estadio = r.GetRequiredString("Estadio"),
        IdSector = r.GetUInt64Value("IDSector"),
        Sector = r.GetRequiredString("Sector"),
        EstadoEntrada = r.GetRequiredString("EstadoEntrada"),
        Costo = r.GetDecimalValue("Costo"),
        TransferenciasAceptadas = Convert.ToInt32(r.GetValue(r.GetOrdinal("TransferenciasAceptadas")))
    };

    private static EntradaQrDto MapEntradaQr(MySqlDataReader r) => new()
    {
        IdEntrada = r.GetUInt64Value("IDEntrada"),
        IdEvento = r.GetUInt64Value("IDEvento"),
        FechaEvento = r.GetDateTimeValue("FechaEvento"),
        EstadoEvento = r.GetRequiredString("EstadoEvento"),
        IdSector = r.GetUInt64Value("IDSector"),
        Sector = r.GetRequiredString("Sector"),
        EstadoEntrada = r.GetRequiredString("EstadoEntrada"),
        Evento = r.GetRequiredString("Evento"),
        Estadio = r.GetRequiredString("Estadio"),
        PropietarioActual = new DocumentoUsuario(r.GetRequiredString("TipoDocumento"), r.GetRequiredString("PaisDocumento"), r.GetRequiredString("Numero"))
    };

    private static TransferenciaDto MapTransferencia(MySqlDataReader r) => new()
    {
        IdTransferencia = r.GetUInt64Value("IDTransferencia"),
        IdEntrada = r.GetUInt64Value("IDEntrada"),
        FechaSolicitud = r.GetDateTimeValue("FechaSolicitud"),
        FechaRespuesta = r.GetNullableDateTime("FechaRespuesta"),
        Estado = r.GetRequiredString("EstadoTransferencia"),
        UsuarioOtorga = r.GetRequiredString("UsuarioOtorga"),
        CorreoOtorga = r.GetRequiredString("CorreoOtorga"),
        UsuarioRecibe = r.GetRequiredString("UsuarioRecibe"),
        CorreoRecibe = r.GetRequiredString("CorreoRecibe"),
        Evento = r.GetRequiredString("Evento"),
        FechaEvento = r.GetDateTimeValue("FechaEvento"),
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
        EstadoEvento = r.GetRequiredString("EstadoEvento"),
        IdSector = r.GetUInt64Value("IDSector"),
        Sector = r.GetRequiredString("NombreSector"),
        Estadio = r.GetRequiredString("Estadio"),
        EntradasValidadas = Convert.ToInt32(r.GetValue(r.GetOrdinal("EntradasValidadas")))
    };

    private static ulong? GetNullableUInt64(MySqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : Convert.ToUInt64(reader.GetValue(ordinal));
    }
}
