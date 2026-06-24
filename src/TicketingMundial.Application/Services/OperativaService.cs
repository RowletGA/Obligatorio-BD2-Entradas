using TicketingMundial.Application.Abstractions.Repositories;
using TicketingMundial.Application.Abstractions.Security;
using TicketingMundial.Application.Abstractions.Services;
using TicketingMundial.Application.DTOs;
using TicketingMundial.Domain.Common;
using TicketingMundial.Domain.Estados;
using TicketingMundial.Domain.Identity;

namespace TicketingMundial.Application.Services;

public sealed class OperativaService(IOperativaRepository repository, IQrTokenService qrTokenService) : IOperativaService
{
    private static readonly int[] PageSizesPermitidos = [10, 25, 50];

    public async Task<OperationResult<CompraPreviewDto>> PreviewCompraAsync(ulong idEvento, IReadOnlyList<CompraSectorCantidad> cantidades, CancellationToken cancellationToken)
    {
        var validation = ValidateCantidades(cantidades);
        if (!validation.Success)
        {
            return OperationResult<CompraPreviewDto>.Failure(validation.Message ?? "Cantidades inválidas.");
        }

        var preview = await repository.ObtenerPreviewCompraAsync(idEvento, NormalizeCantidades(cantidades), cancellationToken);
        return preview.EstadoEvento == EstadoEvento.Programado
            ? OperationResult<CompraPreviewDto>.Ok(preview)
            : OperationResult<CompraPreviewDto>.Failure("Este evento ya no admite nuevas compras.");
    }

    public async Task<OperationResult<CompraResultadoDto>> ComprarAsync(DocumentoUsuario comprador, ulong idEvento, IReadOnlyList<CompraSectorCantidad> cantidades, CancellationToken cancellationToken)
    {
        var validation = ValidateCantidades(cantidades);
        if (!validation.Success)
        {
            return OperationResult<CompraResultadoDto>.Failure(validation.Message ?? "Cantidades inválidas.");
        }

        var result = await repository.ComprarAsync(NormalizeDocumento(comprador), idEvento, NormalizeCantidades(cantidades), cancellationToken);
        return OperationResult<CompraResultadoDto>.Ok(result, "Compra confirmada correctamente.");
    }

    public Task<IReadOnlyList<CompraResumenDto>> ListarComprasAsync(DocumentoUsuario comprador, CancellationToken cancellationToken) =>
        repository.ListarComprasAsync(NormalizeDocumento(comprador), cancellationToken);

    public Task<PagedResult<CompraResumenDto>> ListarComprasAsync(DocumentoUsuario comprador, CompraListQuery query, CancellationToken cancellationToken) =>
        repository.ListarComprasAsync(NormalizeDocumento(comprador), NormalizeCompraQuery(query), cancellationToken);

    public Task<CompraDetalleDto?> ObtenerCompraAsync(DocumentoUsuario comprador, ulong idVenta, CancellationToken cancellationToken) =>
        repository.ObtenerCompraAsync(NormalizeDocumento(comprador), idVenta, cancellationToken);

    public Task<IReadOnlyList<EntradaResumenDto>> ListarEntradasPropiasAsync(DocumentoUsuario propietario, CancellationToken cancellationToken) =>
        repository.ListarEntradasPropiasAsync(NormalizeDocumento(propietario), cancellationToken);

    public Task<PagedResult<EntradaResumenDto>> ListarEntradasPropiasAsync(DocumentoUsuario propietario, EntradaListQuery query, CancellationToken cancellationToken) =>
        repository.ListarEntradasPropiasAsync(NormalizeDocumento(propietario), NormalizeEntradaQuery(query), cancellationToken);

    public Task<EntradaResumenDto?> ObtenerEntradaPropiaAsync(DocumentoUsuario propietario, ulong idEntrada, CancellationToken cancellationToken) =>
        repository.ObtenerEntradaPropiaAsync(NormalizeDocumento(propietario), idEntrada, cancellationToken);

    public async Task<OperationResult<QrEntradaGeneradoDto>> GenerarQrEntradaAsync(DocumentoUsuario propietario, ulong idEntrada, string? generationGrant, CancellationToken cancellationToken)
    {
        if (idEntrada == 0)
        {
            return OperationResult<QrEntradaGeneradoDto>.Failure("Entrada no disponible.");
        }

        var documento = NormalizeDocumento(propietario);
        var grant = string.Empty;
        DateTimeOffset grantVenceUtc = default;
        if (!string.IsNullOrWhiteSpace(generationGrant))
        {
            var grantValidation = qrTokenService.ValidarPermisoGeneracion(generationGrant.Trim(), documento);
            if (grantValidation.EsValido && grantValidation.Payload is not null && grantValidation.Payload.IdEntrada == idEntrada)
            {
                grant = generationGrant.Trim();
                grantVenceUtc = grantValidation.Payload.VenceUtc;
            }
        }

        var entrada = await repository.ObtenerEntradaQrAsync(documento, idEntrada, cancellationToken);
        if (entrada is null)
        {
            return OperationResult<QrEntradaGeneradoDto>.Failure("Entrada no disponible.");
        }

        if (entrada.EstadoEntrada == EstadoEntrada.Anulada)
        {
            return OperationResult<QrEntradaGeneradoDto>.Failure("La entrada está anulada.");
        }

        if (entrada.EstadoEntrada == EstadoEntrada.Validada)
        {
            return OperationResult<QrEntradaGeneradoDto>.Failure("La entrada ya fue validada.");
        }

        if (entrada.EstadoEntrada != EstadoEntrada.Activa)
        {
            return OperationResult<QrEntradaGeneradoDto>.Failure("La entrada ya no está activa.");
        }

        if (entrada.EstadoEvento == EstadoEvento.Cancelado)
        {
            return OperationResult<QrEntradaGeneradoDto>.Failure("El evento fue cancelado.");
        }

        if (entrada.EstadoEvento == EstadoEvento.Finalizado)
        {
            return OperationResult<QrEntradaGeneradoDto>.Failure("El evento ya finalizó.");
        }

        if (entrada.EstadoEvento is not (EstadoEvento.Programado or EstadoEvento.EnCurso))
        {
            return OperationResult<QrEntradaGeneradoDto>.Failure("El evento no admite validación en este momento.");
        }

        var generated = qrTokenService.Generar(new QrTokenContext(entrada.IdEntrada, entrada.IdEvento, entrada.PropietarioActual));
        if (string.IsNullOrWhiteSpace(grant))
        {
            var newGrant = qrTokenService.GenerarPermisoGeneracion(new QrTokenContext(entrada.IdEntrada, entrada.IdEvento, entrada.PropietarioActual), TimeSpan.FromMinutes(5));
            grant = newGrant.Grant;
            grantVenceUtc = newGrant.VenceUtc;
        }

        return OperationResult<QrEntradaGeneradoDto>.Ok(new QrEntradaGeneradoDto
        {
            Token = generated.Token,
            VenceUtc = generated.VenceUtc,
            SegundosRestantes = generated.SegundosRestantes,
            GenerationGrant = grant,
            GenerationGrantVenceUtc = grantVenceUtc,
            ConsultoBase = true
        });
    }

    public Task<UsuarioDestinoDto?> BuscarUsuarioGeneralPorCorreoAsync(string correo, CancellationToken cancellationToken) =>
        repository.BuscarUsuarioGeneralPorCorreoAsync(correo.Trim(), cancellationToken);

    public async Task<OperationResult<ulong>> CrearTransferenciaAsync(DocumentoUsuario otorga, ulong idEntrada, string correoDestino, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(correoDestino))
        {
            return OperationResult<ulong>.Failure("Debe indicar el correo del destinatario.");
        }

        var id = await repository.CrearTransferenciaAsync(NormalizeDocumento(otorga), idEntrada, correoDestino.Trim(), cancellationToken);
        return OperationResult<ulong>.Ok(id, "Transferencia solicitada correctamente.");
    }

    public Task<IReadOnlyList<TransferenciaDto>> ListarTransferenciasEnviadasAsync(DocumentoUsuario usuario, CancellationToken cancellationToken) =>
        repository.ListarTransferenciasEnviadasAsync(NormalizeDocumento(usuario), cancellationToken);

    public Task<IReadOnlyList<TransferenciaDto>> ListarTransferenciasRecibidasAsync(DocumentoUsuario usuario, CancellationToken cancellationToken) =>
        repository.ListarTransferenciasRecibidasAsync(NormalizeDocumento(usuario), cancellationToken);

    public Task<PagedResult<TransferenciaDto>> ListarTransferenciasAsync(DocumentoUsuario usuario, TransferenciaListQuery query, CancellationToken cancellationToken) =>
        repository.ListarTransferenciasAsync(NormalizeDocumento(usuario), NormalizeTransferenciaQuery(query), cancellationToken);

    public async Task<OperationResult> ResponderTransferenciaAsync(DocumentoUsuario usuario, ulong idTransferencia, string estado, bool receptor, CancellationToken cancellationToken)
    {
        var normalized = estado.Trim().ToUpperInvariant();
        if (normalized is not ("ACEPTADA" or "RECHAZADA" or "CANCELADA"))
        {
            return OperationResult.Failure("Estado de transferencia inválido.");
        }

        var updated = await repository.ResponderTransferenciaAsync(NormalizeDocumento(usuario), idTransferencia, normalized, receptor, cancellationToken);
        return updated ? OperationResult.Ok("Transferencia actualizada.") : OperationResult.Failure("No se encontró una transferencia pendiente para responder.");
    }

    public Task<IReadOnlyList<FuncionarioDto>> ListarFuncionariosAsync(CancellationToken cancellationToken) => repository.ListarFuncionariosAsync(cancellationToken);

    public Task<IReadOnlyList<AsignacionFuncionarioDto>> ListarAsignacionesAsync(string paisSede, CancellationToken cancellationToken) =>
        repository.ListarAsignacionesAsync(paisSede.Trim().ToUpperInvariant(), cancellationToken);

    public async Task<OperationResult> AsignarFuncionarioAsync(DocumentoUsuario funcionario, ulong idEvento, ulong idSector, string paisSede, CancellationToken cancellationToken)
    {
        if (idEvento == 0 || idSector == 0)
        {
            return OperationResult.Failure("Debe seleccionar evento y sector.");
        }

        var documento = NormalizeDocumento(funcionario);
        var pais = paisSede.Trim().ToUpperInvariant();
        if (!await repository.ExisteFuncionarioAsync(documento, cancellationToken))
        {
            return OperationResult.Failure("Funcionario inválido.");
        }

        var evento = await repository.ObtenerEventoAsignableAsync(idEvento, pais, cancellationToken);
        if (evento is null)
        {
            return OperationResult.Failure("El evento no existe, está fuera de jurisdicción o no admite nuevas asignaciones.");
        }

        if (evento.EstadoEvento is EstadoEvento.Finalizado or EstadoEvento.Cancelado)
        {
            return OperationResult.Failure("No se pueden asignar funcionarios a eventos finalizados o cancelados.");
        }

        if (!await repository.SectorHabilitadoParaEventoAsync(idEvento, idSector, pais, cancellationToken))
        {
            return OperationResult.Failure("El sector seleccionado no está habilitado para ese evento.");
        }

        if (await repository.ExisteAsignacionFuncionarioAsync(documento, idEvento, idSector, cancellationToken))
        {
            return OperationResult.Failure("El funcionario ya se encuentra asignado a ese evento y sector.");
        }

        await repository.AsignarFuncionarioAsync(documento, idEvento, idSector, pais, cancellationToken);
        return OperationResult.Ok("Funcionario asignado correctamente.");
    }

    public Task<IReadOnlyList<AsignacionFuncionarioDto>> ListarAsignacionesFuncionarioAsync(DocumentoUsuario funcionario, CancellationToken cancellationToken) =>
        repository.ListarAsignacionesFuncionarioAsync(NormalizeDocumento(funcionario), cancellationToken);

    public async Task<OperationResult<ValidacionEntradaDto>> ValidarQrAsync(DocumentoUsuario funcionario, string token, CancellationToken cancellationToken)
    {
        var normalizedToken = token?.Trim() ?? string.Empty;
        if (normalizedToken.Length == 0)
        {
            return OperationResult<ValidacionEntradaDto>.Failure("Debe indicar el token QR.");
        }

        if (normalizedToken.Length > qrTokenService.MaxTokenLength)
        {
            return OperationResult<ValidacionEntradaDto>.Failure("Formato de QR inválido.");
        }

        var parsed = qrTokenService.LeerPayload(normalizedToken);
        if (!parsed.EsValido)
        {
            return OperationResult<ValidacionEntradaDto>.Failure(parsed.Motivo ?? "Formato de QR inválido.");
        }

        var result = await repository.ValidarEntradaQrAsync(NormalizeDocumento(funcionario), normalizedToken, cancellationToken);
        return OperationResult<ValidacionEntradaDto>.Ok(result, "Entrada válida.");
    }

    public Task<IReadOnlyList<ReporteEventoVendidoDto>> ReporteEventosVendidosAsync(DateTime? desde, DateTime? hasta, int limite, CancellationToken cancellationToken) =>
        repository.ReporteEventosVendidosAsync(desde, hasta, ClampLimit(limite), cancellationToken);

    public Task<IReadOnlyList<ReporteCompradorDto>> ReporteCompradoresAsync(DateTime? desde, DateTime? hasta, int limite, CancellationToken cancellationToken) =>
        repository.ReporteCompradoresAsync(desde, hasta, ClampLimit(limite), cancellationToken);

    public Task<IReadOnlyList<ReporteValidacionesFuncionarioDto>> ReporteValidacionesPorFuncionarioAsync(
        ulong? idEvento,
        string? funcionario,
        DateTime? desde,
        DateTime? hasta,
        CancellationToken cancellationToken) =>
        repository.ReporteValidacionesPorFuncionarioAsync(idEvento, NullIfWhiteSpace(funcionario), desde, hasta, cancellationToken);

    public Task<IReadOnlyList<ReporteTransferidorDto>> ReporteTransferidoresAsync(DateTime? desde, DateTime? hasta, int limite, CancellationToken cancellationToken) =>
        repository.ReporteTransferidoresAsync(desde, hasta, ClampLimit(limite), cancellationToken);

    private static OperationResult ValidateCantidades(IReadOnlyList<CompraSectorCantidad> cantidades)
    {
        var total = cantidades.Where(item => item.Cantidad > 0).Sum(item => item.Cantidad);
        if (total <= 0)
        {
            return OperationResult.Failure("Debe seleccionar al menos una entrada.");
        }

        if (total > 5)
        {
            return OperationResult.Failure("Una compra no puede superar cinco entradas.");
        }

        if (cantidades.Any(item => item.Cantidad < 0))
        {
            return OperationResult.Failure("Las cantidades no pueden ser negativas.");
        }

        return OperationResult.Ok();
    }

    private static IReadOnlyList<CompraSectorCantidad> NormalizeCantidades(IReadOnlyList<CompraSectorCantidad> cantidades) =>
        cantidades
            .Where(item => item.Cantidad > 0)
            .GroupBy(item => item.IdSector)
            .Select(group => new CompraSectorCantidad(group.Key, group.Sum(item => item.Cantidad)))
            .ToArray();

    private static DocumentoUsuario NormalizeDocumento(DocumentoUsuario documento) =>
        new(documento.TipoDocumento.Trim().ToUpperInvariant(), documento.PaisDocumento.Trim().ToUpperInvariant(), documento.NumeroDocumento.Trim());

    private static int ClampLimit(int limite) => limite is < 1 or > 100 ? 10 : limite;

    private static int ClampPage(int page) => page < 1 ? 1 : page;

    private static int ClampPageSize(int pageSize) => PageSizesPermitidos.Contains(pageSize) ? pageSize : 10;

    private static string? ClampSearch(string? value)
    {
        var trimmed = NullIfWhiteSpace(value);
        return trimmed is null ? null : trimmed[..Math.Min(trimmed.Length, 100)];
    }

    private static CompraListQuery NormalizeCompraQuery(CompraListQuery query) => new()
    {
        Busqueda = ClampSearch(query.Busqueda),
        Estado = NullIfWhiteSpace(query.Estado)?.ToLowerInvariant(),
        Evento = ClampSearch(query.Evento),
        Desde = query.Desde,
        Hasta = query.Hasta,
        Sort = NullIfWhiteSpace(query.Sort),
        Direction = query.Direction == "asc" ? "asc" : "desc",
        Page = ClampPage(query.Page),
        PageSize = ClampPageSize(query.PageSize)
    };

    private static EntradaListQuery NormalizeEntradaQuery(EntradaListQuery query) => new()
    {
        Busqueda = ClampSearch(query.Busqueda),
        Estado = NullIfWhiteSpace(query.Estado)?.ToUpperInvariant(),
        FechaEventoDesde = query.FechaEventoDesde,
        FechaEventoHasta = query.FechaEventoHasta,
        FechaAdquisicionDesde = query.FechaAdquisicionDesde,
        FechaAdquisicionHasta = query.FechaAdquisicionHasta,
        Sort = NullIfWhiteSpace(query.Sort),
        Direction = query.Direction == "asc" ? "asc" : "desc",
        Page = ClampPage(query.Page),
        PageSize = ClampPageSize(query.PageSize)
    };

    private static TransferenciaListQuery NormalizeTransferenciaQuery(TransferenciaListQuery query) => new()
    {
        Busqueda = ClampSearch(query.Busqueda),
        Estado = NullIfWhiteSpace(query.Estado)?.ToUpperInvariant(),
        Tipo = NullIfWhiteSpace(query.Tipo)?.ToLowerInvariant(),
        Desde = query.Desde,
        Hasta = query.Hasta,
        Sort = NullIfWhiteSpace(query.Sort),
        Direction = query.Direction == "asc" ? "asc" : "desc",
        Page = ClampPage(query.Page),
        PageSize = ClampPageSize(query.PageSize)
    };

    private static string? NullIfWhiteSpace(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
